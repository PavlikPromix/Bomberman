using Bomberman.Server.Infrastructure;
using Bomberman.Server.Models;
using Bomberman.Server.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IUserRepo, InMemoryUserRepo>();
builder.Services.AddSingleton<ILobbyRepo, InMemoryLobbyRepo>();
builder.Services.AddSingleton<IGameRepo, InMemoryGameRepo>();
builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddSingleton<GameWs>();
builder.Services.AddHostedService<GameTicker>();

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseWebSockets();
app.MapControllers();

// WebSocket endpoint: /api/game/state
app.Map("/api/game/state", async (HttpContext ctx, IGameRepo games, GameWs hub) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest)
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsJsonAsync(new ErrorResponse { ErrorCode = "invalid_input", ErrorMessage = "WebSocket expected." });
        return;
    }

    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    var buffer = new byte[4096];
    var jsonOptions = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };

    string? currentGameId = null;

    try
    {
        while (ws.State == System.Net.WebSockets.WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close) break;

            var json = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
            try
            {
                var msg = System.Text.Json.JsonSerializer.Deserialize<WsClientMessage>(json, jsonOptions);
                if (msg is null) throw new ArgumentException("Invalid message schema.");
                Guard.NotEmpty(msg.GameId, "gameId");
                Guard.NotEmpty(msg.PlayerId, "playerId");
                Guard.NotEmpty(msg.Move, "move");

                currentGameId ??= msg.GameId;
                // register this socket to receive broadcasts for this game
                hub.Register(msg.GameId, ws);

                var gs = games.ApplyMove(msg.GameId, msg.PlayerId, msg.Move);
                // respond to sender and broadcast to all
                var ok = new WsServerSuccess(gs);
                var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(ok, jsonOptions);
                await ws.SendAsync(payload, System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
                await hub.BroadcastAsync(gs.GameId, gs);
            }
            catch (System.Text.Json.JsonException ex)
            {
                var err = new ErrorResponse { ErrorCode = "invalid_input", ErrorMessage = "Malformed JSON: " + ex.Message };
                var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(err, jsonOptions);
                await ws.SendAsync(payload, System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (UnauthorizedAccessException ex)
            {
                var err = new ErrorResponse { ErrorCode = "unauthorized", ErrorMessage = ex.Message };
                var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(err, jsonOptions);
                await ws.SendAsync(payload, System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (ArgumentException ex)
            {
                var err = new ErrorResponse { ErrorCode = "invalid_input", ErrorMessage = ex.Message };
                var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(err, jsonOptions);
                await ws.SendAsync(payload, System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch
            {
                var err = new ErrorResponse { ErrorCode = "server_error", ErrorMessage = "Unexpected server error." };
                var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(err, jsonOptions);
                await ws.SendAsync(payload, System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
    finally
    {
        hub.Unregister(ws);
    }
});

app.Run();

