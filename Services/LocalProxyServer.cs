using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GuardianOS.Core;

namespace GuardianOS.Services;

/// <summary>
/// Local proxy server for Bungie API requests.
/// Runs on localhost:5050 and forwards requests to Bungie with proper headers.
/// This bypasses CORS restrictions for the WebView2 3D viewer.
/// </summary>
public class LocalProxyServer : IDisposable
{
    private IHost? _host;
    private readonly int _port;
    private readonly string _apiKey;
    private static HttpClient? _httpClient;
    private static GearAssetService? _gearAssetService;
    
    public bool IsRunning { get; private set; }
    public string BaseUrl => $"http://localhost:{_port}";

    public LocalProxyServer(GearAssetService gearAssetService, int port = 5050)
    {
        _port = port;
        _apiKey = Constants.BUNGIE_API_KEY;
        _gearAssetService = gearAssetService;
    }

    /// <summary>
    /// Start the proxy server
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning) return;

        try
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", $"{Constants.APP_NAME}/{Constants.APP_VERSION}");
            _httpClient.Timeout = TimeSpan.FromSeconds(60);

            _host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel(options =>
                    {
                        options.ListenLocalhost(_port);
                    });
                    webBuilder.Configure(app =>
                    {
                        // Enable CORS for WebView2
                        app.UseCors(builder => builder
                            .AllowAnyOrigin()
                            .AllowAnyMethod()
                            .AllowAnyHeader());

                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            // Health check
                            endpoints.MapGet("/health", async context =>
                            {
                                await context.Response.WriteAsync("OK");
                            });

                            // Proxy: Manifest
                            endpoints.MapGet("/api/manifest", async context =>
                            {
                                await ProxyRequest(context, "https://www.bungie.net/Platform/Destiny2/Manifest/");
                            });

                            // Proxy: Gear Asset Definition
                            endpoints.MapGet("/api/manifest/{hash}", async context =>
                            {
                                var hash = context.Request.RouteValues["hash"]?.ToString();
                                var url = $"https://www.bungie.net/Platform/Destiny2/Manifest/DestinyInventoryItemDefinition/{hash}/";
                                await ProxyRequest(context, url);
                            });

                            // Gear Asset Data from local SQLite cache
                            endpoints.MapGet("/api/gearasset/{hash}", async context =>
                            {
                                var hashStr = context.Request.RouteValues["hash"]?.ToString();
                                if (long.TryParse(hashStr, out var hash) && _gearAssetService != null)
                                {
                                    try
                                    {
                                        var asset = await _gearAssetService.GetGearAssetAsync(hash);
                                        if (asset != null)
                                        {
                                            context.Response.ContentType = "application/json";
                                            context.Response.Headers["Access-Control-Allow-Origin"] = "*";
                                            var json = System.Text.Json.JsonSerializer.Serialize(new { 
                                                ErrorCode = 1,
                                                Response = new { requestedId = hash, gearAsset = asset }
                                            });
                                            await context.Response.WriteAsync(json);
                                            return;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"[LocalProxy] GearAsset error: {ex.Message}");
                                    }
                                }
                                // Fallback to API proxy if local fails
                                var url = $"https://www.bungie.net/Platform/Destiny2/Manifest/DestinyGearAssetsDefinition/{hashStr}/";
                                await ProxyRequest(context, url);
                            });

                            // Proxy: Geometry files
                            endpoints.MapGet("/api/geometry/{**path}", async context =>
                            {
                                var path = context.Request.RouteValues["path"]?.ToString();
                                var url = $"https://www.bungie.net/common/destiny2_content/geometry/{path}";
                                await ProxyBinaryRequest(context, url);
                            });

                            // Proxy: Mobile textures
                            endpoints.MapGet("/api/texture/{**path}", async context =>
                            {
                                var path = context.Request.RouteValues["path"]?.ToString();
                                var url = $"https://www.bungie.net/common/destiny2_content/geometry/platform/mobile/textures/{path}";
                                await ProxyBinaryRequest(context, url);
                            });

                            // Proxy: Plated textures
                            endpoints.MapGet("/api/plated/{**path}", async context =>
                            {
                                var path = context.Request.RouteValues["path"]?.ToString();
                                var url = $"https://www.bungie.net/common/destiny2_content/geometry/platform/mobile/plated_textures/{path}";
                                await ProxyBinaryRequest(context, url);
                            });

                            // Proxy: Gear files
                            endpoints.MapGet("/api/gear/{**path}", async context =>
                            {
                                var path = context.Request.RouteValues["path"]?.ToString();
                                var url = $"https://www.bungie.net/common/destiny2_content/geometry/gear/{path}";
                                await ProxyBinaryRequest(context, url);
                            });

                            // Proxy: Generic content path
                            endpoints.MapGet("/api/content/{**path}", async context =>
                            {
                                var path = context.Request.RouteValues["path"]?.ToString();
                                var url = $"https://www.bungie.net/common/destiny2_content/{path}";
                                await ProxyBinaryRequest(context, url);
                            });

                            // Proxy: Any Bungie.net path
                            endpoints.MapGet("/proxy/{**path}", async context =>
                            {
                                var path = context.Request.RouteValues["path"]?.ToString();
                                var url = $"https://www.bungie.net/{path}";
                                await ProxyBinaryRequest(context, url);
                            });
                        });
                    });
                    webBuilder.ConfigureServices(services =>
                    {
                        services.AddCors();
                        services.AddRouting();
                    });
                })
                .Build();

            await _host.StartAsync(cancellationToken);
            IsRunning = true;
            Debug.WriteLine($"[LocalProxy] Server started on {BaseUrl}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LocalProxy] Failed to start: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Proxy JSON API request
    /// </summary>
    private static async Task ProxyRequest(HttpContext context, string url)
    {
        try
        {
            Debug.WriteLine($"[LocalProxy] Proxying: {url}");
            
            var response = await _httpClient!.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            context.Response.ContentType = "application/json";
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.StatusCode = (int)response.StatusCode;
            
            await context.Response.WriteAsync(content);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LocalProxy] Error: {ex.Message}");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync($"{{\"error\": \"{ex.Message}\"}}");
        }
    }

    /// <summary>
    /// Proxy binary content (geometry, textures)
    /// </summary>
    private static async Task ProxyBinaryRequest(HttpContext context, string url)
    {
        try
        {
            Debug.WriteLine($"[LocalProxy] Proxying binary: {url}");
            
            var response = await _httpClient!.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                context.Response.StatusCode = (int)response.StatusCode;
                return;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            var bytes = await response.Content.ReadAsByteArrayAsync();

            context.Response.ContentType = contentType;
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.StatusCode = 200;
            
            await context.Response.Body.WriteAsync(bytes);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LocalProxy] Binary error: {ex.Message}");
            context.Response.StatusCode = 500;
        }
    }

    /// <summary>
    /// Stop the proxy server
    /// </summary>
    public async Task StopAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
            _host = null;
        }
        
        _httpClient?.Dispose();
        _httpClient = null;
        
        IsRunning = false;
        Debug.WriteLine("[LocalProxy] Server stopped");
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }
}
