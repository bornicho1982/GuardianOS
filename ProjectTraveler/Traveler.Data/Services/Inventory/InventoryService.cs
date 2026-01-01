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
    
    /// <summary>
    /// List of all characters for the current account.
    /// </summary>
    public ObservableCollection<CharacterInfo> Characters { get; } = new();
    
    /// <summary>
    /// Event fired when inventory refresh is complete.
    /// </summary>
    /// <summary>
    /// Event fired when inventory refresh is complete.
    /// </summary>
    public event Action? InventoryRefreshed;

    public async Task<List<CharacterInfo>> GetCurrentUserCharactersAsync()
    {
        Console.WriteLine("[InventoryService] GetCurrentUserCharactersAsync called");
        
        if (!_authService.IsAuthenticated)
        {
            Console.WriteLine("[InventoryService] Not authenticated");
            return new List<CharacterInfo>();
        }

        try
        {
            var membershipType = (int)_authService.DestinyMembershipType;
            var membershipId = _authService.DestinyMembershipId;
            // Components: 200 (Characters), 202 (CharacterProgressions)
            var url = $"{BaseUrl}/Destiny2/{membershipType}/Profile/{membershipId}/?components=200,202";

            Console.WriteLine($"[InventoryService] Fetching Characters: {url}");
            
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.AccessToken);

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[InventoryService] API Error: {response.StatusCode}");
                return new List<CharacterInfo>();
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            if (!root.TryGetProperty("Response", out var profileResponse))
                return new List<CharacterInfo>();

            var characters = new List<CharacterInfo>();

            // Parse Characters (Component 200)
            if (profileResponse.TryGetProperty("characters", out var charComp) &&
                charComp.TryGetProperty("data", out var charData))
            {
                // Parse Progressions (Component 202) for Season Rank
                JsonElement? progressionsData = null;
                if (profileResponse.TryGetProperty("characterProgressions", out var progComp) &&
                    progComp.TryGetProperty("data", out var progData))
                {
                    progressionsData = progData;
                }

                foreach (var prop in charData.EnumerateObject())
                {
                    var charId = prop.Name;
                    var character = prop.Value;
                    
                    // Basic Info
                    var classType = character.TryGetProperty("classType", out var ct) ? ct.GetInt32() : 0;
                    var className = classType switch { 0 => "Titan", 1 => "Hunter", 2 => "Warlock", _ => "Guardian" };
                    var raceType = character.TryGetProperty("raceType", out var rt) ? rt.GetInt32() : 0;
                    var raceName = raceType switch { 0 => "Human", 1 => "Awoken", 2 => "Exo", _ => "Unknown" };
                    var light = character.TryGetProperty("light", out var l) ? l.GetInt32() : 0;
                    var emblemPath = character.TryGetProperty("emblemPath", out var ep) ? ep.GetString() : "";
                    var emblemBackgroundPath = character.TryGetProperty("emblemBackgroundPath", out var ebp) ? ebp.GetString() : "";

                    // Season Rank Calculation (from Progressions)
                    var seasonRank = 0;
                    var seasonProgress = 0.0;
                    
                    if (progressionsData.HasValue && progressionsData.Value.TryGetProperty(charId, out var charProgs) &&
                        charProgs.TryGetProperty("progressions", out var progsDict))
                    {
                        // Current Season Hash (approximate/dynamic needs manifest, but common hash is 1628407317 for season rank text, 
                        // or we look for the active season progression. For now, we'll try a known hash or iterate)
                        // TODO: Use Manifest to find current season hash. 
                        // For prototype, we might skip precise hash lookup or use a reliable one if available.
                        // However, usually the 'season' progression is prominent. 
                        // Let's check for 'overview' progression or similar if we can't find specific hash.
                    }

                    var charInfo = new CharacterInfo
                    {
                        CharacterId = charId,
                        ClassName = className,
                        RaceName = raceName,
                        LightLevel = light,
                        EmblemPath = emblemPath ?? "",
                        EmblemBackgroundPath = emblemBackgroundPath ?? "",
                        SeasonRank = seasonRank, 
                        SeasonProgressPercent = seasonProgress
                    };
                    
                    characters.Add(charInfo);
                }
            }

            // Update local cache
            Characters.Clear();
            foreach (var c in characters) Characters.Add(c);

            return characters;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryService] Error getting characters: {ex.Message}");
            return new List<CharacterInfo>();
        }
    }

    /// <summary>
    /// Gets the current vault item count from the profile.
    /// Component 102 = ProfileInventories (includes vault)
    /// </summary>
    public async Task<(int used, int total)> GetVaultStatusAsync()
    {
        const int VAULT_BUCKET_HASH = 138197802; // General Vault bucket
        const int VAULT_MAX_CAPACITY = 700; // Updated in The Final Shape (June 2024)

        if (!_authService.IsAuthenticated)
            return (0, VAULT_MAX_CAPACITY);

        try
        {
            var membershipType = (int)_authService.DestinyMembershipType;
            var membershipId = _authService.DestinyMembershipId;
            var url = $"{BaseUrl}/Destiny2/{membershipType}/Profile/{membershipId}/?components=102";

            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.AccessToken);

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return (0, VAULT_MAX_CAPACITY);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            
            if (doc.RootElement.TryGetProperty("Response", out var resp) &&
                resp.TryGetProperty("profileInventory", out var inventory) &&
                inventory.TryGetProperty("data", out var data) &&
                data.TryGetProperty("items", out var items))
            {
                var count = items.GetArrayLength();
                Console.WriteLine($"[InventoryService] Vault count: {count}/{VAULT_MAX_CAPACITY}");
                return (count, VAULT_MAX_CAPACITY);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryService] GetVaultStatus error: {ex.Message}");
        }

        return (0, VAULT_MAX_CAPACITY);
    }

    /// <summary>
    /// Gets profile currencies (Glimmer, Legendary Shards, Bright Dust, Silver).
    /// Component 100 = Profiles (includes profileCurrencies)
    /// </summary>
    public async Task<Dictionary<uint, long>> GetCurrenciesAsync()
    {
        var currencies = new Dictionary<uint, long>();
        
        // Common currency hashes
        const uint GLIMMER = 3159615086;
        const uint LEGENDARY_SHARDS = 1022552290;
        const uint BRIGHT_DUST = 2817410917;
        const uint SILVER = 3147280338;

        if (!_authService.IsAuthenticated)
            return currencies;

        try
        {
            var membershipType = (int)_authService.DestinyMembershipType;
            var membershipId = _authService.DestinyMembershipId;
            // Component 103 = ProfileCurrencies
            var url = $"{BaseUrl}/Destiny2/{membershipType}/Profile/{membershipId}/?components=103";

            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.AccessToken);

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[InventoryService] GetCurrencies failed: {response.StatusCode}");
                return currencies;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("Response", out var resp) &&
                resp.TryGetProperty("profileCurrencies", out var profileCurrencies) &&
                profileCurrencies.TryGetProperty("data", out var data) &&
                data.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("itemHash", out var hashProp) &&
                        item.TryGetProperty("quantity", out var quantityProp))
                    {
                        var hash = hashProp.GetUInt32();
                        var quantity = quantityProp.GetInt64();
                        currencies[hash] = quantity;
                    }
                }
                
                Console.WriteLine($"[InventoryService] Currencies loaded: {currencies.Count} types");
                if (currencies.ContainsKey(GLIMMER))
                    Console.WriteLine($"  Glimmer: {currencies[GLIMMER]:N0}");
                if (currencies.ContainsKey(LEGENDARY_SHARDS))
                    Console.WriteLine($"  Legendary Shards: {currencies[LEGENDARY_SHARDS]:N0}");
                if (currencies.ContainsKey(BRIGHT_DUST))
                    Console.WriteLine($"  Bright Dust: {currencies[BRIGHT_DUST]:N0}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryService] GetCurrencies error: {ex.Message}");
        }

        return currencies;
    }

    /// <summary>
    /// Counts items in the Postmaster for a character.
    /// Component 201 = CharacterInventories (bucket 215593132 = Postmaster)
    /// </summary>
    public async Task<int> GetPostmasterCountAsync(string characterId)
    {
        const uint POSTMASTER_BUCKET = 215593132;

        if (!_authService.IsAuthenticated || string.IsNullOrEmpty(characterId))
            return 0;

        try
        {
            var membershipType = (int)_authService.DestinyMembershipType;
            var membershipId = _authService.DestinyMembershipId;
            var url = $"{BaseUrl}/Destiny2/{membershipType}/Profile/{membershipId}/?components=201";

            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.AccessToken);

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return 0;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("Response", out var resp) &&
                resp.TryGetProperty("characterInventories", out var charInvs) &&
                charInvs.TryGetProperty("data", out var data) &&
                data.TryGetProperty(characterId, out var charInv) &&
                charInv.TryGetProperty("items", out var items))
            {
                int postmasterCount = 0;
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("bucketHash", out var bucket) && 
                        bucket.GetUInt32() == POSTMASTER_BUCKET)
                    {
                        postmasterCount++;
                    }
                }
                Console.WriteLine($"[InventoryService] Postmaster count for {characterId}: {postmasterCount}");
                return postmasterCount;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryService] GetPostmasterCount error: {ex.Message}");
        }

        return 0;
    }
    
    /// <summary>
    /// Gets Crucible (PvP) historical statistics for the authenticated user.
    /// Uses GetHistoricalStats endpoint with mode=5 (AllPublicPvP).
    /// </summary>
    public async Task<CrucibleStats> GetCrucibleStatsAsync()
    {
        var stats = new CrucibleStats();
        
        if (!_authService.IsAuthenticated)
            return stats;

        try
        {
            var membershipType = (int)_authService.DestinyMembershipType;
            var membershipId = _authService.DestinyMembershipId;
            
            // GetHistoricalStats endpoint with characterId=0 for account aggregate
            // mode=5 = AllPvP, groups=General for basic stats
            var url = $"{BaseUrl}/Destiny2/{membershipType}/Account/{membershipId}/Character/0/Stats/?modes=5&groups=General&periodType=AllTime";

            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.AccessToken);

            Console.WriteLine($"[InventoryService] Fetching Crucible stats: {url}");
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[InventoryService] GetCrucibleStats failed: {response.StatusCode}");
                return stats;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            // Navigate to Response.allPvP.allTime for aggregate stats
            if (doc.RootElement.TryGetProperty("Response", out var resp) &&
                resp.TryGetProperty("allPvP", out var allPvP) &&
                allPvP.TryGetProperty("allTime", out var allTime))
            {
                // Extract individual stats
                if (allTime.TryGetProperty("kills", out var kills) && 
                    kills.TryGetProperty("basic", out var killsBasic))
                    stats.Kills = killsBasic.TryGetProperty("value", out var kv) ? (int)kv.GetDouble() : 0;
                    
                if (allTime.TryGetProperty("deaths", out var deaths) && 
                    deaths.TryGetProperty("basic", out var deathsBasic))
                    stats.Deaths = deathsBasic.TryGetProperty("value", out var dv) ? (int)dv.GetDouble() : 0;
                    
                if (allTime.TryGetProperty("assists", out var assists) && 
                    assists.TryGetProperty("basic", out var assistsBasic))
                    stats.Assists = assistsBasic.TryGetProperty("value", out var av) ? (int)av.GetDouble() : 0;
                    
                if (allTime.TryGetProperty("killsDeathsRatio", out var kdr) && 
                    kdr.TryGetProperty("basic", out var kdrBasic))
                    stats.KillDeathRatio = kdrBasic.TryGetProperty("value", out var kdrv) ? kdrv.GetDouble() : 0;
                    
                if (allTime.TryGetProperty("efficiency", out var eff) && 
                    eff.TryGetProperty("basic", out var effBasic))
                    stats.Efficiency = effBasic.TryGetProperty("value", out var effv) ? effv.GetDouble() : 0;
                    
                if (allTime.TryGetProperty("activitiesEntered", out var matches) && 
                    matches.TryGetProperty("basic", out var matchesBasic))
                    stats.MatchesPlayed = matchesBasic.TryGetProperty("value", out var mv) ? (int)mv.GetDouble() : 0;
                    
                if (allTime.TryGetProperty("activitiesWon", out var wins) && 
                    wins.TryGetProperty("basic", out var winsBasic))
                    stats.Wins = winsBasic.TryGetProperty("value", out var wv) ? (int)wv.GetDouble() : 0;
                    
                if (allTime.TryGetProperty("secondsPlayed", out var time) && 
                    time.TryGetProperty("basic", out var timeBasic))
                    stats.SecondsPlayed = timeBasic.TryGetProperty("value", out var tv) ? (long)tv.GetDouble() : 0;
                    
                if (allTime.TryGetProperty("longestKillSpree", out var streak) && 
                    streak.TryGetProperty("basic", out var streakBasic))
                    stats.BestKillStreak = streakBasic.TryGetProperty("value", out var sv) ? (int)sv.GetDouble() : 0;
                    
                if (allTime.TryGetProperty("precisionKills", out var precision) && 
                    precision.TryGetProperty("basic", out var precisionBasic))
                    stats.PrecisionKills = precisionBasic.TryGetProperty("value", out var pv) ? (int)pv.GetDouble() : 0;
                
                // Calculate win rate
                stats.Losses = stats.MatchesPlayed - stats.Wins;
                stats.WinRate = stats.MatchesPlayed > 0 ? (double)stats.Wins / stats.MatchesPlayed * 100 : 0;
                
                Console.WriteLine($"[InventoryService] Crucible Stats loaded:");
                Console.WriteLine($"  K/D: {stats.KillDeathRatio:F2}, Efficiency: {stats.Efficiency:F2}");
                Console.WriteLine($"  Win Rate: {stats.WinRate:F1}%, Matches: {stats.MatchesPlayed:N0}");
                Console.WriteLine($"  Time Played: {stats.TimePlayed}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryService] GetCrucibleStats error: {ex.Message}");
        }

        return stats;
    }
    
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

    // ===== TRANSFER LOGIC (Bungie API) =====
    
    /// <summary>
    /// Transfers an item to Vault or to a specific character.
    /// Note: Bungie API only supports Character ↔ Vault transfers.
    /// Character A → Character B requires: A → Vault → B (two API calls).
    /// </summary>
    public async Task TransferItemAsync(InventoryItem item, string targetCharacterId, bool toVault, int stackSize = 1)
    {
        Console.WriteLine($"[Transfer] Request: {item.Name} ({item.InstanceId}) → {(toVault ? "Vault" : $"Char:{targetCharacterId}")}");
        
        if (!_authService.IsAuthenticated)
        {
            Console.WriteLine("[Transfer] Error: Not authenticated");
            return;
        }

        try
        {
            var currentLocation = item.Location;
            
            // CASE 1: Item is on a character, moving TO Vault
            if (toVault && currentLocation != "vault")
            {
                await ExecuteTransferToVault(item, currentLocation, stackSize);
                item.Location = "vault";
                Console.WriteLine("[Transfer] Success: Item moved to Vault");
            }
            // CASE 2: Item is in Vault, moving TO a character
            else if (!toVault && currentLocation == "vault")
            {
                await ExecuteTransferFromVault(item, targetCharacterId, stackSize);
                item.Location = targetCharacterId;
                Console.WriteLine($"[Transfer] Success: Item moved to Character {targetCharacterId}");
            }
            // CASE 3: Character A → Character B (requires routing through Vault)
            else if (!toVault && currentLocation != "vault" && currentLocation != targetCharacterId)
            {
                Console.WriteLine($"[Transfer] Routing: Char {currentLocation} → Vault → Char {targetCharacterId}");
                
                // Step 1: Move to Vault
                await ExecuteTransferToVault(item, currentLocation, stackSize);
                item.Location = "vault";
                
                // Small delay to avoid rate limiting
                await Task.Delay(100);
                
                // Step 2: Move from Vault to target character
                await ExecuteTransferFromVault(item, targetCharacterId, stackSize);
                item.Location = targetCharacterId;
                
                Console.WriteLine("[Transfer] Success: Routed through Vault");
            }
            else
            {
                Console.WriteLine("[Transfer] No action needed (item already at destination)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Transfer] Error: {ex.Message}");
            throw;
        }
    }

    private async Task ExecuteTransferToVault(InventoryItem item, string sourceCharacterId, int stackSize)
    {
        var requestBody = new
        {
            itemReferenceHash = item.ItemHash,
            stackSize = stackSize,
            transferToVault = true,
            itemId = item.InstanceId.ToString(),
            characterId = sourceCharacterId,
            membershipType = MembershipType
        };

        await CallTransferItemEndpoint(requestBody);
    }

    private async Task ExecuteTransferFromVault(InventoryItem item, string targetCharacterId, int stackSize)
    {
        var requestBody = new
        {
            itemReferenceHash = item.ItemHash,
            stackSize = stackSize,
            transferToVault = false,
            itemId = item.InstanceId.ToString(),
            characterId = targetCharacterId,
            membershipType = MembershipType
        };

        await CallTransferItemEndpoint(requestBody);
    }

    private async Task CallTransferItemEndpoint(object requestBody)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        
        // Add OAuth token
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.AccessToken);
        
        var response = await _httpClient.PostAsync($"{BaseUrl}/Destiny2/Actions/Items/TransferItem/", content);
        var responseBody = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[Transfer] API Error: {response.StatusCode} - {responseBody}");
            throw new Exception($"Transfer failed: {response.StatusCode}");
        }
        
        Console.WriteLine($"[Transfer] API Response: {response.StatusCode}");
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
        Console.WriteLine($"[InventoryService] Auth state: IsAuthenticated={_authService.IsAuthenticated}, MembershipId={_authService.DestinyMembershipId}");
        
        if (_authService.DestinyMembershipId == 0 || !_authService.IsAuthenticated)
        {
            Console.WriteLine("[InventoryService] Not authenticated - cannot refresh inventory");
            AllItems.Clear();
            Characters.Clear();
            CharacterIds.Clear();
            InventoryRefreshed?.Invoke(); // Notify UI of the empty state
            return; // Graceful return instead of throw
        }

        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.AccessToken);

            // Fetch profile with required components
            var membershipType = (int)_authService.DestinyMembershipType;
            var membershipId = _authService.DestinyMembershipId;
            var components = "100,102,200,201,205,300,304,305"; // Profiles, Inventories, Characters, Equipment, Instances, Stats, Sockets
            var url = $"{BaseUrl}/Destiny2/{membershipType}/Profile/{membershipId}/?components={components}";
            
            Console.WriteLine($"[InventoryService] Fetching: {url}");
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[InventoryService] API Error: {response.StatusCode}");
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[InventoryService] Error response: {errorContent}");
                throw new HttpRequestException($"API Error: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Response", out var profileResponse))
            {
                Console.WriteLine("[InventoryService] No Response in API data");
                throw new InvalidOperationException("Invalid API response: No 'Response' property found");
            }

            AllItems.Clear();
            CharacterIds.Clear();
            Characters.Clear();
            
            // Parse characters (class, race, stats, power level)
            if (profileResponse.TryGetProperty("characters", out var characters) &&
                characters.TryGetProperty("data", out var charData))
            {
                ParseCharacters(charData);
            }

            // Parse item instances (for power level and damage type)
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

            // Parse item stats (Component 304)
            var itemStats = new Dictionary<long, Dictionary<uint, int>>();
            if (itemComp.ValueKind != JsonValueKind.Undefined && 
                itemComp.TryGetProperty("stats", out var statsComp) &&
                statsComp.TryGetProperty("data", out var statsData))
            {
                foreach (var prop in statsData.EnumerateObject())
                {
                    if (long.TryParse(prop.Name, out var id))
                    {
                        var statsDict = new Dictionary<uint, int>();
                        if (prop.Value.TryGetProperty("stats", out var statsObj))
                        {
                            foreach (var statProp in statsObj.EnumerateObject())
                            {
                                if (uint.TryParse(statProp.Name, out var statHash) && 
                                    statProp.Value.TryGetProperty("value", out var statVal))
                                {
                                    statsDict[statHash] = statVal.GetInt32();
                                }
                            }
                        }
                        itemStats[id] = statsDict;
                    }
                }
            }

            // Parse item sockets (Component 305)
            var itemSockets = new Dictionary<long, List<uint>>();
            if (itemComp.ValueKind != JsonValueKind.Undefined && 
                itemComp.TryGetProperty("sockets", out var socketsComp) &&
                socketsComp.TryGetProperty("data", out var socketsData))
            {
                foreach (var prop in socketsData.EnumerateObject())
                {
                    if (long.TryParse(prop.Name, out var id))
                    {
                        var plugs = new List<uint>();
                        if (prop.Value.TryGetProperty("sockets", out var socketList))
                        {
                            foreach (var socket in socketList.EnumerateArray())
                            {
                                if (socket.TryGetProperty("plugHash", out var plugHash))
                                {
                                    plugs.Add(plugHash.GetUInt32());
                                }
                            }
                        }
                        itemSockets[id] = plugs;
                    }
                }
            }

            // Parse vault items (profileInventory)
            if (profileResponse.TryGetProperty("profileInventory", out var vault) &&
                vault.TryGetProperty("data", out var vaultData) &&
                vaultData.TryGetProperty("items", out var vaultItems))
            {
                Console.WriteLine($"[InventoryService] Processing vault items");
                await ProcessItemsAsync(vaultItems, instances, itemStats, itemSockets, "vault");
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
                        await ProcessItemsAsync(items, instances, itemStats, itemSockets, charId);
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
                        await ProcessItemsAsync(items, instances, itemStats, itemSockets, charId, isEquipped: true);
                    }
                }
            }

            Console.WriteLine($"[InventoryService] Total items loaded: {AllItems.Count}");
            InventoryRefreshed?.Invoke();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryService] Exception: {ex.Message}");
            Console.WriteLine($"[InventoryService] Stack: {ex.StackTrace}");
            throw; // Re-throw to let caller handle
        }
    }

    private void ParseCharacters(JsonElement charData)
    {
        foreach (var prop in charData.EnumerateObject())
        {
            var character = prop.Value;
            var charIdStr = prop.Name;
            
            if (!long.TryParse(charIdStr, out var charId)) continue;

            // Class Type (0: Titan, 1: Hunter, 2: Warlock)
            var classType = character.TryGetProperty("classType", out var ct) ? ct.GetInt32() : 0;
            var className = classType switch { 0 => "Titan", 1 => "Hunter", 2 => "Warlock", _ => "Guardian" };

            // Race (0: Human, 1: Awoken, 2: Exo)
            var raceType = character.TryGetProperty("raceType", out var rt) ? rt.GetInt32() : 0;
            var raceName = raceType switch { 0 => "Human", 1 => "Awoken", 2 => "Exo", _ => "Unknown" };

            // Gender (0: Male, 1: Female)
            var genderType = character.TryGetProperty("genderType", out var gt) ? gt.GetInt32() : 0;
            // var genderName = genderType == 0 ? "Male" : "Female";

            // Light Level
            var light = character.TryGetProperty("light", out var l) ? l.GetInt32() : 0;

            // Stats
            var mobility = 0;
            var resilience = 0;
            var recovery = 0;
            var discipline = 0;
            var intellect = 0;
            var strength = 0;

            if (character.TryGetProperty("stats", out var stats))
            {
                mobility = stats.TryGetProperty(StatHashes.Mobility.ToString(), out var s1) ? s1.GetInt32() : 0;
                resilience = stats.TryGetProperty(StatHashes.Resilience.ToString(), out var s2) ? s2.GetInt32() : 0;
                recovery = stats.TryGetProperty(StatHashes.Recovery.ToString(), out var s3) ? s3.GetInt32() : 0;
                discipline = stats.TryGetProperty(StatHashes.Discipline.ToString(), out var s4) ? s4.GetInt32() : 0;
                intellect = stats.TryGetProperty(StatHashes.Intellect.ToString(), out var s5) ? s5.GetInt32() : 0;
                strength = stats.TryGetProperty(StatHashes.Strength.ToString(), out var s6) ? s6.GetInt32() : 0;
            }

            // Emblem
            var emblemPath = character.TryGetProperty("emblemPath", out var ep) ? ep.GetString() : "";
            var emblemBackgroundPath = character.TryGetProperty("emblemBackgroundPath", out var ebp) ? ebp.GetString() : "";

            var charInfo = new CharacterInfo
            {
                CharacterId = charId.ToString(),
                ClassName = className,
                RaceName = raceName,
                LightLevel = light,
                EmblemPath = emblemPath ?? "",
                EmblemBackgroundPath = emblemBackgroundPath ?? "",
                Mobility = mobility,
                Resilience = resilience,
                Recovery = recovery,
                Discipline = discipline,
                Intellect = intellect,
                Strength = strength
            };

            Characters.Add(charInfo);
        }
    }

    private async Task ProcessItemsAsync(
        JsonElement items, 
        Dictionary<long, JsonElement> instances, 
        Dictionary<long, Dictionary<uint, int>> itemStats,
        Dictionary<long, List<uint>> itemSockets,
        string location, 
        bool isEquipped = false)
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
                
                var bucketHash = item.TryGetProperty("bucketHash", out var bucketProp) 
                    ? bucketProp.GetUInt32() 
                    : 0;

                var powerLevel = 0;
                uint damageTypeHash = 0;
                int damageType = 0;
                int energyCapacity = 0;
                
                if (instanceId > 0 && instances.TryGetValue(instanceId, out var instData))
                {
                    if (instData.TryGetProperty("primaryStat", out var ps) && 
                        ps.TryGetProperty("value", out var psVal))
                    {
                        powerLevel = psVal.GetInt32();
                    }
                    if (instData.TryGetProperty("damageTypeHash", out var dmgHashProp))
                    {
                        damageTypeHash = dmgHashProp.GetUInt32();
                    }
                    if (instData.TryGetProperty("damageType", out var dmgTypeProp))
                    {
                        damageType = dmgTypeProp.GetInt32();
                    }
                    if (instData.TryGetProperty("energy", out var energyComp) && 
                        energyComp.TryGetProperty("energyCapacity", out var ec))
                    {
                        energyCapacity = ec.GetInt32();
                    }
                }
                
                if (damageTypeHash == 0 && item.TryGetProperty("damageTypeHash", out var itemDmgHash))
                {
                     damageTypeHash = itemDmgHash.GetUInt32();
                }

                var stats = new Dictionary<uint, int>();
                if (instanceId > 0 && itemStats.TryGetValue(instanceId, out var s))
                {
                    stats = s;
                }

                // Sockets parsing (Mods, Archetypes, Masterwork)
                var socketIcons = new List<string>();
                var abilityIcons = new List<string>();
                string archetype = "", archetypeIcon = "";
                int masterworkLevel = 0;

                var definition = await GetItemDefinitionFromManifestAsync(itemHash);

                if (isEquipped && instanceId > 0 && itemSockets.TryGetValue(instanceId, out var plugs))
                {
                    // 1. First Pass: Archetype & Masterwork (Inspect all sockets)
                    foreach (var plugHash in plugs)
                    {
                        var plugDef = await GetItemDefinitionFromManifestAsync(plugHash);
                        if (plugDef == null) continue;

                        // Archetype
                        if (plugDef.ItemType == "Intrinsic Trait" || plugDef.ItemType == "Archetype")
                        {
                            archetype = plugDef.Name ?? "";
                            archetypeIcon = plugDef.Icon;
                        }

                        // Masterwork Detection
                        if (plugDef.UiPlugLabel?.ToLower() == "masterwork") masterworkLevel = 10;
                        else if (plugDef.Name?.StartsWith("Tier ") == true && int.TryParse(plugDef.Name.Replace("Tier ", ""), out var t)) masterworkLevel = t;
                    }

                    // 2. Second Pass: Weapon Perks via SocketCategories (DIM Logic)
                    // Weapon Perks Category Hash = 4241087561
                    // User Request Logic implementation
                    if (definition?.SocketCategories != null)
                    {
                        var perksCategory = definition.SocketCategories.FirstOrDefault(c => c.SocketCategoryHash == 4241087561);
                        
                        if (perksCategory != null)
                        {
                            foreach (var index in perksCategory.SocketIndexes)
                            {
                                if (index >= 0 && index < plugs.Count)
                                {
                                    var plugHash = plugs[index];
                                    var plugDef = await GetItemDefinitionFromManifestAsync(plugHash);
                                    
                                    if (!string.IsNullOrEmpty(plugDef?.Icon))
                                    {
                                        var iconUrl = plugDef.Icon.StartsWith("/") ? $"https://www.bungie.net{plugDef.Icon}" : plugDef.Icon;
                                        socketIcons.Add(iconUrl);
                                    }
                                }
                            }
                        }
                    }
                }

                var state = item.TryGetProperty("state", out var stateProp) ? stateProp.GetInt32() : 0;
                var isLocked = (state & 1) != 0;
                
                // Phase 6 & 7: DIM Visual Properties & Damage Type (From Manifest)
                var damageTypeIcon = "";
                if (damageTypeHash > 0)
                {
                    damageTypeIcon = await _manifestDatabase.GetDamageTypeIconAsync(damageTypeHash);
                }
                else if (definition?.DefaultDamageTypeHash > 0)
                {
                     damageTypeIcon = await _manifestDatabase.GetDamageTypeIconAsync((uint)definition.DefaultDamageTypeHash);
                }
                
                if (!string.IsNullOrEmpty(damageTypeIcon) && damageTypeIcon.StartsWith("/"))
                {
                    damageTypeIcon = $"https://www.bungie.net{damageTypeIcon}";
                }

                // DIM Tier Pips Logic (Approximate based on Rarity)
                string tierPipsUrl = "";
                if (definition?.TierType == 5) // Legendary
                    tierPipsUrl = "/common/destiny2_content/icons/inventory-item-tier3.png"; 
                else if (definition?.TierType == 6) // Exotic
                    tierPipsUrl = "/common/destiny2_content/icons/inventory-item-tier3.png"; 

                AllItems.Add(new InventoryItem
                {
                    ItemHash = itemHash,
                    InstanceId = instanceId,
                    Name = definition?.Name ?? $"Item #{itemHash}",
                    Description = definition?.Description ?? "",
                    Icon = definition?.Icon ?? "",
                    ItemType = definition?.ItemType ?? "Unknown",
                    TierType = GetTierName(definition?.TierType ?? 0),
                    PowerLevel = powerLevel,
                    PrimaryStatValue = powerLevel,
                    IsExotic = definition?.IsExotic ?? false,
                    IsArtifice = definition?.IsArtifice ?? false,
                    IsTier5 = definition?.IsTier5 ?? false,
                    IsLocked = isLocked,
                    Location = location,
                    IsEquipped = isEquipped,
                    BucketHash = bucketHash,
                    DamageTypeHash = damageTypeHash,
                    Stats = stats,
                    Archetype = archetype,
                    ArchetypeIcon = archetypeIcon,
                    SocketIcons = socketIcons,
                    AbilityIcons = abilityIcons,
                    // Phase 5: New Properties
                    WatermarkIconUrl = definition?.IconWatermark ?? "",
                    AmmoType = definition?.AmmoType ?? 0,
                    MasterworkLevel = masterworkLevel,
                    
                    // Phase 6
                    EnergyCapacity = energyCapacity,
                    DamageType = damageType,
                    DamageTypeIcon = damageTypeIcon ?? "", 
                    
                    // Phase 7: DIM Visual Properties
                    IsFeatured = definition?.IsFeaturedItem ?? false,
                    FeaturedFlagUrl = (definition?.IsFeaturedItem == true) ? "/img/destiny_content/items/featured-flag.png" : "",
                    SeasonIconUrl = definition?.SeasonIconUrl ?? "",
                    MasterworkGlowUrl = masterworkLevel >= 10 ? "/img/destiny_content/items/masterwork-overlay.png" : "",
                    TierPipsUrl = tierPipsUrl
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[InventoryService] Error processing item: {ex.Message}");
            }
        }
    }


    private string GetBucketName(uint bucketHash) => bucketHash switch
    {
        1498876634 => "Kinetic Weapons",
        2465295065 => "Energy Weapons",
        953998645 => "Power Weapons",
        3448274439 => "Helmets",
        3551918588 => "Gauntlets",
        14239492 => "Chest Armor",
        20886954 => "Leg Armor",
        1585787867 => "Class Armor",
        _ => "General"
    };

    private async Task<ItemDefinition?> GetItemDefinitionFromManifestAsync(uint hash)
    {
        try
        {
            // Use the new ManifestDatabase API
            var def = await _manifestDatabase.GetItemDefinitionAsync(hash);
            if (def == null) return null;

            return new ItemDefinition
            {
                Name = def.Name,
                Description = def.Description,
                Icon = def.Icon ?? "",
                ItemType = def.ItemType,
                TierType = def.TierType,
                IsExotic = def.IsExotic,
                IsArtifice = def.UiPlugLabel == "artifice",
                IsTier5 = false,
                IconWatermark = def.IconWatermark,
                AmmoType = def.AmmoType,
                UiPlugLabel = def.UiPlugLabel, 
                // Phase 7: DIM Visual Properties
                IsFeaturedItem = def.IsFeaturedItem,
                SeasonIconUrl = def.SeasonIconUrl,
                DefaultDamageTypeHash = def.DefaultDamageTypeHash,
                SocketCategories = def.SocketCategories
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryService] Error getting definition: {ex.Message}");
            return null;
        }
    }

    private string GetTierName(int tierType) => tierType switch
    {
        6 => "Exotic",
        5 => "Legendary",
        4 => "Rare",
        3 => "Uncommon",
        2 => "Common",
        _ => "Common"
    };


    // LoadMockData has been removed - using real API only

     private string GetDamageTypeIcon(int type, ItemDefinition? def)
    {
        // Deprecated, logic moved to ProcessItemsAsync (using ManifestDatabase)
        return "";
    }

    private record ItemDefinition
    {
        public string? Name { get; init; }
        public string? Description { get; init; }
        public string? Icon { get; init; }
        public string? ItemType { get; init; }
        public int TierType { get; init; } // int from Manifest
        public bool IsExotic { get; init; }
        public bool IsArtifice { get; init; }
        public bool IsTier5 { get; init; }
        public string? IconWatermark { get; init; }
        public int AmmoType { get; init; }
        public string? UiPlugLabel { get; init; }
        
        // DIM Visual Properties
        public bool IsFeaturedItem { get; init; } // Renamed from IsFeatured
        public string? SeasonIconUrl { get; init; }
        public string? IconWatermarkShelved { get; init; }
        public int DefaultDamageTypeHash { get; init; }
        public List<SocketCategoryDefinition> SocketCategories { get; init; } = new();
    }
}
