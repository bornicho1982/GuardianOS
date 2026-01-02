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
    /// Injects JavaScript to hide navigation elements for a cleaner embedded 3D canvas experience
    /// </summary>
    private void InjectCleanupScript()
    {
        if (_webView == null) return;
        
        try
        {
            // Enhanced script to clean up ParacausalForge UI - leave only the 3D canvas
            var script = @"
                (function() {
                    // Create style element for hiding common UI elements
                    var style = document.createElement('style');
                    style.innerHTML = `
                        /* Hide headers, footers, navigation */
                        header, footer, nav, .navbar, .nav-bar, 
                        [class*='header'], [class*='footer'], [class*='nav-'],
                        [class*='menu'], [class*='sidebar'], [class*='toolbar'] {
                            display: none !important;
                        }
                        
                        /* Hide ads, social, promotional elements */
                        .ad-container, .ads, .advertisement,
                        .social, .socials, [class*='social'],
                        .share, [class*='share'], .promo, .banner,
                        [class*='cookie'], [class*='consent'] {
                            display: none !important;
                        }
                        
                        /* Make body background transparent/dark */
                        body {
                            background-color: #0D0D0D !important;
                            overflow: hidden !important;
                            margin: 0 !important;
                            padding: 0 !important;
                        }
                        
                        /* Hide any fixed positioned elements (popups, overlays) */
                        [style*='position: fixed'], [style*='position:fixed'] {
                            display: none !important;
                        }
                        
                        /* Try to make canvas full screen */
                        canvas {
                            width: 100vw !important;
                            height: 100vh !important;
                        }
                    `;
                    document.head.appendChild(style);
                    
                    // Try to click ""Hide Menu"" button if it exists on ParacausalForge
                    var hideMenuBtn = document.querySelector('[class*=""hide""], [class*=""Hide""], .hide-menu');
                    if (hideMenuBtn) {
                        hideMenuBtn.click();
                        console.log('GuardianOS: Clicked Hide Menu button');
                    }
                    
                    // Remove any floating elements
                    var floatingElements = document.querySelectorAll('[style*=""position: absolute""], [style*=""position:absolute""]');
                    floatingElements.forEach(el => {
                        if (!el.querySelector('canvas')) {
                            el.style.display = 'none';
                        }
                    });
                    
                    console.log('GuardianOS: Cleanup script executed - UI elements hidden');
                })();
            ";
            
            _webView.ExecuteScript(script);
            Console.WriteLine("[GuardianDetail] Enhanced cleanup script injected");
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
