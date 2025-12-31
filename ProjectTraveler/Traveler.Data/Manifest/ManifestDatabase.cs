using System.Data;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Traveler.Data.Manifest;

/// <summary>
/// Manages the Destiny 2 Manifest SQLite database.
/// Contains all game definitions: items, stats, records, vendors, etc.
/// </summary>
public class ManifestDatabase
{
    // Store manifest in AppData to avoid permission issues
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GuardianOS", "manifest");
    
    private static readonly string ManifestPath = Path.Combine(AppDataDir, "world_sql_content.sqlite");
    private static readonly string VersionPath = Path.Combine(AppDataDir, "version.txt");
    private const string ApiKey = "e1a73d9d631a46a8b7e2b6e37ae30492";
    
    private string? _currentVersion;
    
    /// <summary>
    /// Initializes the manifest, downloading if necessary.
    /// </summary>
    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(AppDataDir);
        
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiKey);
        
        // Get manifest metadata
        var manifestUrl = "https://www.bungie.net/Platform/Destiny2/Manifest/";
        var response = await client.GetAsync(manifestUrl);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[Manifest] Failed to get manifest info: {response.StatusCode}");
            return;
        }
        
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        
        var version = doc.RootElement
            .GetProperty("Response")
            .GetProperty("version")
            .GetString();
            
        var contentPath = doc.RootElement
            .GetProperty("Response")
            .GetProperty("mobileWorldContentPaths")
            .GetProperty("en")
            .GetString();
        
        // Check if we need to update
        if (File.Exists(VersionPath))
        {
            var storedVersion = await File.ReadAllTextAsync(VersionPath);
            if (storedVersion == version && File.Exists(ManifestPath))
            {
                _currentVersion = version;
                Console.WriteLine($"[Manifest] Already up to date: {version}");
                return;
            }
        }
        
        // Download new manifest
        Console.WriteLine($"[Manifest] Downloading version {version}...");
        await DownloadAndExtractManifest(contentPath!);
        await File.WriteAllTextAsync(VersionPath, version!);
        _currentVersion = version;
        Console.WriteLine($"[Manifest] Updated to version {version}");
    }
    
    private async Task DownloadAndExtractManifest(string mobileWorldContentPath)
    {
        using var client = new HttpClient();
        var url = $"https://www.bungie.net{mobileWorldContentPath}";
        
        Console.WriteLine($"[Manifest] Downloading from {url}");
        var zipBytes = await client.GetByteArrayAsync(url);
        
        var zipPath = Path.Combine(AppDataDir, "temp_manifest.zip");
        
        try
        {
            await File.WriteAllBytesAsync(zipPath, zipBytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Manifest] Failed to write zip: {ex.Message}");
            return;
        }

        // Extract - use unique directory name to avoid conflicts
        var extractDir = Path.Combine(AppDataDir, $"temp_extract_{Guid.NewGuid():N}");
        
        try
        {
            Console.WriteLine($"[Manifest] Extracting to {extractDir}");
            ZipFile.ExtractToDirectory(zipPath, extractDir);
            
            // Find the .content file and move it
            var contentFiles = Directory.GetFiles(extractDir, "*.content", SearchOption.AllDirectories);
            if (contentFiles.Length > 0)
            {
                // Clear SQLite connection pools to release file locks
                SqliteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                
                // Try to delete existing file
                if (File.Exists(ManifestPath))
                {
                    try 
                    { 
                        File.Delete(ManifestPath);
                        Console.WriteLine("[Manifest] Deleted old manifest");
                    }
                    catch (Exception ex)
                    { 
                        Console.WriteLine($"[Manifest] Could not delete old manifest: {ex.Message}");
                        // Try moving with a new name instead
                        var newPath = ManifestPath + $".{DateTime.Now.Ticks}";
                        File.Move(ManifestPath, newPath);
                    }
                }
                
                File.Move(contentFiles[0], ManifestPath, overwrite: true);
                Console.WriteLine($"[Manifest] Extracted to {ManifestPath}");
            }
            else
            {
                Console.WriteLine("[Manifest] No .content file found in zip!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Manifest] Extraction failed: {ex.Message}");
        }
        finally
        {
            // Cleanup
            try { File.Delete(zipPath); } catch { }
            try 
            { 
                if (Directory.Exists(extractDir))
                    Directory.Delete(extractDir, true); 
            } 
            catch { }
        }
    }

    public IDbConnection GetConnection()
    {
        var connectionString = $"Data Source={ManifestPath}";
        return new SqliteConnection(connectionString);
    }

    /// <summary>
    /// Gets an item definition by hash.
    /// </summary>
    public async Task<T?> GetDefinitionAsync<T>(uint hash, string tableName) where T : class
    {
        int signedHash = unchecked((int)hash);
        
        using var conn = GetConnection();
        var json = await conn.QueryFirstOrDefaultAsync<string>(
            $"SELECT json FROM {tableName} WHERE id = @id", 
            new { id = signedHash });

        if (string.IsNullOrEmpty(json))
            return null;
            
        return JsonSerializer.Deserialize<T>(json);
    }

    /// <summary>
    /// Gets item definition (name, icon, type, tier).
    /// </summary>
    public async Task<ItemDefinition?> GetItemDefinitionAsync(uint hash)
    {
        int signedHash = unchecked((int)hash);
        
        using var conn = GetConnection();
        var json = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT json FROM DestinyInventoryItemDefinition WHERE id = @id", 
            new { id = signedHash });

        if (string.IsNullOrEmpty(json))
            return null;
            
        return ParseItemDefinition(json);
    }

    private ItemDefinition? ParseItemDefinition(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            var displayProps = root.GetProperty("displayProperties");
            
            return new ItemDefinition
            {
                Hash = root.GetProperty("hash").GetUInt32(),
                Name = displayProps.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                Description = displayProps.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                Icon = displayProps.TryGetProperty("icon", out var i) ? i.GetString() : null,
                ItemType = root.TryGetProperty("itemTypeDisplayName", out var t) ? t.GetString() ?? "" : "",
                TierType = root.TryGetProperty("inventory", out var inv) && 
                           inv.TryGetProperty("tierType", out var tier) ? tier.GetInt32() : 0,
                BucketHash = root.TryGetProperty("inventory", out var inv2) && 
                             inv2.TryGetProperty("bucketTypeHash", out var bucket) ? bucket.GetUInt32() : 0,
                ClassType = root.TryGetProperty("classType", out var ct) ? ct.GetInt32() : 3, // 3 = Unknown
                IsExotic = root.TryGetProperty("inventory", out var inv3) && 
                           inv3.TryGetProperty("tierType", out var t2) && t2.GetInt32() == 6,
                // Phase 5: New details
                IconWatermark = root.TryGetProperty("iconWatermark", out var iwm) ? iwm.GetString() : root.TryGetProperty("iconWatermarkShelved", out var iwms) ? iwms.GetString() : null,
                AmmoType = root.TryGetProperty("equippingBlock", out var eq) && eq.TryGetProperty("ammoType", out var at) ? at.GetInt32() : 0,
                // For masterwork detection - get plug.uiPlugLabel
                UiPlugLabel = root.TryGetProperty("plug", out var plug) && plug.TryGetProperty("uiPlugLabel", out var label) ? label.GetString() : null,
                // Phase 7: DIM Visual Properties
                IsFeaturedItem = root.TryGetProperty("isFeaturedItem", out var featured) && featured.GetBoolean(),
                SeasonIconUrl = displayProps.TryGetProperty("iconHash", out var iconHash) ? 
                    // We'd need a secondary lookup to get the secondaryBackground from Icon table
                    // For now, use iconWatermark as a fallback
                    (root.TryGetProperty("iconWatermark", out var seasonIwm) ? seasonIwm.GetString() : null) : null,
                DefaultDamageTypeHash = root.TryGetProperty("defaultDamageTypeHash", out var dmgHash) ? (int)dmgHash.GetUInt32() : 0,
                SocketCategories = ParseSocketCategories(root)
            };
        }
        catch
        {
            return null;
        }
    }

    private List<SocketCategoryDefinition> ParseSocketCategories(JsonElement root)
    {
        var list = new List<SocketCategoryDefinition>();
        if (root.TryGetProperty("sockets", out var sockets) && 
            sockets.TryGetProperty("socketCategories", out var categories))
        {
            foreach (var cat in categories.EnumerateArray())
            {
                if (cat.TryGetProperty("socketCategoryHash", out var hashProp) &&
                    cat.TryGetProperty("socketIndexes", out var indexesProp))
                {
                    var indexes = new List<int>();
                    foreach (var idx in indexesProp.EnumerateArray())
                    {
                        indexes.Add(idx.GetInt32());
                    }
                    list.Add(new SocketCategoryDefinition(hashProp.GetUInt32(), indexes));
                }
            }
        }
        return list;
    }

    /// <summary>
    /// Gets stat definition by hash.
    /// </summary>
    public async Task<string?> GetStatNameAsync(uint hash)
    {
        int signedHash = unchecked((int)hash);
        
        using var conn = GetConnection();
        var json = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT json FROM DestinyStatDefinition WHERE id = @id", 
            new { id = signedHash });

        if (string.IsNullOrEmpty(json))
            return null;

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("displayProperties")
            .GetProperty("name")
            .GetString();
    }
    
    /// <summary>
    /// Gets stat icon URL by hash.
    /// </summary>
    public async Task<string?> GetStatIconAsync(uint hash)
    {
        int signedHash = unchecked((int)hash);
        
        using var conn = GetConnection();
        var json = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT json FROM DestinyStatDefinition WHERE id = @id", 
            new { id = signedHash });

        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var icon = doc.RootElement
                .GetProperty("displayProperties")
                .GetProperty("icon")
                .GetString();
            return icon;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets record/triumph definition by hash.
    /// </summary>
    public async Task<RecordDefinition?> GetRecordDefinitionAsync(uint hash)
    {
        int signedHash = unchecked((int)hash);
        
        using var conn = GetConnection();
        var json = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT json FROM DestinyRecordDefinition WHERE id = @id", 
            new { id = signedHash });

        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var displayProps = root.GetProperty("displayProperties");
            
            return new RecordDefinition
            {
                Hash = root.GetProperty("hash").GetUInt32(),
                Name = displayProps.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                Description = displayProps.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                Icon = displayProps.TryGetProperty("icon", out var i) ? i.GetString() : null,
                CompletionValue = root.TryGetProperty("completionInfo", out var ci) && 
                                  ci.TryGetProperty("scoreValue", out var sv) ? sv.GetInt32() : 0
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets presentation node (triumph category) by hash.
    /// </summary>
    public async Task<PresentationNodeDefinition?> GetPresentationNodeAsync(uint hash)
    {
        int signedHash = unchecked((int)hash);
        
        using var conn = GetConnection();
        var json = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT json FROM DestinyPresentationNodeDefinition WHERE id = @id", 
            new { id = signedHash });

        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var displayProps = root.GetProperty("displayProperties");
            
            var node = new PresentationNodeDefinition
            {
                Hash = root.GetProperty("hash").GetUInt32(),
                Name = displayProps.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                Icon = displayProps.TryGetProperty("icon", out var i) ? i.GetString() : null,
                ChildNodes = new List<uint>(),
                ChildRecords = new List<uint>()
            };
            
            if (root.TryGetProperty("children", out var children))
            {
                if (children.TryGetProperty("presentationNodes", out var nodes))
                {
                    foreach (var child in nodes.EnumerateArray())
                    {
                        if (child.TryGetProperty("presentationNodeHash", out var h))
                            node.ChildNodes.Add(h.GetUInt32());
                    }
                }
                if (children.TryGetProperty("records", out var records))
                {
                    foreach (var child in records.EnumerateArray())
                    {
                        if (child.TryGetProperty("recordHash", out var h))
                            node.ChildRecords.Add(h.GetUInt32());
                    }
                }
            }
            
            return node;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Search items by name.
    /// </summary>
    public async Task<IEnumerable<ItemDefinition>> SearchItemsAsync(string keyword, int limit = 20)
    {
        using var conn = GetConnection();
        var sql = @"
            SELECT json 
            FROM DestinyInventoryItemDefinition 
            WHERE json LIKE '%""name"":""' || @keyword || '%""' 
            LIMIT @limit";

        var results = await conn.QueryAsync<string>(sql, new { keyword, limit });
        
        var items = new List<ItemDefinition>();
        foreach (var json in results)
        {
            var item = ParseItemDefinition(json);
            if (item != null)
                items.Add(item);
        }
        return items;
    }
    /// <summary>
    /// Gets damage type definition by hash.
    /// </summary>
    public async Task<string?> GetDamageTypeIconAsync(uint hash)
    {
        int signedHash = unchecked((int)hash);
        
        using var conn = GetConnection();
        var json = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT json FROM DestinyDamageTypeDefinition WHERE id = @id", 
            new { id = signedHash });

        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var icon = doc.RootElement
                .GetProperty("displayProperties")
                .GetProperty("icon")
                .GetString();
            return icon;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Parsed item definition from manifest.
/// </summary>
public record ItemDefinition
{
    public uint Hash { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string? Icon { get; init; }
    public string ItemType { get; init; } = "";
    public int TierType { get; init; }
    public uint BucketHash { get; init; }
    public int ClassType { get; init; }
    public bool IsExotic { get; init; }
    public string? IconWatermark { get; init; }
    public int AmmoType { get; init; }
    public string? UiPlugLabel { get; init; } // "masterwork" for masterwork plugs
    
    // DIM Visual Properties (Phase 7)
    public bool IsFeaturedItem { get; init; }
    public string? SeasonIconUrl { get; init; }
    public int DefaultDamageTypeHash { get; init; }
    
    // Phase 8: Socket Categories (for Perks)
    public List<SocketCategoryDefinition> SocketCategories { get; init; } = new();
}

public record SocketCategoryDefinition(uint SocketCategoryHash, List<int> SocketIndexes);

/// <summary>
/// Parsed record/triumph definition.
/// </summary>
public record RecordDefinition
{
    public uint Hash { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string? Icon { get; init; }
    public int CompletionValue { get; init; }
}

/// <summary>
/// Parsed presentation node (category) definition.
/// </summary>
public record PresentationNodeDefinition
{
    public uint Hash { get; init; }
    public string Name { get; init; } = "";
    public string? Icon { get; init; }
    public List<uint> ChildNodes { get; init; } = new();
    public List<uint> ChildRecords { get; init; } = new();
}
