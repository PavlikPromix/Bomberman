using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Bomberman.Server.Services;

public static class JwtServiceExtensions
{
    /// <summary>
    /// Issues a JWT for the given userId using the symmetric key in env var Jwt__Key (or Jwt:Key).
    /// Valid for 7 days. Puts userId into the "sub" claim.
    /// </summary>
    public static string TokenFor(this IJwtService _, string userId)
    {
        var key = Environment.GetEnvironmentVariable("Jwt__Key")
               ?? Environment.GetEnvironmentVariable("Jwt:Key")
               ?? throw new InvalidOperationException("JWT key not configured. Set Jwt__Key.");
        var bytes = Encoding.UTF8.GetBytes(key);
        if (bytes.Length < 32)
            throw new InvalidOperationException("Jwt__Key must be at least 32 bytes.");

        var creds = new SigningCredentials(new SymmetricSecurityKey(bytes), SecurityAlgorithms.HmacSha256);
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, userId) };

        var token = new JwtSecurityToken(
            issuer: null,
            audience: null,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
