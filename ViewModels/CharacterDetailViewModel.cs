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
    
    // Server for local output
    private static LocalViewerServer? _viewerServer;
    
    // Unity HDRP Viewer Bridge
    private static UnityViewerBridge? _unityBridge;

    // Bucket Hashes de Destiny 2
    private const long BUCKET_KINETIC = 1498876634;
    private const long BUCKET_ENERGY = 2465295065;
    private const long BUCKET_POWER = 953998645;
    private const long BUCKET_HELMET = 3448274439;
    private const long BUCKET_GAUNTLETS = 3551918588;
    private const long BUCKET_CHEST = 14239492;
    private const long BUCKET_LEGS = 20886954;
    private const long BUCKET_CLASS_ITEM = 1585787867;
    private const long BUCKET_SUBCLASS = 3284755031;

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

    [ObservableProperty]
    private InventoryItem? _subclass;

    // WebView2 Properties
    [ObservableProperty]
    private bool _isWebViewLoading = true;

    [ObservableProperty]
    private bool _isWebViewReady = false;

    [ObservableProperty]
    private bool _useStaticImage = true; // Fallback si WebView falla

    /// <summary>
    /// Toggle to enable/disable 3D viewer (for slower PCs)
    /// </summary>
    [ObservableProperty]
    private bool _is3DEnabled = true;

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

    /// <summary>
    /// Launches the Unity HDRP 3D Viewer
    /// </summary>
    [RelayCommand]
    private async Task LaunchUnityViewer()
    {
        try
        {
            Debug.WriteLine("[Unity] Launching Unity HDRP Viewer...");

            // Dispose existing bridge if any
            _unityBridge?.Dispose();
            _unityBridge = new UnityViewerBridge();

            // Set up event handlers
            _unityBridge.OnError += (error) => Debug.WriteLine($"[Unity] Error: {error}");
            _unityBridge.OnEventReceived += (evt) => Debug.WriteLine($"[Unity] Event: {evt}");
            _unityBridge.OnConnectionChanged += (connected) => Debug.WriteLine($"[Unity] Connected: {connected}");

            // Start Unity and connect
            bool connected = await _unityBridge.StartAndConnectAsync();
            
            if (connected)
            {
                Debug.WriteLine("[Unity] Connected successfully!");
                
                // Send ping to verify connection
                await _unityBridge.PingAsync();
            }
            else
            {
                Debug.WriteLine("[Unity] Failed to connect to Unity viewer");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Unity] Error launching viewer: {ex.Message}");
        }
    }

    /// <summary>
    /// Exporta datos del personaje a JSON y abre el visor 3D local.
    /// </summary>
    [RelayCommand]
    private async Task ExportToViewer()
    {
        try
        {
            Debug.WriteLine("[Export] Starting export to local viewer...");

            // Ruta del archivo JSON
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var exportPath = Path.Combine(basePath, "Assets", "CharmExport");
            var jsonPath = Path.Combine(exportPath, "character_data.json");
            
            // Asegurar directorio
            if (!Directory.Exists(exportPath)) Directory.CreateDirectory(exportPath);
            
            // Recolectar definiciones de materiales (Shaders reales)
            var materialsDict = new Dictionary<string, object>();
            
            // Función auxiliar local para procesar pieza
            async Task ProcessPiece(string key, uint? itemHash, uint? shaderHash)
            {
                ShaderDefinition? def = null;

                // 1. Try Shader Hash first
                if (shaderHash.HasValue && shaderHash.Value != 0)
                {
                    def = await _manifestRepository.GetShaderDefinitionAsync(shaderHash.Value);
                }

                // 2. Fallback to Item Hash if shader invalid or empty
                if (def == null && itemHash.HasValue && itemHash.Value != 0)
                {
                    Console.WriteLine($"[Export] Shader {shaderHash} has no dyes. Trying Item {itemHash}...");
                    def = await _manifestRepository.GetShaderDefinitionAsync(itemHash.Value);
                }

                if (def != null)
                {
                    materialsDict[key] = def;
                }
                else 
                {
                     Console.WriteLine($"[Export] No dyes found for piece {key} (Shader {shaderHash}, Item {itemHash})");
                }
            }

            // Cargar materiales en paralelo
            await Task.WhenAll(
                ProcessPiece("helmet", (uint?)Helmet?.ItemHash, Helmet?.ShaderHash),
                ProcessPiece("arms", (uint?)Gauntlets?.ItemHash, Gauntlets?.ShaderHash),
                ProcessPiece("chest", (uint?)ChestArmor?.ItemHash, ChestArmor?.ShaderHash),
                ProcessPiece("legs", (uint?)LegArmor?.ItemHash, LegArmor?.ShaderHash),
                ProcessPiece("classItem", (uint?)ClassItem?.ItemHash, ClassItem?.ShaderHash)
            );

            // Crear objeto con datos del personaje
            // Mapa de archivos encontrados/copiados
            var modelFiles = new Dictionary<string, string>();
            
            // Lógica de importación externa (Hardcoded path requested by user)
            string externalPath = @"E:\D2_Exports\ApiOutput1";
            if (Directory.Exists(externalPath))
            {
                 Debug.WriteLine($"[Export] Importing assets from {externalPath}...");
                 
                 // 1. Copy Textures
                 var textSrc = Path.Combine(externalPath, "Textures");
                 var textDst = Path.Combine(exportPath, "Textures");
                 if (Directory.Exists(textSrc))
                 {
                     if (!Directory.Exists(textDst)) Directory.CreateDirectory(textDst);
                     foreach (var file in Directory.GetFiles(textSrc, "*.png"))
                     {
                         var fname = Path.GetFileName(file);
                         File.Copy(file, Path.Combine(textDst, fname), true);
                     }
                 }

                 // 2. Scan and Copy FBX (Heuristic Mapping)
                 foreach (var file in Directory.GetFiles(externalPath, "*.fbx"))
                 {
                     var fname = Path.GetFileName(file);
                     var lower = fname.ToLower();
                     string? targetKey = null;

                     if (lower.Contains("mask") || lower.Contains("helmet")) targetKey = "helmet";
                     else if (lower.Contains("grasps") || lower.Contains("arms") || lower.Contains("gauntlets") || lower.Contains("gloves")) targetKey = "arms";
                     else if (lower.Contains("vest") || lower.Contains("chest") || lower.Contains("plate") || lower.Contains("robes")) targetKey = "chest";
                     else if (lower.Contains("strides") || lower.Contains("legs") || lower.Contains("boots") || lower.Contains("greaves")) targetKey = "legs";
                     else if (lower.Contains("cloak") || lower.Contains("mark") || lower.Contains("bond")) targetKey = "classItem";

                     if (targetKey != null)
                     {
                         var destFile = Path.Combine(exportPath, fname);
                         File.Copy(file, destFile, true);
                         modelFiles[targetKey] = fname; // Guardar nombre de archivo
                         Debug.WriteLine($"[Export] Mapped {fname} to {targetKey}");
                     }
                 }
            }

            var exportData = new
            {
                timestamp = DateTime.Now,
                character = new
                {
                    id = Character.CharacterId,
                    className = Character.ClassName,
                    gender = (Character.GenderType == 1 ? "Female" : "Male"),
                    race = Character.RaceType switch { 0 => "Human", 1 => "Awoken", 2 => "Exo", _ => "Unknown" },
                    light = Character.Light
                },
                armor = new
                {
                    helmet = new { 
                        itemHash = Helmet?.ItemHash, 
                        shaderHash = Helmet?.ShaderHash, 
                        ornamentHash = Helmet?.OrnamentHash,
                        name = Helmet?.Name,
                        modelFile = modelFiles.ContainsKey("helmet") ? modelFiles["helmet"] : null
                    },
                    arms = new { 
                        itemHash = Gauntlets?.ItemHash, 
                        shaderHash = Gauntlets?.ShaderHash, 
                        ornamentHash = Gauntlets?.OrnamentHash,
                        name = Gauntlets?.Name,
                        modelFile = modelFiles.ContainsKey("arms") ? modelFiles["arms"] : null
                    },
                    chest = new { 
                        itemHash = ChestArmor?.ItemHash, 
                        shaderHash = ChestArmor?.ShaderHash, 
                        ornamentHash = ChestArmor?.OrnamentHash,
                        name = ChestArmor?.Name,
                        modelFile = modelFiles.ContainsKey("chest") ? modelFiles["chest"] : null
                    },
                    legs = new { 
                        itemHash = LegArmor?.ItemHash, 
                        shaderHash = LegArmor?.ShaderHash, 
                        ornamentHash = LegArmor?.OrnamentHash,
                        name = LegArmor?.Name,
                        modelFile = modelFiles.ContainsKey("legs") ? modelFiles["legs"] : null
                    },
                    classItem = new { 
                        itemHash = ClassItem?.ItemHash, 
                        shaderHash = ClassItem?.ShaderHash, 
                        ornamentHash = ClassItem?.OrnamentHash,
                        name = ClassItem?.Name,
                        modelFile = modelFiles.ContainsKey("classItem") ? modelFiles["classItem"] : null
                    }
                },
                weapons = new
                {
                    kinetic = new { itemHash = KineticWeapon?.ItemHash, name = KineticWeapon?.Name },
                    energy = new { itemHash = EnergyWeapon?.ItemHash, name = EnergyWeapon?.Name },
                    power = new { itemHash = PowerWeapon?.ItemHash, name = PowerWeapon?.Name }
                },
                materials = materialsDict // Nuevos datos reales del shader
            };
            
            // Serializar y guardar
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(exportData, Newtonsoft.Json.Formatting.Indented);
            await File.WriteAllTextAsync(jsonPath, json);
            
            Debug.WriteLine($"[Export] Data saved to: {jsonPath}");
            
            // Iniciar servidor local (singleton pattern simple)
            if (_viewerServer == null)
            {
                _viewerServer = new Services.LocalViewerServer(exportPath);
                _viewerServer.Start();
            }
            else 
            {
                // Ensure it's running
                _viewerServer.Start();
            }
            
            // Abrir el visor HTML vía HTTP (Bypasses CORS)
            var viewerUrl = _viewerServer.Url + "viewer.html";
            Debug.WriteLine($"[Export] Opening viewer at: {viewerUrl}");

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = viewerUrl,
                UseShellExecute = true
            });

        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Export] Error: {ex.Message}");
            System.Windows.MessageBox.Show(
                $"Error al exportar: {ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
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
                    case BUCKET_SUBCLASS:
                        Subclass = invItem;
                        Debug.WriteLine($"[CharacterDetail] Loaded Subclass: {invItem.Name}");
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
                
                // Search ALL sockets for cosmetic shader AND ornament
                for (int i = 0; i < socketData.Sockets.Count; i++)
                {
                    var socket = socketData.Sockets[i];
                    
                    if (socket.PlugHash.HasValue && socket.PlugHash.Value > 0 && socket.IsEnabled)
                    {
                        try
                        {
                            var plugDef = await _manifestRepository.GetItemDefinitionAsync((uint)socket.PlugHash.Value);
                            if (plugDef != null)
                            {
                                // Check for SHADER (category 41 or ItemTypeDisplayName contains "Shader")
                                var isShader = plugDef.ItemTypeDisplayName?.Contains("Shader") == true ||
                                             plugDef.ItemTypeDisplayName?.Contains("shader") == true ||
                                             (plugDef.ItemCategoryHashes?.Contains(41) == true);
                                
                                if (isShader && invItem.ShaderHash == null)
                                {
                                    invItem.ShaderHash = socket.PlugHash.Value;
                                    Debug.WriteLine($"[CharacterDetail] ✓ Found SHADER: {plugDef.Name} (hash {socket.PlugHash})");
                                }
                                
                                // Check for ORNAMENT (category 3109687656 or ItemTypeDisplayName contains "Ornament")
                                var isOrnament = plugDef.ItemTypeDisplayName?.Contains("Ornament") == true ||
                                               plugDef.ItemTypeDisplayName?.Contains("ornament") == true ||
                                               (plugDef.ItemCategoryHashes?.Contains(3109687656) == true);
                                
                                if (isOrnament && invItem.OrnamentHash == null)
                                {
                                    invItem.OrnamentHash = socket.PlugHash.Value;
                                    Debug.WriteLine($"[CharacterDetail] ✓ Found ORNAMENT: {plugDef.Name} (hash {socket.PlugHash})");
                                }
                                
                                // If we found both, stop searching
                                if (invItem.ShaderHash != null && invItem.OrnamentHash != null)
                                {
                                    break;
                                }
                            }
                        }
                        catch { /* Ignore manifest lookup errors */ }
                    }
                }
                
                if (invItem.ShaderHash == null)
                    Debug.WriteLine($"[CharacterDetail] No shader found for item {item.ItemHash}");
                if (invItem.OrnamentHash == null)
                    Debug.WriteLine($"[CharacterDetail] No ornament found for item {item.ItemHash}");
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
