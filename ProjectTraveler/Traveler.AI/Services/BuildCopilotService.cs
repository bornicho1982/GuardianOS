using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
// using Microsoft.SemanticKernel.Orchestration; 
// Note: Depending on SK version, namespaces vary. Assuming SK 1.x.

using Traveler.Core.Interfaces;
using Traveler.Core.Models.AI;
using Traveler.Data.Manifest;

namespace Traveler.AI.Services;

public class BuildCopilotService : IBuildCopilotService
{
    private readonly IInventoryService _inventoryService;
    private readonly ManifestDatabase _manifestDatabase;
    private readonly Kernel _kernel;

    public BuildCopilotService(IInventoryService inventoryService, ManifestDatabase manifestDatabase)
    {
        _inventoryService = inventoryService;
        _manifestDatabase = manifestDatabase;

        // Initialize Kernel with Local Ollama (OpenAI Compatible)
        var builder = Kernel.CreateBuilder();
        
        // Configuration for Ollama
        // Endpoint: http://localhost:11434/v1 (The /v1 is important for SK OpenAI connector compatibility)
        // Model: phi3 (Must match what was pulled in setup_ai.ps1)
        // ApiKey: "ollama" (Dummy)
        try 
        {
            builder.AddOpenAIChatCompletion(
                modelId: "phi3",
                apiKey: "ollama",
                endpoint: new Uri("http://localhost:11434/v1"));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to configure Local AI: {ex.Message}");
        }
        
        _kernel = builder.Build();
    }

    public async Task<BuildRecommendation> SuggestBuildAsync(string userQuery)
    {
        // 1. Keyword Extraction (Naive)
        // Split by space and filter small words? Or just pass full query to simple search?
        // Let's pick a few potential keywords if possible, or just search the query string if simple.
        // For better results, we'd use an LLM step to extract keywords, but let's do a simple one.
        var keywords = userQuery.Split(' ').Where(w => w.Length > 4).Take(2).ToList();
        
        // 2. RAG: Search Manifest
        var searchResults = Enumerable.Empty<string>();
        if (keywords.Any())
        {
             // Taking first keyword for simplicity of the SQL LIKE
             searchResults = await _manifestDatabase.SearchItemsAsync(keywords.First(), 5);
        }

        // 3. Filter Inventory (Context Reduction)
        var userExotics = _inventoryService.AllItems.Where(i => i.IsExotic).Select(i => i.Name).ToList();
        
        // 4. Construct Prompt
        var prompt = $$"""
                       You are a Destiny 2 Buildmaster.
                       The user wants: {{userQuery}}

                       Here are some relevant items from the Destiny Database that match criteria:
                       {{string.Join("\n", searchResults)}}

                       Here is the user's available Exotic Armor:
                       {{string.Join(", ", userExotics)}}

                       Based on this, recommend a build (Exotic Armor + Weapons) that exists in their inventory or explains why they need to farm something.
                       Output purely valid JSON in the following format:
                       {
                         "BuildName": "Name",
                         "RecommendedExoticArmor": "Name",
                         "RecommendedWeapons": ["Weapon1", "Weapon2"],
                         "Reasoning": "Explanation..."
                       }
                       """;

        // 5. Execute LLM
        // Without a valid key, this throws. We will wrap in try/catch or return mock for scaffolding.
        try
        {
            var function = _kernel.CreateFunctionFromPrompt(prompt, new OpenAIPromptExecutionSettings { MaxTokens = 500, Temperature = 0.7 });
            var result = await _kernel.InvokeAsync(function);
            
            var json = result.GetValue<string>();
            return JsonSerializer.Deserialize<BuildRecommendation>(json) ?? new BuildRecommendation { Reasoning = "Failed to parse JSON" };
        }
        catch (Exception ex)
        {
            // Fallback for Phase 5 demo
            return new BuildRecommendation 
            { 
                Reasoning = $"AI Request failed. Ensure 'setup_ai.ps1' has been run and Ollama is running. Trace: {ex.Message}"
            };
        }
    }
}
