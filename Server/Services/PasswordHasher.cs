using System.Security.Cryptography;
using System.Text;

namespace Bomberman.Server.Services;

public static class PasswordHasher
{
    public static (string Hash, string Salt) Hash(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 100_000, HashAlgorithmName.SHA256);
        var hashBytes = pbkdf2.GetBytes(32);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    public static bool Verify(string password, string saltBase64, string hashBase64)
    {
        var saltBytes = Convert.FromBase64String(saltBase64);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 100_000, HashAlgorithmName.SHA256);
        var computed = pbkdf2.GetBytes(32);
        var expected = Convert.FromBase64String(hashBase64);
        return CryptographicOperations.FixedTimeEquals(computed, expected);
    }
}
