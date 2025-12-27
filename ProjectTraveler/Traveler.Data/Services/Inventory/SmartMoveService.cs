using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Traveler.Core.Interfaces;
using Traveler.Core.Models;

namespace Traveler.Data.Services.Inventory;

/// <summary>
/// Service implementing "Smart Moves" with automatic displacement when target is full.
/// </summary>
public class SmartMoveService : ISmartMoveService
{
    private readonly IInventoryService _inventoryService;

    // Bucket capacities (9 unequipped slots per bucket on character)
    private const int CharacterSlotCapacity = 9;
    private const int VaultSlotCapacity = 500; // General vault capacity

    // Character and Vault location IDs
    public const string VaultId = "vault";

    public SmartMoveService(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    /// <summary>
    /// Transfers an item to the target character/vault with automatic displacement if full.
    /// </summary>
    public async Task TransferItemAsync(InventoryItem item, string targetId)
    {
        Console.WriteLine($"[SmartMove] Requesting transfer: {item.Name} -> {targetId}");

        // Get bucket hash for the item (simplified - would come from manifest)
        var bucketHash = InferBucketHash(item.ItemType);
        
        // Check current items in target bucket
        var targetItems = GetItemsInBucket(targetId, bucketHash);
        var capacity = targetId == VaultId ? VaultSlotCapacity : CharacterSlotCapacity;

        if (targetItems.Count >= capacity)
        {
            Console.WriteLine($"[SmartMove] Target {targetId} is FULL ({targetItems.Count}/{capacity}). Initiating displacement...");
            
            // Find lowest priority item to displace
            var toDisplace = FindLowestPriorityItem(targetItems);
            
            if (toDisplace != null)
            {
                Console.WriteLine($"[SmartMove] Displacing '{toDisplace.Name}' (Priority: {CalculatePriority(toDisplace)}) to Vault");
                
                // Move displaced item to vault first
                await ExecuteTransferAsync(toDisplace, VaultId);
            }
            else
            {
                Console.WriteLine("[SmartMove] ERROR: No items available for displacement. All locked?");
                return;
            }
        }

        // Now move the original item
        await ExecuteTransferAsync(item, targetId);
        Console.WriteLine($"[SmartMove] SUCCESS: {item.Name} is now on {targetId}");
    }

    /// <summary>
    /// Gets all items in a specific bucket on a location.
    /// </summary>
    private List<InventoryItem> GetItemsInBucket(string locationId, uint bucketHash)
    {
        // In real implementation, we'd filter by Location property
        // For now, using all items as mock (single character assumption)
        return _inventoryService.AllItems
            .Where(i => InferBucketHash(i.ItemType) == bucketHash)
            .ToList();
    }

    /// <summary>
    /// Finds the lowest priority item that can be displaced (not locked, not equipped).
    /// </summary>
    private InventoryItem? FindLowestPriorityItem(List<InventoryItem> items)
    {
        return items
            .Where(i => !i.IsLocked && !i.IsEquipped)
            .OrderBy(CalculatePriority)
            .FirstOrDefault();
    }

    /// <summary>
    /// Calculates a priority score for an item. Higher = more valuable, keep.
    /// Lower = can be displaced.
    /// </summary>
    private int CalculatePriority(InventoryItem item)
    {
        int score = 0;

        // Exotic items are high priority
        if (item.IsExotic) score += 100;

        // High power level increases priority
        score += item.PowerLevel / 10;

        // Masterworked items (not implemented yet, placeholder)
        // if (item.IsMasterworked) score += 50;

        // Custom tags would increase priority (Tags system not implemented yet)
        // if (item.Tags.Contains("keep")) score += 200;
        // if (item.Tags.Contains("junk")) score -= 100;

        // Artifice/Tier 5 armor is valuable
        if (item.IsArtifice) score += 30;
        if (item.IsTier5) score += 25;

        return score;
    }

    /// <summary>
    /// Executes the actual transfer via Bungie API.
    /// </summary>
    private async Task ExecuteTransferAsync(InventoryItem item, string targetId)
    {
        // Optimistic UI update - move item in local state immediately
        // Real implementation would:
        // 1. Call Bungie TransferItem API
        // 2. On success, update local state
        // 3. On failure, revert and show error

        Console.WriteLine($"[API] POST /TransferItem: {item.InstanceId} -> {targetId}");
        
        // Simulate network delay
        await Task.Delay(100);

        // Update local state (simplified)
        // item.Location = targetId;
        
        // Trigger UI refresh
        // _inventoryService.NotifyItemMoved(item, targetId);
    }

    /// <summary>
    /// Infers bucket hash from item type string (simplified mapping).
    /// In real implementation, this would come from DestinyInventoryItemDefinition.
    /// </summary>
    private uint InferBucketHash(string itemType)
    {
        // Weapons
        if (itemType.Contains("Cannon", StringComparison.OrdinalIgnoreCase) ||
            itemType.Contains("Rifle", StringComparison.OrdinalIgnoreCase) ||
            itemType.Contains("Bow", StringComparison.OrdinalIgnoreCase))
            return 1498876634; // Kinetic (simplified)

        if (itemType.Contains("Launcher", StringComparison.OrdinalIgnoreCase) ||
            itemType.Contains("Sword", StringComparison.OrdinalIgnoreCase) ||
            itemType.Contains("Machine Gun", StringComparison.OrdinalIgnoreCase))
            return 953998645; // Power

        // Armor
        if (itemType.Contains("Helmet", StringComparison.OrdinalIgnoreCase))
            return 3448274439;
        if (itemType.Contains("Gauntlets", StringComparison.OrdinalIgnoreCase))
            return 3551918588;
        if (itemType.Contains("Chest", StringComparison.OrdinalIgnoreCase))
            return 14239492;
        if (itemType.Contains("Leg", StringComparison.OrdinalIgnoreCase))
            return 20886954;
        if (itemType.Contains("Class", StringComparison.OrdinalIgnoreCase))
            return 1585787867;

        // Default (Consumables/General)
        return 0;
    }
}

