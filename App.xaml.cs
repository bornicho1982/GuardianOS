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
    /// Servidor proxy local para el visor 3D.
    /// </summary>
    private LocalProxyServer? _proxyServer;
    
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
        // Configurar manejo global de excepciones
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        // Configurar servicios
        var services = new ServiceCollection();
        ConfigureServices(services);
        
        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash(e.Exception);
        e.Handled = true; // Intentar evitar el cierre total si es posible, o al menos mostrar el error
        MessageBox.Show($"Error inesperado: {e.Exception.Message}\nVer crash.log para más detalles.");
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogCrash(ex);
            MessageBox.Show($"Error fatal (Dominio): {ex.Message}\nVer crash.log para más detalles.");
        }
    }

    private void LogCrash(Exception ex)
    {
        var error = $"[{DateTime.Now}] CRASH: {ex.Message}\nStack Trace: {ex.StackTrace}\nInner Exception: {ex.InnerException?.Message}\n--------------------------\n";
        System.IO.File.AppendAllText("crash.log", error);
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

        // Manifest Services
        services.AddSingleton<IManifestService, ManifestService>();
        services.AddSingleton<IManifestRepository, ManifestRepository>();
        
        // Gear Asset Service for 3D model data
        services.AddSingleton<GearAssetService>();
        
        // Local Proxy Server for 3D viewer
        services.AddSingleton<LocalProxyServer>();
        
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
    /// <summary>
    /// Maneja el inicio de la aplicación.
    /// Resuelve y muestra la ventana principal.
    /// </summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);
            
            // Log simple para debug
            System.IO.File.WriteAllText("start.log", "Iniciando aplicación...\n");
            
            // Start local proxy server for 3D viewer
            _proxyServer = _serviceProvider?.GetService<LocalProxyServer>();
            if (_proxyServer != null)
            {
                System.IO.File.AppendAllText("start.log", "Iniciando servidor proxy local...\n");
                await _proxyServer.StartAsync();
                System.IO.File.AppendAllText("start.log", $"Proxy iniciado en {_proxyServer.BaseUrl}\n");
            }
            
            // Resolver la ventana principal desde el contenedor
            var mainWindow = _serviceProvider?.GetRequiredService<MainWindow>();
            
            if (mainWindow != null)
            {
                System.IO.File.AppendAllText("start.log", "MainWindow resuelta correctamente. Llamando a Show().\n");
                mainWindow.Show();
            }
            else
            {
                var msg = "Error: MainWindow es null después de resolver del contenedor.";
                System.IO.File.AppendAllText("start.log", msg + "\n");
                MessageBox.Show(msg);
                Shutdown(1);
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"CRASH AL INICIO: {ex.Message}\nStack Trace: {ex.StackTrace}";
            System.IO.File.AppendAllText("start.log", errorMsg);
            MessageBox.Show($"Error fatal al iniciar: {ex.Message}");
            Shutdown(1);
        }
    }
    
    /// <summary>
    /// Limpieza al cerrar la aplicación.
    /// </summary>
    protected override async void OnExit(ExitEventArgs e)
    {
        // Stop proxy server
        if (_proxyServer != null)
        {
            await _proxyServer.StopAsync();
        }
        
        // Dispose del contenedor de DI
        _serviceProvider?.Dispose();
        
        base.OnExit(e);
    }
}
