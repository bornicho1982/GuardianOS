using Avalonia.Media;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using Traveler.Core.Models;
using Traveler.Core.Interfaces;

namespace Traveler.Desktop.ViewModels;

public class GuardianDetailViewModel : ViewModelBase
{
    private readonly IInventoryService _inventoryService;

    public CharacterInfo Character { get; }

    // Equipped Items
    public InventoryItem? EquippedKinetic { get; private set; }
    public InventoryItem? EquippedEnergy { get; private set; }
    public InventoryItem? EquippedPower { get; private set; }
    public InventoryItem? EquippedSubclass { get; private set; }
    public InventoryItem? EquippedHelmet { get; private set; }
    public InventoryItem? EquippedGauntlets { get; private set; }
    public InventoryItem? EquippedChest { get; private set; }
    public InventoryItem? EquippedLegs { get; private set; }
    public InventoryItem? EquippedClassItem { get; private set; }

    // Element Glow
    public IBrush ElementGlowColor { get; private set; } = Brushes.Transparent;

    // Stat Icon URLs (from Bungie CDN)
    public static string MobilityIcon => "https://www.bungie.net/common/destiny2_content/icons/e26e0e93a9daf4fdd21bf64eb9246340.png";
    public static string ResilienceIcon => "https://www.bungie.net/common/destiny2_content/icons/202ecc1c6febeb6b97dafc856e863140.png";
    public static string RecoveryIcon => "https://www.bungie.net/common/destiny2_content/icons/128eee4ee7fc127851ab32eac6ca617f.png";
    public static string DisciplineIcon => "https://www.bungie.net/common/destiny2_content/icons/79be1d1a657c8fd6f37d5a9e14345603.png";
    public static string IntellectIcon => "https://www.bungie.net/common/destiny2_content/icons/59732534ce7060dba681e1a27c3247fa.png";
    public static string StrengthIcon => "https://www.bungie.net/common/destiny2_content/icons/c7eefc8abbaa586eeab79e962a79d6ad.png";

    // Total Stats (individual values for icons)
    public int TotalMobility => Character.Mobility;
    public int TotalResilience => Character.Resilience;
    public int TotalRecovery => Character.Recovery;
    public int TotalDiscipline => Character.Discipline;
    public int TotalIntellect => Character.Intellect;
    public int TotalStrength => Character.Strength;

    // Stats Text (Horizontal) - kept for backwards compatibility
    public string HelmetStatsText => FormatStats(EquippedHelmet?.Stats);
    public string GauntletsStatsText => FormatStats(EquippedGauntlets?.Stats);
    public string ChestStatsText => FormatStats(EquippedChest?.Stats);
    public string LegsStatsText => FormatStats(EquippedLegs?.Stats);
    public string ClassItemStatsText => FormatStats(EquippedClassItem?.Stats);
    public string TotalStatsText => $"MOB: {Character.Mobility}  RES: {Character.Resilience}  REC: {Character.Recovery}  DIS: {Character.Discipline}  INT: {Character.Intellect}  STR: {Character.Strength}";

    private static class BucketHashes
    {
        public const uint Kinetic = 1498876634;
        public const uint Energy = 2465295065;
        public const uint Power = 953998645;
        public const uint Helmet = 3448274439;
        public const uint Gauntlets = 3551918588;
        public const uint Chest = 14239492;
        public const uint Legs = 20886954;
        public const uint ClassItem = 1585787867;
        public const uint Subclass = 3284755031;
    }

    private static class StatHashes
    {
        public const uint Mobility = 2996146975;
        public const uint Resilience = 392767087;
        public const uint Recovery = 1943323491;
        public const uint Discipline = 1735777505;
        public const uint Intellect = 144602215;
        public const uint Strength = 4244567218;
    }

    public GuardianDetailViewModel(CharacterInfo character, IInventoryService inventoryService)
    {
        Character = character;
        _inventoryService = inventoryService;
        LoadEquippedItems();
    }

    private void LoadEquippedItems()
    {
        var charIdStr = Character.CharacterId.ToString();
        var equipped = _inventoryService.AllItems
            .Where(i => i.IsEquipped && i.Location == charIdStr)
            .ToList();

        EquippedKinetic = equipped.FirstOrDefault(i => i.BucketHash == BucketHashes.Kinetic);
        EquippedEnergy = equipped.FirstOrDefault(i => i.BucketHash == BucketHashes.Energy);
        EquippedPower = equipped.FirstOrDefault(i => i.BucketHash == BucketHashes.Power);
        EquippedSubclass = equipped.FirstOrDefault(i => i.BucketHash == BucketHashes.Subclass);
        EquippedHelmet = equipped.FirstOrDefault(i => i.BucketHash == BucketHashes.Helmet);
        EquippedGauntlets = equipped.FirstOrDefault(i => i.BucketHash == BucketHashes.Gauntlets);
        EquippedChest = equipped.FirstOrDefault(i => i.BucketHash == BucketHashes.Chest);
        EquippedLegs = equipped.FirstOrDefault(i => i.BucketHash == BucketHashes.Legs);
        EquippedClassItem = equipped.FirstOrDefault(i => i.BucketHash == BucketHashes.ClassItem);

        // Determine Element Glow from Subclass DamageType
        var damageType = EquippedSubclass?.DamageType ?? 0;
        ElementGlowColor = damageType switch
        {
            2 => new SolidColorBrush(Color.FromRgb(121, 181, 232)), // Arc - Blue
            3 => new SolidColorBrush(Color.FromRgb(245, 166, 35)),  // Solar - Orange
            4 => new SolidColorBrush(Color.FromRgb(167, 94, 211)),  // Void - Purple
            6 => new SolidColorBrush(Color.FromRgb(79, 196, 232)),  // Stasis - Cyan
            7 => new SolidColorBrush(Color.FromRgb(91, 217, 104)),  // Strand - Green
            _ => Brushes.Gray
        };

        this.RaisePropertyChanged(nameof(EquippedKinetic));
        this.RaisePropertyChanged(nameof(EquippedEnergy));
        this.RaisePropertyChanged(nameof(EquippedPower));
        this.RaisePropertyChanged(nameof(EquippedSubclass));
        this.RaisePropertyChanged(nameof(EquippedHelmet));
        this.RaisePropertyChanged(nameof(EquippedGauntlets));
        this.RaisePropertyChanged(nameof(EquippedChest));
        this.RaisePropertyChanged(nameof(EquippedLegs));
        this.RaisePropertyChanged(nameof(EquippedClassItem));
        this.RaisePropertyChanged(nameof(ElementGlowColor));
        this.RaisePropertyChanged(nameof(HelmetStatsText));
        this.RaisePropertyChanged(nameof(GauntletsStatsText));
        this.RaisePropertyChanged(nameof(ChestStatsText));
        this.RaisePropertyChanged(nameof(LegsStatsText));
        this.RaisePropertyChanged(nameof(ClassItemStatsText));
        this.RaisePropertyChanged(nameof(TotalStatsText));
    }

    private string FormatStats(Dictionary<uint, int>? stats)
    {
        if (stats == null || stats.Count == 0) return "";
        var mob = stats.TryGetValue(StatHashes.Mobility, out var m) ? m : 0;
        var res = stats.TryGetValue(StatHashes.Resilience, out var r) ? r : 0;
        var rec = stats.TryGetValue(StatHashes.Recovery, out var rc) ? rc : 0;
        var dis = stats.TryGetValue(StatHashes.Discipline, out var d) ? d : 0;
        var intel = stats.TryGetValue(StatHashes.Intellect, out var i) ? i : 0;
        var str = stats.TryGetValue(StatHashes.Strength, out var s) ? s : 0;
        return $"MOB:{mob} RES:{res} REC:{rec} DIS:{dis} INT:{intel} STR:{str}";
    }
}
