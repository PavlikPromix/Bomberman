using System.Text.Json.Serialization;

namespace Bomberman.Server.Models;

public class User
{
    public string Id { get; set; } = default!; // GUID
    public string Username { get; set; } = default!;
    public UserStats Stats { get; set; } = new();
}

public class UserStats
{
    public int GamesPlayed { get; set; }
    public int GamesWon { get; set; }
    public int TotalScore { get; set; }
}

public class Lobby
{
    public string LobbyId { get; set; } = default!;
    public List<User> Players { get; set; } = new();
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LobbyStatus Status { get; set; } = LobbyStatus.Waiting;
}
public enum LobbyStatus { Waiting, InProgress }

public class GameState
{
    public string GameId { get; set; } = default!;
    public string LobbyId { get; set; } = default!;
    public List<User> Players { get; set; } = new();
    public int[][] Board { get; set; } = Array.Empty<int[]>();
    public bool Active { get; set; }
}

public class ErrorResponse
{
    public string ErrorCode { get; set; } = default!;
    public string ErrorMessage { get; set; } = default!;
}

// Request/Response DTOs
public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, User User);

public record LeaderboardResponse(List<User> Users, int Page, int TotalPages);
public record CreateLobbyRequest(int MaxPlayers);
public record JoinLobbyRequest(string LobbyId, string Token);

// WebSocket DTOs
public record WsClientMessage(string GameId, string PlayerId, string Move);
public record WsServerSuccess(GameState GameState);
