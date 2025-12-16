using System.Text.Json;
using GuardianOS.Models;

namespace GuardianOS.Services;

/// <summary>
/// Bridge service to translate C# models to JSON payloads for WebGL renderers.
/// Supports both local Three.js viewer and external D2Foundry integration.
/// </summary>
public class ThreeJsBridge
{
    private const string D2FOUNDRY_BASE_URL = "https://d2foundry.gg";
    
    /// <summary>
    /// Generates a JSON payload containing all equipped items, shaders, and ornaments
    /// for consumption by the JavaScript 3D viewer.
    /// </summary>
    public string GenerateLoadoutPayload(
        DestinyCharacter character,
        List<InventoryItem> armorItems,
        InventoryItem? subclass = null)
    {
        var itemHashes = new List<long>();
        var shaderHashes = new List<long>();
        var ornamentHashes = new List<long>();

        foreach (var item in armorItems)
        {
            itemHashes.Add(item.ItemHash);
            shaderHashes.Add(item.ShaderHash ?? 0);
            ornamentHashes.Add(item.OrnamentHash ?? 0);
        }

        var payload = new
        {
            action = "loadGuardian",
            config = new
            {
                itemHashes,
                shaderHashes,
                ornamentHashes,
                subclassHash = subclass?.ItemHash ?? 0,
                classType = character.ClassType,
                isFemale = character.GenderType == 1,
                characterId = character.CharacterId,
                emblemUrl = character.EmblemUrl
            }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });
    }

    /// <summary>
    /// Generates a D2Foundry URL for viewing the character's loadout.
    /// Used as fallback when local renderer fails.
    /// </summary>
    public string GenerateD2FoundryUrl(DestinyCharacter character, List<InventoryItem> items)
    {
        // D2Foundry uses item hashes in URL for loadout preview
        var itemHashParams = string.Join(",", items.Select(i => i.ItemHash));
        return $"{D2FOUNDRY_BASE_URL}/loadout?items={itemHashParams}&class={character.ClassType}";
    }

    /// <summary>
    /// Gets the base D2Foundry URL for proof of concept testing.
    /// </summary>
    public static string GetD2FoundryBaseUrl() => D2FOUNDRY_BASE_URL;
}
