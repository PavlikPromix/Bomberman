using Bomberman.Server.Infrastructure;
using Bomberman.Server.Models;
using Bomberman.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bomberman.Server.Controllers;

[ApiController]
[Route("api/lobbies")]
public class LobbiesController : ControllerBase
{
    private readonly ILobbyRepo _lobbies;
    private readonly IUserRepo _users;
    private readonly IJwtService _jwt;
    private readonly IGameRepo _games;
    private readonly ILobbySettingsRepo _settings;

    public LobbiesController(ILobbyRepo lobbies, IUserRepo users, IJwtService jwt, IGameRepo games, ILobbySettingsRepo settings)
    { _lobbies = lobbies; _users = users; _jwt = jwt; _games = games; _settings = settings; }

    [HttpGet("{lobbyId}")]
    public ActionResult<Lobby> GetLobby([FromRoute] string lobbyId)
    {
        Guard.NotEmpty(lobbyId, "lobbyId");
        var lobby = _lobbies.Get(lobbyId) ?? _lobbies.GetByLobbyOrCode(lobbyId) ?? throw new ArgumentException("Lobby not found.");
        return Ok(lobby);
    }

    [HttpGet("{lobbyId}/code")]
    public ActionResult<object> GetCode([FromRoute] string lobbyId)
    {
        Guard.NotEmpty(lobbyId, "lobbyId");
        var lobby = _lobbies.Get(lobbyId) ?? _lobbies.GetByLobbyOrCode(lobbyId) ?? throw new ArgumentException("Lobby not found.");
        var code = _lobbies.GetCode(lobby.LobbyId) ?? throw new ArgumentException("Code not found.");
        return Ok(new { code });
    }

    [HttpGet("{lobbyId}/settings")]
    public ActionResult<LobbySettings> GetSettings([FromRoute] string lobbyId)
    {
        Guard.NotEmpty(lobbyId, "lobbyId");
        var lobby = _lobbies.GetByLobbyOrCode(lobbyId) ?? throw new ArgumentException("Lobby not found.");
        return Ok(_settings.GetOrDefault(lobby.LobbyId));
    }

    public record SetSettingsRequest(string LobbyId, string Token, int RoundsToWin, int BombLimit);

    [HttpPost("settings")]
    public ActionResult<LobbySettings> SetSettings([FromBody] SetSettingsRequest req)
    {
        Guard.NotEmpty(req.LobbyId, "lobbyId");
        Guard.NotEmpty(req.Token, "token");
        if (req.RoundsToWin < 1 || req.RoundsToWin > 20) throw new ArgumentException("RoundsToWin must be 1..20.");
        if (req.BombLimit   < 1 || req.BombLimit   > 10) throw new ArgumentException("BombLimit must be 1..10.");

        var (ok, userId) = _jwt.Validate(req.Token);
        if (!ok || userId is null) throw new UnauthorizedAccessException("Invalid token.");

        var lobby = _lobbies.GetByLobbyOrCode(req.LobbyId) ?? throw new ArgumentException("Lobby not found.");
        if (lobby.Players.Count == 0 || lobby.Players[0].Id != userId)
            throw new UnauthorizedAccessException("Only the lobby leader can change settings.");

        var s = _settings.GetOrDefault(lobby.LobbyId);
        s.RoundsToWin = req.RoundsToWin;
        s.BombLimit = req.BombLimit;
        _settings.Set(lobby.LobbyId, s);
        return Ok(s);
    }

    [HttpPost("create")]
    public ActionResult<Lobby> Create([FromBody] CreateLobbyRequest req)
    {
        Guard.AtLeast(req.MaxPlayers, 2, "maxPlayers");
        var lobby = _lobbies.Create(req.MaxPlayers);
        return Ok(lobby);
    }

    [HttpPost("join")]
    public ActionResult<Lobby> Join([FromBody] JoinLobbyRequest req)
    {
        Guard.NotEmpty(req.LobbyId, "lobbyId"); // accepts GUID *or* code
        Guard.NotEmpty(req.Token, "token");

        var (ok, userId) = _jwt.Validate(req.Token);
        if (!ok || userId is null) throw new UnauthorizedAccessException("Invalid token.");

        var user = _users.FindById(userId) ?? throw new UnauthorizedAccessException("User not found for token.");
        var lobby = _lobbies.GetByLobbyOrCode(req.LobbyId) ?? throw new ArgumentException("Lobby not found.");
        var updated = _lobbies.Join(lobby.LobbyId, user, maxPlayers: 4);
        return Ok(updated);
    }

    public record StartLobbyRequest(string LobbyId, string Token);

    [HttpPost("start")]
    public ActionResult<GameState> Start([FromBody] StartLobbyRequest req)
    {
        Guard.NotEmpty(req.LobbyId, "lobbyId");
        Guard.NotEmpty(req.Token, "token");

        var (ok, userId) = _jwt.Validate(req.Token);
        if (!ok || userId is null) throw new UnauthorizedAccessException("Invalid token.");

        var lobby = _lobbies.GetByLobbyOrCode(req.LobbyId) ?? throw new ArgumentException("Lobby not found.");
        if (lobby.Players.Count == 0) throw new UnauthorizedAccessException("No players in lobby.");
        if (lobby.Players[0].Id != userId) throw new UnauthorizedAccessException("Only the lobby leader can start the game.");
        if (lobby.Players.Count < 2) throw new ArgumentException("At least 2 players required to start.");

        lobby.Status = LobbyStatus.InProgress;
        var s = _settings.GetOrDefault(lobby.LobbyId);
        var game = _games.StartForLobby(lobby, s.RoundsToWin, s.BombLimit);
        return Ok(game);
    }
}
