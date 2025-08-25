using Microsoft.Data.Sqlite;
using Bomberman.Server.Models;

namespace Bomberman.Server.Services;

public class SqliteUserRepo : IUserRepo
{
    private readonly string _cs;
    private readonly object _lock = new();

    public SqliteUserRepo(IWebHostEnvironment env)
    {
        var dbPath = Path.Combine(env.ContentRootPath, "App_Data", "bomberman.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _cs = $"Data Source={dbPath}";
        Init();
    }

    private void Init()
    {
        using var con = new SqliteConnection(_cs);
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Users(
  Id TEXT PRIMARY KEY,
  Username TEXT NOT NULL UNIQUE,
  PasswordHash TEXT NOT NULL,
  Salt TEXT NOT NULL,
  GamesPlayed INTEGER NOT NULL DEFAULT 0,
  GamesWon INTEGER NOT NULL DEFAULT 0,
  TotalScore INTEGER NOT NULL DEFAULT 0
);
";
        cmd.ExecuteNonQuery();
    }

    public User? FindById(string id)
    {
        using var con = new SqliteConnection(_cs); con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT Id,Username,GamesPlayed,GamesWon,TotalScore FROM Users WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return ReadUser(r);
    }

    public User? FindByUsername(string username)
    {
        using var con = new SqliteConnection(_cs); con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT Id,Username,GamesPlayed,GamesWon,TotalScore FROM Users WHERE Username=@u";
        cmd.Parameters.AddWithValue("@u", username);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return ReadUser(r);
    }

    public User CreateUser(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Password required.");
        lock (_lock)
        {
            using var con = new SqliteConnection(_cs); con.Open();
            using var tx = con.BeginTransaction();
            using var check = con.CreateCommand();
            check.Transaction = tx;
            check.CommandText = "SELECT 1 FROM Users WHERE Username=@u";
            check.Parameters.AddWithValue("@u", username);
            if (check.ExecuteScalar() != null) throw new ArgumentException("User exists.");

            var (hash, salt) = PasswordHasher.Hash(password);
            var id = Guid.NewGuid().ToString();

            using var ins = con.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = "INSERT INTO Users(Id,Username,PasswordHash,Salt,GamesPlayed,GamesWon,TotalScore) VALUES(@id,@u,@h,@s,0,0,0)";
            ins.Parameters.AddWithValue("@id", id);
            ins.Parameters.AddWithValue("@u", username);
            ins.Parameters.AddWithValue("@h", hash);
            ins.Parameters.AddWithValue("@s", salt);
            ins.ExecuteNonQuery();
            tx.Commit();

            return new User { Id = id, Username = username, Stats = new UserStats() };
        }
    }

    public bool VerifyPassword(string username, string password)
    {
        using var con = new SqliteConnection(_cs); con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT PasswordHash,Salt FROM Users WHERE Username=@u";
        cmd.Parameters.AddWithValue("@u", username);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return false;
        var hash = r.GetString(0);
        var salt = r.GetString(1);
        return PasswordHasher.Verify(password, salt, hash);
    }

    public void AddStats(string userId, int playedDelta, int wonDelta, int scoreDelta)
    {
        using var con = new SqliteConnection(_cs); con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"UPDATE Users SET GamesPlayed=GamesPlayed+@p, GamesWon=GamesWon+@w, TotalScore=TotalScore+@s WHERE Id=@id";
        cmd.Parameters.AddWithValue("@p", playedDelta);
        cmd.Parameters.AddWithValue("@w", wonDelta);
        cmd.Parameters.AddWithValue("@s", scoreDelta);
        cmd.Parameters.AddWithValue("@id", userId);
        cmd.ExecuteNonQuery();
    }

    public IEnumerable<User> All()
    {
        using var con = new SqliteConnection(_cs); con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT Id,Username,GamesPlayed,GamesWon,TotalScore FROM Users ORDER BY TotalScore DESC, Username ASC";
        using var r = cmd.ExecuteReader();
        while (r.Read()) yield return ReadUser(r);
    }

    private static User ReadUser(SqliteDataReader r) => new User
    {
        Id = r.GetString(0),
        Username = r.GetString(1),
        Stats = new UserStats {
            GamesPlayed = r.GetInt32(2),
            GamesWon = r.GetInt32(3),
            TotalScore = r.GetInt32(4)
        }
    };
}
