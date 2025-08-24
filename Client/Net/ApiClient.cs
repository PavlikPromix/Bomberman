using System;                         // Uri
using System.Collections.Generic;     // List<>, Dictionary<>
using System.Net.Http;                // HttpClient
using System.Net.Http.Json;           // PostAsJsonAsync, ReadFromJsonAsync
using System.Threading.Tasks;         // Task

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

    public async Task<Lobby> GetLobbyAsync(string lobbyIdOrCode)
        => (await _http.GetFromJsonAsync<Lobby>($"/api/lobbies/{lobbyIdOrCode}"))!;

    public async Task<string> GetLobbyCodeAsync(string lobbyId)
    {
        var obj = await _http.GetFromJsonAsync<CodeDto>($"/api/lobbies/{lobbyId}/code");
        return obj!.Code;
    }

    public async Task<LobbySettings> GetLobbySettingsAsync(string lobbyId)
        => (await _http.GetFromJsonAsync<LobbySettings>($"/api/lobbies/{lobbyId}/settings"))!;

    public async Task<LobbySettings> SetLobbySettingsAsync(string lobbyId, int roundsToWin, int bombLimit)
    {
        var res = await _http.PostAsJsonAsync("/api/lobbies/settings", new { lobbyId, token = Token, roundsToWin, bombLimit });
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadFromJsonAsync<ErrorResponse>();
            throw new Exception($"{err?.ErrorCode}: {err?.ErrorMessage}");
        }
        return (await res.Content.ReadFromJsonAsync<LobbySettings>())!;
    }

    public async Task<GameState> StartLobbyAsync(string lobbyId)
    {
        var res = await _http.PostAsJsonAsync("/api/lobbies/start", new { lobbyId, token = Token });
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadFromJsonAsync<ErrorResponse>();
            throw new Exception($"{err?.ErrorCode}: {err?.ErrorMessage}");
        }
        return (await res.Content.ReadFromJsonAsync<GameState>())!;
    }

    public async Task<GameState> GetActiveGameByLobbyAsync(string lobbyId)
        => (await _http.GetFromJsonAsync<GameState>($"/api/game/by-lobby/{lobbyId}"))!;

    private class CodeDto { public string Code { get; set; } = ""; }
}

// Contracts
public class User { public string Id { get; set; } = ""; public string Username { get; set; } = ""; public UserStats Stats { get; set; } = new(); }
public class UserStats { public int GamesPlayed { get; set; } public int GamesWon { get; set; } public int TotalScore { get; set; } }
public class Lobby { public string LobbyId { get; set; } = ""; public List<User> Players { get; set; } = new(); public string Status { get; set; } = "waiting"; }
public class LobbySettings { public int RoundsToWin { get; set; } = 5; public int BombLimit { get; set; } = 3; }

public class GameState
{
    public string GameId { get; set; } = "";
    public string LobbyId { get; set; } = "";
    public List<User> Players { get; set; } = new();
    public int[][] Board { get; set; } = Array.Empty<int[]>();
    public bool Active { get; set; }
    public Dictionary<string,int> Scores { get; set; } = new();
    public int RoundsToWin { get; set; } = 5;
    public string? WinnerId { get; set; }
}

public class ErrorResponse { public string ErrorCode { get; set; } = ""; public string ErrorMessage { get; set; } = ""; }
public class LoginResponse { public string Token { get; set; } = ""; public User User { get; set; } = new(); }
public class LeaderboardResponse { public List<User> Users { get; set; } = new(); public int Page { get; set; } public int TotalPages { get; set; } }
