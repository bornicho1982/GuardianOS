using System;
using System.Collections.Generic;
using System.Linq;
using Google.OrTools.Sat;
using Traveler.Core.Models;
using Traveler.Core.Models.Optimization;

namespace Traveler.Core.Optimization;

public class OptimizationSolver
{
    // Common Stat Hashes (Example values, would normally be constants)
    // Mobility, Resilience, Recovery, Discipline, Intellect, Strength
    private static readonly uint[] StatHashes = { 2996146975, 392767087, 1943323491, 1735777505, 144602215, 4244567218 };
    
    // Bucket Hashes
    private const uint BucketHelmet = 3448274439;
    private const uint BucketGauntlets = 3551918588;
    private const uint BucketChest = 14239492;
    private const uint BucketLegs = 20886954;
    private const uint BucketClassItem = 1585787867;

    public LoadoutResult Solve(LoadoutRequest request)
    {
        var model = new CpModel();
        var items = request.AvailableItems;
        
        // 1. Decision Variables: x[i] is 1 if item i is selected
        var x = new Dictionary<InventoryItem, BoolVar>();
        foreach (var item in items)
        {
            x[item] = model.NewBoolVar($"x_{item.InstanceId}");
        }

        // 2. Slot Constraints: Select exactly 1 of each armor type
        // Note: This relies on ItemType or BucketHash being correct.
        // Simplified check since our model uses strings currently or we infer from somewhere.
        // Assuming we filter input to only include Armor.
        AddSlotConstraint(model, x, items, "Helmet"); // Using string match for now based on InventoryItem model
        AddSlotConstraint(model, x, items, "Gauntlets");
        AddSlotConstraint(model, x, items, "Chest Armor");
        AddSlotConstraint(model, x, items, "Leg Armor");
        AddSlotConstraint(model, x, items, "Class Armor");

        // 3. Exotic Constraint: Max 1 Exotic
        var exotics = items.Where(i => i.IsExotic).ToList();
        model.Add(LinearExpr.Sum(exotics.Select(i => x[i])) <= 1);

        // 4. Tier 5 Tuning Logic
        // For each T5 item, we have 30 possible tuning configurations (+5 A, -5 B)
        // t[item_instance, plusStat, minusStat]
        var tuningVars = new Dictionary<long, Dictionary<(uint Plus, uint Minus), BoolVar>>();

        foreach (var item in items.Where(i => i.IsTier5))
        {
            tuningVars[item.InstanceId] = new Dictionary<(uint, uint), BoolVar>();
            var itemVars = new List<BoolVar>();

            foreach (var plus in StatHashes)
            {
                foreach (var minus in StatHashes)
                {
                    if (plus == minus) continue;

                    var tVar = model.NewBoolVar($"tuning_{item.InstanceId}_{plus}_{minus}");
                    tuningVars[item.InstanceId][(plus, minus)] = tVar;
                    itemVars.Add(tVar);
                }
            }

            // Constraint: Sum(tuning variants) == x[item]
            // If item is selected, EXACTLY one tuning must be active.
            // If item not selected, NO tuning active.
            model.Add(LinearExpr.Sum(itemVars) == x[item]);
        }

        // 5. Stat Totals
        // TotalStat[S] = Sum(ItemBase[S] * x[i]) + Sum(Tuning)
        var totalStats = new Dictionary<uint, LinearExpr>();
        
        foreach (var statHash in StatHashes)
        {
            var components = new List<LinearExpr>();
            
            // Base Stats
            foreach (var item in items)
            {
                if (item.Stats.TryGetValue(statHash, out var val))
                {
                    components.Add(x[item] * val);
                }
            }

            // Tuning Adjustments
            foreach (var item in items.Where(i => i.IsTier5))
            {
                foreach (var kvp in tuningVars[item.InstanceId])
                {
                    var (plus, minus) = kvp.Key;
                    var tVar = kvp.Value;

                    if (plus == statHash) components.Add(tVar * 5);
                    if (minus == statHash) components.Add(tVar * -5);
                }
            }

            totalStats[statHash] = LinearExpr.Sum(components);
        }

        // 6. User Constraints (Min Stats)
        foreach (var req in request.MinimumStats)
        {
            if (totalStats.ContainsKey(req.Key))
            {
                model.Add(totalStats[req.Key] >= req.Value);
            }
        }

        // 7. Objective: Maximize Total Stats
        var totalScore = LinearExpr.Sum(totalStats.Values);
        model.Maximize(totalScore);

        // 8. Solve
        var solver = new CpSolver();
        var status = solver.Solve(model);

        if (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible)
        {
            var result = new LoadoutResult { IsValid = true };
            
            // Collect Selected Items
            foreach (var item in items)
            {
                if (solver.Value(x[item]) == 1)
                {
                    result.SelectedItems.Add(item);
                }
            }

            // Collect Tuning Info
            foreach (var item in items.Where(i => i.IsTier5))
            {
                if (result.SelectedItems.Contains(item))
                {
                    foreach (var kvp in tuningVars[item.InstanceId])
                    {
                        if (solver.Value(kvp.Value) == 1)
                        {
                            result.TuningAdjustments[item.InstanceId] = $"+5 {GetStatName(kvp.Key.Plus)} / -5 {GetStatName(kvp.Key.Minus)}";
                            break;
                        }
                    }
                }
            }

            // Calculate Finals
            foreach (var s in StatHashes)
            {
                result.FinalStats[s] = (int)solver.Value(totalStats[s]);
            }

            return result;
        }

        return new LoadoutResult { IsValid = false };
    }

    private void AddSlotConstraint(CpModel model, Dictionary<InventoryItem, BoolVar> x, List<InventoryItem> items, string itemType)
    {
        var slotItems = items.Where(i => i.ItemType.Equals(itemType, StringComparison.OrdinalIgnoreCase)).ToList();
        if (slotItems.Any())
        {
            model.Add(LinearExpr.Sum(slotItems.Select(i => x[i])) == 1);
        }
    }

    private string GetStatName(uint hash)
    {
        // Simple lookup for demo
        if (hash == 2996146975) return "Mobility";
        if (hash == 392767087) return "Resilience";
        if (hash == 1943323491) return "Recovery";
        if (hash == 1735777505) return "Discipline";
        if (hash == 144602215) return "Intellect";
        if (hash == 4244567218) return "Strength";
        return "Unknown";
    }
}
