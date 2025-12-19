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
    private static ColladaGeneratorService? _colladaService;
    
    // ============================================================
    // LOCAL TEXTURE CACHE CONFIGURATION
    // ============================================================
    // Path to TextureCache folder (organized by item hash)
    // Structure: TextureCache/{itemHash}/DestinyModel0/
    private const string TEXTURE_CACHE_FOLDER = @"E:\GuardianOS\Tools\TextureCache";
    // ============================================================
    
    public bool IsRunning { get; private set; }
    public string BaseUrl => $"http://localhost:{_port}";

    public LocalProxyServer(GearAssetService gearAssetService, int port = 5050)
    {
        _port = port;
        _apiKey = Constants.BUNGIE_API_KEY;
        _gearAssetService = gearAssetService;
        _colladaService = new ColladaGeneratorService();
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

                            // Shader item definition - query DestinyInventoryItemDefinition for shader's material_properties
                            // This is where the actual color data (primary_albedo_tint, etc) lives
                            endpoints.MapGet("/api/shader/{hash}", async context =>
                            {
                                var hashStr = context.Request.RouteValues["hash"]?.ToString();
                                Debug.WriteLine($"[LocalProxy] Shader definition request for hash: {hashStr}");
                                
                                // Query DestinyInventoryItemDefinition - this contains the shader's material_properties
                                var url = $"https://www.bungie.net/Platform/Destiny2/Manifest/DestinyInventoryItemDefinition/{hashStr}/";
                                await ProxyRequest(context, url);
                            });

                            // Proxy: Geometry files (with texture fallback)
                            endpoints.MapGet("/api/geometry/{**path}", async context =>
                            {
                                var path = context.Request.RouteValues["path"]?.ToString();
                                
                                // If this is a texture request, try multiple sources
                                if (path != null && (path.Contains("/textures/") || path.Contains("/plated_textures/") || path.Contains("_textures/")))
                                {
                                    var filename = Path.GetFileName(path);
                                    var cleanFilename = filename.Replace(".tgxm.bin", "").Replace(".png", "");
                                    
                                    // Try to extract any digits for folder lookup
                                    var hashStr = new string(filename.TakeWhile(char.IsDigit).ToArray());

                                    Debug.WriteLine($"[LocalProxy] Texture Request: {filename}");

                                    // 1. Search in local folders (Recursive)
                                    // Try both Tools (generated) and Assets (manual exports)
                                    // 1. Search in local folders (Recursive)
                                    // Try both Tools (generated), Assets, and External Exports
                                    var searchRoots = new[] { 
                                        @"E:\GuardianOS\Tools", 
                                        @"E:\GuardianOS\Assets",
                                        @"E:\D2_Exports",
                                        @"E:\D2_Exports\ApiOutput1"
                                    };
                                    foreach (var root in searchRoots)
                                    {
                                        if (Directory.Exists(root))
                                        {
                                            var localMatch = Directory.GetFiles(root, $"*{cleanFilename}*", SearchOption.AllDirectories)
                                                .FirstOrDefault(f => f.EndsWith(".png") || f.EndsWith(".tgxm.bin") || f.EndsWith(".jpg"));

                                            if (localMatch != null)
                                            {
                                                Debug.WriteLine($"[LocalProxy] Serving LOCAL (Recursive): {localMatch}");
                                                var ext = Path.GetExtension(localMatch).ToLower();
                                                context.Response.ContentType = ext == ".png" ? "image/png" : 
                                                                              (ext == ".jpg" || ext == ".jpeg" ? "image/jpeg" : "application/octet-stream");
                                                await context.Response.SendFileAsync(localMatch);
                                                return;
                                            }
                                        }
                                    }
                                    
                                    // 2. Search in TextureCache (Recursive)
                                    if (Directory.Exists(TEXTURE_CACHE_FOLDER))
                                    {
                                        var cacheMatch = Directory.GetFiles(TEXTURE_CACHE_FOLDER, $"{cleanFilename}*", SearchOption.AllDirectories)
                                            .FirstOrDefault();
                                        
                                        if (cacheMatch != null)
                                        {
                                            Debug.WriteLine($"[LocalProxy] Serving CACHED (Recursive): {cacheMatch}");
                                            var ext = Path.GetExtension(cacheMatch).ToLower();
                                            context.Response.ContentType = ext == ".png" ? "image/png" : "application/octet-stream";
                                            await context.Response.SendFileAsync(cacheMatch);
                                            return;
                                        }
                                    }
                                    
                                    // 3. Fallback to Bungie CDN with expanded path guessing
                                    var baseBungie = "https://www.bungie.net/common/destiny2_content/geometry";
                                    var urls = new[] {
                                        $"{baseBungie}/{path}",
                                        $"{baseBungie}/platform/mobile/textures/{filename}",
                                        $"{baseBungie}/platform/mobile/plated_textures/{filename}",
                                        $"{baseBungie}/platform/mobile/geometry_textures/{filename}",
                                        $"{baseBungie}/platform/pc/textures/{filename}",
                                        $"{baseBungie}/platform/pc/plated_textures/{filename}"
                                    };
                                    
                                    await ProxyBinaryRequestWithFallback(context, urls);
                                }
                                else
                                {
                                    // Standard geometry request
                                    var url = $"https://www.bungie.net/common/destiny2_content/geometry/{path}";
                                    await ProxyBinaryRequest(context, url);
                                }
                            });

                            // ============================================================
                            // LOCAL TEXTURE SERVER - Serves extracted PNG textures
                            // Serve local textures by item hash - NEW CACHE SYSTEM
                            // URL: /api/armor-texture/{itemHash}/{textureType}
                            // textureType: diffuse, normal, gearstack, dyeslot
                            endpoints.MapGet("/api/armor-texture/{itemHash}/{textureType}", async context =>
                            {
                                var itemHash = context.Request.RouteValues["itemHash"]?.ToString();
                                var textureType = context.Request.RouteValues["textureType"]?.ToString() ?? "diffuse";
                                context.Response.Headers["Access-Control-Allow-Origin"] = "*";
                                
                                if (string.IsNullOrEmpty(itemHash) || string.IsNullOrEmpty(TEXTURE_CACHE_FOLDER))
                                {
                                    context.Response.StatusCode = 404;
                                    return;
                                }
                                
                                try
                                {
                                    // Path: TextureCache/{itemHash}/DestinyModel0/
                                    var itemFolder = Path.Combine(TEXTURE_CACHE_FOLDER, itemHash, "DestinyModel0");
                                    
                                    if (!Directory.Exists(itemFolder))
                                    {
                                        Debug.WriteLine($"[LocalProxy] No cached textures for item: {itemHash}");
                                        context.Response.StatusCode = 404;
                                        return;
                                    }
                                    
                                    // FLEXIBLE SEARCH (Male/Female agnostic)
                                    // ColladaGenerator output format: {Gender}-_{type}_{index}.png
                                    // e.g. Female-_dyeslot_0.png or Male-_diffuse_0.png
                                    
                                    var searchPattern = textureType.ToLower() switch
                                    {
                                        "dyeslot" => "*_dyeslot_0.png",
                                        "gearstack" => "*_gearstack_0.png",
                                        "normal" => "*_normal_0.png",
                                        "diffuse" => "*_diffuse_0.png",
                                        _ => $"*_{textureType}_0.png"
                                    };
                                    
                                    var files = Directory.GetFiles(itemFolder, searchPattern);
                                    
                                    if (files.Length > 0)
                                    {
                                        var texturePath = files[0];
                                        Debug.WriteLine($"[LocalProxy] Serving cached: {itemHash}/{Path.GetFileName(texturePath)}");
                                        context.Response.ContentType = "image/png";
                                        await context.Response.SendFileAsync(texturePath);
                                        return;
                                    }
                                    
                                    Debug.WriteLine($"[LocalProxy] Texture not found in cache: {itemFolder}\\{searchPattern}");
                                    context.Response.StatusCode = 404;
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[LocalProxy] Cache error: {ex.Message}");
                                    context.Response.StatusCode = 500;
                                }
                            });
                            
                            // Legacy endpoint - search by texture hash in any cached folder
                            endpoints.MapGet("/api/local-texture/{hash}", async context =>
                            {
                                var hash = context.Request.RouteValues["hash"]?.ToString();
                                context.Response.Headers["Access-Control-Allow-Origin"] = "*";
                                
                                if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(TEXTURE_CACHE_FOLDER))
                                {
                                    context.Response.StatusCode = 404;
                                    return;
                                }
                                
                                try
                                {
                                    if (!Directory.Exists(TEXTURE_CACHE_FOLDER))
                                    {
                                        context.Response.StatusCode = 404;
                                        return;
                                    }
                                    
                                    // Search all item folders for a texture containing this hash
                                    foreach (var itemDir in Directory.GetDirectories(TEXTURE_CACHE_FOLDER))
                                    {
                                        var texturesDir = Path.Combine(itemDir, "DestinyModel0", "Textures");
                                        if (!Directory.Exists(texturesDir)) continue;
                                        
                                        var matchingFile = Directory.GetFiles(texturesDir, "*.*")
                                            .FirstOrDefault(f => Path.GetFileName(f).Contains(hash));
                                        
                                        if (matchingFile != null)
                                        {
                                            Debug.WriteLine($"[LocalProxy] Found cached texture: {Path.GetFileName(matchingFile)}");
                                            var ext = Path.GetExtension(matchingFile).ToLower();
                                            context.Response.ContentType = ext == ".jpg" || ext == ".jpeg" ? "image/jpeg" : "image/png";
                                            await context.Response.SendFileAsync(matchingFile);
                                            return;
                                        }
                                    }
                                    
                                    context.Response.StatusCode = 404;
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[LocalProxy] Search error: {ex.Message}");
                                    context.Response.StatusCode = 500;
                                }
                            });
                            
                            // Proxy: Mobile textures (legacy endpoint)
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

                            // Collada model generation
                            endpoints.MapGet("/api/collada/{hash}", async context =>
                            {
                                var hashStr = context.Request.RouteValues["hash"]?.ToString();
                                context.Response.Headers["Access-Control-Allow-Origin"] = "*";
                                
                                if (uint.TryParse(hashStr, out var hash) && _colladaService != null)
                                {
                                    try
                                    {
                                        var daePath = await _colladaService.GenerateModel(hash);
                                        if (daePath != null && File.Exists(daePath))
                                        {
                                            context.Response.ContentType = "model/vnd.collada+xml";
                                            await context.Response.SendFileAsync(daePath);
                                            return;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"[LocalProxy] Collada error: {ex.Message}");
                                    }
                                }
                                
                                context.Response.StatusCode = 404;
                                await context.Response.WriteAsync("Model not found");
                            });

                            // Serve local FBX files from Charm exports
                            endpoints.MapGet("/api/fbx/{**path}", async context =>
                            {
                                var path = context.Request.RouteValues["path"]?.ToString();
                                context.Response.Headers["Access-Control-Allow-Origin"] = "*";
                                
                                var basePath = AppDomain.CurrentDomain.BaseDirectory;
                                var fbxPath = Path.Combine(basePath, "Assets", "3DViewer", "models", "fbx", path ?? "");
                                
                                if (File.Exists(fbxPath))
                                {
                                    var ext = Path.GetExtension(fbxPath).ToLower();
                                    context.Response.ContentType = ext switch
                                    {
                                        ".fbx" => "application/octet-stream",
                                        ".png" => "image/png",
                                        ".jpg" or ".jpeg" => "image/jpeg",
                                        ".tga" => "image/x-tga",
                                        _ => "application/octet-stream"
                                    };
                                    await context.Response.SendFileAsync(fbxPath);
                                    return;
                                }
                                
                                context.Response.StatusCode = 404;
                                await context.Response.WriteAsync("File not found");
                            });

                            // Texture proxy - serve textures from bungie.net with local cache
                            endpoints.MapGet("/api/texture/{**path}", async context =>
                            {
                                var path = context.Request.RouteValues["path"]?.ToString();
                                context.Response.Headers["Access-Control-Allow-Origin"] = "*";
                                
                                if (string.IsNullOrEmpty(path))
                                {
                                    context.Response.StatusCode = 400;
                                    return;
                                }

                                // Cache directory for textures
                                var cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "textures");
                                Directory.CreateDirectory(cacheDir);
                                
                                var cachedFile = Path.Combine(cacheDir, path.Replace("/", "_"));
                                
                                // Check cache first
                                if (File.Exists(cachedFile))
                                {
                                    var ext = Path.GetExtension(path).ToLower();
                                    context.Response.ContentType = ext switch
                                    {
                                        ".png" => "image/png",
                                        ".jpg" or ".jpeg" => "image/jpeg",
                                        _ => "application/octet-stream"
                                    };
                                    await context.Response.SendFileAsync(cachedFile);
                                    return;
                                }

                                // Fetch from Bungie
                                try
                                {
                                    var textureUrl = $"https://www.bungie.net/{path}";
                                    Debug.WriteLine($"[LocalProxy] Fetching texture: {textureUrl}");
                                    
                                    using var response = await _httpClient.GetAsync(textureUrl);
                                    if (response.IsSuccessStatusCode)
                                    {
                                        var data = await response.Content.ReadAsByteArrayAsync();
                                        
                                        // Cache it
                                        await File.WriteAllBytesAsync(cachedFile, data);
                                        
                                        var ext = Path.GetExtension(path).ToLower();
                                        context.Response.ContentType = ext switch
                                        {
                                            ".png" => "image/png",
                                            ".jpg" or ".jpeg" => "image/jpeg",
                                            _ => "application/octet-stream"
                                        };
                                        await context.Response.Body.WriteAsync(data);
                                        return;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[LocalProxy] Texture fetch error: {ex.Message}");
                                }
                                
                                context.Response.StatusCode = 404;
                            });

                            // Gear JSON proxy - load gear data with dye colors
                            endpoints.MapGet("/api/gear/{hash}", async context =>
                            {
                                var hash = context.Request.RouteValues["hash"]?.ToString();
                                context.Response.Headers["Access-Control-Allow-Origin"] = "*";
                                context.Response.ContentType = "application/json";
                                
                                if (string.IsNullOrEmpty(hash))
                                {
                                    context.Response.StatusCode = 400;
                                    return;
                                }

                                // Cache directory for gear JSON
                                var cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "gear");
                                Directory.CreateDirectory(cacheDir);
                                
                                var cachedFile = Path.Combine(cacheDir, $"{hash}.json");
                                
                                // Check cache first
                                if (File.Exists(cachedFile))
                                {
                                    await context.Response.SendFileAsync(cachedFile);
                                    return;
                                }

                                // Fetch from Bungie - gear JSON with dye data (files have .js extension)
                                try
                                {
                                    var gearUrl = $"https://www.bungie.net/common/destiny2_content/geometry/gear/{hash}.js";
                                    Debug.WriteLine($"[LocalProxy] Fetching gear JSON: {gearUrl}");
                                    
                                    using var response = await _httpClient.GetAsync(gearUrl);
                                    if (response.IsSuccessStatusCode)
                                    {
                                        var data = await response.Content.ReadAsStringAsync();
                                        
                                        // Cache it
                                        await File.WriteAllTextAsync(cachedFile, data);
                                        
                                        await context.Response.WriteAsync(data);
                                        return;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[LocalProxy] Gear fetch error: {ex.Message}");
                                }
                                
                                context.Response.StatusCode = 404;
                                await context.Response.WriteAsync("{}");
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
    /// Proxy binary content with fallback URLs - tries multiple sources before failing
    /// </summary>
    private static async Task ProxyBinaryRequestWithFallback(HttpContext context, string[] urls)
    {
        foreach (var url in urls)
        {
            try
            {
                Debug.WriteLine($"[LocalProxy] Trying binary URL: {url}");
                
                var response = await _httpClient!.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                    var bytes = await response.Content.ReadAsByteArrayAsync();

                    context.Response.ContentType = contentType;
                    context.Response.Headers["Access-Control-Allow-Origin"] = "*";
                    context.Response.StatusCode = 200;
                    
                    await context.Response.Body.WriteAsync(bytes);
                    Debug.WriteLine($"[LocalProxy] Success from: {url}");
                    return;
                }
                
                Debug.WriteLine($"[LocalProxy] Failed with {response.StatusCode}: {url}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LocalProxy] Error trying {url}: {ex.Message}");
            }
        }
        
        // All URLs failed
        Debug.WriteLine("[LocalProxy] All fallback URLs failed, returning 404");
        context.Response.StatusCode = 404;
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
