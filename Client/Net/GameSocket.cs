using System;                      // IAsyncDisposable, Action, Uri
using System.Threading;            // CancellationTokenSource, CancellationToken
using System.Threading.Tasks;      // Task, ValueTask
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Bomberman.Client.Net;

public class GameSocket : IAsyncDisposable
{
    private readonly ClientWebSocket _ws = new();
    private CancellationTokenSource? _cts;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task ConnectAsync(Uri wsUri)
    {
        _cts = new CancellationTokenSource();
        await _ws.ConnectAsync(wsUri, CancellationToken.None);
    }

    public async Task SendMoveAsync(string gameId, string playerId, string move)
    {
        var json = JsonSerializer.Serialize(new { gameId, playerId, move });
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    /// <summary>Back-compat receive (WsScreen). Returns either a GameState or an ErrorResponse.</summary>
    public async Task<(GameState? state, ErrorResponse? error)> ReceiveAsync()
    {
        var buff = new byte[8192];
        var res = await _ws.ReceiveAsync(buff, CancellationToken.None);
        if (res.MessageType == WebSocketMessageType.Close)
            return (null, new ErrorResponse { ErrorCode = "closed", ErrorMessage = "WebSocket closed." });

        var json = Encoding.UTF8.GetString(buff, 0, res.Count);
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("gameState", out _))
            {
                var ok = JsonSerializer.Deserialize<WsServerSuccess>(json, JsonOpts);
                return (ok!.GameState, null);
            }
            else
            {
                var err = JsonSerializer.Deserialize<ErrorResponse>(json, JsonOpts);
                if (err != null) return (null, err);
                return (null, new ErrorResponse { ErrorCode = "invalid_input", ErrorMessage = "Unrecognized payload." });
            }
        }
        catch (JsonException ex)
        {
            return (null, new ErrorResponse { ErrorCode = "invalid_input", ErrorMessage = "Malformed JSON: " + ex.Message });
        }
    }

    /// <summary>Continuous listener (used by gameplay). Calls callbacks on each message.</summary>
    public void StartListening(Action<GameState>? onState, Action<ErrorResponse>? onError)
    {
        if (_cts == null) _cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            var buff = new byte[8192];
            while (!_cts!.IsCancellationRequested && _ws.State == WebSocketState.Open)
            {
                try
                {
                    var res = await _ws.ReceiveAsync(buff, _cts.Token);
                    if (res.MessageType == WebSocketMessageType.Close) break;
                    var json = Encoding.UTF8.GetString(buff, 0, res.Count);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("gameState", out _))
                    {
                        var ok = JsonSerializer.Deserialize<WsServerSuccess>(json, JsonOpts);
                        if (ok != null) onState?.Invoke(ok.GameState);
                    }
                    else
                    {
                        var err = JsonSerializer.Deserialize<ErrorResponse>(json, JsonOpts);
                        if (err != null) onError?.Invoke(err);
                    }
                }
                catch
                {
                    // ignore transient receive errors
                }
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        try { _cts?.Cancel(); } catch { }
        if (_ws.State == WebSocketState.Open)
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
        _ws.Dispose();
    }

    private class WsServerSuccess { public GameState GameState { get; set; } = new(); }
}
