using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Traveler.Core.Interfaces;
using Traveler.Core.Models;
using Traveler.Data.Manifest;

namespace Traveler.Data.Services.Triumphs;

/// <summary>
/// Service for fetching and managing Triumphs/Records.
/// Parses DestinyPresentationNodeDefinition recursively.
/// </summary>
public class TriumphsService : ITriumphsService
{
    private readonly ManifestDatabase _manifestDatabase;
    private List<Triumph> _cachedTree = new();
    
    // Bungie root presentation node hashes for Triumphs
    private const uint TriumphsRootHash = 1024788583; // Main Triumphs root
    private const uint SealsRootHash = 1652422747;    // Seals/Titles root

    public TriumphsService(ManifestDatabase manifestDatabase)
    {
        _manifestDatabase = manifestDatabase;
    }

    public async Task<List<Triumph>> GetTriumphTreeAsync()
    {
        if (_cachedTree.Any())
            return _cachedTree;

        // Build the tree from root nodes
        var roots = new List<Triumph>();
        
        // Add main Triumphs root
        var triumphsRoot = await BuildNodeAsync(TriumphsRootHash);
        if (triumphsRoot != null)
            roots.Add(triumphsRoot);
        
        // Add Seals/Titles root
        var sealsRoot = await BuildNodeAsync(SealsRootHash);
        if (sealsRoot != null)
            roots.Add(sealsRoot);

        _cachedTree = roots;
        return _cachedTree;
    }

    public Task RefreshTriumphStatesAsync()
    {
        // TODO: Fetch live record states from API
        // This would call Bungie API with ProfileRecords component
        // and update the State property on each cached Triumph
        Console.WriteLine("RefreshTriumphStatesAsync - TODO: Fetch live states from API");
        return Task.CompletedTask;
    }

    private async Task<Triumph?> BuildNodeAsync(uint hash, int depth = 0)
    {
        // Prevent infinite recursion
        if (depth > 10) return null;

        try
        {
            // Get node definition from manifest
            var nodeJson = await _manifestDatabase.GetItemName(hash);
            if (string.IsNullOrEmpty(nodeJson))
                return null;

            // Parse JSON to extract node info
            // In a real implementation, we'd use System.Text.Json to parse
            // For now, return a placeholder structure
            var node = new Triumph
            {
                Hash = hash,
                Name = $"Node {hash}", // Would come from parsed JSON
                Description = "Description from manifest",
                State = 0, // Would come from live API
                Children = new List<Triumph>()
            };

            // TODO: Parse child node hashes from definition
            // and recursively build each child
            // var childHashes = ParseChildHashes(nodeJson);
            // foreach (var childHash in childHashes)
            // {
            //     var child = await BuildNodeAsync(childHash, depth + 1);
            //     if (child != null) node.Children.Add(child);
            // }

            return node;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error building triumph node {hash}: {ex.Message}");
            return null;
        }
    }
}
