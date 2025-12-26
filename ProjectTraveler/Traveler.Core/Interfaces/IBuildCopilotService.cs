using System.Threading.Tasks;
using Traveler.Core.Models.AI;

namespace Traveler.Core.Interfaces;

public interface IBuildCopilotService
{
    /// <summary>
    /// Analyzes the user's request and inventory to suggest a build.
    /// </summary>
    /// <param name="userQuery">Natural language query (e.g. "I want a solar ignition build")</param>
    /// <returns>A recommendation object.</returns>
    Task<BuildRecommendation> SuggestBuildAsync(string userQuery);
}
