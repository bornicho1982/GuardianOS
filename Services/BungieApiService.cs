using System.Net.Http;
using System.Net.Http.Headers;
using GuardianOS.Core;
using GuardianOS.Models;
using Newtonsoft.Json;
using System.Diagnostics;

namespace GuardianOS.Services;

/// <summary>
/// Implementación del servicio para comunicación con la API de Bungie.
/// Usa HttpClient propio en lugar de DI para evitar problemas de configuración.
/// </summary>
public class BungieApiService : IBungieApiService
{
    private readonly HttpClient _httpClient;
    
    /// <summary>
    /// Constructor que crea su propio HttpClient configurado.
    /// </summary>
    public BungieApiService()
    {
        _httpClient = new HttpClient();
        ConfigureHttpClient();
    }
    
    /// <summary>
    /// Configura los headers del HttpClient.
    /// </summary>
    private void ConfigureHttpClient()
    {
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        
        if (!_httpClient.DefaultRequestHeaders.Contains("X-API-Key"))
        {
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", Constants.BUNGIE_API_KEY);
        }
        
        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                $"{Constants.APP_NAME}/{Constants.APP_VERSION}");
        }
        
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        Debug.WriteLine("[BungieAPI] HttpClient configured with API Key");
    }
    
    /// <inheritdoc/>
    public async Task<DestinyManifest?> GetDestinyManifestAsync()
    {
        try
        {
            var url = $"{Constants.BUNGIE_API_BASE_URL}/Destiny2/Manifest/";
            Debug.WriteLine($"[BungieAPI] GET {url}");
            
            var response = await _httpClient.GetAsync(url);
            Debug.WriteLine($"[BungieAPI] Response: {(int)response.StatusCode}");
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[BungieAPI] Error: {errorContent}");
                return null;
            }
            
            var content = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonConvert.DeserializeObject<BungieApiResponse<DestinyManifest>>(content);
            
            if (apiResponse is { IsSuccess: true })
            {
                Debug.WriteLine($"[BungieAPI] Manifest version: {apiResponse.Response?.Version}");
                return apiResponse.Response;
            }
            
            Debug.WriteLine($"[BungieAPI] API Error: {apiResponse?.ErrorStatus} - {apiResponse?.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BungieAPI] Exception: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }
    
    /// <inheritdoc/>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            Debug.WriteLine("[BungieAPI] Testing connection...");
            var manifest = await GetDestinyManifestAsync();
            var success = manifest != null && !string.IsNullOrEmpty(manifest.Version);
            
            if (success)
            {
                Debug.WriteLine($"[BungieAPI] SUCCESS! Version: {manifest!.Version}");
            }
            
            return success;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BungieAPI] Test failed: {ex.Message}");
            return false;
        }
    }
    
    /// <inheritdoc/>
    public async Task<LinkedProfilesResponse?> GetLinkedProfilesAsync(string membershipId, string accessToken)
    {
        try
        {
            // Bungie.net membership type is 254
            var url = $"{Constants.BUNGIE_API_BASE_URL}/Destiny2/254/Profile/{membershipId}/LinkedProfiles/";
            Debug.WriteLine($"[BungieAPI] GET {url}");
            
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            
            Debug.WriteLine($"[BungieAPI] LinkedProfiles: {(int)response.StatusCode}");
            
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[BungieAPI] Error: {content}");
                return null;
            }
            
            var apiResponse = JsonConvert.DeserializeObject<BungieApiResponse<LinkedProfilesResponse>>(content);
            return apiResponse?.Response;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BungieAPI] LinkedProfiles error: {ex.Message}");
            return null;
        }
    }
    
    /// <inheritdoc/>
    public async Task<DestinyProfileResponse?> GetProfileAsync(int membershipType, string membershipId, string accessToken, int[]? components = null)
    {
        try
        {
            // Components: 100=Profiles, 103=ProfileCurrencies, 200=Characters, 205=CharacterEquipment, 102=Vault, 201=CharInventories, 300=ItemInstances
            var componentsList = components != null && components.Length > 0 
                ? string.Join(",", components) 
                : "100,103,200,205"; // Default básico
                
            var url = $"{Constants.BUNGIE_API_BASE_URL}/Destiny2/{membershipType}/Profile/{membershipId}/?components={componentsList}";
            Debug.WriteLine($"[BungieAPI] GET {url}");
            
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            
            Debug.WriteLine($"[BungieAPI] Profile: {(int)response.StatusCode}");
            
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[BungieAPI] Error: {content}");
                return null;
            }
            
            var apiResponse = JsonConvert.DeserializeObject<BungieApiResponse<DestinyProfileResponse>>(content);
            
            // Debug: Log parts of raw JSON to see what itemComponents contains
            try
            {
                var debugPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_api_response.log");
                using (var log = new System.IO.StreamWriter(debugPath, false))
                {
                    log.WriteLine($"[{DateTime.Now}] API Response Debug");
                    log.WriteLine($"URL: {url}");
                    log.WriteLine($"Components requested: {componentsList}");
                    
                    // Parse raw JSON to see structure
                    var rawJson = Newtonsoft.Json.Linq.JObject.Parse(content);
                    var itemComponents = rawJson["Response"]?["itemComponents"];
                    
                    if (itemComponents != null)
                    {
                        log.WriteLine($"itemComponents keys: {string.Join(", ", ((Newtonsoft.Json.Linq.JObject)itemComponents).Properties().Select(p => p.Name))}");
                        
                        // Check if sockets exists
                        var sockets = itemComponents["sockets"];
                        log.WriteLine($"sockets exists: {sockets != null}");
                        
                        if (sockets != null)
                        {
                            log.WriteLine($"sockets.data count: {sockets["data"]?.Count() ?? 0}");
                        }
                    }
                    else
                    {
                        log.WriteLine("itemComponents is null in raw JSON!");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BungieAPI] Debug logging error: {ex.Message}");
            }
            
            return apiResponse?.Response;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BungieAPI] Profile error: {ex.Message}");
            return null;
        }
    }
}
