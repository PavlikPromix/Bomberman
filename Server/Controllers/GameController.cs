using Bomberman.Server.Infrastructure;
using Bomberman.Server.Models;
using Bomberman.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bomberman.Server.Controllers;

[ApiController]
[Route("api/game")]
public class GameController : ControllerBase
{
    private readonly IGameRepo _games;
    public GameController(IGameRepo games) => _games = games;

    // GET /api/game/by-lobby/{lobbyId}  -> GameState (if active) or 400
    [HttpGet("by-lobby/{lobbyId}")]
    public ActionResult<GameState> GetByLobby([FromRoute] string lobbyId)
    {
        Guard.NotEmpty(lobbyId, "lobbyId");
        var gs = _games.GetByLobby(lobbyId) ?? throw new ArgumentException("Game not found for lobby.");
        return Ok(gs);
    }
}
