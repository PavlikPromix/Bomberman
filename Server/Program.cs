using Bomberman.Server.Infrastructure;
using Bomberman.Server.Models;
using Bomberman.Server.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IUserRepo, InMemoryUserRepo>();
builder.Services.AddSingleton<ILobbyRepo, InMemoryLobbyRepo>();
builder.Services.AddSingleton<IGameRepo, InMemoryGameRepo>();
builder.Services.AddSingleton<IJwtService, JwtService>();
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
app.Map("/api/game/state", async (HttpContext ctx, IGameRepo games) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest)
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsJsonAsync(new ErrorResponse { ErrorCode = "invalid_input", ErrorMessage = "WebSocket expected." });
        return;
    }

    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    var jsonOptions = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
    var buffer = new byte[4096];
    var serializer = System.Text.Json.JsonSerializerOptions.Default;

    while (ws.State == System.Net.WebSockets.WebSocketState.Open)
    {
        var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
        if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close) break;

        var json = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
        try
        {
            var msg = System.Text.Json.JsonSerializer.Deserialize<WsClientMessage>(json, jsonOptions);
            if (msg is null) throw new ArgumentException("Invalid message schema.");
            Bomberman.Server.Infrastructure.Guard.NotEmpty(msg.GameId, "gameId");
            Bomberman.Server.Infrastructure.Guard.NotEmpty(msg.PlayerId, "playerId");
            Bomberman.Server.Infrastructure.Guard.NotEmpty(msg.Move, "move");

            var gs = games.ApplyMove(msg.GameId, msg.PlayerId, msg.Move);
            var ok = new WsServerSuccess(gs);
            var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(ok, jsonOptions);
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
});

app.Run();
