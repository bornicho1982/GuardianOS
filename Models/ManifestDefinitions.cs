using Newtonsoft.Json;

namespace GuardianOS.Models;

/// <summary>
/// Definición parcial de un ítem del Manifiesto (DestinyInventoryItemDefinition).
/// </summary>
public class InventoryItemDefinition
{
    [JsonProperty("hash")]
    public uint Hash { get; set; }

    [JsonProperty("displayProperties")]
    public DisplayPropertiesDefinition DisplayProperties { get; set; } = new();

    [JsonProperty("itemType")]
    public int ItemType { get; set; }

    [JsonProperty("itemSubType")]
    public int ItemSubType { get; set; }

    [JsonProperty("classType")]
    public int ClassType { get; set; }

    [JsonProperty("itemTypeDisplayName")]
    public string ItemTypeDisplayName { get; set; } = string.Empty;

    [JsonProperty("itemCategoryHashes")]
    public List<long>? ItemCategoryHashes { get; set; }

    [JsonProperty("inventory")]
    public InventoryDefinition Inventory { get; set; } = new();
    
    // Propiedades útiles para UI
    [JsonIgnore]
    public string Name => DisplayProperties.Name;
    
    [JsonIgnore]
    public string Icon => DisplayProperties.Icon;
    
    [JsonIgnore]
    public bool HasIcon => !string.IsNullOrEmpty(DisplayProperties.Icon);
}

public class DisplayPropertiesDefinition
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("icon")]
    public string Icon { get; set; } = string.Empty;
    
    [JsonProperty("hasIcon")]
    public bool HasIcon { get; set; }
}

public class InventoryDefinition
{
    [JsonProperty("bucketTypeHash")]
    public uint BucketTypeHash { get; set; }
    
    [JsonProperty("tierType")]
    public int TierType { get; set; }
    
    [JsonProperty("tierTypeName")]
    public string TierTypeName { get; set; } = string.Empty;
}

/// <summary>
/// Modelo simplificado para extraer colores de shader.
/// </summary>
public class ShaderDefinition
{
    public uint Hash { get; set; }
    public List<ShaderColorLayer> Colors { get; set; } = new();
}

public class ShaderColorLayer
{
    public string ChannelName { get; set; } = "Unknown";
    public uint ChannelHash { get; set; }
    public string HexColor { get; set; } = "#FFFFFF"; // HTML Hex w/ Alpha
    public byte[] ARGB { get; set; } = new byte[4];
}

