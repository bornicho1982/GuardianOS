using System;
using System.Linq;
using System.Threading.Tasks;
using Traveler.Core.Interfaces;
using Traveler.Core.Models;

namespace Traveler.Data.Services.Inventory;

public class SmartMoveService : ISmartMoveService
{
    private readonly IInventoryService _inventoryService;

    // Bucket Hashes (Examples)
    private const uint BucketKinetic = 1498876634;
    private const uint BucketEnergy = 2465295065;
    private const uint BucketPower = 953998645;
    private const uint BucketHelmet = 3448274439;
    // ... others

    public SmartMoveService(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public async Task TransferItemAsync(InventoryItem item, string targetId)
    {
        // 1. Identify Target Bucket
        // Need to know what bucket the item belongs to (from Manifest)
        // uint bucketHash = GetBucketHash(item.ItemHash); 
        
        // 2. Check Capacity on Target
        // int count = _inventoryService.AllItems.Count(i => i.Location == targetId && i.BucketHash == bucketHash);
        // int capacity = 9; // 10 slots - 1 equipped? usually 9 in top section.

        /*
        if (count >= capacity)
        {
             // 3. TARGET FULL -> DISPLACEMENT STRATEGY
             // Find oldest item in that bucket on target
             var oldest = _inventoryService.AllItems
                .Where(i => i.Location == targetId && i.BucketHash == bucketHash && !i.IsEquipped)
                .OrderBy(i => i.InstanceId) // Approximation of age
                .FirstOrDefault();

             if (oldest != null)
             {
                 Console.WriteLine($"SmartMove: Target full. Moving {oldest.Name} to Vault.");
                 await ExecuteBungieTransfer(oldest, "Vault");
             }
        }
        */

        // 4. Move Item
        Console.WriteLine($"SmartMove: Moving {item.Name} to {targetId}.");
        // await ExecuteBungieTransfer(item, targetId);
    }
}
