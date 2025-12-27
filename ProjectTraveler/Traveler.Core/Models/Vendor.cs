using System.Collections.Generic;

namespace Traveler.Core.Models;

/// <summary>
/// Represents a vendor and their current sales.
/// </summary>
public record Vendor
{
    public uint VendorHash { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? IconPath { get; init; }
    public string? Location { get; init; }
    public List<VendorSale> Sales { get; init; } = new();
}

/// <summary>
/// Represents a single item for sale by a vendor.
/// </summary>
public record VendorSale
{
    public uint ItemHash { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ItemType { get; init; } = string.Empty;
    public string? IconPath { get; init; }
    
    /// <summary>
    /// Currency costs for this item.
    /// </summary>
    public List<CurrencyCost> Costs { get; init; } = new();
    
    /// <summary>
    /// Whether the user can afford this item.
    /// </summary>
    public bool IsAffordable { get; init; }
    
    /// <summary>
    /// Whether this item/roll matches a loaded wishlist.
    /// </summary>
    public bool IsWishlistMatch { get; init; }
    
    /// <summary>
    /// Perk hashes on this item (for roll checking).
    /// </summary>
    public List<uint> Perks { get; init; } = new();
    
    /// <summary>
    /// Vendor selling this item.
    /// </summary>
    public string VendorName { get; init; } = string.Empty;
}

/// <summary>
/// Represents a currency cost for purchasing an item.
/// </summary>
public record CurrencyCost
{
    public uint CurrencyHash { get; init; }
    public string CurrencyName { get; init; } = string.Empty;
    public string? IconPath { get; init; }
    public int Quantity { get; init; }
    public int UserBalance { get; init; }
    
    public bool CanAfford => UserBalance >= Quantity;
}
