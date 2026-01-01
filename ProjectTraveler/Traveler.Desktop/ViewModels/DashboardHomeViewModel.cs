using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Traveler.Core.Models;
using Traveler.Core.Services;
using Avalonia.Media;

namespace Traveler.Desktop.ViewModels
{
    public partial class DashboardHomeViewModel : ObservableObject
    {
        // Colecciones de Datos
        [ObservableProperty]
        private ObservableCollection<CharacterInfo> _characters;

        [ObservableProperty]
        private ObservableCollection<Currency> _currencies;

        [ObservableProperty]
        private ObservableCollection<DailyRotation> _dailyActivities;

        [ObservableProperty]
        private ObservableCollection<VendorItem> _featuredVendors;

        // Estado del Vault
        [ObservableProperty]
        private double _vaultSpaceUsed;

        [ObservableProperty]
        private double _vaultSpaceTotal;

        [ObservableProperty]
        private string _vaultSpaceText;

        [ObservableProperty]
        private IBrush _vaultSpaceColor;

        // Estado del Postmaster (Alertas)
        [ObservableProperty]
        private bool _isPostmasterWarning;

        [ObservableProperty]
        private string _postmasterStatusText;

        [ObservableProperty]
        private string _postmasterIconPath;

        // Estado de Sesión y Perfil
        [ObservableProperty]
        private bool _isLoggedIn;

        [ObservableProperty]
        private string _userName;

        [ObservableProperty]
        private string _userAvatarPath;

        [ObservableProperty]
        private string _userTitle;

        public IRelayCommand LoginCommand { get; }
        public IRelayCommand LogoutCommand { get; }

        public DashboardHomeViewModel()
        {
            // En una app real, inyectaríamos el servicio de Auth aquí
            LoginCommand = new RelayCommand(SimulateLogin);
            LogoutCommand = new RelayCommand(SimulateLogout);
            LoadMockData();
        }

        private void SimulateLogin()
        {
            IsLoggedIn = true;
            UserName = "Guardian#1234";
            UserAvatarPath = "/common/destiny2_content/icons/87dfeb3fb385a4a28966276707373972.jpg"; // Placeholder Avatar
            UserTitle = "Paragon";
        }

        private void SimulateLogout()
        {
            IsLoggedIn = false;
            UserName = string.Empty;
            UserAvatarPath = string.Empty;
            UserTitle = string.Empty;
        }

        private void LoadMockData()
        {
            // 1. Personajes (Squad View)
            Characters = new ObservableCollection<CharacterInfo>
            {
                new CharacterInfo
                {
                    CharacterId = "1", ClassType = "Warlock", RaceName = "Exo",
                    LightLevel = 1825, BasePowerLevel = 1810, ArtifactBonus = 15,
                    EmblemBackgroundPath = "/common/destiny2_content/icons/f5304fcd2796417730e75525048d8b33.jpg",
                    Title = "Godslayer",
                    SeasonRank = 95, SeasonProgressPercent = 0.85, SeasonRewardIcon = "/common/destiny2_content/icons/SeasonRewardPlaceholder.jpg"
                },
                new CharacterInfo
                {
                    CharacterId = "2", ClassType = "Hunter", RaceName = "Human",
                    LightLevel = 1822, BasePowerLevel = 1807, ArtifactBonus = 15,
                    EmblemBackgroundPath = "/common/destiny2_content/icons/87dfeb3fb385a4a28966276707373972.jpg",
                    Title = "Revenant",
                    SeasonRank = 95, SeasonProgressPercent = 0.85, SeasonRewardIcon = ""
                },
                new CharacterInfo
                {
                    CharacterId = "3", ClassType = "Titan", RaceName = "Awoken",
                    LightLevel = 1815, BasePowerLevel = 1800, ArtifactBonus = 15,
                    EmblemBackgroundPath = "/common/destiny2_content/icons/2a6cb5690327ba290eb9e355e4b77943.jpg",
                    Title = "Dredgen",
                    SeasonRank = 95, SeasonProgressPercent = 0.85, SeasonRewardIcon = ""
                }
            };

            // 2. Actividades Diarias
            DailyActivities = new ObservableCollection<DailyRotation>
            {
                new DailyRotation
                {
                    ActivityName = "Chamber of Starlight",
                    ActivityType = "Legend Lost Sector",
                    Reward = "Exotic Arms",
                    Modifiers = new List<string> { "Solar Burn", "Champions: Unstoppable" }
                },
                new DailyRotation
                {
                    ActivityName = "The Lightblade",
                    ActivityType = "Grandmaster Nightfall",
                    Reward = "Wendigo GL3 (Adept)",
                    Modifiers = new List<string> { "Arc Burn", "Champions: Barrier" }
                }
            };

            // 3. Vendedores Destacados
            FeaturedVendors = new ObservableCollection<VendorItem>
            {
                new VendorItem { VendorName = "Ada-1", Status = "Selling Powerful Friends", VendorIcon = "/common/destiny2_content/icons/Ada1.jpg" },
                new VendorItem { VendorName = "Banshee-44", Status = "God Roll Funnelweb", VendorIcon = "/common/destiny2_content/icons/Banshee44.jpg" },
                new VendorItem { VendorName = "Xûr", Status = "At Watcher's Grave", VendorIcon = "/common/destiny2_content/icons/Xur.jpg" }
            };

            // 4. Divisas
            Currencies = new ObservableCollection<Currency>
            {
                new Currency { Name = "Glimmer", Quantity = 250000, Icon = "/common/destiny2_content/icons/6b1702878985223049da03c27e49ba3f.png" },
                new Currency { Name = "Ascendant Shard", Quantity = 10, Icon = "/common/destiny2_content/icons/12b28d689b93220336214376c9dbd390.png" },
                new Currency { Name = "Enhancement Core", Quantity = 450, Icon = "/common/destiny2_content/icons/3973907a3469d80d2699f7d23d839fa3.png" },
                new Currency { Name = "Silver", Quantity = 1500, Icon = "/common/destiny2_content/icons/865c34cb24249a200702df9c4d920252.png" }
            };

            // 4. Estado de Bóveda y Postmaster
            VaultSpaceUsed = 580;
            VaultSpaceTotal = 600;
            VaultSpaceText = $"{VaultSpaceUsed} / {VaultSpaceTotal}";
            VaultSpaceColor = SolidColorBrush.Parse(VaultSpaceUsed > 550 ? "#FFC107" : "#4CAF50"); // Amarillo si está lleno

            // Simulación de Postmaster lleno
            IsPostmasterWarning = true; 
            PostmasterStatusText = "Postmaster is overflowing on Hunter!";
            PostmasterIconPath = "/common/destiny2_content/icons/25d691a5470d0ae966236b281bf2ab8e.png"; // Kadi 55-30 or generic Engram glyph
        }
    }

    public class Currency
    {
        public string Name { get; set; }
        public long Quantity { get; set; }
        public string Icon { get; set; }
    }
}
