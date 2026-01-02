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
    /// Injects JavaScript to hide Bungie.net navigation elements for a cleaner embedded 3D viewer experience
    /// </summary>
    private void InjectCleanupScript()
    {
        if (_webView == null) return;
        
        try
        {
            // Enhanced script to clean up Bungie.net Armory UI - leave only the 3D viewer
            var script = @"
                (function() {
                    // Create style element for hiding Bungie.net UI elements
                    var style = document.createElement('style');
                    style.innerHTML = `
                        /* Hide Bungie.net global navigation header */
                        #bungie-header, .bungie-header, header, 
                        [class*='GlobalHeader'], [class*='global-header'],
                        [class*='bnet-header'], [class*='bungie-nav'] {
                            display: none !important;
                        }
                        
                        /* Hide Bungie.net footer */
                        footer, #bungie-footer, .bungie-footer,
                        [class*='GlobalFooter'], [class*='global-footer'] {
                            display: none !important;
                        }
                        
                        /* Hide navigation bars and menus */
                        nav, .nav, .navbar, .navigation,
                        [class*='NavigationContainer'], [class*='nav-bar'],
                        [class*='Breadcrumb'], [class*='breadcrumb'] {
                            display: none !important;
                        }
                        
                        /* Hide sidebars and action buttons */
                        [class*='sidebar'], [class*='Sidebar'],
                        [class*='action-button'], [class*='ActionButton'],
                        [class*='share-button'], [class*='ShareButton'] {
                            display: none !important;
                        }
                        
                        /* Hide promotional/marketing content */
                        [class*='promotion'], [class*='marketing'],
                        [class*='cookie'], [class*='consent'],
                        [class*='banner'], [class*='Banner'] {
                            display: none !important;
                        }
                        
                        /* Make body dark background */
                        body {
                            background-color: #0D0D0D !important;
                            overflow: hidden !important;
                        }
                        
                        /* Hide any fixed positioned overlays */
                        [style*='position: fixed'], [style*='position:fixed'] {
                            display: none !important;
                        }
                        
                        /* Make the 3D canvas container fill the viewport */
                        [class*='armory'], [class*='Armory'],
                        [class*='character-viewer'], [class*='CharacterViewer'],
                        [class*='gear-viewer'], [class*='GearViewer'] {
                            position: fixed !important;
                            top: 0 !important;
                            left: 0 !important;
                            width: 100vw !important;
                            height: 100vh !important;
                            z-index: 9999 !important;
                        }
                        
                        /* Full screen canvas */
                        canvas {
                            width: 100vw !important;
                            height: 100vh !important;
                        }
                    `;
                    document.head.appendChild(style);
                    
                    console.log('GuardianOS: Bungie.net Armory cleanup script executed');
                })();
            ";
            
            _webView.ExecuteScript(script);
            Console.WriteLine("[GuardianDetail] Bungie.net Armory cleanup script injected");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GuardianDetail] Script execution failed: {ex.Message}");
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
