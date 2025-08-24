using System.Text.Json;
using Microsoft.Extensions.Hosting;

namespace Bomberman.Server.Services;

public class GameTicker : BackgroundService
{
    private readonly IGameRepo _games;
    private readonly GameWs _ws;
    public GameTicker(IGameRepo games, GameWs ws) { _games = games; _ws = ws; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var updated = _games.TickAll();
            foreach (var state in updated)
            {
                await _ws.BroadcastAsync(state.GameId, state, stoppingToken);
            }
            await Task.Delay(100, stoppingToken); // 10 ticks/sec
        }
    }
}
