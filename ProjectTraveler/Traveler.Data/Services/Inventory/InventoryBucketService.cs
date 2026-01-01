using System.Collections.ObjectModel;
using Traveler.Core.Interfaces;
using Traveler.Core.Models;

namespace Traveler.Data.Services.Inventory;

public class InventoryBucketService : IInventoryBucketService
{
    private readonly Dictionary<BucketCategory, InventoryBucket> _buckets;

    public InventoryBucketService()
    {
        _buckets = new Dictionary<BucketCategory, InventoryBucket>();
        InitializeBuckets();
    }

    private void InitializeBuckets()
    {
        // Weapons
        CreateBucket(BucketCategory.Kinetic, 1498876634, "Kinetic Weapons");
        CreateBucket(BucketCategory.Energy, 2465295065, "Energy Weapons");
        CreateBucket(BucketCategory.Power, 953998645, "Power Weapons");

        // Armor
        CreateBucket(BucketCategory.Helmet, 3448274439, "Helmets");
        CreateBucket(BucketCategory.Gauntlets, 3551918588, "Gauntlets");
        CreateBucket(BucketCategory.Chest, 14239492, "Chest Armor");
        CreateBucket(BucketCategory.Legs, 20886954, "Leg Armor");
        CreateBucket(BucketCategory.ClassItem, 1585787867, "Class Items");

        // Other
        CreateBucket(BucketCategory.Ghost, 4023194814, "Ghost");
        CreateBucket(BucketCategory.Subclass, 3284755031, "Subclass");
        CreateBucket(BucketCategory.Emblem, 4274335291, "Emblems");
        CreateBucket(BucketCategory.Consumable, 138197802, "Consumables");
        
        // Postmaster & Engrams (Hashes vary or need specific handling, using placeholders or generic logic)
        CreateBucket(BucketCategory.LostItems, 215593132, "Postmaster"); 
        CreateBucket(BucketCategory.Engrams, 375726501, "Engrams");
    }

    private void CreateBucket(BucketCategory category, uint hash, string name)
    {
        _buckets[category] = new InventoryBucket
        {
            Hash = hash,
            Name = name,
            Category = category,
            Items = new ObservableCollection<InventoryItem>()
        };
    }

    public InventoryBucket GetBucket(BucketCategory category)
    {
        if (_buckets.TryGetValue(category, out var bucket))
        {
            return bucket;
        }
        
        // Return a dummy bucket if not found to prevent null refs
        return new InventoryBucket { Name = "Unknown", Items = new ObservableCollection<InventoryItem>() };
    }

    public IEnumerable<InventoryBucket> GetAllBuckets() => _buckets.Values;

    public void DistributeItems(IEnumerable<InventoryItem> items)
    {
        // 1. Clear all buckets
        foreach (var bucket in _buckets.Values)
        {
            bucket.Items.Clear();
        }

        // 2. Sort items
        foreach (var item in items)
        {
            var category = GetCategoryFromHash(item.BucketHash);
            if (_buckets.TryGetValue(category, out var bucket))
            {
                bucket.Items.Add(item);
            }
        }
    }

    private BucketCategory GetCategoryFromHash(uint hash)
    {
        return hash switch
        {
            1498876634 => BucketCategory.Kinetic,
            2465295065 => BucketCategory.Energy,
            953998645 => BucketCategory.Power,
            3448274439 => BucketCategory.Helmet,
            3551918588 => BucketCategory.Gauntlets,
            14239492 => BucketCategory.Chest,
            20886954 => BucketCategory.Legs,
            1585787867 => BucketCategory.ClassItem,
            4023194814 => BucketCategory.Ghost,
            3284755031 => BucketCategory.Subclass,
            4274335291 => BucketCategory.Emblem,
            138197802 => BucketCategory.Consumable,
            215593132 => BucketCategory.LostItems,
            375726501 => BucketCategory.Engrams,
            _ => BucketCategory.Unknown
        };
    }
}
