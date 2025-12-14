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
