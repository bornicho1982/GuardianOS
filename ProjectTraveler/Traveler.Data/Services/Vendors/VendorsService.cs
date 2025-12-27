using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Traveler.Core.Interfaces;
using Traveler.Core.Models;
using Traveler.Data.Manifest;

namespace Traveler.Data.Services.Vendors;

/// <summary>
/// Service for fetching vendor inventories and checking wishlist matches.
/// </summary>
public class VendorsService : IVendorsService
{
    private readonly ManifestDatabase _manifestDatabase;
    private readonly IInventoryService _inventoryService;
    private Dictionary<uint, HashSet<uint>> _wishlist = new(); // ItemHash -> Required PerkHashes

    public VendorsService(ManifestDatabase manifestDatabase, IInventoryService inventoryService)
    {
        _manifestDatabase = manifestDatabase;
        _inventoryService = inventoryService;
    }

    public async Task<List<VendorSale>> GetVendorSalesAsync()
    {
        // TODO: Implement actual API call to fetch vendor sales
        // This would call GetVendor for each vendor hash
        
        // Mock data for now
        var mockSales = new List<VendorSale>
        {
            new VendorSale
            {
                ItemHash = 12345,
                Name = "Hawkmoon",
                ItemType = "Hand Cannon",
                VendorName = "Xûr",
                IsAffordable = true,
                IsWishlistMatch = false,
                Costs = new List<CurrencyCost>
                {
                    new CurrencyCost
                    {
                        CurrencyHash = 1,
                        CurrencyName = "Legendary Shards",
                        Quantity = 125,
                        UserBalance = 500
                    }
                }
            },
            new VendorSale
            {
                ItemHash = 67890,
                Name = "The Lament",
                ItemType = "Sword",
                VendorName = "Banshee-44",
                IsAffordable = false,
                IsWishlistMatch = true,
                Costs = new List<CurrencyCost>
                {
                    new CurrencyCost
                    {
                        CurrencyHash = 2,
                        CurrencyName = "Ascendant Shards",
                        Quantity = 3,
                        UserBalance = 1
                    }
                }
            }
        };

        return await Task.FromResult(mockSales);
    }

    public async Task LoadWishlistAsync(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"Wishlist file not found: {path}");
                return;
            }

            var json = await File.ReadAllTextAsync(path);
            // Expected format: { "itemHash": [perkHash1, perkHash2], ... }
            var parsed = JsonSerializer.Deserialize<Dictionary<string, List<uint>>>(json);
            
            _wishlist.Clear();
            if (parsed != null)
            {
                foreach (var kvp in parsed)
                {
                    if (uint.TryParse(kvp.Key, out var itemHash))
                    {
                        _wishlist[itemHash] = kvp.Value.ToHashSet();
                    }
                }
            }

            Console.WriteLine($"Loaded wishlist with {_wishlist.Count} items.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading wishlist: {ex.Message}");
        }
    }

    public bool IsWishlistMatch(uint itemHash, List<uint> perkHashes)
    {
        if (!_wishlist.TryGetValue(itemHash, out var requiredPerks))
            return false;

        // Check if all required perks are present
        return requiredPerks.All(required => perkHashes.Contains(required));
    }
}
