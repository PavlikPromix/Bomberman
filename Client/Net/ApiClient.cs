using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Bomberman.Client.Net;

public class ApiClient
{
    private readonly HttpClient _http;
    public string? Token { get; private set; }
    public User? Me { get; private set; }

    public ApiClient(string baseUrl) => _http = new HttpClient { BaseAddress = new Uri(baseUrl) };

    public async Task LoginAsync(string username, string password)
    {
        var res = await _http.PostAsJsonAsync("/api/auth/login", new { username, password });
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadFromJsonAsync<ErrorResponse>();
            throw new Exception($"{err?.ErrorCode}: {err?.ErrorMessage}");
        }
        var ok = await res.Content.ReadFromJsonAsync<LoginResponse>();
        Token = ok!.Token; Me = ok!.User;
    }

    public async Task<LeaderboardResponse> GetLeaderboardAsync(int page = 1, int pageSize = 10)
        => await _http.GetFromJsonAsync<LeaderboardResponse>($"/api/stats/leaderboard?page={page}&pageSize={pageSize}")!;

    public async Task<Lobby> CreateLobbyAsync(int maxPlayers)
    {
        var res = await _http.PostAsJsonAsync("/api/lobbies/create", new { maxPlayers });
        return await res.Content.ReadFromJsonAsync<Lobby>()!;
    }

    public async Task<Lobby> JoinLobbyAsync(string lobbyId)
    {
        var res = await _http.PostAsJsonAsync("/api/lobbies/join", new { lobbyId, token = Token });
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadFromJsonAsync<ErrorResponse>();
            throw new Exception($"{err?.ErrorCode}: {err?.ErrorMessage}");
        }
        return (await res.Content.ReadFromJsonAsync<Lobby>())!;
    }
}

// Contracts mirror server shapes
public class User { public string Id { get; set; } = ""; public string Username { get; set; } = ""; public UserStats Stats { get; set; } = new(); }
public class UserStats { public int GamesPlayed { get; set; } public int GamesWon { get; set; } public int TotalScore { get; set; } }
public class Lobby { public string LobbyId { get; set; } = ""; public List<User> Players { get; set; } = new(); public string Status { get; set; } = "waiting"; }
public class GameState { public string GameId { get; set; } = ""; public string LobbyId { get; set; } = ""; public List<User> Players { get; set; } = new(); public int[][] Board { get; set; } = Array.Empty<int[]>(); public bool Active { get; set; } }
public class ErrorResponse { public string ErrorCode { get; set; } = ""; public string ErrorMessage { get; set; } = ""; }
public class LoginResponse { public string Token { get; set; } = ""; public User User { get; set; } = new(); }
public class LeaderboardResponse { public List<User> Users { get; set; } = new(); public int Page { get; set; } public int TotalPages { get; set; } }
