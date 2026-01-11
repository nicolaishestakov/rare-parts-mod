using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CMS.UI;
using CMS.UI.Windows;
using MelonLoader;
using RareParts.Extensions;

namespace RareParts;

public class RepairScrap
{
    public void Repair()
    {
        var scrapWindow = GetScrapWindowIfActive();

        if (scrapWindow is null)
        {
            MelonLogger.Msg("Scrap window is not active, exiting repair.");
            return;
        }
        
        var inventory = Singleton<Inventory>.Instance;
        RepairFromInventory(inventory, scrapWindow);
    }

    private static ScrapWindow GetScrapWindowIfActive()
    {
        var windowManager = WindowManager.Instance;

        if (!windowManager.IsWindowActive(WindowID.Scrap))
        {
            return null;
        }

        return windowManager.GetWindowByID<ScrapWindow>(WindowID.Scrap);
    }
    
    public void RepairFromInventory(Inventory inventory, ScrapWindow scrapWindow)
    {
        var currentItem = GetCurrentItem(scrapWindow); // if an item is selected, it has priority to repair

        if (currentItem is not null)
        {
            MelonLogger.Msg("Currently selected item:");
            Debug.LogItemState(currentItem);
        }
        
        var repairableItems = GetRepairableItems(inventory, currentItem?.ID);
        var uiManager = UIManager.Get();
        
        if (repairableItems.Count < 2)
        {
            uiManager.ShowInfoWindow("No repairable items found. You need several identical parts to repair one item.");
            return;
        }
        
        MelonLogger.Msg("Items to scrap-repair:");
        foreach (var item in repairableItems)
        {
            Debug.LogItemState(item);
        }
        
        repairableItems = repairableItems
            .OrderBy(x => x.UID == currentItem?.UID ? 0 : 1) // if an item is selected, it will be taken as primary and will keep additional properties like quality or wheel size
            .ThenByDescending(x => x.Quality)
            .ToList();
        
        var chosenItem = repairableItems[0];
        
        var repairedCondition = GetRepairedCondition(repairableItems.Select(x => x.Condition));

        var description = new StringBuilder();
        description.AppendLine($"Repairing {chosenItem.GetLocalizedName()} parts:");
        description.Append(ConditionToPercent(repairableItems[0].Condition));
        
        for (var i = 2; i <= repairableItems.Count; i++)
        {
            description.Append(" + ");
            description.Append(ConditionToPercent(repairableItems[i-1].Condition));
        }

        description.Append($" to one {ConditionToPercent(repairedCondition)} part.");

        var repairAction = new Action<bool>(confirmed =>
        {
            if (!confirmed)
            {
                return;
            }

            chosenItem.Condition = repairedCondition;

            foreach (var item in repairableItems.Where(x => x != chosenItem))
            {
                MelonLogger.Msg($"Deleting UID: {item.UID}");
                inventory.Delete(item);
            }

            uiManager.ShowPopup("SCRAP-REPAIR",
                $"{chosenItem.GetLocalizedName()} repaired from {repairableItems.Count} parts to {ConditionToPercent(chosenItem.Condition)}.", PopupType.Normal);

            scrapWindow.choosePartDownWindow?.Refresh();
        });
        
        uiManager.ShowAskWindow("SCRAP-REPAIR ITEMS", description.ToString(), repairAction);
    }

    private static BaseItem GetCurrentItem(ScrapWindow scrapWindow)
    {
        return scrapWindow.choosePartDownWindow?.GetCurrentItem(out var _)?.BaseItem;
    }

    private static string ConditionToPercent(float condition) => $"{(int)(condition * 100)}%";
    
    private static float GetRepairedCondition(IEnumerable<float> conditions)
    {
        float q = 1;

        foreach (var condition in conditions)
        {
            q *= (1 - condition);
        }
        
        return 1 - q;
    }

    private List<Item> GetRepairableItems(Inventory inventory, string priorId)
    {
        var itemsGroup = inventory.items.ToEnumerable()
            .GroupBy(x => x.ID)
            .OrderBy(x => x.Key == priorId? 0 : 1)
            .ThenByDescending(x => x.Count())
            .ThenBy(x => x.Sum(y => y.Condition))
            .Select(x => FilterOptimalItems(x.ToList()))
            .FirstOrDefault(x => x.Count > 1);

        return itemsGroup ?? [];
    }

    private List<Item> FilterOptimalItems(List<Item> items)
    {
        if (items.Count < 2)
        {
            return items;
        }

        var itemSample = items[0];

        if (PartsInfo.NonRepairableParts.Contains(itemSample.ID))
        {
            return items;
        }

        double damageLevel = 1 - itemSample.MinConditionToRepair;
        
        return FilterOptimalItems(items.Where(x => GetItemDamageLevel(x) > damageLevel).ToList() , damageLevel);
    }

    private List<Item> FilterOptimalItems(List<Item> items, double maxDamageLevel)
    {
        // Trivial cases
        if (items is null || items.Count < 2)
        {
            return items;
        }

        // Pre-calc damage levels and check the product of ALL items
        var damageLevels = new double[items.Count];
        double productAll = 1.0d;
        for (int i = 0; i < items.Count; i++)
        {
            var d = (double)GetItemDamageLevel(items[i]);
            // Guard against invalid values
            if (d <= 0.0d)
            {
                d = 1e-12; // practically zero but positive to keep logs defined
            }
            else if (d >= 1.0d)
            {
                d = 0.999999999999; // keep strictly inside (0,1) for log stability
            }
            damageLevels[i] = d;
            productAll *= d;
        }

        // If the product of all items is more than the allowed max, return all items as per requirements
        if (productAll > maxDamageLevel)
        {
            return items;
        }

        // Dynamic Programming approach via log-transform:
        // Let w_i = -ln(d_i) (>= 0). Product constraint prod < M becomes sum w_i > -ln(M).
        // We need the minimal sum strictly greater than the threshold. That maximizes the product while being < M.
        double threshold = -Math.Log(maxDamageLevel <= 0 ? double.Epsilon : maxDamageLevel);

        // Use integer weights to keep DP map compact and stable
        const double scale = 1e5; // precision ~1e-5 in log-space
        var weights = new long[damageLevels.Length];
        for (int i = 0; i < damageLevels.Length; i++)
        {
            var w = -Math.Log(damageLevels[i]);
            if (double.IsInfinity(w) || double.IsNaN(w))
            {
                w = 50.0; // fallback; corresponds to d ~ 1.928e-22
            }
            weights[i] = (long)Math.Round(w * scale);
            if (weights[i] < 0) weights[i] = 0;
        }
        long thresholdInt = (long)Math.Floor(threshold * scale);

        // DP: store achievable sums and how we got there
        // parent[sum] = (prevSum, lastItemIndex)
        var parent = new Dictionary<long, (long prev, int idx)>
        {
            [0] = (-1, -1)
        };

        // To iterate current frontier of sums
        var sums = new List<long> { 0 };

        for (int i = 0; i < weights.Length; i++)
        {
            long wi = weights[i];
            // snapshot to avoid mixing within same round
            var current = sums.ToArray();
            foreach (var s in current)
            {
                long t = s + wi;
                if (!parent.ContainsKey(t))
                {
                    parent[t] = (s, i);
                    sums.Add(t);
                }
            }
        }

        // Find minimal sum strictly greater than thresholdInt
        long best = long.MaxValue;
        foreach (var s in sums)
        {
            if (s > thresholdInt && s < best)
            {
                best = s;
            }
        }

        // If we didn't find any sum > threshold, fall back to returning the whole set
        if (best == long.MaxValue)
        {
            return items;
        }

        // Reconstruct chosen subset
        var chosenIdx = new List<int>();
        long cur = best;
        while (cur != 0)
        {
            var node = parent[cur];
            if (node.idx >= 0)
            {
                chosenIdx.Add(node.idx);
            }
            cur = node.prev;
        }

        // Build result list in any order; preserve input order for stability
        chosenIdx.Sort();
        var result = new List<Item>(chosenIdx.Count);
        foreach (var idx in chosenIdx)
        {
            result.Add(items[idx]);
        }

        // Safety: ensure we return at least 1 item; otherwise, fall back to best single item under constraint
        if (result.Count == 0)
        {
            // pick the single item with largest damage level that is < maxDamageLevel
            Item bestSingle = null;
            double bestD = -1;
            foreach (var it in items)
            {
                var d = (double)GetItemDamageLevel(it);
                if (d < maxDamageLevel && d > bestD)
                {
                    bestD = d;
                    bestSingle = it;
                }
            }
            if (bestSingle != null)
            {
                return new List<Item> { bestSingle };
            }
            return items;
        }

        return result;
    }
    
    private static float GetItemDamageLevel(Item item) => 1.0f - item.Condition;
}