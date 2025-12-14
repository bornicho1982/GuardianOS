using System.Net.Http;
using System.Windows;
using GuardianOS.Core;
using GuardianOS.Services;
using GuardianOS.ViewModels;
using GuardianOS.Views;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using System.Net.Http.Headers;

namespace GuardianOS;

/// <summary>
/// Punto de entrada de la aplicación GuardianOS.
/// Configura el contenedor de inyección de dependencias y arranca la ventana principal.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Proveedor de servicios para inyección de dependencias.
    /// </summary>
    private ServiceProvider? _serviceProvider;
    
    /// <summary>
    /// Expone el proveedor de servicios para acceso global (usar con precaución).
    /// </summary>
    public static ServiceProvider? Services { get; private set; }
    
    /// <summary>
    /// Constructor de la aplicación.
    /// Configura todos los servicios antes de que la app inicie.
    /// </summary>
    public App()
    {
        // Configurar servicios
        var services = new ServiceCollection();
        ConfigureServices(services);
        
        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;
    }
    
    /// <summary>
    /// Configura todos los servicios de la aplicación.
    /// </summary>
    /// <param name="services">Colección de servicios a configurar.</param>
    private static void ConfigureServices(IServiceCollection services)
    {
        // ========== HTTP CLIENT ==========
        // Registrar HttpClient para AuthService
        services.AddHttpClient<AuthService>();
        
        // ========== SERVICIOS ==========
        // BungieApiService - crea su propio HttpClient internamente
        services.AddSingleton<IBungieApiService, BungieApiService>();
        
        // TokenStorage - Singleton porque maneja estado persistente
        services.AddSingleton<TokenStorageService>();
        
        // AuthService - Singleton para mantener estado de autenticación
        services.AddSingleton<IAuthService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(AuthService));
            var tokenStorage = sp.GetRequiredService<TokenStorageService>();
            return new AuthService(httpClient, tokenStorage);
        });
        
        // ========== VIEWMODELS ==========
        services.AddTransient<MainViewModel>();
        
        // ========== VIEWS ==========
        services.AddTransient<MainWindow>();
    }
    
    /// <summary>
    /// Maneja el inicio de la aplicación.
    /// Resuelve y muestra la ventana principal.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Resolver la ventana principal desde el contenedor
        var mainWindow = _serviceProvider?.GetRequiredService<MainWindow>();
        
        if (mainWindow != null)
        {
            mainWindow.Show();
        }
        else
        {
            MessageBox.Show(
                "Error al inicializar la aplicación. No se pudo crear la ventana principal.",
                "Error de Inicialización",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            
            Shutdown(1);
        }
    }
    
    /// <summary>
    /// Limpieza al cerrar la aplicación.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        // Dispose del contenedor de DI
        _serviceProvider?.Dispose();
        
        base.OnExit(e);
    }
}
