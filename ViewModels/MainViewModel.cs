using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GuardianOS.Models;
using GuardianOS.Services;

namespace GuardianOS.ViewModels;

/// <summary>
/// ViewModel principal de la aplicación.
/// Maneja la navegación entre módulos y el estado global de autenticación.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IBungieApiService _bungieApiService;
    private readonly IAuthService _authService;
    
    #region Observable Properties
    
    /// <summary>
    /// Vista actual mostrada en el área de contenido principal.
    /// </summary>
    [ObservableProperty]
    private ViewModelBase? _currentViewModel;
    
    /// <summary>
    /// Índice del módulo seleccionado en el menú de navegación.
    /// 0 = Inventario, 1 = Mapas, 2 = Builds, 3 = Eventos, 4 = Armas
    /// </summary>
    [ObservableProperty]
    private int _selectedNavigationIndex;
    
    /// <summary>
    /// Indica si el usuario ha iniciado sesión con Bungie.
    /// </summary>
    [ObservableProperty]
    private bool _isUserAuthenticated;
    
    /// <summary>
    /// Nombre del guardián conectado (si está autenticado).
    /// </summary>
    [ObservableProperty]
    private string? _guardianName;
    
    /// <summary>
    /// Mensaje de estado para mostrar en la barra de estado.
    /// </summary>
    [ObservableProperty]
    private string? _statusMessage;
    
    /// <summary>
    /// Indica si hay una operación de conexión en progreso.
    /// </summary>
    [ObservableProperty]
    private bool _isConnecting;
    
    /// <summary>
    /// Indica si la API de Bungie está disponible.
    /// </summary>
    [ObservableProperty]
    private bool _isApiAvailable;
    
    /// <summary>
    /// Título del módulo actual.
    /// </summary>
    [ObservableProperty]
    private string _currentModuleTitle = "Bienvenido a GuardianOS";
    
    /// <summary>
    /// ID de membresía del usuario autenticado.
    /// </summary>
    [ObservableProperty]
    private string? _membershipId;
    
    /// <summary>
    /// Tipo de membresía (plataforma) del usuario.
    /// </summary>
    [ObservableProperty]
    private int _membershipType;
    
    /// <summary>
    /// Lista de personajes del jugador.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DestinyCharacter> _characters = new();
    
    /// <summary>
    /// Personaje actualmente seleccionado.
    /// </summary>
    [ObservableProperty]
    private DestinyCharacter? _selectedCharacter;
    
    /// <summary>
    /// Indica si se están cargando los personajes (para mostrar skeletons).
    /// </summary>
    [ObservableProperty]
    private bool _isLoadingCharacters;
    
    /// <summary>
    /// Cantidad de Glimmer del jugador.
    /// </summary>
    [ObservableProperty]
    private int _glimmer;
    
    /// <summary>
    /// Cantidad de Legendary Shards (Fragmentos Legendarios).
    /// </summary>
    [ObservableProperty]
    private int _legendaryShards;
    
    /// <summary>
    /// Cantidad de Bright Dust (Polvo Luminoso).
    /// </summary>
    [ObservableProperty]
    private int _brightDust;
    
    /// <summary>
    /// Cantidad de Enhancement Cores (Núcleos de Mejora).
    /// </summary>
    [ObservableProperty]
    private int _enhancementCores;
    
    #endregion
    
    /// <summary>
    /// Constructor con inyección de dependencias.
    /// </summary>
    /// <param name="authService">Servicio de autenticación OAuth.</param>
    public MainViewModel(IAuthService authService)
    {
        // Crear BungieApiService directamente (no via DI porque causa problemas)
        _bungieApiService = new BungieApiService();
        _authService = authService;
        
        Title = "GuardianOS";
        StatusMessage = "Listo";
        
        // Suscribirse a cambios de estado de autenticación
        _authService.AuthenticationStateChanged += OnAuthenticationStateChanged;
    }
    
    /// <summary>
    /// Maneja cambios en el estado de autenticación.
    /// </summary>
    private async void OnAuthenticationStateChanged(object? sender, bool isAuthenticated)
    {
        IsUserAuthenticated = isAuthenticated;
        
        if (isAuthenticated && _authService.CurrentToken != null)
        {
            MembershipId = _authService.CurrentToken.MembershipId;
            GuardianName = $"Guardian #{MembershipId?[..6]}...";
            StatusMessage = "¡Conectado! Cargando personajes...";
            
            // Cargar personajes
            await LoadCharactersAsync();
        }
        else
        {
            MembershipId = null;
            GuardianName = null;
            Characters.Clear();
            SelectedCharacter = null;
            CurrentModuleTitle = "Bienvenido a GuardianOS";
        }
    }
    
    /// <summary>
    /// Carga los personajes del jugador autenticado.
    /// </summary>
    private async Task LoadCharactersAsync()
    {
        try
        {
            IsLoadingCharacters = true;
            
            var token = await _authService.GetValidAccessTokenAsync();
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(MembershipId))
            {
                IsLoadingCharacters = false;
                return;
            }
            
            StatusMessage = "Obteniendo cuentas vinculadas...";
            
            // Obtener perfiles vinculados
            var linkedProfiles = await _bungieApiService.GetLinkedProfilesAsync(MembershipId, token);
            
            if (linkedProfiles?.Profiles == null || linkedProfiles.Profiles.Count == 0)
            {
                StatusMessage = "No se encontraron cuentas de Destiny";
                return;
            }
            
            // Usar el primer perfil (normalmente Steam/Epic)
            var mainProfile = linkedProfiles.Profiles[0];
            MembershipType = mainProfile.MembershipType;
            GuardianName = mainProfile.FullBungieName;
            
            StatusMessage = "Cargando personajes...";
            
            // Obtener datos del perfil con personajes
            var profileData = await _bungieApiService.GetProfileAsync(
                mainProfile.MembershipType, 
                mainProfile.MembershipId, 
                token);
            
            if (profileData?.Characters?.Data == null)
            {
                StatusMessage = "No se encontraron personajes";
                return;
            }
            
            // Agregar personajes a la colección
            Characters.Clear();
            foreach (var character in profileData.Characters.Data.Values.OrderByDescending(c => c.DateLastPlayed))
            {
                // Extraer tipo de subclase del equipo
                if (profileData.CharacterEquipment?.Data != null &&
                    profileData.CharacterEquipment.Data.TryGetValue(character.CharacterId, out var equipment))
                {
                    // Bucket hash 3284755031 = Subclass slot
                    var subclassItem = equipment.Items?.FirstOrDefault(i => i.BucketHash == 3284755031);
                    if (subclassItem != null)
                    {
                        character.SubclassType = GetDamageTypeFromSubclassHash(subclassItem.ItemHash);
                    }
                }
                Characters.Add(character);
            }
            
            // Seleccionar el último jugado
            SelectedCharacter = Characters.FirstOrDefault();
            
            // Extraer currencies del perfil
            if (profileData.ProfileCurrencies?.Data?.Items != null)
            {
                // Currency hashes conocidos
                const long GLIMMER_HASH = 3159615086;
                const long LEGENDARY_SHARDS_HASH = 1022552290;
                const long BRIGHT_DUST_HASH = 2817410917;
                const long ENHANCEMENT_CORES_HASH = 3853748946;
                
                foreach (var currency in profileData.ProfileCurrencies.Data.Items)
                {
                    switch (currency.ItemHash)
                    {
                        case GLIMMER_HASH:
                            Glimmer = currency.Quantity;
                            break;
                        case LEGENDARY_SHARDS_HASH:
                            LegendaryShards = currency.Quantity;
                            break;
                        case BRIGHT_DUST_HASH:
                            BrightDust = currency.Quantity;
                            break;
                        case ENHANCEMENT_CORES_HASH:
                            EnhancementCores = currency.Quantity;
                            break;
                    }
                }
            }
            
            StatusMessage = $"{Characters.Count} personaje(s) cargados";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al cargar personajes: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[VM] LoadCharacters error: {ex.Message}");
        }
        finally
        {
            IsLoadingCharacters = false;
        }
    }
    
    /// <summary>
    /// Selecciona un personaje para ver sus detalles.
    /// </summary>
    [RelayCommand]
    private void SelectCharacter(DestinyCharacter? character)
    {
        if (character == null) return;
        
        SelectedCharacter = character;
        StatusMessage = $"Personaje seleccionado: {character.ClassName} - {character.Light}✦";
    }
    
    /// <summary>
    /// Mapea el hash de una subclase a su tipo de daño.
    /// Damage Types: 2=Arc, 3=Solar, 4=Void, 6=Stasis, 7=Strand, 8=Prismatic
    /// </summary>
    private static int GetDamageTypeFromSubclassHash(long itemHash)
    {
        // Subclass hashes conocidos de Destiny 2 (The Final Shape era)
        // Los hashes pueden variar - estos son aproximados basados en data conocida
        return itemHash switch
        {
            // === SOLAR (3) ===
            2240888816 => 3, // Gunslinger (Hunter Solar)
            2550323932 => 3, // Sunbreaker (Titan Solar)
            3941205951 => 3, // Dawnblade (Warlock Solar)
            
            // === ARC (2) ===
            2328211300 => 2, // Arcstrider (Hunter Arc)
            2932390016 => 2, // Striker (Titan Arc) - legacy
            1616346845 => 2, // Striker (Titan Arc) - Arc 3.0 confirmed
            3168997075 => 2, // Stormcaller (Warlock Arc)
            
            // === VOID (4) ===
            2453351420 => 4, // Nightstalker (Hunter Void)
            2842471112 => 4, // Sentinel (Titan Void)
            2849050827 => 4, // Voidwalker (Warlock Void)
            
            // === STASIS (6) ===
            873720784 => 6,  // Revenant (Hunter Stasis)
            613647804 => 6,  // Behemoth (Titan Stasis)
            3291545503 => 6, // Shadebinder (Warlock Stasis)
            
            // === STRAND (7) ===
            3785442599 => 7, // Threadrunner (Hunter Strand)
            242419885 => 7,  // Berserker (Titan Strand)
            4204413574 => 7, // Broodweaver (Warlock Strand)
            
            // === PRISMATIC (8) ===
            // Prismatic subclasses (Lightfall/Final Shape)
            _ when IsPrismaticHash(itemHash) => 8,
            
            _ => 0 // Desconocido
        };
    }
    
    /// <summary>
    /// Determina si el hash corresponde a una subclase Prismática.
    /// </summary>
    private static bool IsPrismaticHash(long itemHash)
    {
        // Hashes conocidos de Prismatic (The Final Shape)
        // Capturados de la API real
        return itemHash is 
            4282591831 or // Hunter Prismatic (confirmed)
            2806466524 or // Hunter Prismatic (alt)
            3979749617 or // Titan Prismatic
            1946006466;   // Warlock Prismatic
    }
    
    #region Navigation Commands
    
    /// <summary>
    /// Navega al módulo de Inventario.
    /// Permite gestionar ítems entre personajes y depósito.
    /// </summary>
    [RelayCommand]
    private void NavigateToInventory()
    {
        SelectedNavigationIndex = 0;
        CurrentModuleTitle = "Inventario";
        StatusMessage = "Módulo de Inventario - Gestiona tus ítems";
        // TODO: Instanciar InventoryViewModel cuando esté implementado
    }
    
    /// <summary>
    /// Navega al módulo de Mapas.
    /// Visualización de coleccionables y checklists.
    /// </summary>
    [RelayCommand]
    private void NavigateToMaps()
    {
        SelectedNavigationIndex = 1;
        CurrentModuleTitle = "Mapas y Coleccionables";
        StatusMessage = "Módulo de Mapas - Explora destinos y coleccionables";
        // TODO: Instanciar MapsViewModel cuando esté implementado
    }
    
    /// <summary>
    /// Navega al módulo de Constructor de Builds.
    /// Motor de IA para calcular combinaciones óptimas de armadura.
    /// </summary>
    [RelayCommand]
    private void NavigateToBuildGenerator()
    {
        SelectedNavigationIndex = 2;
        CurrentModuleTitle = "Constructor de Builds";
        StatusMessage = "Constructor de Builds con IA - Optimiza tu armadura";
        // TODO: Instanciar BuildGeneratorViewModel cuando esté implementado
    }
    
    /// <summary>
    /// Navega al módulo de Eventos y Vendedores.
    /// Rastrea rotaciones semanales y diarias.
    /// </summary>
    [RelayCommand]
    private void NavigateToEvents()
    {
        SelectedNavigationIndex = 3;
        CurrentModuleTitle = "Eventos y Vendedores";
        StatusMessage = "Eventos - Rotaciones semanales y diarias";
        // TODO: Instanciar EventsViewModel cuando esté implementado
    }
    
    /// <summary>
    /// Navega al módulo de Evaluación de Armas.
    /// Compara perks y determina God Rolls.
    /// </summary>
    [RelayCommand]
    private void NavigateToWeapons()
    {
        SelectedNavigationIndex = 4;
        CurrentModuleTitle = "Evaluación de Armas";
        StatusMessage = "Armas - Analiza rolls y encuentra God Rolls";
        // TODO: Instanciar WeaponsViewModel cuando esté implementado
    }
    
    #endregion
    
    #region Authentication Commands
    
    /// <summary>
    /// Inicia el proceso de autenticación OAuth con Bungie.
    /// </summary>
    [RelayCommand]
    private async Task ConnectWithBungieAsync()
    {
        if (IsConnecting) return;
        
        try
        {
            IsConnecting = true;
            
            // Si ya verificamos en InitializeAsync que la API funciona, ir directo a OAuth
            if (!IsApiAvailable)
            {
                // Hacer un test rápido si no está disponible
                StatusMessage = "Verificando conexión...";
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Add("X-API-Key", Core.Constants.BUNGIE_API_KEY);
                var response = await client.GetAsync($"{Core.Constants.BUNGIE_API_BASE_URL}/Destiny2/Manifest/");
                IsApiAvailable = response.IsSuccessStatusCode;
                
                if (!IsApiAvailable)
                {
                    StatusMessage = "Error: API de Bungie no disponible";
                    return;
                }
            }
            
            StatusMessage = "Abriendo navegador para autenticación...";
            
            // Iniciar flujo OAuth
            var success = await _authService.StartAuthenticationAsync();
            
            if (success)
            {
                StatusMessage = "¡Autenticación exitosa! Bienvenido, Guardián.";
            }
            else
            {
                StatusMessage = "Autenticación cancelada o fallida";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            IsApiAvailable = false;
        }
        finally
        {
            IsConnecting = false;
        }
    }
    
    /// <summary>
    /// Cierra la sesión del usuario.
    /// </summary>
    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.LogoutAsync();
        StatusMessage = "Sesión cerrada";
    }
    
    #endregion
    
    #region Lifecycle
    
    /// <summary>
    /// Inicialización asíncrona al cargar la ventana principal.
    /// </summary>
    public override async Task InitializeAsync()
    {
        StatusMessage = "Iniciando GuardianOS...";
        
        try
        {
            // Intentar restaurar sesión anterior
            StatusMessage = "Verificando sesión anterior...";
            var sessionRestored = await _authService.TryRestoreSessionAsync();
            
            if (sessionRestored)
            {
                StatusMessage = "Sesión restaurada. ¡Bienvenido de nuevo, Guardián!";
                return;
            }
            
            // TEST DIRECTO - mismo código que funcionó antes
            StatusMessage = "Verificando conexión con Bungie...";
            
            using var directClient = new System.Net.Http.HttpClient();
            directClient.DefaultRequestHeaders.Add("X-API-Key", Core.Constants.BUNGIE_API_KEY);
            directClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            
            var testUrl = $"{Core.Constants.BUNGIE_API_BASE_URL}/Destiny2/Manifest/";
            System.Diagnostics.Debug.WriteLine($"[TEST] GET: {testUrl}");
            
            var response = await directClient.GetAsync(testUrl);
            System.Diagnostics.Debug.WriteLine($"[TEST] Status: {(int)response.StatusCode}");
            
            if (response.IsSuccessStatusCode)
            {
                IsApiAvailable = true;
                StatusMessage = "Conectado a Bungie. Inicia sesión para continuar.";
            }
            else
            {
                IsApiAvailable = false;
                StatusMessage = $"Error API: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TEST] Error: {ex.Message}");
            StatusMessage = $"Error: {ex.Message}";
            IsApiAvailable = false;
        }
    }
    
    /// <summary>
    /// Limpieza al cerrar el ViewModel.
    /// </summary>
    public override void Cleanup()
    {
        _authService.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        base.Cleanup();
    }
    
    #endregion
}
