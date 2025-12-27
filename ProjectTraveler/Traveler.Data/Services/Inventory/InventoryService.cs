using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Traveler.Core.Interfaces;
using Traveler.Core.Models;
using Traveler.Data.Auth;
using Traveler.Data.Manifest;

namespace Traveler.Data.Services.Inventory;

/// <summary>
/// Service for fetching and managing user inventory from Bungie API.
/// Uses raw HTTP calls for maximum compatibility with API changes.
/// </summary>
public class InventoryService : IInventoryService
{
    private readonly BungieAuthService _authService;
    private readonly ManifestDatabase _manifestDatabase;
    private readonly HttpClient _httpClient;

    private const string BaseUrl = "https://www.bungie.net/Platform";

    // Stat hash constants (Armor 3.0 range: 0-200)
    private static class StatHashes
    {
        public const uint Mobility = 2996146975;
        public const uint Resilience = 392767087;
        public const uint Recovery = 1943323491;
        public const uint Discipline = 1735777505;
        public const uint Intellect = 144602215;
        public const uint Strength = 4244567218;
    }

    public ObservableCollection<InventoryItem> AllItems { get; } = new();
    
    public int MembershipType { get; private set; }
    public long DestinyMembershipId { get; private set; }
    public List<long> CharacterIds { get; private set; } = new();

    public InventoryService(BungieAuthService authService, ManifestDatabase manifestDatabase)
    {
        _authService = authService;
        _manifestDatabase = manifestDatabase;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", BungieAuthService.ApiKey);
    }

    /// <summary>
    /// Sets membership info (call after OAuth login).
    /// </summary>
    public void SetMembership(int membershipType, long membershipId)
    {
        MembershipType = membershipType;
        DestinyMembershipId = membershipId;
        Console.WriteLine($"[InventoryService] Membership set: {membershipType} / {membershipId}");
    }

    public async Task RefreshInventoryAsync()
    {
        Console.WriteLine("[InventoryService] RefreshInventoryAsync called");
        
        if (DestinyMembershipId == 0 || !_authService.IsAuthenticated)
        {
            Console.WriteLine("[InventoryService] Not authenticated - using MOCK DATA");
            LoadMockData();
            return;
        }

        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.AccessToken);

            // Fetch profile with required components
            var components = "100,102,200,201,205,300,304,305"; // Profiles, Inventories, Characters, Equipment, Instances, Stats, Sockets
            var url = $"{BaseUrl}/Destiny2/{MembershipType}/Profile/{DestinyMembershipId}/?components={components}";
            
            Console.WriteLine($"[InventoryService] Fetching: {url}");
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[InventoryService] API Error: {response.StatusCode}");
                LoadMockData();
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Response", out var profileResponse))
            {
                Console.WriteLine("[InventoryService] No Response in API data");
                LoadMockData();
                return;
            }

            AllItems.Clear();
            CharacterIds.Clear();

            // Parse item instances (for power level)
            var instances = new Dictionary<long, JsonElement>();
            if (profileResponse.TryGetProperty("itemComponents", out var itemComp) &&
                itemComp.TryGetProperty("instances", out var inst) &&
                inst.TryGetProperty("data", out var instData))
            {
                foreach (var prop in instData.EnumerateObject())
                {
                    if (long.TryParse(prop.Name, out var id))
                        instances[id] = prop.Value;
                }
            }

            // Parse vault items (profileInventory)
            if (profileResponse.TryGetProperty("profileInventory", out var vault) &&
                vault.TryGetProperty("data", out var vaultData) &&
                vaultData.TryGetProperty("items", out var vaultItems))
            {
                Console.WriteLine($"[InventoryService] Processing vault items");
                await ProcessItemsAsync(vaultItems, instances, "vault");
            }

            // Parse character inventories
            if (profileResponse.TryGetProperty("characterInventories", out var charInv) &&
                charInv.TryGetProperty("data", out var charInvData))
            {
                foreach (var charProp in charInvData.EnumerateObject())
                {
                    var charId = charProp.Name;
                    if (long.TryParse(charId, out var cid))
                        CharacterIds.Add(cid);
                    
                    if (charProp.Value.TryGetProperty("items", out var items))
                    {
                        Console.WriteLine($"[InventoryService] Processing character {charId} inventory");
                        await ProcessItemsAsync(items, instances, charId);
                    }
                }
            }

            // Parse equipped items
            if (profileResponse.TryGetProperty("characterEquipment", out var charEquip) &&
                charEquip.TryGetProperty("data", out var charEquipData))
            {
                foreach (var charProp in charEquipData.EnumerateObject())
                {
                    var charId = charProp.Name;
                    if (charProp.Value.TryGetProperty("items", out var items))
                    {
                        Console.WriteLine($"[InventoryService] Processing character {charId} equipment");
                        await ProcessItemsAsync(items, instances, charId, isEquipped: true);
                    }
                }
            }

            Console.WriteLine($"[InventoryService] Total items loaded: {AllItems.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryService] Exception: {ex.Message}");
            LoadMockData();
        }
    }

    private async Task ProcessItemsAsync(JsonElement items, Dictionary<long, JsonElement> instances, string location, bool isEquipped = false)
    {
        foreach (var item in items.EnumerateArray())
        {
            try
            {
                if (!item.TryGetProperty("itemHash", out var hashProp)) continue;
                var itemHash = hashProp.GetUInt32();
                
                var instanceId = item.TryGetProperty("itemInstanceId", out var instIdProp) 
                    ? long.Parse(instIdProp.GetString() ?? "0") 
                    : 0;

                // Get power level from instances
                var powerLevel = 0;
                if (instanceId > 0 && instances.TryGetValue(instanceId, out var instData))
                {
                    if (instData.TryGetProperty("primaryStat", out var ps) && 
                        ps.TryGetProperty("value", out var psVal))
                    {
                        powerLevel = psVal.GetInt32();
                    }
                }

                // Get locked state
                var state = item.TryGetProperty("state", out var stateProp) ? stateProp.GetInt32() : 0;
                var isLocked = (state & 1) != 0; // ItemState.Locked = 1

                // Get definition from manifest
                var definition = await GetItemDefinitionAsync(itemHash);
                if (definition == null) continue;

                AllItems.Add(new InventoryItem
                {
                    ItemHash = itemHash,
                    InstanceId = instanceId,
                    Name = definition.Name ?? "Unknown",
                    Icon = definition.Icon ?? "",
                    ItemType = definition.ItemType ?? "",
                    TierType = definition.TierType ?? "",
                    PowerLevel = powerLevel,
                    IsExotic = definition.IsExotic,
                    IsArtifice = definition.IsArtifice,
                    IsTier5 = definition.IsTier5,
                    IsLocked = isLocked,
                    Location = location,
                    IsEquipped = isEquipped
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[InventoryService] Error processing item: {ex.Message}");
            }
        }
    }

    private async Task<ItemDefinition?> GetItemDefinitionAsync(uint hash)
    {
        try
        {
            var json = await _manifestDatabase.GetItemName(hash);
            if (string.IsNullOrEmpty(json)) return null;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var name = "";
            var icon = "";
            if (root.TryGetProperty("displayProperties", out var dp))
            {
                name = dp.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                icon = dp.TryGetProperty("icon", out var i) ? i.GetString() ?? "" : "";
            }

            var itemType = root.TryGetProperty("itemTypeDisplayName", out var itd) ? itd.GetString() ?? "" : "";
            
            var tierType = 0;
            if (root.TryGetProperty("inventory", out var inv) && inv.TryGetProperty("tierType", out var tt))
                tierType = tt.GetInt32();

            return new ItemDefinition
            {
                Name = name,
                Icon = icon,
                ItemType = itemType,
                TierType = tierType == 6 ? "Exotic" : tierType == 5 ? "Legendary" : "Other",
                IsExotic = tierType == 6,
                IsArtifice = false,
                IsTier5 = false
            };
        }
        catch
        {
            return null;
        }
    }

    private void LoadMockData()
    {
        Console.WriteLine("[InventoryService] Loading MOCK DATA");
        AllItems.Clear();

        var mockItems = new List<InventoryItem>
        {
            new() { Name = "Gjallarhorn", PowerLevel = 1810, ItemType = "Rocket Launcher", IsExotic = true, ItemHash = 12345, InstanceId = 1, Location = "vault" },
            new() { Name = "Fatebringer", PowerLevel = 1800, ItemType = "Hand Cannon", IsExotic = false, ItemHash = 67890, InstanceId = 2, Location = "vault" },
            new() { Name = "Celestial Nighthawk", PowerLevel = 1805, ItemType = "Helmet", IsExotic = true, ItemHash = 11111, InstanceId = 3, Location = "vault",
                Stats = new Dictionary<uint, int> { { StatHashes.Mobility, 24 }, { StatHashes.Resilience, 16 }, { StatHashes.Recovery, 18 },
                    { StatHashes.Discipline, 20 }, { StatHashes.Intellect, 22 }, { StatHashes.Strength, 10 } } },
            new() { Name = "Ikelos_SMG_v1.0.3", PowerLevel = 1800, ItemType = "Submachine Gun", IsExotic = false, ItemHash = 22222, InstanceId = 4, Location = "vault" },
            new() { Name = "Apex Predator", PowerLevel = 1810, ItemType = "Rocket Launcher", IsExotic = false, ItemHash = 33333, InstanceId = 5, Location = "vault" },
            new() { Name = "Lorely Splendor", PowerLevel = 1805, ItemType = "Helmet", IsExotic = true, ItemHash = 44444, InstanceId = 6, Location = "char1",
                Stats = new Dictionary<uint, int> { { StatHashes.Mobility, 10 }, { StatHashes.Resilience, 30 }, { StatHashes.Recovery, 6 },
                    { StatHashes.Discipline, 16 }, { StatHashes.Intellect, 6 }, { StatHashes.Strength, 20 } } },
            new() { Name = "Cuirass of the Falling Star", PowerLevel = 1808, ItemType = "Chest Armor", IsExotic = true, ItemHash = 55555, InstanceId = 7, Location = "char1", IsArtifice = true },
            new() { Name = "Artifice Gauntlets", PowerLevel = 1800, ItemType = "Gauntlets", IsExotic = false, ItemHash = 66666, InstanceId = 8, Location = "vault", IsArtifice = true, IsTier5 = true },
        };

        foreach (var item in mockItems)
            AllItems.Add(item);
    }

    private record ItemDefinition
    {
        public string? Name { get; init; }
        public string? Icon { get; init; }
        public string? ItemType { get; init; }
        public string? TierType { get; init; }
        public bool IsExotic { get; init; }
        public bool IsArtifice { get; init; }
        public bool IsTier5 { get; init; }
    }
}
