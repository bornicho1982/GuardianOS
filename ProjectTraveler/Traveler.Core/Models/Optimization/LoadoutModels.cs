using System.Collections.Generic;
using Traveler.Core.Models;

namespace Traveler.Core.Models.Optimization;

public class LoadoutRequest
{
    public List<InventoryItem> AvailableItems { get; set; } = new();
    
    // Key: StatHash (e.g. Mobility), Value: Min Value (e.g. 100)
    public Dictionary<uint, int> MinimumStats { get; set; } = new();
    
    // If true, we prioritize Total Tier (Sum of stat tiers/10)
    public bool MaximizeTotalTiers { get; set; } = true;

    // TODO: Add constraints for specific exotics, etc.
    public List<uint> CompulsoryItemHashes { get; set; } = new();
}

public class LoadoutResult
{
    public List<InventoryItem> SelectedItems { get; set; } = new();
    public Dictionary<uint, int> FinalStats { get; set; } = new();
    
    // Key: InstanceId, Value: Description of tuning (e.g. "+5 Mobility / -5 Resilience")
    public Dictionary<long, string> TuningAdjustments { get; set; } = new();
    
    public bool IsValid { get; set; }
}
