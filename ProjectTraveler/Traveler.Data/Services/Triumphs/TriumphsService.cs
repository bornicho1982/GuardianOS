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
            // Get presentation node definition from manifest
            var nodeDef = await _manifestDatabase.GetPresentationNodeAsync(hash);
            if (nodeDef == null)
                return null;

            var node = new Triumph
            {
                Hash = hash,
                Name = nodeDef.Name,
                Description = "", // Could be expanded
                State = 0, // Would come from live API
                Children = new List<Triumph>()
            };

            // Build child nodes recursively
            foreach (var childHash in nodeDef.ChildNodes)
            {
                var child = await BuildNodeAsync(childHash, depth + 1);
                if (child != null) 
                    node.Children.Add(child);
            }

            // Build child records (leaf triumphs)
            foreach (var recordHash in nodeDef.ChildRecords)
            {
                var recordDef = await _manifestDatabase.GetRecordDefinitionAsync(recordHash);
                if (recordDef != null)
                {
                    node.Children.Add(new Triumph
                    {
                        Hash = recordHash,
                        Name = recordDef.Name,
                        Description = recordDef.Description,
                        State = 0, // Would come from live API
                        Children = new List<Triumph>()
                    });
                }
            }

            return node;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error building triumph node {hash}: {ex.Message}");
            return null;
        }
    }
}
