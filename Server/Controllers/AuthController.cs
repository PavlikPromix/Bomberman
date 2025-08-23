using Bomberman.Server.Infrastructure;
using Bomberman.Server.Models;
using Bomberman.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bomberman.Server.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepo _users;
    private readonly IJwtService _jwt;
    public AuthController(IUserRepo users, IJwtService jwt) { _users = users; _jwt = jwt; }

    [HttpPost("login")]
    public ActionResult<LoginResponse> Login([FromBody] LoginRequest req)
    {
        Guard.NotEmpty(req.Username, "username");
        Guard.NotEmpty(req.Password, "password"); // In real life: verify properly

        var user = _users.Upsert(req.Username);
        var token = _jwt.IssueToken(user.Id, user.Username);
        return Ok(new LoginResponse(token, user));
    }
}
