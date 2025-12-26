using System.Collections.Generic;

namespace Traveler.Core.Models.AI;

public class BuildRecommendation
{
    public string BuildName { get; set; } = string.Empty;
    public string RecommendedExoticArmor { get; set; } = string.Empty;
    public List<string> RecommendedWeapons { get; set; } = new();
    public string Reasoning { get; set; } = string.Empty;
}
