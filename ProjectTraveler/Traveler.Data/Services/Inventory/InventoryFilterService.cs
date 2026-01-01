using Traveler.Core.Interfaces;
using Traveler.Core.Models;

namespace Traveler.Data.Services.Inventory;

public class InventoryFilterService : IInventoryFilterService
{
    public IEnumerable<InventoryItem> FilterItems(IEnumerable<InventoryItem> items, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return items;

        var predicates = ParseQuery(query.ToLowerInvariant());

        return items.Where(item => predicates.All(p => p(item)));
    }

    public bool ItemMatches(InventoryItem item, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        var predicates = ParseQuery(query.ToLowerInvariant());
        return predicates.All(p => p(item));
    }

    private List<Func<InventoryItem, bool>> ParseQuery(string query)
    {
        var predicates = new List<Func<InventoryItem, bool>>();
        
        // Split by spaces, but respect quotes (simplified for now)
        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            if (token.Contains(':'))
            {
                var parts = token.Split(':');
                if (parts.Length == 2)
                {
                    var prefix = parts[0];
                    var value = parts[1];
                    predicates.Add(CreatePredicate(prefix, value));
                }
                else
                {
                     // Malformed logic, treat as text
                     predicates.Add(i => i.Name.ToLower().Contains(token));
                }
            }
            else
            {
                // Free text search (Name, Type, Archetype)
                predicates.Add(i => 
                    i.Name.ToLower().Contains(token) || 
                    i.ItemType.ToLower().Contains(token) ||
                    i.Archetype.ToLower().Contains(token));
            }
        }

        return predicates;
    }

    private Func<InventoryItem, bool> CreatePredicate(string prefix, string value)
    {
        return prefix switch
        {
            "is" => item => CheckIsPredicate(item, value),
            "tag" => item => item.Tags.Contains(value),
            "tier" => item => item.TierType.ToLower() == value,
            "stat" => item => true, // Placeholder for stat > x
            _ => item => true
        };
    }

    private bool CheckIsPredicate(InventoryItem item, string value)
    {
        return value switch
        {
            // Elements
            "kinetic" => item.DamageTypeHash == 3373582085,
            "arc" => item.DamageTypeHash == 2303181850,
            "solar" => item.DamageTypeHash == 1847026933,
            "void" => item.DamageTypeHash == 3454344768,
            "stasis" => item.DamageTypeHash == 1513470552 || item.DamageTypeHash == 151347233,
            "strand" => item.DamageTypeHash == 3949783978,
            
            // Rarity
            "exotic" => item.IsExotic || item.TierType.ToLower() == "exotic",
            "legendary" => item.TierType.ToLower() == "legendary",
            "rare" => item.TierType.ToLower() == "rare",
            
            // Category
            "weapon" => item.BucketHash == 1498876634 || item.BucketHash == 2465295065 || item.BucketHash == 953998645,
            "armor" => item.BucketHash == 3448274439 || item.BucketHash == 3551918588 || item.BucketHash == 14239492 || item.BucketHash == 20886954 || item.BucketHash == 1585787867,
            
            // State
            "crafted" => item.IsCrafted,
            "masterwork" => item.IsMasterwork,
            "enhanced" => item.IsEnhanced,
            "locked" => item.IsLocked,
            
            _ => false
        };
    }
}
