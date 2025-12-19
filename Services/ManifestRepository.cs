using Microsoft.Data.Sqlite;
using GuardianOS.Models;
using Newtonsoft.Json;
using System.Diagnostics;

namespace GuardianOS.Services;

public class ManifestRepository : IManifestRepository
{
    private readonly IManifestService _manifestService;

    public ManifestRepository(IManifestService manifestService)
    {
        _manifestService = manifestService;
    }

    public async Task<InventoryItemDefinition?> GetItemDefinitionAsync(uint hash)
    {
        if (!_manifestService.IsManifestReady) return null;

        try
        {
            // Bungie IDs can be signed 32-bit or unsigned 32-bit stored as Long in SQLite.
            // We search for both just in case.
            long idSigned = (int)hash;
            long idUnsigned = (long)hash;

            using var connection = new SqliteConnection($"Data Source={_manifestService.ManifestDatabasePath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT json FROM DestinyInventoryItemDefinition WHERE id = @id1 OR id = @id2";
            command.Parameters.AddWithValue("@id1", idSigned);
            command.Parameters.AddWithValue("@id2", idUnsigned);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var json = reader.GetString(0);
                return JsonConvert.DeserializeObject<InventoryItemDefinition>(json);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ManifestRepository] Error querying hash {hash}: {ex.Message}");
        }

        return null;
    }

    public async Task<ShaderDefinition?> GetShaderDefinitionAsync(uint hash)
    {
        if (!_manifestService.IsManifestReady) return null;

        try
        {
            long idSigned = (int)hash;
            long idUnsigned = (long)hash;

            using var connection = new SqliteConnection($"Data Source={_manifestService.ManifestDatabasePath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT json FROM DestinyInventoryItemDefinition WHERE id = @id1 OR id = @id2";
            command.Parameters.AddWithValue("@id1", idSigned);
            command.Parameters.AddWithValue("@id2", idUnsigned);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var jsonStr = reader.GetString(0);
                var jsonObj = Newtonsoft.Json.Linq.JObject.Parse(jsonStr);

                // Check translationBlock -> customDyes
                var customDyes = jsonObj["translationBlock"]?["customDyes"];
                if (customDyes == null)
                {
                    // Fallback to defaultDyes (used in some items/shaders)
                    customDyes = jsonObj["defaultDyes"];
                }

                if (customDyes != null)
                {
                    Console.WriteLine($"[ManifestRepository] SUCCESS: Found dyes for {hash}");
                    var definition = new ShaderDefinition { Hash = hash };
                    
                    foreach (var dye in customDyes)
                    {
                        var channelHash = (uint?)dye["channelHash"] ?? 0;
                        var dyeHash = (uint?)dye["dyeHash"] ?? 0;

                        var channelName = GetChannelName(channelHash);
                        var (argb, hex) = DecodeColor(dyeHash);

                        definition.Colors.Add(new ShaderColorLayer
                        {
                            ChannelHash = channelHash,
                            ChannelName = channelName,
                            ARGB = argb,
                            HexColor = hex
                        });
                    }
                    
                    return definition;
                }
                else 
                {
                     Console.WriteLine($"[ManifestRepository] Hash {hash} found but NO customDyes/defaultDyes. Keys: {string.Join(", ", ((Newtonsoft.Json.Linq.JObject)jsonObj).Properties().Select(p => p.Name))}");
                }
            }
            else
            {
                Console.WriteLine($"[ManifestRepository] Hash {hash} NOT found in DB (Tried {idSigned} and {idUnsigned}).");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ManifestRepository] Error extracting shader colors for {hash}: {ex.Message}");
        }

        return null;
    }

    private string GetChannelName(uint channelHash)
    {
        return channelHash switch
        {
            662199250 => "Primary",    // Blue channel usually
            218592586 => "Secondary",  // Green channel usually
            1367384683 => "Tertiary",  // Red channel usually
            246198591 => "Quaternary", // Alpha channel?
            _ => "Unknown"
        };
    }

    private (byte[], string) DecodeColor(uint dyeValue)
    {
        // Value is ARGB encoded int32
        byte a = (byte)((dyeValue >> 24) & 0xFF);
        byte r = (byte)((dyeValue >> 16) & 0xFF);
        byte g = (byte)((dyeValue >> 8) & 0xFF);
        byte b = (byte)(dyeValue & 0xFF);

        // Validar si el canal Alpha es 0, a veces significa opaco (255) en algunos sistemas, 
        // pero Bungie suele usar 255 para opaco. Si es 0, el shader no lo verá.
        // Jacarina tenía Alpha 158/200, así que sí se usa.
        
        string hex = $"#{a:X2}{r:X2}{g:X2}{b:X2}";
        return (new byte[] { a, r, g, b }, hex);
    }
}
