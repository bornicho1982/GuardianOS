using Newtonsoft.Json;

namespace GuardianOS.Models;

/// <summary>
/// Representa el perfil de Destiny 2 de un jugador.
/// </summary>
public class DestinyProfile
{
    /// <summary>
    /// Tipo de membresía (1=Xbox, 2=PSN, 3=Steam, etc.)
    /// </summary>
    [JsonProperty("membershipType")]
    public int MembershipType { get; set; }
    
    /// <summary>
    /// ID de membresía del jugador.
    /// </summary>
    [JsonProperty("membershipId")]
    public string MembershipId { get; set; } = string.Empty;
    
    /// <summary>
    /// Nombre para mostrar del jugador.
    /// </summary>
    [JsonProperty("displayName")]
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// Código de nombre Bungie (ej: #1234).
    /// </summary>
    [JsonProperty("bungieGlobalDisplayNameCode")]
    public int? BungieNameCode { get; set; }
    
    /// <summary>
    /// Lista de personajes del jugador.
    /// </summary>
    public List<DestinyCharacter> Characters { get; set; } = new();
    
    /// <summary>
    /// Nombre completo con código Bungie.
    /// </summary>
    public string FullBungieName => BungieNameCode.HasValue 
        ? $"{DisplayName}#{BungieNameCode:D4}" 
        : DisplayName;
}

/// <summary>
/// Respuesta de la API para LinkedProfiles.
/// </summary>
public class LinkedProfilesResponse
{
    [JsonProperty("profiles")]
    public List<DestinyProfile>? Profiles { get; set; }
    
    [JsonProperty("profilesWithErrors")]
    public List<object>? ProfilesWithErrors { get; set; }
}

/// <summary>
/// Respuesta del endpoint GetProfile.
/// </summary>
public class DestinyProfileResponse
{
    [JsonProperty("profile")]
    public ProfileComponent? Profile { get; set; }
    
    [JsonProperty("profileCurrencies")]
    public ProfileCurrenciesComponent? ProfileCurrencies { get; set; }
    
    [JsonProperty("characters")]
    public CharactersComponent? Characters { get; set; }
    
    [JsonProperty("characterEquipment")]
    public CharacterEquipmentComponent? CharacterEquipment { get; set; }
}

/// <summary>
/// Componente de monedas del perfil.
/// </summary>
public class ProfileCurrenciesComponent
{
    [JsonProperty("data")]
    public ProfileCurrenciesData? Data { get; set; }
}

public class ProfileCurrenciesData
{
    [JsonProperty("items")]
    public List<CurrencyItem>? Items { get; set; }
}

public class CurrencyItem
{
    [JsonProperty("itemHash")]
    public long ItemHash { get; set; }
    
    [JsonProperty("quantity")]
    public int Quantity { get; set; }
}

public class ProfileComponent
{
    [JsonProperty("data")]
    public ProfileData? Data { get; set; }
}

public class ProfileData
{
    [JsonProperty("userInfo")]
    public DestinyUserInfo? UserInfo { get; set; }
    
    [JsonProperty("characterIds")]
    public List<string>? CharacterIds { get; set; }
}

public class DestinyUserInfo
{
    [JsonProperty("membershipType")]
    public int MembershipType { get; set; }
    
    [JsonProperty("membershipId")]
    public string MembershipId { get; set; } = string.Empty;
    
    [JsonProperty("bungieGlobalDisplayName")]
    public string DisplayName { get; set; } = string.Empty;
    
    [JsonProperty("bungieGlobalDisplayNameCode")]
    public int? DisplayNameCode { get; set; }
}

public class CharactersComponent
{
    [JsonProperty("data")]
    public Dictionary<string, DestinyCharacter>? Data { get; set; }
}

/// <summary>
/// Componente de equipo de personajes.
/// </summary>
public class CharacterEquipmentComponent
{
    [JsonProperty("data")]
    public Dictionary<string, CharacterEquipmentData>? Data { get; set; }
}

public class CharacterEquipmentData
{
    [JsonProperty("items")]
    public List<EquippedItem>? Items { get; set; }
}

public class EquippedItem
{
    [JsonProperty("itemHash")]
    public long ItemHash { get; set; }
    
    [JsonProperty("itemInstanceId")]
    public string? ItemInstanceId { get; set; }
    
    [JsonProperty("bucketHash")]
    public long BucketHash { get; set; }
}
