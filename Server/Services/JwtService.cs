using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Bomberman.Server.Services;

public interface IJwtService
{
    string IssueToken(string userId, string username);
    (bool ok, string? userId) Validate(string token);
}

public class JwtService : IJwtService
{
    private readonly byte[] _key;
    public JwtService(IConfiguration cfg)
    {
        _key = Encoding.UTF8.GetBytes(cfg["Jwt:Key"] ?? "REPLACE_ME_DEV_KEY_256_BITS_____");
    }

    public string IssueToken(string userId, string username)
    {
        var creds = new SigningCredentials(new SymmetricSecurityKey(_key), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            claims: new[] { new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Name, username) },
            expires: DateTime.UtcNow.AddHours(6),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    public (bool ok, string? userId) Validate(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        try
        {
            var res = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                IssuerSigningKey = new SymmetricSecurityKey(_key),
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            }, out var _);
            var id = res.FindFirstValue(ClaimTypes.NameIdentifier);
            return (id != null, id);
        }
        catch { return (false, null); }
    }
}
