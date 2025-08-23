using System.Collections.Concurrent;
using Bomberman.Server.Models;

namespace Bomberman.Server.Services;

public interface IUserRepo
{
    User Upsert(string username);
    User? FindById(string id);
    IEnumerable<User> All();
}
public class InMemoryUserRepo : IUserRepo
{
    private readonly ConcurrentDictionary<string, User> _byName = new();
    private readonly ConcurrentDictionary<string, User> _byId = new();
    public User Upsert(string username)
    {
        var user = _byName.GetOrAdd(username, u => {
            var created = new User { Id = Guid.NewGuid().ToString(), Username = u, Stats = new UserStats() };
            _byId[created.Id] = created;
            return created;
        });
        return user;
    }
    public User? FindById(string id) => _byId.TryGetValue(id, out var u) ? u : null;
    public IEnumerable<User> All() => _byId.Values;
}

public interface ILobbyRepo
{
    Lobby Create(int maxPlayers);
    Lobby? Get(string lobbyId);
    Lobby Join(string lobbyId, User user, int maxPlayers);
}
public class InMemoryLobbyRepo : ILobbyRepo
{
    private readonly ConcurrentDictionary<string, Lobby> _lobbies = new();
    private readonly ConcurrentDictionary<string, int> _max = new();

    public Lobby Create(int maxPlayers)
    {
        var lobby = new Lobby { LobbyId = Guid.NewGuid().ToString(), Status = LobbyStatus.Waiting };
        _lobbies[lobby.LobbyId] = lobby;
        _max[lobby.LobbyId] = maxPlayers;
        return lobby;
    }
    public Lobby? Get(string lobbyId) => _lobbies.TryGetValue(lobbyId, out var l) ? l : null;

    public Lobby Join(string lobbyId, User user, int maxPlayers)
    {
        var lobby = Get(lobbyId) ?? throw new ArgumentException("Lobby not found.");
        var capacity = _max[lobbyId];
        if (lobby.Players.Any(p => p.Id == user.Id)) return lobby;
        if (lobby.Players.Count >= capacity) throw new ArgumentException("Lobby is full.");
        if (lobby.Status != LobbyStatus.Waiting) throw new ArgumentException("Lobby already in progress.");
        lobby.Players.Add(user);
        if (lobby.Players.Count >= 2) lobby.Status = LobbyStatus.InProgress; // simplistic start condition
        return lobby;
    }
}

public interface IGameRepo
{
    GameState StartForLobby(Lobby lobby);
    GameState? Get(string gameId);
    GameState ApplyMove(string gameId, string playerId, string move);
}
public class InMemoryGameRepo : IGameRepo
{
    private readonly ConcurrentDictionary<string, GameState> _games = new();
    public GameState StartForLobby(Lobby lobby)
    {
        var id = Guid.NewGuid().ToString();
        var board = Enumerable.Range(0, 13).Select(_ => new int[15]).ToArray(); // 13x15 zero grid
        var gs = new GameState { GameId = id, LobbyId = lobby.LobbyId, Players = lobby.Players.ToList(), Board = board, Active = true };
        Console.WriteLine($"[Game] Started {id} for lobby {lobby.LobbyId}");
        _games[id] = gs;
        return gs;
    }
    public GameState? Get(string gameId) => _games.TryGetValue(gameId, out var g) ? g : null;

    private static readonly HashSet<string> AllowedMoves = new(StringComparer.OrdinalIgnoreCase)
        { "up", "down", "left", "right", "bomb", "stay" };

    public GameState ApplyMove(string gameId, string playerId, string move)
    {
        if (!AllowedMoves.Contains(move)) throw new ArgumentException("Invalid move.");
        var gs = Get(gameId) ?? throw new ArgumentException("Game not found.");
        if (!gs.Active) throw new ArgumentException("Game not active.");
        if (!gs.Players.Any(p => p.Id == playerId)) throw new UnauthorizedAccessException("Player not in game.");

        // Extremely simplified: encode last move into board[0][0] for demo determinism
        gs.Board[0][0] = move.ToLowerInvariant() switch
        {
            "up" => 1, "down" => 2, "left" => 3, "right" => 4, "bomb" => 9, _ => 0
        };
        return gs;
    }
}
