namespace Traveler.Core.Models;

/// <summary>
/// Represents a simplified, hydrated inventory item (Armor/Weapon) with Armor 3.0 stats.
/// </summary>
public record InventoryItem
{
    public required uint ItemHash { get; init; }
    public required long InstanceId { get; init; }
    public required string Name { get; init; }
    public string Icon { get; init; } = string.Empty;
    public string ItemType { get; init; } = string.Empty; // e.g. "Helmet", "Hand Cannon"
    public string TierType { get; init; } = string.Empty; // e.g. "Exotic", "Legendary"

    /// <summary>
    /// Stats in the 0-200 range (Armor 3.0).
    /// Key is the StatHash.
    /// </summary>
    public Dictionary<uint, int> Stats { get; init; } = new();

    /// <summary>
    /// Base stats before mods/masterwork, if needed for optimization.
    /// </summary>
    public Dictionary<uint, int> BaseStats { get; init; } = new();

    public int PowerLevel { get; init; }

    public bool IsExotic { get; init; }
    public bool IsArtifice { get; init; }
    
    /// <summary>
    /// Indicates if this is a Tier 5 item capable of Tuning.
    /// </summary>
    public bool IsTier5 { get; init; }

    /// <summary>
    /// Identifier for Set Bonuses (e.g. "raid_crotas_end").
    /// </summary>
    public string? SetBonusId { get; init; }

    // Logic for Loadout Optimizer
    public bool IsLocked { get; set; }
    public bool IsJunk { get; set; }
    
    // ===== Location & State (for SmartMove) =====
    
    /// <summary>
    /// Current location: Character ID or "vault".
    /// </summary>
    public string Location { get; set; } = "vault";
    
    /// <summary>
    /// Bucket hash from manifest (e.g., Helmet, Kinetic Weapon).
    /// </summary>
    public uint BucketHash { get; init; }
    
    /// <summary>
    /// Whether this item is currently equipped on a character.
    /// </summary>
    public bool IsEquipped { get; set; }
    
    /// <summary>
    /// Damage type hash (Solar, Arc, Void, Strand, Stasis).
    /// </summary>
    public uint DamageTypeHash { get; init; }
    
    /// <summary>
    /// User-defined tags (e.g., "keep", "junk", "pvp", "pve").
    /// </summary>
    public List<string> Tags { get; set; } = new();
    
    /// <summary>
    /// User notes/annotations for this item.
    /// </summary>
    public string? Notes { get; set; }
}
