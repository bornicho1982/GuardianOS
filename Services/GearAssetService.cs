using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using GuardianOS.Core;

namespace GuardianOS.Services;

/// <summary>
/// Service for accessing Destiny 2 gear asset data from the mobile manifest SQLite database.
/// Downloads, caches, and queries the mobileGearAssetDataBases for 3D model information.
/// </summary>
public class GearAssetService : IDisposable
{
    private readonly HttpClient _httpClient;
    private SqliteConnection? _connection;
    private string? _dbPath;
    private bool _isInitialized;
    
    private const string CACHE_DIR = "Data/GearAssets";
    private const string DB_FILENAME = "gear_assets.db";
    
    public bool IsInitialized => _isInitialized;

    public GearAssetService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", Constants.BUNGIE_API_KEY);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"{Constants.APP_NAME}/{Constants.APP_VERSION}");
    }

    /// <summary>
    /// Initialize the service by downloading and extracting the gear asset database
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            Debug.WriteLine("[GearAsset] Initializing gear asset service...");
            
            // Ensure cache directory exists
            Directory.CreateDirectory(CACHE_DIR);
            _dbPath = Path.Combine(CACHE_DIR, DB_FILENAME);
            
            // Check if we need to download the manifest
            bool needsDownload = !File.Exists(_dbPath);
            
            if (needsDownload)
            {
                await DownloadManifestAsync();
            }
            
            // Open database connection
            await OpenDatabaseAsync();
            
            _isInitialized = true;
            Debug.WriteLine("[GearAsset] Service initialized successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GearAsset] Initialization failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Download the latest mobileGearAssetDataBases from Bungie
    /// </summary>
    private async Task DownloadManifestAsync()
    {
        Debug.WriteLine("[GearAsset] Downloading manifest...");
        
        // Get manifest to find the asset database path
        var manifestUrl = "https://www.bungie.net/Platform/Destiny2/Manifest/";
        var manifestResponse = await _httpClient.GetStringAsync(manifestUrl);
        
        using var manifestDoc = JsonDocument.Parse(manifestResponse);
        var response = manifestDoc.RootElement.GetProperty("Response");
        var assetDatabases = response.GetProperty("mobileGearAssetDataBases");
        
        // Get the latest version (last in array)
        string? assetPath = null;
        foreach (var db in assetDatabases.EnumerateArray())
        {
            assetPath = db.GetProperty("path").GetString();
        }
        
        if (string.IsNullOrEmpty(assetPath))
        {
            throw new Exception("No gear asset database found in manifest");
        }
        
        Debug.WriteLine($"[GearAsset] Asset DB path: {assetPath}");
        
        // Download the compressed database
        var dbUrl = $"https://www.bungie.net{assetPath}";
        var compressedData = await _httpClient.GetByteArrayAsync(dbUrl);
        
        Debug.WriteLine($"[GearAsset] Downloaded {compressedData.Length / 1024} KB, extracting...");
        
        // Extract the ZIP file
        using var compressedStream = new MemoryStream(compressedData);
        using var archive = new ZipArchive(compressedStream, ZipArchiveMode.Read);
        
        if (archive.Entries.Count == 0)
        {
            throw new Exception("Empty ZIP archive");
        }
        
        // Extract the first (and usually only) entry
        var entry = archive.Entries[0];
        using var entryStream = entry.Open();
        using var fileStream = File.Create(_dbPath!);
        await entryStream.CopyToAsync(fileStream);
        
        Debug.WriteLine($"[GearAsset] Extracted database to {_dbPath}");
    }

    /// <summary>
    /// Open the SQLite database connection
    /// </summary>
    private async Task OpenDatabaseAsync()
    {
        if (_dbPath == null || !File.Exists(_dbPath))
        {
            throw new FileNotFoundException("Gear asset database not found");
        }
        
        var connectionString = $"Data Source={_dbPath};Mode=ReadOnly";
        _connection = new SqliteConnection(connectionString);
        await _connection.OpenAsync();
        
        Debug.WriteLine("[GearAsset] Database connection opened");
        
        // List all tables for debugging
        await ListTablesAsync();
    }
    
    /// <summary>
    /// List all tables in the database for debugging
    /// </summary>
    private async Task ListTablesAsync()
    {
        if (_connection == null) return;
        
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
        
        using var reader = await command.ExecuteReaderAsync();
        var tables = new System.Collections.Generic.List<string>();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }
        
        var tablesLog = $"[GearAsset] Tables in database: {string.Join(", ", tables)}";
        Debug.WriteLine(tablesLog);
        File.AppendAllText("gearasset.log", tablesLog + "\n");
        
        // Get sample row from first table to understand structure
        if (tables.Count > 0)
        {
            var tableName = tables[0];
            using var schemaCmd = _connection.CreateCommand();
            schemaCmd.CommandText = $"PRAGMA table_info({tableName})";
            using var schemaReader = await schemaCmd.ExecuteReaderAsync();
            var columns = new System.Collections.Generic.List<string>();
            while (await schemaReader.ReadAsync())
            {
                columns.Add(schemaReader.GetString(1)); // column name
            }
            var columnsLog = $"[GearAsset] Table '{tableName}' columns: {string.Join(", ", columns)}";
            Debug.WriteLine(columnsLog);
            File.AppendAllText("gearasset.log", columnsLog + "\n");
            
            // Get sample data
            using var sampleCmd = _connection.CreateCommand();
            sampleCmd.CommandText = $"SELECT * FROM {tableName} LIMIT 1";
            using var sampleReader = await sampleCmd.ExecuteReaderAsync();
            if (await sampleReader.ReadAsync())
            {
                var sampleData = "";
                for (int i = 0; i < sampleReader.FieldCount; i++)
                {
                    var value = sampleReader.GetValue(i)?.ToString();
                    if (value?.Length > 100) value = value.Substring(0, 100) + "...";
                    sampleData += $"{sampleReader.GetName(i)}={value}; ";
                }
                File.AppendAllText("gearasset.log", $"[GearAsset] Sample data: {sampleData}\n");
            }
        }
    }

    /// <summary>
    /// Get gear asset data for an item by its hash
    /// </summary>
    public async Task<GearAssetData?> GetGearAssetAsync(long itemHash)
    {
        if (!_isInitialized || _connection == null)
        {
            await InitializeAsync();
        }
        
        try
        {
            // The table stores hash as signed int32
            // Bungie hashes can be > 2^31 which become negative when stored as signed int
            var signedHash = unchecked((int)(uint)itemHash);
            
            File.AppendAllText("gearasset.log", $"[GearAsset] Querying hash: {itemHash} -> signed: {signedHash}\n");
            
            using var command = _connection!.CreateCommand();
            command.CommandText = "SELECT json FROM DestinyGearAssetsDefinition WHERE id = @signedHash LIMIT 1";
            command.Parameters.AddWithValue("@signedHash", signedHash);
            
            var result = await command.ExecuteScalarAsync();
            
            if (result == null || result == DBNull.Value)
            {
                File.AppendAllText("gearasset.log", $"[GearAsset] No data found for hash: {itemHash} (signed: {signedHash})\n");
                Debug.WriteLine($"[GearAsset] No data found for hash: {itemHash}");
                return null;
            }
            
            var json = result.ToString();
            File.AppendAllText("gearasset.log", $"[GearAsset] Found data for hash {itemHash}: {json?.Substring(0, Math.Min(200, json?.Length ?? 0))}...\n");
            Debug.WriteLine($"[GearAsset] Found data for hash {itemHash}");
            
            return JsonSerializer.Deserialize<GearAssetData>(json!);
        }
        catch (Exception ex)
        {
            File.AppendAllText("gearasset.log", $"[GearAsset] Error querying hash {itemHash}: {ex.Message}\n");
            Debug.WriteLine($"[GearAsset] Error querying hash {itemHash}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Refresh the manifest database (re-download)
    /// </summary>
    public async Task RefreshAsync()
    {
        _isInitialized = false;
        _connection?.Close();
        _connection?.Dispose();
        _connection = null;
        
        if (_dbPath != null && File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
        
        await InitializeAsync();
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
        _httpClient?.Dispose();
    }
}

/// <summary>
/// Gear asset data structure from the manifest
/// JSON format: {"gear":["archivo.js"], "content":[{...}]}
/// </summary>
public class GearAssetData
{
    public string[]? gear { get; set; }  // Array of JS file paths for gear data
    public GearAssetContent[]? content { get; set; }
}

public class GearAssetContent
{
    public string? platform { get; set; }
    public string[]? geometry { get; set; }  // Array of .tgxm file paths for geometry
    public string[]? textures { get; set; }  // Array of texture file paths
    public string[]? plate_regions { get; set; }
    public Dictionary<string, GearAssetIndexSet[]>? region_index_sets { get; set; }
    public GearAssetIndexSet? female_index_set { get; set; }
    public GearAssetIndexSet? male_index_set { get; set; }
    public GearAssetDyeIndexSet? dye_index_set { get; set; }
}

public class GearAssetIndexSet
{
    public int[]? geometry { get; set; }
    public int[]? textures { get; set; }
    public int[]? plate_regions { get; set; }
}

public class GearAssetDyeIndexSet
{
    public int[]? textures { get; set; }
}
