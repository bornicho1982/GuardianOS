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
            // Convertir hash uint a long (signed) que es como SQLite suele almacenar los IDs grandes
            // En el manifiesto, los IDs son signed 32-bit int o signed 64-bit int.
            // Para hashes de items (uint32), Bungie a veces los trata como signed int32 en la DB.
            long idToQuery = (int)hash; 

            using var connection = new SqliteConnection($"Data Source={_manifestService.ManifestDatabasePath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT json FROM DestinyInventoryItemDefinition WHERE id = @id";
            command.Parameters.AddWithValue("@id", idToQuery);

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
}
