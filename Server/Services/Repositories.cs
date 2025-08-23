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
    Lobby? GetByLobbyOrCode(string idOrCode);
    Lobby Join(string lobbyIdOrCode, User user, int maxPlayers);
    string? GetCode(string lobbyId);
}

public class InMemoryLobbyRepo : ILobbyRepo
{
    private readonly ConcurrentDictionary<string, Lobby> _lobbies = new();
    private readonly ConcurrentDictionary<string, int> _max = new();
    private readonly ConcurrentDictionary<string, string> _lobbyCode = new(); // lobbyId -> CODE
    private readonly ConcurrentDictionary<string, string> _codeToLobby = new(); // CODE -> lobbyId
    private static readonly char[] CodeAlphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ".ToCharArray();

    private static string NewCode(HashSet<string> existing)
    {
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var bytes = new byte[5];
        while (true)
        {
            rng.GetBytes(bytes);
            var chars = new char[5];
            for (int i = 0; i < 5; i++) chars[i] = CodeAlphabet[bytes[i] % CodeAlphabet.Length];
            var code = new string(chars);
            if (!existing.Contains(code)) return code;
        }
    }

    public Lobby Create(int maxPlayers)
    {
        var lobby = new Lobby { LobbyId = Guid.NewGuid().ToString(), Status = LobbyStatus.Waiting };
        _lobbies[lobby.LobbyId] = lobby;
        _max[lobby.LobbyId] = maxPlayers;

        // Assign a unique short code (case-insensitive)
        var existing = new HashSet<string>(_codeToLobby.Keys, StringComparer.OrdinalIgnoreCase);
        var code = NewCode(existing);
        _lobbyCode[lobby.LobbyId] = code;
        _codeToLobby[code.ToUpperInvariant()] = lobby.LobbyId;

        return lobby;
    }

    public Lobby? Get(string lobbyId) => _lobbies.TryGetValue(lobbyId, out var l) ? l : null;

    public Lobby? GetByLobbyOrCode(string idOrCode)
    {
        if (string.IsNullOrWhiteSpace(idOrCode)) return null;
        if (_lobbies.TryGetValue(idOrCode, out var byId)) return byId;
        var key = idOrCode.Trim().ToUpperInvariant();
        if (_codeToLobby.TryGetValue(key, out var lobbyId) && _lobbies.TryGetValue(lobbyId, out var byCode))
            return byCode;
        return null;
    }

    public string? GetCode(string lobbyId) => _lobbyCode.TryGetValue(lobbyId, out var c) ? c : null;

    public Lobby Join(string lobbyIdOrCode, User user, int maxPlayers)
    {
        var lobby = GetByLobbyOrCode(lobbyIdOrCode) ?? throw new ArgumentException("Lobby not found.");
        var capacity = _max[lobby.LobbyId]; // ensure we key by real lobbyId
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
        _games[id] = gs;
        Console.WriteLine($"[Game] Started {id} for lobby {lobby.LobbyId}");
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

        // Demo: encode last move into board[0][0]
        gs.Board[0][0] = move.ToLowerInvariant() switch
        {
            "up" => 1, "down" => 2, "left" => 3, "right" => 4, "bomb" => 9, _ => 0
        };
        return gs;
    }
}
