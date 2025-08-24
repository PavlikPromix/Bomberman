namespace Bomberman.Server.Services;

public class LobbySettings
{
    public int RoundsToWin { get; set; } = 5;  // 1..20
    public int BombLimit   { get; set; } = 3;  // 1..10
}

public interface ILobbySettingsRepo
{
    LobbySettings GetOrDefault(string lobbyId);
    void Set(string lobbyId, LobbySettings s);
}

public class InMemoryLobbySettingsRepo : ILobbySettingsRepo
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, LobbySettings> _map = new();
    public LobbySettings GetOrDefault(string lobbyId)
        => _map.GetOrAdd(lobbyId, _ => new LobbySettings());
    public void Set(string lobbyId, LobbySettings s) => _map[lobbyId] = s;
}
