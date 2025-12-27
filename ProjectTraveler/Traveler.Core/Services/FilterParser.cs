using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Traveler.Core.Models;

namespace Traveler.Core.Services;

/// <summary>
/// Parser for DIM-style filter queries.
/// Supports: is:exotic, is:solar, stat:health:>100, tag:keep, name:"Gjallarhorn"
/// </summary>
public class FilterParser
{
    private readonly List<Func<InventoryItem, bool>> _filters = new();

    /// <summary>
    /// Parses a filter query string and returns a predicate function.
    /// </summary>
    public Func<InventoryItem, bool> Parse(string query)
    {
        _filters.Clear();
        
        if (string.IsNullOrWhiteSpace(query))
            return _ => true;

        // Split by spaces (respecting quoted strings)
        var tokens = Tokenize(query);
        
        foreach (var token in tokens)
        {
            var filter = ParseToken(token);
            if (filter != null)
                _filters.Add(filter);
        }

        // Combine all filters with AND logic
        return item => _filters.All(f => f(item));
    }

    private List<string> Tokenize(string query)
    {
        var tokens = new List<string>();
        var regex = new Regex(@"(?:""([^""]*)""|(\S+))");
        
        foreach (Match match in regex.Matches(query))
        {
            tokens.Add(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
        }
        
        return tokens;
    }

    private Func<InventoryItem, bool>? ParseToken(string token)
    {
        // is:exotic, is:legendary, is:solar, is:void, etc.
        if (token.StartsWith("is:", StringComparison.OrdinalIgnoreCase))
        {
            var value = token[3..].ToLowerInvariant();
            return value switch
            {
                "exotic" => item => item.IsExotic,
                "legendary" => item => !item.IsExotic, // Simplified
                "armor" => item => IsArmor(item.ItemType),
                "weapon" => item => IsWeapon(item.ItemType),
                "locked" => item => item.IsLocked,
                "unlocked" => item => !item.IsLocked,
                "artifice" => item => item.IsArtifice,
                "tier5" or "t5" => item => item.IsTier5,
                // TODO: Add element filters when we have DamageType in model
                _ => null
            };
        }

        // stat:mobility:>100, stat:resilience:>=50
        if (token.StartsWith("stat:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = token.Split(':');
            if (parts.Length >= 3)
            {
                var statName = parts[1].ToLowerInvariant();
                var comparison = parts[2];
                
                var statHash = GetStatHashByName(statName);
                if (statHash.HasValue && TryParseComparison(comparison, out var op, out var threshold))
                {
                    return item =>
                    {
                        if (!item.Stats.TryGetValue(statHash.Value, out var statValue))
                            return false;
                        return op switch
                        {
                            ">" => statValue > threshold,
                            ">=" => statValue >= threshold,
                            "<" => statValue < threshold,
                            "<=" => statValue <= threshold,
                            "=" => statValue == threshold,
                            _ => false
                        };
                    };
                }
            }
        }

        // power:>1800, power:1810
        if (token.StartsWith("power:", StringComparison.OrdinalIgnoreCase))
        {
            var comparison = token[6..];
            if (TryParseComparison(comparison, out var op, out var threshold))
            {
                return item => op switch
                {
                    ">" => item.PowerLevel > threshold,
                    ">=" => item.PowerLevel >= threshold,
                    "<" => item.PowerLevel < threshold,
                    "<=" => item.PowerLevel <= threshold,
                    "=" => item.PowerLevel == threshold,
                    _ => false
                };
            }
        }

        // name:"Gjallarhorn" or just plain text search
        if (token.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
        {
            var searchTerm = token[5..].Trim('"');
            return item => item.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
        }

        // Plain text = name search
        if (!token.Contains(':'))
        {
            return item => item.Name.Contains(token, StringComparison.OrdinalIgnoreCase);
        }

        return null;
    }

    private bool TryParseComparison(string input, out string op, out int value)
    {
        op = "=";
        value = 0;

        var match = Regex.Match(input, @"^(>=|<=|>|<|=)?(\d+)$");
        if (!match.Success)
            return false;

        op = match.Groups[1].Success && match.Groups[1].Value != "" ? match.Groups[1].Value : "=";
        return int.TryParse(match.Groups[2].Value, out value);
    }

    private uint? GetStatHashByName(string name)
    {
        return name switch
        {
            "mobility" or "mob" => 2996146975,
            "resilience" or "res" => 392767087,
            "recovery" or "rec" => 1943323491,
            "discipline" or "dis" => 1735777505,
            "intellect" or "int" => 144602215,
            "strength" or "str" => 4244567218,
            // Armor 3.0 new stat names (if different)
            "health" => 392767087, // Mapped to Resilience
            "class" => 1943323491, // Mapped to Recovery
            _ => null
        };
    }

    private bool IsArmor(string itemType)
    {
        return itemType.Contains("Helmet", StringComparison.OrdinalIgnoreCase)
            || itemType.Contains("Gauntlets", StringComparison.OrdinalIgnoreCase)
            || itemType.Contains("Chest", StringComparison.OrdinalIgnoreCase)
            || itemType.Contains("Leg", StringComparison.OrdinalIgnoreCase)
            || itemType.Contains("Class", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsWeapon(string itemType)
    {
        return itemType.Contains("Cannon", StringComparison.OrdinalIgnoreCase)
            || itemType.Contains("Rifle", StringComparison.OrdinalIgnoreCase)
            || itemType.Contains("Launcher", StringComparison.OrdinalIgnoreCase)
            || itemType.Contains("Sword", StringComparison.OrdinalIgnoreCase)
            || itemType.Contains("Bow", StringComparison.OrdinalIgnoreCase)
            || itemType.Contains("Gun", StringComparison.OrdinalIgnoreCase);
    }
}
