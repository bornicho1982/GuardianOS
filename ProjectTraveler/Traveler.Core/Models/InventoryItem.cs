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
}
