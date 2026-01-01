using System.Collections.ObjectModel;
using System.Reactive;
using Avalonia.Media;
using ReactiveUI;
using Traveler.Core.Models;
using Traveler.Core.Interfaces;
using Traveler.Core.Services;

namespace Traveler.Desktop.ViewModels;

public class DashboardHomeViewModel : ViewModelBase
{
    private ObservableCollection<CharacterInfo> _characters;
    private ObservableCollection<Currency> _currencies;
    private ObservableCollection<DailyRotation> _dailyActivities;
    
    private double _vaultSpaceUsed;
    private double _vaultSpaceTotal;
    private string _vaultSpaceText;
    private IBrush _vaultSpaceColor;
    private bool _isLoggedIn;
    private bool _isPostmasterWarning;
    private string _postmasterStatusText;

    public ObservableCollection<CharacterInfo> Characters
    {
        get => _characters;
        set => this.RaiseAndSetIfChanged(ref _characters, value);
    }

    public ObservableCollection<Currency> Currencies
    {
        get => _currencies;
        set => this.RaiseAndSetIfChanged(ref _currencies, value);
    }

    public ObservableCollection<DailyRotation> DailyActivities
    {
        get => _dailyActivities;
        set => this.RaiseAndSetIfChanged(ref _dailyActivities, value);
    }

    public double VaultSpaceUsed
    {
        get => _vaultSpaceUsed;
        set => this.RaiseAndSetIfChanged(ref _vaultSpaceUsed, value);
    }

    public double VaultSpaceTotal
    {
        get => _vaultSpaceTotal;
        set => this.RaiseAndSetIfChanged(ref _vaultSpaceTotal, value);
    }

    public string VaultSpaceText
    {
        get => _vaultSpaceText;
        set => this.RaiseAndSetIfChanged(ref _vaultSpaceText, value);
    }

    public IBrush VaultSpaceColor
    {
        get => _vaultSpaceColor;
        set => this.RaiseAndSetIfChanged(ref _vaultSpaceColor, value);
    }

    // Session State
    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        set => this.RaiseAndSetIfChanged(ref _isLoggedIn, value);
    }

    public ReactiveCommand<Unit, Unit> LoginCommand { get; }

    // Alerts
    public bool IsPostmasterWarning
    {
        get => _isPostmasterWarning;
        set => this.RaiseAndSetIfChanged(ref _isPostmasterWarning, value);
    }

    public string PostmasterStatusText
    {
        get => _postmasterStatusText;
        set => this.RaiseAndSetIfChanged(ref _postmasterStatusText, value);
    }

    public DashboardHomeViewModel()
    {
        // Commands
        LoginCommand = ReactiveCommand.Create(() => 
        {
            IsLoggedIn = true;
        });

        // Mock Data
        LoadMockData();
    }

    private void LoadMockData()
    {
        // 1. Session (Start as logged out for demo, or logged in as preferred?)
        // User requested button to show if !IsLoggedIn. Let's start as false to show off UI.
        IsLoggedIn = false; 

        // 2. Mock Characters (Squad View)
        Characters = new ObservableCollection<CharacterInfo>
        {
            new CharacterInfo
            {
                CharacterId = 1,
                ClassName = "Warlock",
                RaceName = "Exo",
                LightLevel = 1821,
                BasePowerLevel = 1810,
                ArtifactBonus = 11,
                EmblemBackgroundPath = "/common/destiny2_content/icons/f5304fcd2796417730e75525048d8b33.jpg", 
                Title = "Godslayer"
            },
            new CharacterInfo
            {
                CharacterId = 2,
                ClassName = "Hunter",
                RaceName = "Human",
                LightLevel = 1819,
                BasePowerLevel = 1809,
                ArtifactBonus = 10,
                EmblemBackgroundPath = "/common/destiny2_content/icons/87dfeb3fb385a4a28966276707373972.jpg", 
                Title = "Revenant"
            },
            new CharacterInfo
            {
                CharacterId = 3,
                ClassName = "Titan",
                RaceName = "Awoken",
                LightLevel = 1815,
                BasePowerLevel = 1805,
                ArtifactBonus = 10,
                EmblemBackgroundPath = "/common/destiny2_content/icons/2a6cb5690327ba290eb9e355e4b77943.jpg", 
                Title = "Dredgen"
            }
        };

        // 3. Economy
        Currencies = new ObservableCollection<Currency>
        {
            new Currency { Name = "Glimmer", Quantity = 250000, Icon = "/common/destiny2_content/icons/6b1702878985223049da03c27e49ba3f.png" },
            new Currency { Name = "Silver", Quantity = 1000, Icon = "/common/destiny2_content/icons/865c34cb24249a200702df9c4d920252.png" },
            new Currency { Name = "Leg. Shards", Quantity = 4500, Icon = "/common/destiny2_content/icons/3973907a3469d80d2699f7d23d839fa3.png" },
            new Currency { Name = "Ascendant Shard", Quantity = 10, Icon = "/common/destiny2_content/icons/12b28d689b93220336214376c9dbd390.png" }
        };

        // 4. Vault State
        VaultSpaceUsed = 580; 
        VaultSpaceTotal = 600;
        VaultSpaceText = $"{VaultSpaceUsed} / {VaultSpaceTotal}";
        VaultSpaceColor = SolidColorBrush.Parse(VaultSpaceUsed / VaultSpaceTotal > 0.9 ? "#FF5555" : "#FFD700");

        // 5. Daily Activities (Mock)
        DailyActivities = new ObservableCollection<DailyRotation>
        {
            new DailyRotation 
            { 
                ActivityName = "Sepulcher",
                ActivityType = "Legend Lost Sector",
                ImagePath = "/common/destiny2_content/icons/DestinyActivityModeDefinition_234e7e954593450e163b4f620bd14371.png", // Generic Lost Sector Icon
                Modifiers = new System.Collections.Generic.List<string> { "Solar Burn", "Champions: Barrier" },
                Reward = "Exotic Legs (If Solo)"
            },
             new DailyRotation 
            { 
                ActivityName = "PsiOps Battleground: Moon",
                ActivityType = "Nightfall",
                ImagePath = "/common/destiny2_content/icons/DestinyActivityModeDefinition_9557fc995c64379a25b16f6b5791262d.png", // Generic Nightfall Icon
                Modifiers = new System.Collections.Generic.List<string> { "Arc Threat", "Champions: Unstoppable" },
                Reward = "Nightfall Weapon"
            }
        };

        // 6. Postmaster Logic
        // Simulate items in postmaster
        int postmasterItemsCount = 18; 
        IsPostmasterWarning = postmasterItemsCount > 15;
        PostmasterStatusText = IsPostmasterWarning ? $"{postmasterItemsCount} Items (Full!)" : $"{postmasterItemsCount} Items";
    }
}

// Clase auxiliar simple para las divisas
public class Currency
{
    public string Name { get; set; } = "";
    public long Quantity { get; set; }
    public string Icon { get; set; } = "";
}
