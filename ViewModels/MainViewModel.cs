using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GuardianOS.Models;
using GuardianOS.Services;
using System.Linq; // Necesario para OrderByDescending y FirstOrDefault
using CommunityToolkit.Mvvm.Messaging;
using GuardianOS.Messages;

namespace GuardianOS.ViewModels;

/// <summary>
/// ViewModel principal de la aplicación (Shell).
/// Maneja la autenticación, el estado de la API y la navegación principal.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IBungieApiService _bungieApiService;
    private readonly IAuthService _authService;
    private readonly IManifestService _manifestService;
    
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
    private int _selectedNavigationIndex = -1; // -1 por defecto (Dashboard/Home)
    
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
    /// Indica si se está descargando el manifiesto de Destiny 2.
    /// </summary>
    [ObservableProperty]
    private bool _isDownloadingManifest;
    
    /// <summary>
    /// Progreso de la descarga del manifiesto (0-100).
    /// </summary>
    [ObservableProperty]
    private double _downloadProgress;

     /// <summary>
    /// Rango de Guardián (Ej. 11).
    /// </summary>
    [ObservableProperty]
    private int _guardianRank = 11; // Hardcodeado por ahora para demo visual

    /// <summary>
    /// Nivel de pase de temporada (Ej. 50).
    /// </summary>
    [ObservableProperty]
    private int _seasonRank = 50; // Hardcodeado por ahora para demo visual
    
    #endregion
    
    public MainViewModel(IAuthService authService, IManifestService manifestService)
    {
        // Crear BungieApiService directamente (podríamos inyectarlo también)
        _bungieApiService = new BungieApiService();
        _authService = authService;
        _manifestService = manifestService;
        
        Title = "GuardianOS";
// ...
        StatusMessage = "Listo";
        
        // Suscribirse a cambios de estado de autenticación
        _authService.AuthenticationStateChanged += OnAuthenticationStateChanged;

        // Registrar mensajes de navegación
        WeakReferenceMessenger.Default.Register<CharacterSelectedMessage>(this, (r, m) =>
        {
            CurrentViewModel = new CharacterDetailViewModel(m.Value);
            CurrentModuleTitle = "Equipamiento";
            SelectedNavigationIndex = -1; // Deseleccionar menú lateral
        });
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
            
            // 1. Obtener perfiles enlazados para determinar la plataforma correcta (Steam, Xbox, etc.)
            var linkedProfiles = await _bungieApiService.GetLinkedProfilesAsync(MembershipId, _authService.CurrentToken.AccessToken);
            
            if (linkedProfiles != null && linkedProfiles.Profiles.Count > 0)
            {
                // Priorizar el perfil con CrossSave activado (si existe) o el que se haya jugado más recientemente
                var bestProfile = linkedProfiles.Profiles.OrderByDescending(p => p.DateLastPlayed).FirstOrDefault();
                
                if (bestProfile != null)
                {
                    MembershipId = bestProfile.MembershipId;
                    MembershipType = bestProfile.MembershipType;
                    
                    // Actualizar nombre con el DisplayName real de la plataforma
                    GuardianName = string.IsNullOrEmpty(bestProfile.DisplayName) 
                        ? $"Guardian #{MembershipId.Substring(0, 4)}..." 
                        : bestProfile.DisplayName;
                }
            }
            else
            {
                 StatusMessage = "Error: No se encontraron perfiles de Destiny vinculados.";
                 IsUserAuthenticated = false; // Forzar logout si no hay perfil
                 return;
            }

            StatusMessage = $"Autenticado como {GuardianName}. Cargando Dashboard...";
            
            // Instanciar y navegar al Dashboard con los IDs correctos
            var dashboardVm = new DashboardViewModel(_bungieApiService, _authService, MembershipId!, MembershipType);
            await dashboardVm.InitializeAsync();
            
            // Si el dashboard cargó un nombre mejor (FullBungieName), intentaríamos obtenerlo?
            // DashboardVM tiene Characters. MainVM no tiene acceso directo a Characters[0].
            // Podríamos exponer un evento en DashboardVM "OnDataLoaded" pero es mucho overhead.
            
            CurrentViewModel = dashboardVm;
            CurrentModuleTitle = "Dashboard";
            SelectedNavigationIndex = -1; // Deseleccionar menú lateral
        }
        else
        {
            MembershipId = null;
            GuardianName = null;
            CurrentViewModel = null; // Volver a pantalla de login
            CurrentModuleTitle = "Bienvenido a GuardianOS";
        }
    }
    
    #region Navigation Commands
    
    [RelayCommand]
    private void NavigateToInventory()
    {
        SelectedNavigationIndex = 0;
        CurrentModuleTitle = "Inventario";
        StatusMessage = "Módulo de Inventario (En construcción)";
        // TODO: CurrentViewModel = new InventoryViewModel(...);
    }
    
    [RelayCommand]
    private void NavigateToMaps()
    {
        SelectedNavigationIndex = 1;
        CurrentModuleTitle = "Mapas y Coleccionables";
        StatusMessage = "Módulo de Mapas (En construcción)";
    }
    
    [RelayCommand]
    private void NavigateToBuildGenerator()
    {
        SelectedNavigationIndex = 2;
        CurrentModuleTitle = "Constructor de Builds";
        StatusMessage = "Constructor de Builds (En construcción)";
    }
    
    [RelayCommand]
    private void NavigateToEvents()
    {
        SelectedNavigationIndex = 3;
        CurrentModuleTitle = "Eventos y Vendedores";
        StatusMessage = "Eventos (En construcción)";
    }
    
    [RelayCommand]
    private void NavigateToWeapons()
    {
        SelectedNavigationIndex = 4;
        CurrentModuleTitle = "Evaluación de Armas";
        StatusMessage = "Evaluación de Armas (En construcción)";
    }
    
    /// <summary>
    /// Vuelve al Dashboard / Home
    /// </summary>
    public void NavigateToHome()
    {
        if (IsUserAuthenticated && MembershipId != null)
        {
             // Reinstanciar o usar cached si quisiéramos. Por ahora reinstanciamos.
             // En un refactor real usaríamos un NavigationService o Factory.
             var dashboardVm = new DashboardViewModel(_bungieApiService, _authService, MembershipId, MembershipType);
             _ = dashboardVm.InitializeAsync(); // Fire and forget warning fix
             CurrentViewModel = dashboardVm;
             CurrentModuleTitle = "Dashboard";
             SelectedNavigationIndex = -1;
        }
        else
        {
            CurrentViewModel = null;
        }
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
            
            if (!IsApiAvailable)
            {
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
            var success = await _authService.StartAuthenticationAsync();
            
            if (success)
            {
                StatusMessage = "¡Autenticación exitosa!";
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
    
    public override async Task InitializeAsync()
    {
        StatusMessage = "Iniciando GuardianOS...";
        
        try
        {
            // Inicializar Manifiesto (descarga si es necesario)
            StatusMessage = "Verificando base de datos de Destiny 2...";
            IsDownloadingManifest = true; // Activar spinner
            await _manifestService.InitializeAsync();
            IsDownloadingManifest = false; // Desactivar spinner
            
            StatusMessage = "Verificando sesión anterior...";
            var sessionRestored = await _authService.TryRestoreSessionAsync();
            
            if (sessionRestored)
            {
                IsApiAvailable = true;
                StatusMessage = "Sesión restaurada.";
                return;
            }
            
            // Test conexión
            StatusMessage = "Verificando conexión...";
            using var directClient = new System.Net.Http.HttpClient();
            directClient.DefaultRequestHeaders.Add("X-API-Key", Core.Constants.BUNGIE_API_KEY);
            var testUrl = $"{Core.Constants.BUNGIE_API_BASE_URL}/Destiny2/Manifest/";
            var response = await directClient.GetAsync(testUrl);
            
            if (response.IsSuccessStatusCode)
            {
                IsApiAvailable = true;
                StatusMessage = "Conectado a Bungie.";
            }
            else
            {
                IsApiAvailable = false;
                StatusMessage = $"Error API: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            IsApiAvailable = false;
        }
    }
    
    public override void Cleanup()
    {
        _authService.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        base.Cleanup();
    }
    
    #endregion
}
