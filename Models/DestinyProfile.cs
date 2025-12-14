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
    /// Fecha de la última vez que se jugó en este perfil.
    /// </summary>
    [JsonProperty("dateLastPlayed")]
    public DateTime DateLastPlayed { get; set; }

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

    [JsonProperty("profileInventory")]
    public ProfileInventoryComponent? ProfileInventory { get; set; }

    [JsonProperty("characterInventories")]
    public CharacterInventoriesComponent? CharacterInventories { get; set; }

    [JsonProperty("itemComponents")]
    public ItemComponentsComponent? ItemComponents { get; set; }
}

public class ProfileInventoryComponent
{
    [JsonProperty("data")]
    public InventoryData? Data { get; set; }
}

public class CharacterInventoriesComponent
{
    [JsonProperty("data")]
    public Dictionary<string, InventoryData>? Data { get; set; }
}

public class InventoryData
{
    [JsonProperty("items")]
    public List<DestinyItemComponent>? Items { get; set; }
}

public class DestinyItemComponent
{
    [JsonProperty("itemHash")]
    public uint ItemHash { get; set; }

    [JsonProperty("itemInstanceId")]
    public long? ItemInstanceId { get; set; }

    [JsonProperty("quantity")]
    public int Quantity { get; set; }

    [JsonProperty("bindStatus")]
    public int BindStatus { get; set; }

    [JsonProperty("location")]
    public int Location { get; set; }

    [JsonProperty("bucketHash")]
    public uint BucketHash { get; set; }

    [JsonProperty("transferStatus")]
    public int TransferStatus { get; set; }
    
    [JsonProperty("lockable")]
    public bool Lockable { get; set; }
    
    [JsonProperty("state")]
    public int State { get; set; }
}

public class ItemComponentsComponent
{
    [JsonProperty("instances")]
    public ItemInstancesComponent? Instances { get; set; }
}

public class ItemInstancesComponent
{
    [JsonProperty("data")]
    public Dictionary<string, ItemInstanceData>? Data { get; set; }
}

public class ItemInstanceData
{
    [JsonProperty("damageType")]
    public int DamageType { get; set; }

    [JsonProperty("primaryStat")]
    public PrimaryStat? PrimaryStat { get; set; }

    [JsonProperty("itemLevel")]
    public int ItemLevel { get; set; }

    [JsonProperty("quality")]
    public int Quality { get; set; }
    
    [JsonProperty("isEquipped")]
    public bool IsEquipped { get; set; }
    
    [JsonProperty("canEquip")]
    public bool CanEquip { get; set; }
    
    [JsonProperty("equipRequiredLevel")]
    public int EquipRequiredLevel { get; set; }
}

public class PrimaryStat
{
    [JsonProperty("statHash")]
    public uint StatHash { get; set; }

    [JsonProperty("value")]
    public int Value { get; set; }
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
