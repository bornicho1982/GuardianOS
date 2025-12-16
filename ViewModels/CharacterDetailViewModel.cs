using System.IO;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GuardianOS.Messages;
using GuardianOS.Models;
using GuardianOS.Services;

namespace GuardianOS.ViewModels;

/// <summary>
/// ViewModel para la vista de detalles del personaje (Inventario/Equipamiento).
/// </summary>
public partial class CharacterDetailViewModel : ViewModelBase
{
    private readonly IBungieApiService _bungieApiService;
    private readonly IAuthService _authService;
    private readonly IManifestRepository _manifestRepository;
    private readonly string _membershipId;
    private readonly int _membershipType;

    // Bucket Hashes de Destiny 2
    private const long BUCKET_KINETIC = 1498876634;
    private const long BUCKET_ENERGY = 2465295065;
    private const long BUCKET_POWER = 953998645;
    private const long BUCKET_HELMET = 3448274439;
    private const long BUCKET_GAUNTLETS = 3551918588;
    private const long BUCKET_CHEST = 14239492;
    private const long BUCKET_LEGS = 20886954;
    private const long BUCKET_CLASS_ITEM = 1585787867;

    #region Observable Properties

    [ObservableProperty]
    private DestinyCharacter _character;

    [ObservableProperty]
    private bool _isLoadingEquipment;

    // Armas
    [ObservableProperty]
    private InventoryItem? _kineticWeapon;

    [ObservableProperty]
    private InventoryItem? _energyWeapon;

    [ObservableProperty]
    private InventoryItem? _powerWeapon;

    // Armadura
    [ObservableProperty]
    private InventoryItem? _helmet;

    [ObservableProperty]
    private InventoryItem? _gauntlets;

    [ObservableProperty]
    private InventoryItem? _chestArmor;

    [ObservableProperty]
    private InventoryItem? _legArmor;

    [ObservableProperty]
    private InventoryItem? _classItem;

    // WebView2 Properties
    [ObservableProperty]
    private bool _isWebViewLoading = true;

    [ObservableProperty]
    private bool _isWebViewReady = false;

    [ObservableProperty]
    private bool _useStaticImage = true; // Fallback si WebView falla

    /// <summary>
    /// URL para el visor 3D de Bungie.net
    /// </summary>
    public string CharacterViewerUrl => 
        $"https://www.bungie.net/7/en/User/Profile/{_membershipType}/{_membershipId}?character={Character.CharacterId}";

    #endregion

    public CharacterDetailViewModel(
        DestinyCharacter character,
        IBungieApiService bungieApiService,
        IAuthService authService,
        IManifestRepository manifestRepository,
        string membershipId,
        int membershipType)
    {
        Character = character;
        _bungieApiService = bungieApiService;
        _authService = authService;
        _manifestRepository = manifestRepository;
        _membershipId = membershipId;
        _membershipType = membershipType;
    }

    // Constructor simple para compatibilidad temporal (sin carga de datos)
    public CharacterDetailViewModel(DestinyCharacter character)
    {
        Character = character;
        _bungieApiService = new BungieApiService();
        _authService = App.Services?.GetService(typeof(IAuthService)) as IAuthService 
                       ?? throw new InvalidOperationException("AuthService not available");
        _manifestRepository = App.Services?.GetService(typeof(IManifestRepository)) as IManifestRepository
                              ?? throw new InvalidOperationException("ManifestRepository not available");
        _membershipId = character.CharacterId; // Fallback, idealmente pasar el correcto
        _membershipType = 3; // Steam por defecto
    }

    [RelayCommand]
    private void GoBack()
    {
        WeakReferenceMessenger.Default.Send(new NavigateToDashboardMessage());
    }

    [RelayCommand]
    private void OpenBungie3DViewer()
    {
        try
        {
            var url = $"https://www.bungie.net/7/en/User/Profile/{_membershipType}/{_membershipId}?character={Character.CharacterId}";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error abriendo visor 3D: {ex.Message}");
        }
    }

    public override async Task InitializeAsync()
    {
        await LoadEquipmentAsync();
    }

    private async Task LoadEquipmentAsync()
    {
        try
        {
            IsLoadingEquipment = true;
            Debug.WriteLine($"[CharacterDetail] Loading equipment for character {Character.CharacterId}");

            var token = await _authService.GetValidAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                Debug.WriteLine("[CharacterDetail] No valid token");
                return;
            }

            // Solicitar componentes: 205=CharEquip, 300=ItemInstances, 302=ItemSockets, 304=ItemCommon, 305=PlugStates, 307=ReusablePlugs
            var components = new[] { 205, 300, 302, 304, 305, 307 };
            var profileResponse = await _bungieApiService.GetProfileAsync(_membershipType, _membershipId, token, components);

            if (profileResponse?.CharacterEquipment?.Data == null)
            {
                Debug.WriteLine("[CharacterDetail] No equipment data in response");
                return;
            }

            // Buscar equipo del personaje actual
            if (!profileResponse.CharacterEquipment.Data.TryGetValue(Character.CharacterId, out var equipmentData))
            {
                Debug.WriteLine($"[CharacterDetail] Character {Character.CharacterId} not found in equipment data");
                return;
            }

            var itemInstances = profileResponse.ItemComponents?.Instances?.Data;
            var itemSockets = profileResponse.ItemComponents?.Sockets?.Data;

            // Log socket data to file for debugging (since WPF doesn't have console)
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_sockets.log");
            using (var log = new StreamWriter(logPath, false)) // overwrite each time
            {
                log.WriteLine($"[{DateTime.Now}] itemSockets is null: {itemSockets == null}");
                log.WriteLine($"profileResponse.ItemComponents is null: {profileResponse.ItemComponents == null}");
                log.WriteLine($"profileResponse.ItemComponents?.Sockets is null: {profileResponse.ItemComponents?.Sockets == null}");
                
                if (itemSockets != null)
                {
                    log.WriteLine($"Got sockets for {itemSockets.Count} items");
                    foreach (var kvp in itemSockets.Take(3)) // Log first 3 items
                    {
                        log.WriteLine($"  Item {kvp.Key}: {kvp.Value.Sockets?.Count ?? 0} sockets");
                        if (kvp.Value.Sockets != null)
                        {
                            for (int i = 0; i < Math.Min(5, kvp.Value.Sockets.Count); i++)
                            {
                                var s = kvp.Value.Sockets[i];
                                log.WriteLine($"    Socket[{i}]: plugHash={s.PlugHash} enabled={s.IsEnabled}");
                            }
                        }
                    }
                }
                else
                {
                    log.WriteLine("NO SOCKETS DATA FROM API!");
                }
            }
            Debug.WriteLine($"[CharacterDetail] Socket debug written to: {logPath}");

            // Procesar cada ítem equipado
            foreach (var item in equipmentData.Items ?? Enumerable.Empty<EquippedItem>())
            {
                var invItem = await CreateInventoryItemAsync(item, itemInstances, itemSockets);
                if (invItem == null) continue;

                // Log sockets for armor items to find shader
                if (item.ItemInstanceId != null && itemSockets != null && 
                    (item.BucketHash == BUCKET_HELMET || item.BucketHash == BUCKET_GAUNTLETS ||
                     item.BucketHash == BUCKET_CHEST || item.BucketHash == BUCKET_LEGS ||
                     item.BucketHash == BUCKET_CLASS_ITEM))
                {
                    if (itemSockets.TryGetValue(item.ItemInstanceId, out var socketData))
                    {
                        Debug.WriteLine($"[CharacterDetail] Sockets for {invItem.Name} (hash {item.ItemHash}):");
                        var socketIndex = 0;
                        foreach (var socket in socketData.Sockets ?? Enumerable.Empty<SocketEntry>())
                        {
                            if (socket.PlugHash.HasValue && socket.PlugHash.Value > 0)
                            {
                                Debug.WriteLine($"  Socket[{socketIndex}]: plugHash={socket.PlugHash} enabled={socket.IsEnabled}");
                            }
                            socketIndex++;
                        }
                    }
                }

                // Asignar al slot correcto según BucketHash
                switch (item.BucketHash)
                {
                    case BUCKET_KINETIC:
                        KineticWeapon = invItem;
                        break;
                    case BUCKET_ENERGY:
                        EnergyWeapon = invItem;
                        break;
                    case BUCKET_POWER:
                        PowerWeapon = invItem;
                        break;
                    case BUCKET_HELMET:
                        Helmet = invItem;
                        break;
                    case BUCKET_GAUNTLETS:
                        Gauntlets = invItem;
                        break;
                    case BUCKET_CHEST:
                        ChestArmor = invItem;
                        break;
                    case BUCKET_LEGS:
                        LegArmor = invItem;
                        break;
                    case BUCKET_CLASS_ITEM:
                        ClassItem = invItem;
                        break;
                }
            }

            Debug.WriteLine($"[CharacterDetail] Equipment loaded: K={KineticWeapon?.Name}, E={EnergyWeapon?.Name}, P={PowerWeapon?.Name}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CharacterDetail] Error loading equipment: {ex.Message}");
        }
        finally
        {
            IsLoadingEquipment = false;
        }
    }

    private async Task<InventoryItem?> CreateInventoryItemAsync(
        EquippedItem item, 
        Dictionary<string, ItemInstanceData>? instances,
        Dictionary<string, ItemSocketData>? sockets)
    {
        var invItem = new InventoryItem
        {
            ItemHash = item.ItemHash,
            ItemInstanceId = long.TryParse(item.ItemInstanceId, out var id) ? id : null,
            BucketHash = item.BucketHash,
            IsEquipped = true,
            Name = "Cargando...",
            Icon = null // Usará fallback en IconUrl
        };

        // Extract shader from sockets (typically the last socket is shader)
        if (item.ItemInstanceId != null && sockets != null)
        {
            Debug.WriteLine($"[CharacterDetail] Looking for sockets for instanceId: {item.ItemInstanceId}");
            
            if (sockets.TryGetValue(item.ItemInstanceId, out var socketData) &&
                socketData.Sockets != null && socketData.Sockets.Count > 0)
            {
                Debug.WriteLine($"[CharacterDetail] Found {socketData.Sockets.Count} sockets for item {item.ItemHash}");
                
                // Shader is usually in one of the last sockets (index varies, often last non-null)
                // Check last few sockets for a valid plugHash
                for (int i = socketData.Sockets.Count - 1; i >= Math.Max(0, socketData.Sockets.Count - 4); i--)
                {
                    var socket = socketData.Sockets[i];
                    Debug.WriteLine($"[CharacterDetail]   Socket[{i}]: plugHash={socket.PlugHash} enabled={socket.IsEnabled}");
                    
                    if (socket.PlugHash.HasValue && socket.PlugHash.Value > 0 && socket.IsEnabled)
                    {
                        invItem.ShaderHash = socket.PlugHash.Value;
                        Debug.WriteLine($"[CharacterDetail] Selected shader for item {item.ItemHash}: {socket.PlugHash}");
                        break;
                    }
                }
            }
            else
            {
                Debug.WriteLine($"[CharacterDetail] No sockets found for item {item.ItemHash}");
            }
        }

        // Intentar obtener definición del Manifest
        try
        {
            var def = await _manifestRepository.GetItemDefinitionAsync((uint)item.ItemHash);
            if (def != null)
            {
                invItem.Name = def.Name;
                invItem.Icon = def.Icon;
                invItem.TierType = def.Inventory.TierTypeName;
                invItem.ItemTypeDisplayName = def.ItemTypeDisplayName;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CharacterDetail] Error getting item definition for {item.ItemHash}: {ex.Message}");
        }

        // Obtener nivel de luz de ItemInstances
        if (item.ItemInstanceId != null && instances != null &&
            instances.TryGetValue(item.ItemInstanceId, out var instanceData))
        {
            invItem.PrimaryStatValue = instanceData.PrimaryStat?.Value ?? 0;
            invItem.DamageType = instanceData.DamageType;
        }

        return invItem;
    }
}
