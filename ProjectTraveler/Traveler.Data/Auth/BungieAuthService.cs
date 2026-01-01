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
using Microsoft.Extensions.Logging;

using Traveler.Core.Interfaces;

namespace Traveler.Data.Auth;

/// <summary>
/// DTO for persisting authentication data
/// </summary>
public class AuthTokenData
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime TokenExpiry { get; set; }
    public long BungieMembershipId { get; set; }
}

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
    private const string ListenerPrefix = "https://localhost:55555/callback/";
    private const string AuthUrl = "https://www.bungie.net/en/OAuth/Authorize";
    private const string TokenUrl = "https://www.bungie.net/Platform/App/OAuth/token/";
    private const string MembershipsUrl = "https://www.bungie.net/Platform/User/GetMembershipsForCurrentUser/";

    private readonly HttpClient _httpClient;
    private readonly ILocalDatabaseService _dbService; // New Dependency
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

    public BungieAuthService(ILocalDatabaseService dbService)
    {
        _dbService = dbService;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", ApiKey);
        
        // Try to load token from DB
        TryLoadToken();
    }
    
    private void TryLoadToken()
    {
        try
        {
            var tokenData = _dbService.Load<AuthTokenData>("auth_tokens", t => true); // Load single/first token
            if (tokenData != null)
            {
                AccessToken = tokenData.AccessToken;
                RefreshToken = tokenData.RefreshToken;
                TokenExpiry = tokenData.TokenExpiry;
                BungieMembershipId = tokenData.BungieMembershipId;
                
                if (IsAuthenticated)
                {
                    Console.WriteLine("[BungieAuth] Token loaded from DB and is VALID.");
                    _ = ResolveMembershipAsync(); // Restore membership info background
                }
                else if (!string.IsNullOrEmpty(RefreshToken))
                {
                    Console.WriteLine("[BungieAuth] Token loaded from DB (Expired). Refreshing...");
                     _ = RefreshTokenAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BungieAuth] Failed to load token: {ex.Message}");
        }
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
            _ = long.TryParse(tokenData.membership_id, out var membershipId);
            BungieMembershipId = membershipId;
            TokenExpiry = DateTime.UtcNow.AddSeconds(tokenData.expires_in);
            
            // SAVE TO DB
            _dbService.Save(new AuthTokenData
            {
                AccessToken = AccessToken,
                RefreshToken = RefreshToken,
                TokenExpiry = TokenExpiry,
                BungieMembershipId = BungieMembershipId
            }, "auth_tokens");

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
        // Note: DotNetBungieAPI client initialization is optional
        // We use direct HttpClient calls for most operations
        // This is here for future use if needed
        try
        {
            var services = new ServiceCollection();
            
            // Add required logging
            services.AddLogging();
            
            services.UseBungieApiClient(config =>
            {
                config.ClientConfiguration.ApiKey = ApiKey;
                config.ClientConfiguration.ClientId = int.Parse(ClientId);
                config.ClientConfiguration.ClientSecret = ClientSecret;
            });

            var provider = services.BuildServiceProvider();
            _bungieClient = provider.GetRequiredService<IBungieClient>();
            
            Console.WriteLine("[BungieAuth] BungieClient initialized (optional)");
        }
        catch (Exception ex)
        {
            // Non-fatal: we can still use HttpClient directly
            Console.WriteLine($"[BungieAuth] BungieClient init skipped (using HttpClient): {ex.Message}");
            _bungieClient = null;
        }
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

        // Include redirect_uri in the authorization request
        var encodedRedirect = Uri.EscapeDataString(RedirectUrl);
        var authorizationRequest = $"{AuthUrl}?client_id={ClientId}&response_type=code&state={state}&code_challenge={codeChallenge}&code_challenge_method=S256&redirect_uri={encodedRedirect}";

        // Get the development certificate
        var cert = GetDevelopmentCertificate();
        if (cert == null)
        {
            throw new Exception("No se encontró el certificado de desarrollo HTTPS. Ejecuta: dotnet dev-certs https --trust");
        }

        // Start HTTPS server
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 55555);
        listener.Start();

        try
        {
            Console.WriteLine("[BungieAuth] Servidor HTTPS iniciado en puerto 55555");
            
            // Open browser
            Process.Start(new ProcessStartInfo
            {
                FileName = authorizationRequest,
                UseShellExecute = true
            });

            // Accept connection
            using var client = await listener.AcceptTcpClientAsync();
            using var sslStream = new System.Net.Security.SslStream(client.GetStream(), false);
            
            // Authenticate as server with certificate
            await sslStream.AuthenticateAsServerAsync(cert, false, System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13, false);
            
            Console.WriteLine("[BungieAuth] Conexión SSL establecida");

            // Read the HTTP request
            var buffer = new byte[4096];
            var bytesRead = await sslStream.ReadAsync(buffer, 0, buffer.Length);
            var requestText = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            
            Console.WriteLine($"[BungieAuth] Request recibido: {requestText.Split('\n')[0]}");

            // Parse query string from GET request
            // Format: GET /callback/?code=xxx&state=yyy HTTP/1.1
            var firstLine = requestText.Split('\n')[0];
            var urlPart = firstLine.Split(' ')[1]; // /callback/?code=xxx&state=yyy
            var queryString = urlPart.Contains('?') ? urlPart.Split('?')[1] : "";
            var queryParams = System.Web.HttpUtility.ParseQueryString(queryString);
            
            var code = queryParams["code"];
            var incomingState = queryParams["state"];

            // Send HTTP response
            var responseHtml = "<html><body style='background:#111;color:#eee;font-family:sans-serif;text-align:center;padding-top:100px;'><h1>✓ GuardianOS Conectado</h1><p>Puedes cerrar esta ventana y volver a la aplicación.</p></body></html>";
            var responseBytes = Encoding.UTF8.GetBytes(
                $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {Encoding.UTF8.GetByteCount(responseHtml)}\r\nConnection: close\r\n\r\n{responseHtml}");
            
            await sslStream.WriteAsync(responseBytes, 0, responseBytes.Length);
            await sslStream.FlushAsync();

            if (incomingState != state)
                throw new Exception("State mismatch - possible CSRF");
            
            if (string.IsNullOrEmpty(code))
                throw new Exception("No code received from Bungie.");

            Console.WriteLine("[BungieAuth] Código de autorización recibido, intercambiando por token...");
            return await ExchangeCodeForTokenAsync(code, codeVerifier);
        }
        finally
        {
            listener.Stop();
            Console.WriteLine("[BungieAuth] Servidor HTTPS detenido");
        }
    }

    private System.Security.Cryptography.X509Certificates.X509Certificate2? GetDevelopmentCertificate()
    {
        // Look for the .NET development certificate in the current user's certificate store
        using var store = new System.Security.Cryptography.X509Certificates.X509Store(
            System.Security.Cryptography.X509Certificates.StoreName.My, 
            System.Security.Cryptography.X509Certificates.StoreLocation.CurrentUser);
        
        store.Open(System.Security.Cryptography.X509Certificates.OpenFlags.ReadOnly);
        
        // Find certificates with the ASP.NET Core HTTPS development certificate OID
        var certs = store.Certificates
            .Find(System.Security.Cryptography.X509Certificates.X509FindType.FindByExtension, "1.3.6.1.4.1.311.84.1.1", false);
        
        if (certs.Count > 0)
        {
            Console.WriteLine($"[BungieAuth] Certificado de desarrollo encontrado: {certs[0].Subject}");
            return certs[0];
        }
        
        // Fallback: look for any localhost certificate
        var localCerts = store.Certificates
            .Find(System.Security.Cryptography.X509Certificates.X509FindType.FindBySubjectName, "localhost", false);
        
        if (localCerts.Count > 0)
        {
            Console.WriteLine($"[BungieAuth] Certificado localhost encontrado: {localCerts[0].Subject}");
            return localCerts[0];
        }
        
        return null;
    }

    private async Task<string> ExchangeCodeForTokenAsync(string code, string codeVerifier)
    {
        var requestBody = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("client_id", ClientId),
            new KeyValuePair<string, string>("client_secret", ClientSecret),
            new KeyValuePair<string, string>("code_verifier", codeVerifier),
            new KeyValuePair<string, string>("redirect_uri", RedirectUrl) // CRITICAL: Required by Bungie OAuth
        });

        Console.WriteLine($"[BungieAuth] Token exchange request to: {TokenUrl}");
        Console.WriteLine($"[BungieAuth] Using redirect_uri: {RedirectUrl}");
        
        var response = await _httpClient.PostAsync(TokenUrl, requestBody);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[BungieAuth] Token exchange failed: {response.StatusCode}");
            Console.WriteLine($"[BungieAuth] Error details: {errorContent}");
            response.EnsureSuccessStatusCode(); // Throw with proper message
        }

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

    /// <summary>
    /// Refreshes the access token using the stored refresh token.
    /// </summary>
    public async Task<bool> RefreshTokenAsync()
    {
        if (string.IsNullOrEmpty(RefreshToken))
        {
            Console.WriteLine("[BungieAuth] No refresh token available");
            return false;
        }

        try
        {
            var requestBody = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", RefreshToken),
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("client_secret", ClientSecret)
            });

            var response = await _httpClient.PostAsync(TokenUrl, requestBody);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[BungieAuth] Token refresh failed: {response.StatusCode}");
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<TokenResponse>(json);
            
            if (tokenData == null)
                return false;

            AccessToken = tokenData.access_token;
            RefreshToken = tokenData.refresh_token;
            TokenExpiry = DateTime.UtcNow.AddSeconds(tokenData.expires_in);

            // Update HTTP client header
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);

            Console.WriteLine($"[BungieAuth] Token refreshed. New expiry: {TokenExpiry}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BungieAuth] Token refresh error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Ensures we have a valid token, refreshing if necessary.
    /// </summary>
    public async Task<bool> EnsureValidTokenAsync()
    {
        if (string.IsNullOrEmpty(AccessToken))
            return false;

        // Refresh if token expires within 5 minutes
        if (TokenExpiry <= DateTime.UtcNow.AddMinutes(5))
        {
            return await RefreshTokenAsync();
        }

        return true;
    }

    /// <summary>
    /// Gets the access token, refreshing if needed.
    /// </summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        if (!await EnsureValidTokenAsync())
            return null;
        return AccessToken;
    }

    // Token response DTO
    private class TokenResponse
    {
        public string access_token { get; set; } = "";
        public string refresh_token { get; set; } = "";
        public int expires_in { get; set; }
        public string membership_id { get; set; } = "";  // Bungie sends this as a string
    }
}

