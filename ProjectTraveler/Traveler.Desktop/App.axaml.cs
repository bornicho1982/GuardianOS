using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Traveler.Desktop.ViewModels;
using Traveler.Desktop.Views;
using Traveler.Data.Auth;
using Traveler.Data.Manifest;
using Traveler.Data.Services.Inventory;

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
            // Composition Root
            var authService = new BungieAuthService();
            var manifestService = new ManifestDatabase();
            var inventoryService = new InventoryService(authService, manifestService);
            var smartMoveService = new SmartMoveService(inventoryService);
            
            var inventoryVm = new InventoryViewModel(inventoryService, smartMoveService);
            var dashboardVm = new DashboardViewModel(inventoryVm);

            desktop.MainWindow = new MainWindow
            {
                DataContext = dashboardVm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
