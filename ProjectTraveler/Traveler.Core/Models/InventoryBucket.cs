using System.Collections.ObjectModel;

namespace Traveler.Core.Models;

/// <summary>
/// Represents a container for items (e.g., "Kinetic Weapons", "Helmets").
/// Maps to a Bungie Inventory Bucket but optimized for UI binding.
/// </summary>
public class InventoryBucket
{
    public uint Hash { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Capacity { get; set; }
    
    /// <summary>
    /// Whether this bucket is for equipment (Weapons/Armor) or general items (Consumables).
    /// </summary>
    public bool HasTransferDestination { get; set; } = true;
    
    /// <summary>
    /// The collection of items currently in this bucket.
    /// </summary>
    public ObservableCollection<InventoryItem> Items { get; set; } = new();

    /// <summary>
    /// Helper to identify category (Weapon, Armor, General).
    /// </summary>
    public BucketCategory Category { get; set; }
}

public enum BucketCategory
{
    Unknown = 0,
    Kinetic = 1,
    Energy = 2,
    Power = 3,
    Helmet = 4,
    Gauntlets = 5,
    Chest = 6,
    Legs = 7,
    ClassItem = 8,
    Ghost = 9,
    Vehicle = 10,
    Ship = 11,
    Emblem = 12,
    Consumable = 13,
    Mod = 14,
    Subclass = 15,
    LostItems = 16, // Postmaster
    Engrams = 17
}
