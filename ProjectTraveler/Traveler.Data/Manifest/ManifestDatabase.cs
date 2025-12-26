using System.Data;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Traveler.Data.Manifest;

public class ManifestDatabase
{
    private const string ManifestPath = "manifest_content.sqlite";
    
    // This should be called with the path from the API response
    public async Task DownloadAndExtractManifest(string mobileWorldContentPath)
    {
        if (File.Exists(ManifestPath))
        {
            // TODO: Check version/hash to see if update is needed
            return;
        }

        using var client = new HttpClient();
        var url = $"https://www.bungie.net{mobileWorldContentPath}";
        var zipBytes = await client.GetByteArrayAsync(url);
        
        var zipPath = "temp_manifest.zip";
        await File.WriteAllBytesAsync(zipPath, zipBytes);

        ZipFile.ExtractToDirectory(zipPath, ".");
        
        // Find the .content file and rename/move it
        var contentFile = Directory.GetFiles(".", "*.content")[0];
        if (File.Exists(ManifestPath)) File.Delete(ManifestPath);
        File.Move(contentFile, ManifestPath);
        File.Delete(zipPath);
    }

    public IDbConnection GetConnection()
    {
        var connectionString = $"Data Source={ManifestPath}";
        return new SqliteConnection(connectionString);
    }

    // Example query
    public async Task<string> GetItemName(uint hash)
    {
        // Bungie hashes are signed int32 in SQLite but uint32 in API. 
        // Need to cast uint hash to int.
        int signedHash = (int)hash;
        
        using var conn = GetConnection();
        // The table names in manifest are usually DestinyInventoryItemDefinition, etc.
        var json = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT json FROM DestinyInventoryItemDefinition WHERE id = @id", 
            new { id = signedHash });

        return json; // Returns raw JSON
    }

    public async Task<IEnumerable<string>> SearchItemsAsync(string keyword, int limit = 10)
    {
        using var conn = GetConnection();
        // Very basic RAG: Search for keyword in the json blob. 
        // Filter by itemTypeDisplayName to avoid junk.
        var sql = @"
            SELECT json 
            FROM DestinyInventoryItemDefinition 
            WHERE json LIKE '%' || @keyword || '%' 
            AND json LIKE '%Exotic%'
            LIMIT @limit";

        return await conn.QueryAsync<string>(sql, new { keyword, limit });
    }
}
