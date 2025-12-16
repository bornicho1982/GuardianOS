using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using GuardianOS.Models;
using GuardianOS.ViewModels;
using Microsoft.Web.WebView2.Core;

namespace GuardianOS.Views;

public partial class CharacterDetailView : UserControl
{
    private bool _webViewInitialized = false;

    public CharacterDetailView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_webViewInitialized) return;

        try
        {
            // Initialize WebView2
            await Viewer3D.EnsureCoreWebView2Async();
            
            // Configure WebView2 settings
            Viewer3D.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            Viewer3D.CoreWebView2.Settings.AreDevToolsEnabled = true; // Enable for debugging
            Viewer3D.CoreWebView2.Settings.IsZoomControlEnabled = false;
            
            // Get the path to the 3D viewer HTML
            // POC: Load D2Foundry directly as requested
            string d2FoundryUrl = Services.ThreeJsBridge.GetD2FoundryBaseUrl();
            Debug.WriteLine($"[3DViewer] Loading D2Foundry POC: {d2FoundryUrl}");
            
            Viewer3D.CoreWebView2.Navigate(d2FoundryUrl);
            
            // Subscribe to navigation completed to handle loading state
            Viewer3D.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
            
            _webViewInitialized = true;
            Debug.WriteLine("[3DViewer] WebView2 initialized successfully");

            /* Local viewer code for future use
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string viewerPath = Path.Combine(basePath, "Assets", "3DViewer", "index.html");
            if (File.Exists(viewerPath)) { ... }
            */
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[3DViewer] ERROR initializing WebView2: {ex.Message}");
        }
    }

    private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            Debug.WriteLine($"[3DViewer] Navigation failed: {e.WebErrorStatus}");
            
            // Update loading state via ViewModel
            if (DataContext is CharacterDetailViewModel viewModel)
            {
                viewModel.IsWebViewLoading = false;
                viewModel.UseStaticImage = true;
            }
            return;
        }

        Debug.WriteLine("[3DViewer] Navigation completed, sending guardian data...");
        
        // Update loading state
        if (DataContext is CharacterDetailViewModel vm)
        {
            vm.IsWebViewLoading = false;
            vm.IsWebViewReady = true;
        }
        
        // Get ViewModel data and send to JavaScript
        if (DataContext is CharacterDetailViewModel viewModel2)
        {
            SendGuardianDataToViewer(viewModel2);
        }
    }

    /// <summary>
    /// Load D2Foundry.gg as proof of concept (external WebGL renderer)
    /// </summary>
    public async Task LoadD2FoundryPOC()
    {
        try
        {
            if (!_webViewInitialized)
            {
                await Viewer3D.EnsureCoreWebView2Async();
                _webViewInitialized = true;
            }
            
            // Navigate to D2Foundry as POC
            var foundryUrl = Services.ThreeJsBridge.GetD2FoundryBaseUrl();
            Debug.WriteLine($"[3DViewer] Loading D2Foundry POC: {foundryUrl}");
            Viewer3D.CoreWebView2.Navigate(foundryUrl);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[3DViewer] Error loading D2Foundry: {ex.Message}");
        }
    }

    /// <summary>
    /// Send guardian equipment data to the JavaScript viewer
    /// </summary>
    private async void SendGuardianDataToViewer(CharacterDetailViewModel viewModel)
    {
        try
        {
            // Wait for equipment to finish loading (up to 10 seconds)
            var timeout = DateTime.Now.AddSeconds(10);
            while (viewModel.IsLoadingEquipment && DateTime.Now < timeout)
            {
                await Task.Delay(100);
            }
            
            // Small additional delay for UI update
            await Task.Delay(200);
            
            Debug.WriteLine($"[3DViewer] Equipment loaded. Helmet shader: {viewModel.Helmet?.ShaderHash}");
            
            // Collect item hashes and shader hashes from equipped items
            var itemHashes = new List<long>();
            var shaderHashes = new List<long>();
            var ornamentHashes = new List<long>();
            
            void AddItem(InventoryItem? item)
            {
                if (item == null) return;
                itemHashes.Add(item.ItemHash);
                shaderHashes.Add(item.ShaderHash.HasValue ? item.ShaderHash.Value : 0);
                ornamentHashes.Add(item.OrnamentHash.HasValue ? item.OrnamentHash.Value : 0);
            }
            
            AddItem(viewModel.Helmet);
            AddItem(viewModel.Gauntlets);
            AddItem(viewModel.ChestArmor);
            AddItem(viewModel.LegArmor);
            AddItem(viewModel.ClassItem);
                
            Debug.WriteLine($"[3DViewer] Found {itemHashes.Count} items with {shaderHashes.Count(s => s > 0)} shaders, {ornamentHashes.Count(o => o > 0)} ornaments");

            // Create config for JavaScript
            var config = new
            {
                action = "loadGuardian",
                config = new
                {
                    itemHashes = itemHashes,
                    shaderHashes = shaderHashes,
                    ornamentHashes = ornamentHashes,
                    classType = viewModel.Character?.ClassType ?? 0,
                    isFemale = viewModel.Character?.GenderType == 1,
                    apiKey = Core.Constants.BUNGIE_API_KEY
                }
            };

            string jsonConfig = JsonSerializer.Serialize(config);
            Debug.WriteLine($"[3DViewer] Sending config: {jsonConfig}");

            // Send message to JavaScript
            Viewer3D.CoreWebView2.PostWebMessageAsJson(jsonConfig);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[3DViewer] ERROR sending data: {ex.Message}");
        }
    }
}
