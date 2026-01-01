using System.Collections.ObjectModel;
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
    private double _vaultSpaceUsed;
    private double _vaultSpaceTotal;
    private string _vaultSpaceText;
    private IBrush _vaultSpaceColor;

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

    public DashboardHomeViewModel()
    {
        // Datos de prueba iniciales (Mock Data)
        LoadMockData();
    }

    private void LoadMockData()
    {
        // 1. Crear 3 Personajes Distintos (Squad View)
        Characters = new ObservableCollection<CharacterInfo>
        {
            new CharacterInfo
            {
                CharacterId = 1,
                ClassName = "Warlock",
                RaceName = "Exo",
                LightLevel = 1810,
                EmblemBackgroundPath = "/common/destiny2_content/icons/f5304fcd2796417730e75525048d8b33.jpg", // Emblema Solar
                Title = "Godslayer"
            },
            new CharacterInfo
            {
                CharacterId = 2,
                ClassName = "Hunter",
                RaceName = "Human",
                LightLevel = 1809,
                EmblemBackgroundPath = "/common/destiny2_content/icons/87dfeb3fb385a4a28966276707373972.jpg", // Emblema Void
                Title = "Revenant"
            },
            new CharacterInfo
            {
                CharacterId = 3,
                ClassName = "Titan",
                RaceName = "Awoken",
                LightLevel = 1800,
                EmblemBackgroundPath = "/common/destiny2_content/icons/2a6cb5690327ba290eb9e355e4b77943.jpg", // Emblema Arc
                Title = "Dredgen"
            }
        };

        // 2. Divisas y Materiales (Economía)
        Currencies = new ObservableCollection<Currency>
        {
            new Currency { Name = "Glimmer", Quantity = 250000, Icon = "/common/destiny2_content/icons/6b1702878985223049da03c27e49ba3f.png" },
            new Currency { Name = "Silver", Quantity = 1000, Icon = "/common/destiny2_content/icons/865c34cb24249a200702df9c4d920252.png" },
            new Currency { Name = "Leg. Shards", Quantity = 4500, Icon = "/common/destiny2_content/icons/3973907a3469d80d2699f7d23d839fa3.png" },
            new Currency { Name = "Ascendant Shard", Quantity = 10, Icon = "/common/destiny2_content/icons/12b28d689b93220336214376c9dbd390.png" } // Bola de golf
        };

        // 3. Estado de la Bóveda (Vault)
        VaultSpaceUsed = 580; // Casi llena para probar alerta
        VaultSpaceTotal = 600;
        VaultSpaceText = $"{VaultSpaceUsed} / {VaultSpaceTotal}";
        
        // Lógica de color: Si está > 90% llena, color Rojo, si no Verde/Amarillo
        if (VaultSpaceUsed / VaultSpaceTotal > 0.9)
            VaultSpaceColor = SolidColorBrush.Parse("#FF5555"); // Rojo alerta
        else
            VaultSpaceColor = SolidColorBrush.Parse("#FFD700"); // Dorado normal
    }
}

// Clase auxiliar simple para las divisas
public class Currency
{
    public string Name { get; set; } = "";
    public long Quantity { get; set; }
    public string Icon { get; set; } = "";
}
