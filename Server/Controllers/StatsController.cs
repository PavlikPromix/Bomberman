using Bomberman.Server.Infrastructure;
using Bomberman.Server.Models;
using Bomberman.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bomberman.Server.Controllers;

[ApiController]
[Route("api/stats")]
public class StatsController : ControllerBase
{
    private readonly IUserRepo _users;
    public StatsController(IUserRepo users) => _users = users;

    [HttpGet("leaderboard")]
    public ActionResult<LeaderboardResponse> Leaderboard([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        Guard.Positive(page, "page");
        Guard.Positive(pageSize, "pageSize");
        if (pageSize > 50) throw new ArgumentException("pageSize must be <= 50.");

        var ordered = _users.All().OrderByDescending(u => u.Stats.TotalScore).ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(ordered.Count / (double)pageSize));
        if (page > totalPages) page = totalPages;
        var slice = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Ok(new LeaderboardResponse(slice, page, totalPages));
    }
}
