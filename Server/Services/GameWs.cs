using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using Bomberman.Server.Models;

namespace Bomberman.Server.Services;

public class GameWs
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<WebSocket, byte>> _subs = new(); // gameId -> sockets
    private readonly JsonSerializerOptions _json;

    public GameWs()
    {
        _json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    }

    public void Register(string gameId, WebSocket ws)
    {
        var map = _subs.GetOrAdd(gameId, _ => new ConcurrentDictionary<WebSocket, byte>());
        map[ws] = 1;
    }

    public void Unregister(WebSocket ws)
    {
        foreach (var kv in _subs)
        {
            if (kv.Value.TryRemove(ws, out _)) break;
        }
    }

    public async Task BroadcastAsync(string gameId, GameState state, CancellationToken ct = default)
    {
        if (!_subs.TryGetValue(gameId, out var map)) return;
        var payload = JsonSerializer.SerializeToUtf8Bytes(new { gameState = state }, _json);
        var dead = new List<WebSocket>();
        foreach (var ws in map.Keys)
        {
            try { await ws.SendAsync(payload, WebSocketMessageType.Text, true, ct); }
            catch { dead.Add(ws); }
        }
        foreach (var d in dead) map.TryRemove(d, out _);
    }
}
