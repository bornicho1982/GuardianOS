using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Web;
using GuardianOS.Core;
using GuardianOS.Models;
using Newtonsoft.Json;

namespace GuardianOS.Services;

/// <summary>
/// Implementación del servicio de autenticación OAuth 2.0 con Bungie.
/// Usa TcpListener + SslStream para servidor HTTPS manual.
/// </summary>
public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly TokenStorageService _tokenStorage;
    private AuthToken? _currentToken;
    private TcpListener? _listener;
    private CancellationTokenSource? _serverCts;
    private string? _expectedState;
    private TaskCompletionSource<string?>? _authCodeTcs;
    
    public event EventHandler<bool>? AuthenticationStateChanged;
    
    public bool IsAuthenticated => _currentToken != null && !_currentToken.IsRefreshExpired;
    
    public AuthToken? CurrentToken => _currentToken;
    
    public AuthService(HttpClient httpClient, TokenStorageService tokenStorage)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
    }
    
    public async Task<bool> StartAuthenticationAsync()
    {
        try
        {
            _expectedState = Guid.NewGuid().ToString("N");
            var authUrl = BuildAuthorizationUrl(_expectedState);
            
            Debug.WriteLine($"[Auth] Starting OAuth flow...");
            
            var code = await StartManualHttpsServerAsync(authUrl);
            
            if (string.IsNullOrEmpty(code))
            {
                Debug.WriteLine("[Auth] No code received");
                return false;
            }
            
            Debug.WriteLine("[Auth] Got code, exchanging...");
            var success = await ExchangeCodeForTokensAsync(code);
            
            if (success)
            {
                AuthenticationStateChanged?.Invoke(this, true);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Auth] Error: {ex.Message}");
            return false;
        }
    }
    
    private string BuildAuthorizationUrl(string state)
    {
        var queryParams = HttpUtility.ParseQueryString(string.Empty);
        queryParams["response_type"] = "code";
        queryParams["client_id"] = Constants.OAUTH_CLIENT_ID;
        queryParams["redirect_uri"] = Constants.OAUTH_REDIRECT_URI;
        queryParams["state"] = state;
        
        return $"{Constants.BUNGIE_AUTH_URL}?{queryParams}";
    }
    
    private async Task<string?> StartManualHttpsServerAsync(string authUrl)
    {
        _authCodeTcs = new TaskCompletionSource<string?>();
        _serverCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        
        try
        {
            var callbackUri = new Uri(Constants.OAUTH_REDIRECT_URI);
            var port = callbackUri.Port;
            
            // Get the dev certificate
            var cert = GetDevelopmentCertificate();
            if (cert == null)
            {
                Debug.WriteLine("[Auth] No dev certificate found!");
                return null;
            }
            
            Debug.WriteLine($"[Auth] Using cert: {cert.Subject}");
            
            // Start TCP listener
            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();
            
            Debug.WriteLine($"[Auth] HTTPS server started on port {port}");
            
            // Handle connections in background
            _ = Task.Run(async () =>
            {
                while (!_serverCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var client = await _listener.AcceptTcpClientAsync(_serverCts.Token);
                        _ = HandleClientAsync(client, cert);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Auth] Accept error: {ex.Message}");
                    }
                }
            });
            
            // Open browser
            Process.Start(new ProcessStartInfo
            {
                FileName = authUrl,
                UseShellExecute = true
            });
            
            Debug.WriteLine("[Auth] Browser opened, waiting...");
            
            // Wait for code
            _serverCts.Token.Register(() => _authCodeTcs?.TrySetResult(null));
            var code = await _authCodeTcs.Task;
            
            // Wait for response to be sent
            await Task.Delay(1000);
            
            return code;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Auth] Server error: {ex.Message}");
            return null;
        }
        finally
        {
            _serverCts?.Cancel();
            _listener?.Stop();
            _listener = null;
        }
    }
    
    private X509Certificate2? GetDevelopmentCertificate()
    {
        // Look in CurrentUser store first
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);
        
        var certs = store.Certificates.Find(
            X509FindType.FindBySubjectName,
            "localhost",
            validOnly: false);
        
        var cert = certs.OfType<X509Certificate2>()
            .Where(c => c.HasPrivateKey)
            .OrderByDescending(c => c.NotAfter)
            .FirstOrDefault();
        
        if (cert != null)
        {
            Debug.WriteLine($"[Auth] Found cert in CurrentUser: {cert.Thumbprint}");
            return cert;
        }
        
        // Try LocalMachine
        try
        {
            using var storeLm = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            storeLm.Open(OpenFlags.ReadOnly);
            
            var certsLm = storeLm.Certificates.Find(
                X509FindType.FindBySubjectName,
                "localhost",
                validOnly: false);
            
            cert = certsLm.OfType<X509Certificate2>()
                .Where(c => c.HasPrivateKey)
                .OrderByDescending(c => c.NotAfter)
                .FirstOrDefault();
            
            if (cert != null)
            {
                Debug.WriteLine($"[Auth] Found cert in LocalMachine: {cert.Thumbprint}");
                return cert;
            }
        }
        catch { }
        
        return null;
    }
    
    private async Task HandleClientAsync(TcpClient client, X509Certificate2 cert)
    {
        try
        {
            using (client)
            await using (var sslStream = new SslStream(client.GetStream(), false))
            {
                await sslStream.AuthenticateAsServerAsync(cert);
                
                // Read request
                var buffer = new byte[4096];
                var bytesRead = await sslStream.ReadAsync(buffer);
                var request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                
                Debug.WriteLine($"[Auth] Request received ({bytesRead} bytes)");
                
                // Parse request line
                var firstLine = request.Split('\n')[0];
                var parts = firstLine.Split(' ');
                var path = parts.Length > 1 ? parts[1] : "/";
                
                // Parse query string
                var queryIndex = path.IndexOf('?');
                var queryString = queryIndex >= 0 ? path[(queryIndex + 1)..] : "";
                var query = HttpUtility.ParseQueryString(queryString);
                
                var code = query["code"];
                var state = query["state"];
                var error = query["error"];
                
                Debug.WriteLine($"[Auth] code={code != null}, state={state}, error={error}");
                
                string html;
                
                if (state != _expectedState)
                {
                    _authCodeTcs?.TrySetResult(null);
                    html = GetErrorHtml("State mismatch");
                }
                else if (!string.IsNullOrEmpty(error))
                {
                    _authCodeTcs?.TrySetResult(null);
                    html = GetErrorHtml(error);
                }
                else if (!string.IsNullOrEmpty(code))
                {
                    _authCodeTcs?.TrySetResult(code);
                    html = GetSuccessHtml();
                }
                else
                {
                    _authCodeTcs?.TrySetResult(null);
                    html = GetErrorHtml("No code received");
                }
                
                // Send response
                var response = $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {Encoding.UTF8.GetByteCount(html)}\r\nConnection: close\r\n\r\n{html}";
                await sslStream.WriteAsync(Encoding.UTF8.GetBytes(response));
                await sslStream.FlushAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Auth] Handle error: {ex.Message}");
        }
    }
    
    private static string GetSuccessHtml() => """
        <!DOCTYPE html>
        <html>
        <head><title>GuardianOS - Success</title>
        <style>body{font-family:'Segoe UI',sans-serif;background:#1a1a2e;color:white;display:flex;justify-content:center;align-items:center;height:100vh;margin:0}.c{text-align:center;padding:40px;background:rgba(255,255,255,0.05);border-radius:16px;border:1px solid rgba(255,215,0,0.3)}h1{color:#FFD700}p{color:#888}</style>
        </head>
        <body><div class="c"><h1>✅ Success!</h1><p>Return to GuardianOS</p></div>
        <script>setTimeout(()=>window.close(),2000)</script>
        </body></html>
        """;
    
    private static string GetErrorHtml(string msg)
    {
        return "<!DOCTYPE html><html><head><title>GuardianOS - Error</title>" +
               "<style>body{font-family:'Segoe UI',sans-serif;background:#2e1a1a;color:white;display:flex;justify-content:center;align-items:center;height:100vh;margin:0}.c{text-align:center;padding:40px;background:rgba(255,255,255,0.05);border-radius:16px;border:1px solid rgba(239,83,80,0.3)}h1{color:#EF5350}p{color:#888}</style>" +
               "</head><body><div class='c'><h1>❌ Error</h1><p>" + HttpUtility.HtmlEncode(msg) + "</p></div></body></html>";
    }
    
    private async Task<bool> ExchangeCodeForTokensAsync(string code)
    {
        try
        {
            var formData = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = Constants.OAUTH_CLIENT_ID,
                ["client_secret"] = Constants.OAUTH_CLIENT_SECRET,
                ["redirect_uri"] = Constants.OAUTH_REDIRECT_URI
            };
            
            var content = new FormUrlEncodedContent(formData);
            var response = await _httpClient.PostAsync(Constants.BUNGIE_TOKEN_URL, content);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            Debug.WriteLine($"[Auth] Token: {response.StatusCode}");
            
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[Auth] Error: {responseContent}");
                return false;
            }
            
            var tokenResponse = JsonConvert.DeserializeObject<BungieTokenResponse>(responseContent);
            
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.access_token))
                return false;
            
            _currentToken = new AuthToken
            {
                AccessToken = tokenResponse.access_token,
                RefreshToken = tokenResponse.refresh_token ?? string.Empty,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in),
                RefreshExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.refresh_expires_in),
                MembershipId = tokenResponse.membership_id ?? string.Empty
            };
            
            await _tokenStorage.SaveTokenAsync(_currentToken);
            Debug.WriteLine($"[Auth] OK! Member: {_currentToken.MembershipId}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Auth] Token error: {ex.Message}");
            return false;
        }
    }
    
    public async Task<bool> RefreshTokenAsync()
    {
        if (_currentToken == null || string.IsNullOrEmpty(_currentToken.RefreshToken))
            return false;
        
        try
        {
            var formData = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = _currentToken.RefreshToken,
                ["client_id"] = Constants.OAUTH_CLIENT_ID,
                ["client_secret"] = Constants.OAUTH_CLIENT_SECRET
            };
            
            var content = new FormUrlEncodedContent(formData);
            var response = await _httpClient.PostAsync(Constants.BUNGIE_TOKEN_URL, content);
            
            if (!response.IsSuccessStatusCode)
                return false;
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonConvert.DeserializeObject<BungieTokenResponse>(responseContent);
            
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.access_token))
                return false;
            
            _currentToken.AccessToken = tokenResponse.access_token;
            _currentToken.RefreshToken = tokenResponse.refresh_token ?? _currentToken.RefreshToken;
            _currentToken.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in);
            
            await _tokenStorage.SaveTokenAsync(_currentToken);
            return true;
        }
        catch { return false; }
    }
    
    public async Task<string?> GetValidAccessTokenAsync()
    {
        if (_currentToken == null) return null;
        
        if (_currentToken.NeedsRefresh && !_currentToken.IsRefreshExpired)
            await RefreshTokenAsync();
        
        return _currentToken.IsExpired ? null : _currentToken.AccessToken;
    }
    
    public Task LogoutAsync()
    {
        _currentToken = null;
        _tokenStorage.DeleteToken();
        AuthenticationStateChanged?.Invoke(this, false);
        return Task.CompletedTask;
    }
    
    public async Task<bool> TryRestoreSessionAsync()
    {
        try
        {
            var token = await _tokenStorage.LoadTokenAsync();
            if (token == null) return false;
            
            if (token.IsRefreshExpired)
            {
                _tokenStorage.DeleteToken();
                return false;
            }
            
            _currentToken = token;
            
            if (token.IsExpired && !await RefreshTokenAsync())
                return false;
            
            AuthenticationStateChanged?.Invoke(this, true);
            return true;
        }
        catch { return false; }
    }
}
