using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Traveler.Desktop.ViewModels;
using System.Diagnostics;
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
        // Initialize WebView after window is fully loaded (small delay for stability)
        Dispatcher.UIThread.Post(async () =>
        {
            await Task.Delay(500); // Small delay for window to fully render
            InitializeWebView();
        });
    }

    private void InitializeWebView()
    {
        try
        {
            // Get the WebView container from XAML
            var container = this.FindControl<Border>("WebViewContainer");
            var loadingOverlay = this.FindControl<Border>("LoadingOverlay");

            if (container == null) 
            {
                Debug.WriteLine("[GuardianDetail] WebViewContainer not found in XAML");
                return;
            }

            Debug.WriteLine("[GuardianDetail] Creating WebView...");

            // Create the WebView control (WebViewControl-Avalonia uses CefGlue)
            _webView = new WebView
            {
                DisableBuiltinContextMenus = true // Cleaner embedded look
            };

            // Get the URL from ViewModel
            if (DataContext is GuardianDetailViewModel vm)
            {
                Debug.WriteLine($"[GuardianDetail] Navigating to: {vm.Target3DUrl}");
                
                // Set the address (triggers navigation)
                _webView.Address = vm.Target3DUrl;
                
                // Handle navigation completed to hide loading overlay
                _webView.Navigated += (s, args) =>
                {
                    Dispatcher.UIThread.Post(async () =>
                    {
                        // Hide loading overlay
                        if (loadingOverlay != null)
                        {
                            loadingOverlay.IsVisible = false;
                            Debug.WriteLine("[GuardianDetail] Navigation completed, showing WebView");
                        }
                        
                        // Wait a bit for page to render, then inject cleanup script
                        await Task.Delay(1000);
                        InjectCleanupScript();
                    });
                };
            }

            // Add WebView to the container
            container.Child = _webView;
            Debug.WriteLine("[GuardianDetail] WebView added to container successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GuardianDetail] WebView initialization failed: {ex.Message}");
            Debug.WriteLine($"[GuardianDetail] Stack trace: {ex.StackTrace}");
            
            // Show error in loading overlay
            ShowErrorInOverlay(ex.Message);
        }
    }

    private void ShowErrorInOverlay(string message)
    {
        var loadingOverlay = this.FindControl<Border>("LoadingOverlay");
        if (loadingOverlay != null)
        {
            // Find and update the text block
            foreach (var child in loadingOverlay.GetVisualDescendants())
            {
                if (child is TextBlock tb && tb.Text?.Contains("Loading") == true)
                {
                    tb.Text = $"WebView Error: {message}";
                    tb.Foreground = Avalonia.Media.Brushes.Red;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Injects JavaScript to hide navigation elements for a cleaner embedded experience
    /// </summary>
    private void InjectCleanupScript()
    {
        if (_webView == null) return;
        
        try
        {
            // Script to hide navigation bar and other UI elements on ParacausalForge
            var script = @"
                (function() {
                    // Hide navigation bar
                    var nav = document.querySelector('nav');
                    if (nav) nav.style.display = 'none';
                    
                    // Hide header if present
                    var header = document.querySelector('header');
                    if (header) header.style.display = 'none';
                    
                    // Hide footer if present
                    var footer = document.querySelector('footer');
                    if (footer) footer.style.display = 'none';
                    
                    // Try to click hide menu button if exists
                    var hideBtn = document.querySelector('.hide-menu, [class*=""hide""]');
                    if (hideBtn) hideBtn.click();
                    
                    console.log('GuardianOS: Navigation elements hidden');
                })();
            ";
            
            _webView.ExecuteScript(script);
            Debug.WriteLine("[GuardianDetail] Cleanup script injected");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GuardianDetail] Script execution failed: {ex.Message}");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        // Clean up WebView when window closes
        if (_webView != null)
        {
            try
            {
                _webView.Dispose();
                _webView = null;
                Debug.WriteLine("[GuardianDetail] WebView disposed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GuardianDetail] WebView dispose failed: {ex.Message}");
            }
        }
        base.OnClosed(e);
    }
}
