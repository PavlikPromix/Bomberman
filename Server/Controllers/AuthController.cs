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

    public record LoginReq(string Username, string Password);
    public record LoginRes(string Token, User User);

    [HttpPost("login")]
    public ActionResult<LoginRes> Login([FromBody] LoginReq req)
    {
        Guard.NotEmpty(req.Username, "username");
        Guard.NotEmpty(req.Password, "password");

        var existing = _users.FindByUsername(req.Username);
        if (existing == null)
        {
            // Create new user with this password
            var user = _users.CreateUser(req.Username, req.Password);
            var token = _jwt.TokenFor(user.Id);
            return Ok(new LoginRes(token, user));
        }
        else
        {
            // Existing user must provide correct password
            if (!_users.VerifyPassword(req.Username, req.Password))
                return Unauthorized(new ErrorResponse { ErrorCode = "auth_failed", ErrorMessage = "Invalid username or password." });

            var token = _jwt.TokenFor(existing.Id);
            return Ok(new LoginRes(token, existing));
        }
    }
}
