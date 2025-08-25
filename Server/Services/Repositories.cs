using System.Collections.Concurrent;
using Bomberman.Server.Models;

namespace Bomberman.Server.Services;

// -------------------- USERS --------------------
public interface IUserRepo
{
    User? FindById(string id);
    User? FindByUsername(string username);
    User CreateUser(string username, string password);
    bool VerifyPassword(string username, string password);
    void AddStats(string userId, int playedDelta, int wonDelta, int scoreDelta);
    IEnumerable<User> All();
}

public class InMemoryUserRepo : IUserRepo
{
    private readonly ConcurrentDictionary<string, User> _byId = new();
    private readonly ConcurrentDictionary<string, string> _passHash = new();
    private readonly ConcurrentDictionary<string, string> _passSalt = new();

    public User? FindById(string id) => _byId.TryGetValue(id, out var u) ? u : null;
    public User? FindByUsername(string username) => _byId.Values.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

    public User CreateUser(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Password required.");
        if (FindByUsername(username) != null) throw new ArgumentException("User exists.");

        var (hash, salt) = PasswordHasher.Hash(password);
        var u = new User { Id = Guid.NewGuid().ToString(), Username = username, Stats = new UserStats() };
        _byId[u.Id] = u;
        _passHash[u.Id] = hash; _passSalt[u.Id] = salt;
        return u;
    }

    public bool VerifyPassword(string username, string password)
    {
        var u = FindByUsername(username);
        if (u == null) return false;
        return PasswordHasher.Verify(password, _passSalt[u.Id], _passHash[u.Id]);
    }

    public void AddStats(string userId, int playedDelta, int wonDelta, int scoreDelta)
    {
        if (_byId.TryGetValue(userId, out var u))
        {
            u.Stats.GamesPlayed += playedDelta;
            u.Stats.GamesWon += wonDelta;
            u.Stats.TotalScore += scoreDelta;
        }
    }

    public IEnumerable<User> All() => _byId.Values;
}

// -------------------- LOBBIES --------------------
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
    private readonly ConcurrentDictionary<string, string> _lobbyCode = new();
    private readonly ConcurrentDictionary<string, string> _codeToLobby = new();
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
        var capacity = _max[lobby.LobbyId];
        if (lobby.Players.Any(p => p.Id == user.Id)) return lobby;
        if (lobby.Players.Count >= capacity) throw new ArgumentException("Lobby is full.");
        if (lobby.Status != LobbyStatus.Waiting) throw new ArgumentException("Lobby already in progress.");
        lobby.Players.Add(user);
        return lobby;
    }
}

// -------------------- GAME --------------------
public interface IGameRepo
{
    GameState StartForLobby(Lobby lobby, int roundsToWin, int bombLimit);
    GameState? Get(string gameId);
    GameState? GetByLobby(string lobbyId);
    GameState ApplyMove(string gameId, string playerId, string move);
    IEnumerable<GameState> TickAll(); // called by ticker
}

internal class Bomb { public int X, Y; public int Fuse; }
internal class Game
{
    public string GameId = "";
    public string LobbyId = "";
    public List<User> Players = new();
    public bool Active = true;
    public const int W = 25, H = 25;
    public int[,] Walls = new int[H, W];
    public int[,] BaseWalls = new int[H, W];
    public List<Bomb> Bombs = new();
    public List<(int X,int Y,int Ticks)> Fire = new();
    public Dictionary<string,(int X,int Y,bool Alive)> Pos = new(); // by userId
    public Dictionary<string,int> Scores = new();
    public int RoundsToWin = 5;
    public int BombLimit = 3;
    public Dictionary<string,int> BombsByPlayer = new();
    public string? WinnerId = null;

    public GameState BuildState()
    {
        var board = new int[H][];
        for (int y=0;y<H;y++) { board[y] = new int[W]; }

        for (int y=0;y<H;y++)
            for (int x=0;x<W;x++)
                board[y][x] = Walls[y,x];

        foreach (var b in Bombs) board[b.Y][b.X] = 20;
        foreach (var f in Fire) board[f.Y][f.X] = 30;

        for (int i=0;i<Players.Count;i++)
        {
            var u = Players[i];
            if (Pos.TryGetValue(u.Id, out var p) && p.Alive)
                board[p.Y][p.X] = 11 + i;
        }

        return new GameState {
            GameId = GameId, LobbyId = LobbyId, Players = Players, Board = board, Active = Active,
            Scores = new Dictionary<string,int>(Scores), RoundsToWin = RoundsToWin, WinnerId = WinnerId
        };
    }
}

public class InMemoryGameRepo : IGameRepo
{
    private readonly ConcurrentDictionary<string, Game> _games = new();
    private readonly ConcurrentDictionary<string, string> _byLobby = new();
    private readonly IUserRepo _users;

    public InMemoryGameRepo(IUserRepo users) { _users = users; }

    public GameState StartForLobby(Lobby lobby, int roundsToWin, int bombLimit)
    {
        var g = new Game
        {
            GameId = Guid.NewGuid().ToString(),
            LobbyId = lobby.LobbyId,
            Players = lobby.Players.ToList(),
            Active = true,
            RoundsToWin = roundsToWin,
            BombLimit = bombLimit
        };

        for (int y=0;y<Game.H;y++)
        for (int x=0;x<Game.W;x++)
        {
            int v = 0;
            if (x==0 || y==0 || x==Game.W-1 || y==Game.H-1) v = 1;
            else if (x%2==0 && y%2==0) v = 1;
            else if ((x+y)%5==0) v = 2;
            g.Walls[y,x] = v;
            g.BaseWalls[y,x] = v;
        }

        void clear3x3(int cx,int cy){
            for(int yy=Math.Max(1,cy-1); yy<=Math.Min(Game.H-2,cy+1); yy++)
                for(int xx=Math.Max(1,cx-1); xx<=Math.Min(Game.W-2,cx+1); xx++)
                    if (g.Walls[yy,xx]==2) g.Walls[yy,xx]=0;
        }
        var spawns = new (int X,int Y)[]{ (1,1), (Game.W-2,Game.H-2), (1,Game.H-2), (Game.W-2,1) };
        for(int i=0;i<g.Players.Count && i<spawns.Length; i++){
            var u = g.Players[i];
            var s = spawns[i];
            clear3x3(s.X,s.Y);
            g.Pos[u.Id] = (s.X,s.Y,true);
            g.Scores[u.Id] = 0;
            g.BombsByPlayer[u.Id] = 0;
        }

        _games[g.GameId] = g;
        _byLobby[g.LobbyId] = g.GameId;
        Console.WriteLine($"[Game] Started {g.GameId} for lobby {g.LobbyId} (to {g.RoundsToWin}, limit {g.BombLimit})");
        return g.BuildState();
    }

    public GameState? Get(string gameId) => _games.TryGetValue(gameId, out var g) ? g.BuildState() : null;
    public GameState? GetByLobby(string lobbyId) { if (_byLobby.TryGetValue(lobbyId, out var gid)) return Get(gid); return null; }

    private static readonly HashSet<string> AllowedMoves = new(StringComparer.OrdinalIgnoreCase)
        { "up","down","left","right","bomb","stay","noop" };

    public GameState ApplyMove(string gameId, string playerId, string move)
    {
        if (!AllowedMoves.Contains(move)) throw new ArgumentException("Invalid move.");
        if (!_games.TryGetValue(gameId, out var g)) throw new ArgumentException("Game not found.");
        if (!g.Active) return g.BuildState();
        if (!g.Pos.ContainsKey(playerId)) throw new UnauthorizedAccessException("Player not in game.");

        var (x,y,alive) = g.Pos[playerId];
        if (!alive) return g.BuildState();

        if (string.Equals(move,"bomb",StringComparison.OrdinalIgnoreCase))
        {
            if (g.BombsByPlayer[playerId] < g.BombLimit && !g.Bombs.Any(b => b.X==x && b.Y==y))
            {
                g.Bombs.Add(new Bomb { X=x, Y=y, Fuse=20 });
                g.BombsByPlayer[playerId] = g.BombsByPlayer[playerId] + 1;
            }
            return g.BuildState();
        }

        int dx=0, dy=0;
        switch(move.ToLowerInvariant()){
            case "up": dy=-1; break;
            case "down": dy=1; break;
            case "left": dx=-1; break;
            case "right": dx=1; break;
            default: break;
        }
        var nx = Math.Clamp(x+dx, 0, Game.W-1);
        var ny = Math.Clamp(y+dy, 0, Game.H-1);

        bool blocked = g.Walls[ny,nx]==1 || g.Walls[ny,nx]==2 || g.Bombs.Any(b=>b.X==nx && b.Y==ny);
        if (!blocked) g.Pos[playerId] = (nx,ny,alive);

        return g.BuildState();
    }

    public IEnumerable<GameState> TickAll()
    {
        var updated = new List<GameState>();
        foreach (var kv in _games)
        {
            var g = kv.Value;
            if (!g.Active) continue;

            for (int i=g.Bombs.Count-1; i>=0; i--)
            {
                var b = g.Bombs[i];
                if (b.Fuse > 0) b.Fuse--;
                if (b.Fuse == 0)
                {
                    Explode(g, b.X, b.Y, radius:3);
                    g.Bombs.RemoveAt(i);
                    foreach (var id in g.BombsByPlayer.Keys.ToList())
                        g.BombsByPlayer[id] = Math.Max(0, g.BombsByPlayer[id] - 1);
                }
            }

            for (int i=g.Fire.Count-1;i>=0;i--)
            {
                var f = g.Fire[i];
                f.Ticks--;
                if (f.Ticks <= 0) g.Fire.RemoveAt(i);
                else g.Fire[i] = f;
            }

            var killed = new HashSet<string>();
            foreach (var u in g.Players)
            {
                var p = g.Pos[u.Id];
                if (!p.Alive) continue;
                if (g.Fire.Any(f=>f.X==p.X && f.Y==p.Y))
                {
                    killed.Add(u.Id);
                    g.Pos[u.Id] = (p.X,p.Y,false);
                }
            }

            if (killed.Count > 0)
            {
                var aliveIds = g.Pos.Where(kv2 => kv2.Value.Alive).Select(kv2 => kv2.Key).ToList();
                if (aliveIds.Count == 1)
                {
                    var winner = aliveIds[0];
                    g.Scores[winner] = g.Scores[winner] + 1;

                    if (g.Scores[winner] >= g.RoundsToWin)
                    {
                        g.Active = false;
                        g.WinnerId = winner;

                        foreach (var u in g.Players)
                        {
                            int won = (u.Id == winner) ? 1 : 0;
                            int pts = g.Scores.TryGetValue(u.Id, out var p) ? p : 0;
                            _users.AddStats(u.Id, playedDelta:1, wonDelta:won, scoreDelta:pts);
                        }
                    }
                    else
                    {
                        ResetRound(g);
                    }
                }
                else
                {
                    ResetRound(g);
                }
            }

            updated.Add(g.BuildState());
        }
        return updated;
    }

    private static void ResetRound(Game g)
    {
        for (int y=0;y<Game.H;y++)
            for (int x=0;x<Game.W;x++)
                g.Walls[y,x] = g.BaseWalls[y,x];

        g.Bombs.Clear();
        g.Fire.Clear();
        foreach (var k in g.BombsByPlayer.Keys.ToList()) g.BombsByPlayer[k] = 0;

        var spawns = new (int X,int Y)[]{ (1,1), (Game.W-2,Game.H-2), (1,Game.H-2), (Game.W-2,1) };
        for(int i=0;i<g.Players.Count && i<spawns.Length; i++){
            var u = g.Players[i];
            var s = spawns[i];
            for(int yy=Math.Max(1,s.Y-1); yy<=Math.Min(Game.H-2,s.Y+1); yy++)
                for(int xx=Math.Max(1,s.X-1); xx<=Math.Min(Game.W-2,s.X+1); xx++)
                    if (g.Walls[yy,xx]==2) g.Walls[yy,xx]=0;
            g.Pos[u.Id] = (s.X,s.Y,true);
        }
    }

    private static void Explode(Game g, int cx, int cy, int radius)
    {
        g.Fire.Add((cx,cy,5));
        foreach(var dir in new[]{ (1,0),(-1,0),(0,1),(0,-1) })
        {
            int x=cx, y=cy;
            for (int r=0;r<radius;r++)
            {
                x += dir.Item1; y += dir.Item2;
                if (x<=0 || y<=0 || x>=Game.W-1 || y>=Game.H-1) break;
                if (g.Walls[y,x] == 1) break;
                g.Fire.Add((x,y,5));
                if (g.Walls[y,x] == 2) { g.Walls[y,x] = 0; break; }
            }
        }
    }
}
