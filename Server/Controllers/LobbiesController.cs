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
        Guard.NotEmpty(req.LobbyId, "lobbyId");
        Guard.NotEmpty(req.Token, "token");

        var (ok, userId) = _jwt.Validate(req.Token);
        if (!ok || userId is null) throw new UnauthorizedAccessException("Invalid token.");

        var user = _users.FindById(userId) ?? throw new UnauthorizedAccessException("User not found for token.");
        var lobby = _lobbies.Get(req.LobbyId) ?? throw new ArgumentException("Lobby not found.");

        // Retrieve stored capacity (we keep it internal); assume 4 default if missing
        var updated = _lobbies.Join(lobby.LobbyId, user, maxPlayers: 4);

        // Optionally auto-start a game when status flips to InProgress
        if (updated.Status == LobbyStatus.InProgress)
        {
            _games.StartForLobby(updated);
        }
        return Ok(updated);
    }
}
