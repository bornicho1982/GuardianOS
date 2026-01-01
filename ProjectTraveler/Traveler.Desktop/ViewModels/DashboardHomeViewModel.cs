using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Traveler.Core.Models;
using Traveler.Core.Services;
using Traveler.Data.Auth;
using Traveler.Core.Interfaces;
using Avalonia.Media;

namespace Traveler.Desktop.ViewModels
{
    public partial class DashboardHomeViewModel : ObservableObject
    {
        // ===== COLLECTIONS =====
        [ObservableProperty]
        private ObservableCollection<CharacterInfo> _characters = new();

        [ObservableProperty]
        private ObservableCollection<Currency> _currencies = new();

        [ObservableProperty]
        private ObservableCollection<DailyRotation> _dailyActivities = new();

        [ObservableProperty]
        private ObservableCollection<VendorItem> _featuredVendors = new();

        // ===== VAULT STATUS =====
        [ObservableProperty]
        private double _vaultSpaceUsed;

        [ObservableProperty]
        private double _vaultSpaceTotal = 600;

        [ObservableProperty]
        private string _vaultSpaceText = "-- / 600";

        [ObservableProperty]
        private IBrush _vaultSpaceColor = SolidColorBrush.Parse("#4CAF50");

        // ===== POSTMASTER ALERTS =====
        [ObservableProperty]
        private bool _isPostmasterWarning;

        [ObservableProperty]
        private string _postmasterStatusText = string.Empty;

        [ObservableProperty]
        private string _postmasterIconPath = string.Empty;

        // ===== USER SESSION =====
        [ObservableProperty]
        private bool _isLoggedIn;

        [ObservableProperty]
        private string _userName = string.Empty;

        [ObservableProperty]
        private string _userAvatarPath = string.Empty;

        [ObservableProperty]
        private string _userTitle = string.Empty;

        // ===== LOADING STATES =====
        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _loadingMessage = string.Empty;

        // ===== WEEKLY RESET COUNTDOWN =====
        [ObservableProperty]
        private string _weeklyResetCountdown = "Calculating...";

        [ObservableProperty]
        private string _dailyResetCountdown = "Calculating...";

        [ObservableProperty]
        private double _weeklyResetProgress = 0;

        private System.Timers.Timer? _countdownTimer;

        // ===== COMMANDS =====
        public IAsyncRelayCommand LoginCommand { get; }
        public IRelayCommand LogoutCommand { get; }
        public IAsyncRelayCommand RefreshCommand { get; }

        // ===== SERVICES =====
        private readonly BungieAuthService _authService;
        private readonly IInventoryService _inventoryService;

        /// <summary>
        /// Constructor with Dependency Injection - the ONLY constructor that should be used
        /// </summary>
        public DashboardHomeViewModel(BungieAuthService authService, IInventoryService inventoryService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));

            // Initialize Commands
            LoginCommand = new AsyncRelayCommand(LoginAsync);
            LogoutCommand = new RelayCommand(ExecuteLogout);
            RefreshCommand = new AsyncRelayCommand(RefreshAllDataAsync);

            // Initialize with default/placeholder data
            InitializeDefaultState();
            
            // Start countdown timer
            StartCountdownTimer();

            // If already authenticated, load real data
            if (_authService.IsAuthenticated)
            {
                IsLoggedIn = true;
                UserName = _authService.DisplayName ?? "Guardian";
                UserTitle = "Online";
                _ = RefreshAllDataAsync();
            }
        }

        /// <summary>
        /// Starts the countdown timer for weekly/daily resets (updates every second)
        /// </summary>
        private void StartCountdownTimer()
        {
            UpdateCountdowns(); // Initial calculation
            
            _countdownTimer = new System.Timers.Timer(1000); // Update every second
            _countdownTimer.Elapsed += (s, e) => 
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateCountdowns());
            };
            _countdownTimer.Start();
        }

        /// <summary>
        /// Calculates time until next weekly (Tuesday 17:00 UTC) and daily reset (17:00 UTC)
        /// </summary>
        private void UpdateCountdowns()
        {
            var now = DateTime.UtcNow;
            
            // Daily reset: 17:00 UTC every day
            var nextDailyReset = now.Date.AddHours(17);
            if (now.Hour >= 17)
                nextDailyReset = nextDailyReset.AddDays(1);
            
            var dailyDiff = nextDailyReset - now;
            DailyResetCountdown = $"{dailyDiff.Hours:D2}h {dailyDiff.Minutes:D2}m {dailyDiff.Seconds:D2}s";
            
            // Weekly reset: Tuesday 17:00 UTC
            var daysUntilTuesday = ((int)DayOfWeek.Tuesday - (int)now.DayOfWeek + 7) % 7;
            if (daysUntilTuesday == 0 && now.Hour >= 17)
                daysUntilTuesday = 7;
            
            var nextWeeklyReset = now.Date.AddDays(daysUntilTuesday).AddHours(17);
            var weeklyDiff = nextWeeklyReset - now;
            
            if (weeklyDiff.Days > 0)
                WeeklyResetCountdown = $"{weeklyDiff.Days}d {weeklyDiff.Hours}h {weeklyDiff.Minutes}m";
            else
                WeeklyResetCountdown = $"{weeklyDiff.Hours}h {weeklyDiff.Minutes}m {weeklyDiff.Seconds}s";
            
            // Progress bar (0-100, where 100 = just reset, 0 = about to reset)
            WeeklyResetProgress = Math.Max(0, (weeklyDiff.TotalDays / 7.0) * 100);
        }

        /// <summary>
        /// Sets default visual state while not logged in
        /// </summary>
        private void InitializeDefaultState()
        {
            // Vault defaults
            VaultSpaceUsed = 0;
            VaultSpaceTotal = 700; // Updated in The Final Shape (June 2024)
            VaultSpaceText = "-- / 700";
            VaultSpaceColor = SolidColorBrush.Parse("#4CAF50");

            // Postmaster (hidden by default)
            IsPostmasterWarning = false;

            // Daily Activities (always show even when logged out)
            DailyActivities = new ObservableCollection<DailyRotation>
            {
                new DailyRotation
                {
                    ActivityName = "Bunker E15",
                    ActivityType = "Legend Lost Sector",
                    Reward = "Exotic Helmet",
                    Modifiers = new List<string> { "Arc Burn", "Barrier Champions" }
                },
                new DailyRotation
                {
                    ActivityName = "The Disgraced",
                    ActivityType = "Nightfall: Grandmaster",
                    Reward = "Wendigo GL3 (Adept)",
                    Modifiers = new List<string> { "Solar Burn", "Match Game" }
                },
                new DailyRotation
                {
                    ActivityName = "Last Wish",
                    ActivityType = "Featured Raid",
                    Reward = "Pinnacle Gear",
                    Modifiers = new List<string> { "Contest Mode Active" }
                }
            };

            // Featured Vendors (static for now - TODO: API integration)
            FeaturedVendors = new ObservableCollection<VendorItem>
            {
                new VendorItem 
                { 
                    VendorName = "Xûr", 
                    Status = "Location Unknown", 
                    VendorIcon = "/common/destiny2_content/icons/801bc8e189a6f92e88ed3d4d54266f99.jpg" 
                },
                new VendorItem 
                { 
                    VendorName = "Ada-1", 
                    Status = "Tower, Annex", 
                    VendorIcon = "/common/destiny2_content/icons/7dc93a4461771f9d28daf0b89ed27480.png" 
                },
                new VendorItem 
                { 
                    VendorName = "Banshee-44", 
                    Status = "Tower, Courtyard", 
                    VendorIcon = "/common/destiny2_content/icons/4bf99bb3d3e9368d12c5da6ea60c56ec.png" 
                }
            };

            // Default currencies (empty until login)
            Currencies = new ObservableCollection<Currency>
            {
                new Currency { Name = "Glimmer", Quantity = 0, Icon = "/common/destiny2_content/icons/6b1702878985223049da03c27e49ba3f.png" },
                new Currency { Name = "Legendary Shards", Quantity = 0, Icon = "/common/destiny2_content/icons/5cebde6dd0315a061348b4a4e444762c.png" },
                new Currency { Name = "Bright Dust", Quantity = 0, Icon = "/common/destiny2_content/icons/d9254c0e5568f65fa48566cf5bad26e5.png" },
                new Currency { Name = "Silver", Quantity = 0, Icon = "/common/destiny2_content/icons/865c34cb24249a200702df9c4d920252.png" }
            };
        }

        /// <summary>
        /// Executes full OAuth login flow
        /// </summary>
        private async Task LoginAsync()
        {
            try
            {
                IsLoading = true;
                LoadingMessage = "Opening Bungie.net...";

                var success = await _authService.LoginAsync();
                
                if (success)
                {
                    IsLoggedIn = true;
                    UserName = _authService.DisplayName ?? "Guardian";
                    UserTitle = "Online";
                    
                    await RefreshAllDataAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DashboardVM] Login failed: {ex.Message}");
                LoadingMessage = $"Login failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                LoadingMessage = string.Empty;
            }
        }

        /// <summary>
        /// Refreshes all dashboard data from Bungie API
        /// </summary>
        private async Task RefreshAllDataAsync()
        {
            if (!_authService.IsAuthenticated)
            {
                System.Diagnostics.Debug.WriteLine("[DashboardVM] Not authenticated, skipping refresh");
                return;
            }

            try
            {
                IsLoading = true;
                LoadingMessage = "Syncing with Bungie...";

                // 1. Characters
                LoadingMessage = "Loading characters...";
                var characters = await _inventoryService.GetCurrentUserCharactersAsync();
                Characters.Clear();
                foreach (var c in characters)
                {
                    Characters.Add(c);
                }

                // 2. User Avatar (use first character's emblem)
                if (characters.Count > 0)
                {
                    UserAvatarPath = characters[0].EmblemPath;
                }

                // 3. Vault Status (Real API)
                LoadingMessage = "Checking vault...";
                var (vaultUsed, vaultTotal) = await _inventoryService.GetVaultStatusAsync();
                VaultSpaceUsed = vaultUsed;
                VaultSpaceTotal = vaultTotal;
                VaultSpaceText = $"{vaultUsed} / {vaultTotal}";
                
                // Color logic: Red if over capacity, Yellow if near full (90%), Orange if getting full (95%), Green otherwise
                if (vaultUsed > vaultTotal)
                    VaultSpaceColor = SolidColorBrush.Parse("#FF5555"); // RED - OVER CAPACITY!
                else if (vaultUsed > 665)  // >95% full
                    VaultSpaceColor = SolidColorBrush.Parse("#FF9800"); // Orange - Critical
                else if (vaultUsed > 630)  // >90% full
                    VaultSpaceColor = SolidColorBrush.Parse("#FFC107"); // Yellow - Warning
                else
                    VaultSpaceColor = SolidColorBrush.Parse("#4CAF50"); // Green - OK

                // 4. Postmaster Check (Real API)
                if (characters.Count > 0)
                {
                    LoadingMessage = "Checking postmaster...";
                    var postmasterCount = await _inventoryService.GetPostmasterCountAsync(characters[0].CharacterId);
                    if (postmasterCount >= 18) // Postmaster has 21 slots max - warn at 18
                    {
                        IsPostmasterWarning = true;
                        PostmasterStatusText = $"{characters[0].ClassName}'s Postmaster has {postmasterCount} items!";
                        PostmasterIconPath = "/common/destiny2_content/icons/25d691a5470d0ae966236b281bf2ab8e.png";
                    }
                    else
                    {
                        IsPostmasterWarning = false;
                    }
                }

                // 5. Update Currencies (Real API - Component 103)
                LoadingMessage = "Loading currencies...";
                var currencyData = await _inventoryService.GetCurrenciesAsync();
                
                // Currency hash -> (Name, Icon) mapping
                var currencyMap = new Dictionary<uint, (string Name, string Icon)>
                {
                    { 3159615086, ("Glimmer", "/common/destiny2_content/icons/6b1702878985223049da03c27e49ba3f.png") },
                    { 1022552290, ("Legendary Shards", "/common/destiny2_content/icons/5cebde6dd0315a061348b4a4e444762c.png") },
                    { 2817410917, ("Bright Dust", "/common/destiny2_content/icons/d9254c0e5568f65fa48566cf5bad26e5.png") },
                    { 3147280338, ("Silver", "/common/destiny2_content/icons/865c34cb24249a200702df9c4d920252.png") }
                };
                
                Currencies.Clear();
                foreach (var (hash, meta) in currencyMap)
                {
                    var quantity = currencyData.ContainsKey(hash) ? currencyData[hash] : 0;
                    Currencies.Add(new Currency { Name = meta.Name, Quantity = (int)quantity, Icon = meta.Icon });
                }

                System.Diagnostics.Debug.WriteLine($"[DashboardVM] Refresh complete. {Characters.Count} characters, Vault: {vaultUsed}/{vaultTotal}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DashboardVM] Refresh failed: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                LoadingMessage = string.Empty;
            }
        }

        /// <summary>
        /// Logs out and clears all user data
        /// </summary>
        private void ExecuteLogout()
        {
            IsLoggedIn = false;
            UserName = string.Empty;
            UserAvatarPath = string.Empty;
            UserTitle = string.Empty;
            Characters.Clear();
            
            // Reset to default state
            InitializeDefaultState();
        }
    }

    /// <summary>
    /// Represents a currency/resource in Destiny 2
    /// </summary>
    public class Currency
    {
        public string Name { get; set; } = string.Empty;
        public long Quantity { get; set; }
        public string Icon { get; set; } = string.Empty;
    }
}

