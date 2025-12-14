using System.IO;
using System.IO.Compression;
using System.Net.Http;
using GuardianOS.Core;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace GuardianOS.Services;

public class ManifestService : IManifestService
{
    private readonly HttpClient _httpClient;
    private const string MANIFEST_FILENAME = "Destiny2Manifest.sqlite";
    private string _localDatabasePath;
    
    public string ManifestDatabasePath => _localDatabasePath;
    public bool IsManifestReady { get; private set; }

    public ManifestService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", Constants.BUNGIE_API_KEY);
        
        // Guardar en AppData para no depender de permisos de escritura en Program Files
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var guardianDir = Path.Combine(appData, "GuardianOS", "Manifest");
        Directory.CreateDirectory(guardianDir);
        _localDatabasePath = Path.Combine(guardianDir, MANIFEST_FILENAME);
    }

    public async Task InitializeAsync()
    {
        try
        {
            // 1. Obtener URL del Manifiesto actual
            var manifestUrl = await GetManifestUrlAsync();
            if (string.IsNullOrEmpty(manifestUrl)) return;

            // 2. Verificar si necesitamos descargar (por ahora descargamos si no existe o forzamos update simple)
            // En producción compararíamos versiones.
            if (!File.Exists(_localDatabasePath))
            {
                await DownloadAndExtractManifestAsync(manifestUrl);
            }
            
            IsManifestReady = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ManifestService] Error: {ex.Message}");
            IsManifestReady = false;
        }
    }

    private async Task<string?> GetManifestUrlAsync()
    {
        try
        {
            var response = await _httpClient.GetStringAsync($"{Constants.BUNGIE_API_BASE_URL}/Destiny2/Manifest/");
            var json = JObject.Parse(response);
            
            // Intentar obtener español (es-mx) o fallback a inglés (en)
            var paths = json["Response"]?["mobileWorldContentPaths"];
            var path = paths?["es-mx"]?.ToString() ?? paths?["en"]?.ToString();
            
            return path;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ManifestService] Error getting URL: {ex.Message}");
            return null;
        }
    }

    private async Task DownloadAndExtractManifestAsync(string relativePath)
    {
        var fullUrl = $"https://www.bungie.net{relativePath}";
        var zipPath = _localDatabasePath + ".zip";

        Debug.WriteLine($"[ManifestService] Downloading manifest from {fullUrl}...");

        // Descargar ZIP
        using (var response = await _httpClient.GetAsync(fullUrl))
        using (var fs = new FileStream(zipPath, FileMode.Create))
        {
            await response.Content.CopyToAsync(fs);
        }

        // Extraer
        Debug.WriteLine("[ManifestService] Extracting...");
        
        if (File.Exists(_localDatabasePath)) File.Delete(_localDatabasePath);

        using (var zip = ZipFile.OpenRead(zipPath))
        {
            // El zip contiene un solo archivo con nombre aleatorio (ej. world_sql_content_...)
            var entry = zip.Entries.FirstOrDefault(e => e.Name.EndsWith(".content", StringComparison.OrdinalIgnoreCase));
            
            if (entry != null)
            {
                entry.ExtractToFile(_localDatabasePath);
            }
        }
        
        // Limpieza
        File.Delete(zipPath);
        Debug.WriteLine("[ManifestService] Manifest ready.");
    }
}
