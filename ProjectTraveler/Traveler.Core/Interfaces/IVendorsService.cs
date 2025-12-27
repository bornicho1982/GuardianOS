using System.Collections.Generic;
using System.Threading.Tasks;
using Traveler.Core.Models;

namespace Traveler.Core.Interfaces;

/// <summary>
/// Service for fetching vendor inventories and checking affordability.
/// </summary>
public interface IVendorsService
{
    /// <summary>
    /// Gets all available vendor sales with affordability info.
    /// </summary>
    Task<List<VendorSale>> GetVendorSalesAsync();
    
    /// <summary>
    /// Loads wishlist from external JSON file.
    /// </summary>
    Task LoadWishlistAsync(string path);
    
    /// <summary>
    /// Checks if an item matches the loaded wishlist (God Roll detection).
    /// </summary>
    bool IsWishlistMatch(uint itemHash, List<uint> perkHashes);
}
