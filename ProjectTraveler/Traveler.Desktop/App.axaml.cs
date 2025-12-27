using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Traveler.Desktop.ViewModels;
using Traveler.Desktop.Views;
using Traveler.Data.Auth;
using Traveler.Data.Manifest;
using Traveler.Data.Services.Inventory;
using Traveler.Data.Services.Triumphs;
using Traveler.Data.Services.Vendors;
using Traveler.Data.Services.Settings;
using Traveler.Core.Optimization;

namespace Traveler.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // ===== COMPOSITION ROOT =====
            
            // Core Services
            var authService = new BungieAuthService();
            var manifestService = new ManifestDatabase();
            var settingsService = new SettingsService();
            _ = settingsService.LoadAsync(); // Fire and forget load
            
            // Inventory Layer
            var inventoryService = new InventoryService(authService, manifestService);
            var smartMoveService = new SmartMoveService(inventoryService);
            
            // AI & Optimization
            var buildCopilotService = new Traveler.AI.Services.BuildCopilotService(inventoryService, manifestService);
            var optimizationSolver = new OptimizationSolver();
            
            // Triumphs & Vendors
            var triumphsService = new TriumphsService(manifestService);
            var vendorsService = new VendorsService(manifestService, inventoryService);
            
            // ===== VIEW MODELS =====
            
            // Main Views
            var inventoryVm = new InventoryViewModel(inventoryService, smartMoveService);
            var organizerVm = new OrganizerViewModel(inventoryService);
            
            // Build Constructor
            var loadoutsVm = new LoadoutsViewModel();
            var loadoutEditorVm = new LoadoutEditorViewModel(optimizationSolver, buildCopilotService, inventoryService);
            var buildVm = new BuildArchitectViewModel(buildCopilotService);
            
            // Content Views
            var vendorsVm = new VendorsViewModel(vendorsService);
            var triumphsVm = new TriumphsViewModel(triumphsService);
            
            // Settings
            var settingsVm = new SettingsViewModel(settingsService);

            // Dashboard (Navigation Shell)
            var dashboardVm = new DashboardViewModel(
                inventoryVm, 
                loadoutsVm, 
                buildVm, 
                vendorsVm, 
                triumphsVm, 
                organizerVm, 
                settingsVm);

            desktop.MainWindow = new MainWindow
            {
                DataContext = dashboardVm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
