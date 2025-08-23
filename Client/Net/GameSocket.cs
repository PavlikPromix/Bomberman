using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Bomberman.Client.Net;

public class GameSocket : IAsyncDisposable
{
    private readonly ClientWebSocket _ws = new();

    public async Task ConnectAsync(Uri wsUri) => await _ws.ConnectAsync(wsUri, CancellationToken.None);

    public async Task SendMoveAsync(string gameId, string playerId, string move)
    {
        var json = JsonSerializer.Serialize(new { gameId, playerId, move });
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public async Task<(GameState? state, ErrorResponse? error)> ReceiveAsync()
    {
        var buff = new byte[4096];
        var res = await _ws.ReceiveAsync(buff, CancellationToken.None);
        var json = Encoding.UTF8.GetString(buff, 0, res.Count);

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("gameState", out _))
        {
            var ok = JsonSerializer.Deserialize<WsServerSuccess>(json);
            return (ok!.GameState, null);
        }
        var err = JsonSerializer.Deserialize<ErrorResponse>(json);
        return (null, err);
    }

    public async ValueTask DisposeAsync()
    {
        if (_ws.State == WebSocketState.Open)
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
        _ws.Dispose();
    }

    private class WsServerSuccess { public GameState GameState { get; set; } = new(); }
}
