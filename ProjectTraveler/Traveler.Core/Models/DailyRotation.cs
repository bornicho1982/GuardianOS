using System.Collections.Generic;

namespace Traveler.Core.Models;

public class DailyRotation
{
    public string ActivityName { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public List<string> Modifiers { get; set; } = new();
    public string Reward { get; set; } = string.Empty;
}
