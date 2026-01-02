using Avalonia.Controls;
using Avalonia.Threading;
using Traveler.Desktop.ViewModels;
using WebViewControl;

namespace Traveler.Desktop.Views;

public partial class GuardianDetailWindow : Window
{
    private WebView? _webView;

    public GuardianDetailWindow()
    {
        InitializeComponent();
        this.Opened += OnWindowOpened;
    }

    public GuardianDetailWindow(GuardianDetailViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        // Initialize WebView after window is fully loaded
        Dispatcher.UIThread.Post(async () =>
        {
            await InitializeWebViewAsync();
        });
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            // Get the WebView container from XAML
            var container = this.FindControl<Border>("WebViewContainer");
            var loadingPlaceholder = this.FindControl<Border>("LoadingPlaceholder");

            if (container == null) return;

            // Create the WebView control
            _webView = new WebView
            {
                // Disable context menu for cleaner look
                DisableBuiltinContextMenus = true
            };

            // Get the URL from ViewModel
            if (DataContext is GuardianDetailViewModel vm)
            {
                // Navigate to the 3D viewer URL
                _webView.Address = vm.Target3DUrl;
                
                // Hide loading placeholder once loaded
                _webView.Navigated += (s, args) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (loadingPlaceholder != null)
                            loadingPlaceholder.IsVisible = false;
                    });
                };
            }

            // Add WebView to the container
            container.Child = _webView;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GuardianDetailWindow] WebView initialization failed: {ex.Message}");
        }
    }
}
