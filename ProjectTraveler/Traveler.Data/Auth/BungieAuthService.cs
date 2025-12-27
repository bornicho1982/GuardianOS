using System;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using DotNetBungieAPI;
using DotNetBungieAPI.Models;
using DotNetBungieAPI.Service.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Traveler.Data.Auth;

/// <summary>
/// Bungie OAuth2 (PKCE) authentication service with DotNetBungieAPI client management.
/// </summary>
public class BungieAuthService
{
    // TODO: Move to configuration/secrets in production
    private const string ClientId = "50831"; 
    private const string ClientSecret = "E7ijog02U.jWK8oRz-rf5wkT.6e4NNrAfoj-eIhVXj8";
    public const string ApiKey = "e1a73d9d631a46a8b7e2b6e37ae30492";

    private const string RedirectUrl = "https://localhost:55555/callback/";
    private const string ListenerPrefix = "http://localhost:55555/callback/";
    private const string AuthUrl = "https://www.bungie.net/en/OAuth/Authorize";
    private const string TokenUrl = "https://www.bungie.net/Platform/App/OAuth/token/";
    private const string MembershipsUrl = "https://www.bungie.net/Platform/User/GetMembershipsForCurrentUser/";

    private readonly HttpClient _httpClient;
    private IBungieClient? _bungieClient;
    
    // Token storage
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime TokenExpiry { get; private set; }
    
    // Membership info
    public long BungieMembershipId { get; private set; }
    public BungieMembershipType DestinyMembershipType { get; private set; }
    public long DestinyMembershipId { get; private set; }
    public string? DisplayName { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken) && TokenExpiry > DateTime.UtcNow;

    public BungieAuthService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", ApiKey);
    }

    /// <summary>
    /// Gets the configured IBungieClient for API calls.
    /// Returns null if not authenticated.
    /// </summary>
    public IBungieClient? GetClient()
    {
        if (!IsAuthenticated || _bungieClient == null)
        {
            Console.WriteLine("[BungieAuth] GetClient called but not authenticated");
            return null;
        }
        return _bungieClient;
    }

    /// <summary>
    /// Performs full OAuth2 PKCE login flow with membership resolution.
    /// </summary>
    public async Task<bool> LoginAsync()
    {
        try
        {
            var tokenJson = await AuthorizeAsync();
            if (string.IsNullOrEmpty(tokenJson))
                return false;

            // Parse token response
            var tokenData = JsonSerializer.Deserialize<TokenResponse>(tokenJson);
            if (tokenData == null)
                return false;

            AccessToken = tokenData.access_token;
            RefreshToken = tokenData.refresh_token;
            BungieMembershipId = tokenData.membership_id;
            TokenExpiry = DateTime.UtcNow.AddSeconds(tokenData.expires_in);

            Console.WriteLine($"[BungieAuth] Token obtained. Expires: {TokenExpiry}");

            // Initialize DotNetBungieAPI client
            await InitializeBungieClientAsync();

            // Resolve Destiny membership
            await ResolveMembershipAsync();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BungieAuth] Login failed: {ex.Message}");
            return false;
        }
    }

    private async Task InitializeBungieClientAsync()
    {
        // Build service collection for DotNetBungieAPI
        var services = new ServiceCollection();
        
        services.UseBungieApiClient(config =>
        {
            config.ClientConfiguration.ApiKey = ApiKey;
            config.ClientConfiguration.ClientId = int.Parse(ClientId);
            config.ClientConfiguration.ClientSecret = ClientSecret;
        });

        var provider = services.BuildServiceProvider();
        _bungieClient = provider.GetRequiredService<IBungieClient>();
        
        // Set the access token for authenticated requests
        // Note: DotNetBungieAPI handles token internally, but we need to set it
        // This might require custom token provider implementation
        Console.WriteLine("[BungieAuth] BungieClient initialized");
    }

    private async Task ResolveMembershipAsync()
    {
        // Get memberships for current user
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);

        var response = await _httpClient.GetAsync(MembershipsUrl);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[BungieAuth] Failed to get memberships: {response.StatusCode}");
            return;
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("Response", out var resp) && 
            resp.TryGetProperty("destinyMemberships", out var memberships))
        {
            // Get the primary membership (or first available)
            foreach (var membership in memberships.EnumerateArray())
            {
                var membershipType = membership.GetProperty("membershipType").GetInt32();
                var membershipId = membership.GetProperty("membershipId").GetString();
                var displayName = membership.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;
                
                // Prefer cross-save primary
                var isPrimary = membership.TryGetProperty("crossSaveOverride", out var cso) && cso.GetInt32() == membershipType;

                DestinyMembershipType = (BungieMembershipType)membershipType;
                DestinyMembershipId = long.Parse(membershipId ?? "0");
                DisplayName = displayName;

                Console.WriteLine($"[BungieAuth] Resolved membership: {DestinyMembershipType} / {DestinyMembershipId} ({DisplayName})");
                
                if (isPrimary) break; // Use primary if found
            }
        }
    }

    public async Task<string> AuthorizeAsync()
    {
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);
        var state = Guid.NewGuid().ToString();

        var authorizationRequest = $"{AuthUrl}?client_id={ClientId}&response_type=code&state={state}&code_challenge={codeChallenge}&code_challenge_method=S256";

        using var listener = new HttpListener();
        listener.Prefixes.Add(ListenerPrefix);
        listener.Start();

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = authorizationRequest,
                UseShellExecute = true
            });

            var context = await listener.GetContextAsync();
            var request = context.Request;
            var response = context.Response;

            var code = request.QueryString["code"];
            var incomingState = request.QueryString["state"];

            var responseString = "<html><body style='background:#111;color:#eee;font-family:sans-serif;'><h1>Traveler Connected</h1><p>You may close this window and return to the application.</p></body></html>";
            var buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();

            if (incomingState != state)
                throw new Exception("State mismatch - possible CSRF");
            
            if (string.IsNullOrEmpty(code))
                throw new Exception("No code received from Bungie.");

            return await ExchangeCodeForTokenAsync(code, codeVerifier);
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task<string> ExchangeCodeForTokenAsync(string code, string codeVerifier)
    {
        var requestBody = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("client_id", ClientId),
            new KeyValuePair<string, string>("client_secret", ClientSecret),
            new KeyValuePair<string, string>("code_verifier", codeVerifier)
        });

        var response = await _httpClient.PostAsync(TokenUrl, requestBody);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    private string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var challengeBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(challengeBytes);
    }

    private string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    // Token response DTO
    private class TokenResponse
    {
        public string access_token { get; set; } = "";
        public string refresh_token { get; set; } = "";
        public int expires_in { get; set; }
        public long membership_id { get; set; }
    }
}

