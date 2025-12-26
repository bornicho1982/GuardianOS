using System;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using System.Collections.Generic;

namespace Traveler.Data.Auth;

public class BungieAuthService
{
    // TODO: Move to configuration/secrets
    private const string ClientId = "YOUR_CLIENT_ID"; 
    // private const string ClientSecret = "YOUR_CLIENT_SECRET"; // Logic differs for public/confidential clients
    private const string RedirectUrl = "https://localhost:55555/callback"; 
    private const string ListenerPrefix = "http://localhost:55555/callback/";
    private const string AuthUrl = "https://www.bungie.net/en/OAuth/Authorize";
    private const string TokenUrl = "https://www.bungie.net/Platform/App/OAuth/token/";

    private HttpClient _httpClient;

    public BungieAuthService()
    {
        _httpClient = new HttpClient();
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

            // Exchange code for token
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
            new KeyValuePair<string, string>("code_verifier", codeVerifier)
            // Client Secret might be needed depending on app type registered in Bungie
        });

        var response = await _httpClient.PostAsync(TokenUrl, requestBody);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        // Parse JSON content to TokenData
        // Securely store token using ProtectedData
        // For now, return the raw JSON
        return content;
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
}
