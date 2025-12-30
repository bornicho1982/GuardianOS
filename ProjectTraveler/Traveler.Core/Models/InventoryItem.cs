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
    
    /// <summary>
    /// Full URL to the item icon on Bungie.net
    /// </summary>
    public string IconUrl => string.IsNullOrEmpty(Icon) 
        ? "" 
        : $"https://www.bungie.net{Icon}";
        
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
    /// Human-readable bucket type name for UI filtering.
    /// </summary>
    public string BucketType => BucketHash switch
    {
        1498876634 => "Kinetic Weapons",
        2465295065 => "Energy Weapons",
        953998645 => "Power Weapons",
        3448274439 => "Helmet",
        3551918588 => "Gauntlets",
        14239492 => "Chest Armor",
        20886954 => "Leg Armor",
        1585787867 => "Class Armor",
        4023194814 => "Ghost",
        4274335291 => "Emblems",
        3284755031 => "Subclass",
        138197802 => "General",
        _ => "Other"
    };
    
    /// <summary>
    /// Whether this item is currently equipped on a character.
    /// </summary>
    public bool IsEquipped { get; set; }
    
    /// <summary>
    /// Damage type hash (Solar, Arc, Void, Strand, Stasis).
    /// </summary>
    public uint DamageTypeHash { get; init; }
    
    /// <summary>
    /// Class type for armor: "Titan", "Hunter", "Warlock", or "Any" for universal armor.
    /// Empty for weapons.
    /// </summary>
    public string ClassType { get; init; } = string.Empty;
    
    /// <summary>
    /// User-defined tags (e.g., "keep", "junk", "pvp", "pve").
    /// </summary>
    public List<string> Tags { get; set; } = new();
    
    /// <summary>
    /// User notes/annotations for this item.
    /// </summary>
    public string? Notes { get; set; }
    
    // ===== Computed Properties for UI =====
    
    /// <summary>
    /// Total of the 6 main stats (Mobility...Strength).
    /// </summary>
    public int TotalStats => Stats.Values.Sum();
    
    /// <summary>
    /// Returns the hex color for the damage type.
    /// </summary>
    public string ElementColor
    {
        get
        {
            return DamageTypeHash switch
            {
                3373582085 => "#A0A0A0", // Kinetic (Gray)
                2303181850 => "#77CDFF", // Arc (Blue)
                1847026933 => "#F4A460", // Solar (Orange)
                3454344768 => "#9B59B6", // Void (Purple)
                1513470552 => "#4682B4", // Stasis (Dark Blue)
                3949783978 => "#32CD32", // Strand (Green)
                _ => "#A0A0A0"           // Default/Kinetic
            };
        }
    }
    
    /// <summary>
    /// Primary stat value (Power Level for weapons, Defense for armor).
    /// </summary>
    public int PrimaryStatValue { get; init; }
    
    /// <summary>
    /// Icon for the ammo type (Primary, Special, Heavy).
    /// </summary>
    public string AmmoTypeIcon { get; init; } = string.Empty;

    // ===== Sockets (Perks, Mods, Abilities) =====

    /// <summary>
    /// Name of the Intrinsic Trait / Archetype (e.g. "Adaptive Frame").
    /// </summary>
    public string Archetype { get; set; } = string.Empty;

    /// <summary>
    /// Icon of the Intrinsic Trait / Archetype.
    /// </summary>
    public string ArchetypeIcon { get; set; } = string.Empty;

    /// <summary>
    /// List of icons for equipped mods (Armor) or Perks (Weapons).
    /// </summary>
    public List<string> SocketIcons { get; set; } = new();
    
    /// <summary>
    /// Specifically for Subclass: Super, Grenade, Melee, Class Ability icons.
    /// </summary>
    public List<string> AbilityIcons { get; set; } = new();

    // New Detailed Properties (Phase 5)
    public string WatermarkIconUrl { get; set; } = "";
    public int AmmoType { get; set; } = 0; // 0:None, 1:Primary, 2:Special, 3:Heavy
    public int MasterworkLevel { get; set; } = 0;
    
    // Phase 6: Deep Dive
    public int EnergyCapacity { get; set; } = 0; // For Armor (1-10)
    public int DamageType { get; set; } = 0; // 0:None, 1:Kinetic, 2:Arc, 3:Solar, 4:Void, 5:Raid, 6:Stasis, 7:Strand
    public string DamageTypeIcon { get; set; } = ""; // Icon for the damage type

    public bool IsMasterwork => MasterworkLevel >= 10;
    
    /// <summary>
    /// Diamond display for masterwork tier (1-10 levels, displayed as ◆).
    /// </summary>
    public string MasterworkTierDisplay => MasterworkLevel switch
    {
        >= 10 => "◆◆◆◆◆",
        >= 8 => "◆◆◆◆◇",
        >= 6 => "◆◆◆◇◇",
        >= 4 => "◆◆◇◇◇",
        >= 2 => "◆◇◇◇◇",
        _ => ""
    };
    
    /// <summary>
    /// Whether to show any masterwork indicator.
    /// </summary>
    public bool HasMasterwork => MasterworkLevel > 0;

    public string AmmoIconUrl
    {
        get
        {
            return AmmoType switch
            {
                1 => "/common/destiny2_content/icons/043addd07b34b172a3f019623e5904bc.png", // Primary
                2 => "/common/destiny2_content/icons/e43c53982e54dd6d63d08502f6ae6403.png", // Special
                3 => "/common/destiny2_content/icons/a2862e3c0428d002a24683058a96431e.png", // Heavy
                _ => ""
            };
        }
    }
}
