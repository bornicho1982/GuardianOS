using Traveler.Core.Models;

namespace Traveler.Core.Interfaces;

public interface IInventoryFilterService
{
    /// <summary>
    /// Filters a collection of items based on a text query.
    /// Supports DIM-like syntax (e.g. "is:solar", "hand cannon", "tag:favorite").
    /// </summary>
    IEnumerable<InventoryItem> FilterItems(IEnumerable<InventoryItem> items, string query);

    /// <summary>
    /// Validates if a single item matches the query.
    /// </summary>
    bool ItemMatches(InventoryItem item, string query);
}
