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
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string viewerPath = Path.Combine(basePath, "Assets", "3DViewer", "index.html");
            
            Debug.WriteLine($"[3DViewer] Loading from: {viewerPath}");
            
            if (File.Exists(viewerPath))
            {
                // Navigate to local HTML file
                Viewer3D.CoreWebView2.Navigate(new Uri(viewerPath).AbsoluteUri);
                
                // Subscribe to navigation completed to send data
                Viewer3D.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                
                _webViewInitialized = true;
                Debug.WriteLine("[3DViewer] WebView2 initialized successfully");
            }
            else
            {
                Debug.WriteLine($"[3DViewer] ERROR: File not found: {viewerPath}");
            }
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
            return;
        }

        Debug.WriteLine("[3DViewer] Navigation completed, sending guardian data...");
        
        // Get ViewModel data and send to JavaScript
        if (DataContext is CharacterDetailViewModel viewModel)
        {
            SendGuardianDataToViewer(viewModel);
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
