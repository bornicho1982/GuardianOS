using Traveler.Core.Models;

namespace Traveler.Core.Interfaces;

public interface IInventoryBucketService
{
    /// <summary>
    /// Returns the bucket definition for a given category.
    /// </summary>
    InventoryBucket GetBucket(BucketCategory category);

    /// <summary>
    /// Returns all managed buckets.
    /// </summary>
    IEnumerable<InventoryBucket> GetAllBuckets();

    /// <summary>
    /// Sorts a collection of items into their respective buckets.
    /// clearing existing bucket contents first.
    /// </summary>
    void DistributeItems(IEnumerable<InventoryItem> items);

    /// <summary>
    /// Creates a fresh list of buckets with items classified according to Titan Edition rules.
    /// </summary>
    List<InventoryBucket> Bucketize(IEnumerable<InventoryItem> items);
}
