using System.Collections.Generic;
using System.Threading.Tasks;
using Traveler.Core.Models;

namespace Traveler.Core.Interfaces;

/// <summary>
/// Service for fetching and managing Triumphs/Records presentation nodes.
/// </summary>
public interface ITriumphsService
{
    /// <summary>
    /// Gets the root presentation nodes for Triumphs.
    /// </summary>
    Task<List<Triumph>> GetTriumphTreeAsync();
    
    /// <summary>
    /// Refreshes triumph completion states from API.
    /// </summary>
    Task RefreshTriumphStatesAsync();
}
