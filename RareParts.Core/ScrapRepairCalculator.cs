using System;
using System.Collections.Generic;
using System.Linq;

namespace RareParts;

// Pure repair calculations shared by the game adapter and the unit tests.
public static class ScrapRepairCalculator
{
    public static float GetRepairedCondition(IEnumerable<float> conditions)
    {
        float q = 1;

        foreach (var condition in conditions)
        {
            q *= (1 - condition);
        }
        
        return 1 - q;
    }

    public static List<T> FilterOptimalItems<T>(
        List<T> items, Func<T, float> getCondition, float minConditionToRepair, bool isNonRepairable)
        where T : class
    {
        if (items.Count < 2)
        {
            return items;
        }

        if (isNonRepairable)
        {
            return items;
        }

        double damageLevel = 1 - minConditionToRepair;
        
        return FilterOptimalItems(items.Where(x => 1.0f - getCondition(x) > damageLevel).ToList(), getCondition, damageLevel);
    }

    private static List<T> FilterOptimalItems<T>(List<T> items, Func<T, float> getCondition, double maxDamageLevel)
        where T : class
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
            var d = (double)(1.0f - getCondition(items[i]));
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
        var result = new List<T>(chosenIdx.Count);
        foreach (var idx in chosenIdx)
        {
            result.Add(items[idx]);
        }

        // Safety: ensure we return at least 1 item; otherwise, fall back to best single item under constraint
        if (result.Count == 0)
        {
            // pick the single item with largest damage level that is < maxDamageLevel
            T bestSingle = null;
            double bestD = -1;
            foreach (var it in items)
            {
                var d = (double)(1.0f - getCondition(it));
                if (d < maxDamageLevel && d > bestD)
                {
                    bestD = d;
                    bestSingle = it;
                }
            }
            if (bestSingle != null)
            {
                return new List<T> { bestSingle };
            }
            return items;
        }

        return result;
    }
}
