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
            var token = await _authService.GetValidAccessTokenAsync();
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(MembershipId))
                return;
            
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
                Characters.Add(character);
            }
            
            // Seleccionar el último jugado
            SelectedCharacter = Characters.FirstOrDefault();
            
            StatusMessage = $"{Characters.Count} personaje(s) cargados";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al cargar personajes: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[VM] LoadCharacters error: {ex.Message}");
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
