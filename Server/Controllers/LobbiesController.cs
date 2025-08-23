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

    public LobbiesController(ILobbyRepo lobbies, IUserRepo users, IJwtService jwt, IGameRepo games)
    { _lobbies = lobbies; _users = users; _jwt = jwt; _games = games; }

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
        Guard.NotEmpty(req.LobbyId, "lobbyId"); // accepts GUID *or* 5-char code
        Guard.NotEmpty(req.Token, "token");

        var (ok, userId) = _jwt.Validate(req.Token);
        if (!ok || userId is null) throw new UnauthorizedAccessException("Invalid token.");

        var user = _users.FindById(userId) ?? throw new UnauthorizedAccessException("User not found for token.");
        var lobby = _lobbies.GetByLobbyOrCode(req.LobbyId) ?? throw new ArgumentException("Lobby not found.");

        // capacity is managed internally in repo; just join
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
        var leaderId = lobby.Players[0].Id;
        if (leaderId != userId) throw new UnauthorizedAccessException("Only the lobby leader can start the game.");
        if (lobby.Players.Count < 2) throw new ArgumentException("At least 2 players required to start.");

        lobby.Status = LobbyStatus.InProgress;
        var game = _games.StartForLobby(lobby);
        return Ok(game);
    }
}
