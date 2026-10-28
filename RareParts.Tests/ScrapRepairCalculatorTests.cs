using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace RareParts.Tests;

public class ScrapRepairCalculatorTests
{
    [Theory]
    [InlineData(0.1f, 0.1f, 0.19f)]
    [InlineData(0.25f, 0.5f, 0.625f)]
    [InlineData(0f, 0.4f, 0.4f)]
    [InlineData(1f, 0.4f, 1f)]
    [InlineData(0f, 0f, 0f)]
    public void RepairedConditionCombinesRemainingDamage(float first, float second, float expected)
    {
        AssertClose(expected, ScrapRepairCalculator.GetRepairedCondition([first, second]));
    }

    [Fact]
    public void RepairedConditionOfNoPartsIsZero()
    {
        Assert.Equal(0f, ScrapRepairCalculator.GetRepairedCondition([]));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.37f)]
    [InlineData(1f)]
    public void RepairedConditionOfOnePartIsUnchanged(float condition)
    {
        AssertClose(condition, ScrapRepairCalculator.GetRepairedCondition([condition]));
    }

    [Fact]
    public void RepairedConditionIncludesEveryPartAndDoesNotDependOnOrder()
    {
        var conditions = new[] { 0.1f, 0.2f, 0.3f, 0.4f };

        AssertClose(0.6976f, ScrapRepairCalculator.GetRepairedCondition(conditions));
        AssertClose(0.6976f, ScrapRepairCalculator.GetRepairedCondition(conditions.Reverse()));
    }

    [Fact]
    public void EmptyInventoryReturnsNoParts()
    {
        Assert.Empty(Select([], 0.15f));
    }

    [Fact]
    public void SinglePartIsReturnedWithoutTryingToCombineIt()
    {
        var part = new Part(0.1f);

        Assert.Same(part, Assert.Single(Select([part], 0.15f)));
    }

    [Theory]
    [InlineData(0.15f)]
    [InlineData(0.8f)]
    [InlineData(1f)]
    public void NonRepairablePartsAreAllUsedRegardlessOfCondition(float threshold)
    {
        var parts = Parts(0f, 0.4f, 1f);

        var selected = Select(parts, threshold, isNonRepairable: true);

        Assert.Equal(parts, selected);
    }

    [Fact]
    public void MaximumConditionUsesAllDamagedPartsIncludingAlreadyRepairableOnes()
    {
        var parts = Parts(0.1f, 0.1f, 0.15f, 0.8f, 0.9f);
        var original = parts.ToArray();

        Assert.Equal(parts.Take(2), Select(parts, 0.15f));

        var selected = Select(parts, 1f);

        Assert.Equal(original, selected);
        Assert.Equal(original, parts);
        AssertClose(0.98623f, ScrapRepairCalculator.GetRepairedCondition(selected.Select(p => p.Condition)));
    }

    [Fact]
    public void MaximumConditionExcludesFullyRepairedParts()
    {
        var parts = Parts(1f, 0.4f, 0.9f, 1f);

        Assert.Equal(new[] { parts[1], parts[2] }, Select(parts, 1f));
        Assert.Empty(Select(Parts(1f, 1f), 1f));
    }

    [Fact]
    public void MaximumConditionIncludesZeroConditionParts()
    {
        var parts = Parts(0f, 0.4f, 0f, 0.9f);

        Assert.Equal(parts, Select(parts, 1f));
        Assert.Equal(parts.Take(1), Select(parts.Take(1).ToList(), 1f));
        Assert.Empty(Select([], 1f));
    }

    [Fact]
    public void MaximumConditionUsesEveryPartEvenWhenCombinedDamageUnderflows()
    {
        var parts = Enumerable.Range(0, 128).Select(_ => new Part(0.999f)).ToList();

        Assert.Equal(parts, Select(parts, 1f));
    }

    [Theory]
    [InlineData(0.15f)]
    [InlineData(0.5f)]
    [InlineData(0.8f)]
    public void PartsAtOrAboveRepairThresholdAreExcluded(float threshold)
    {
        var parts = Parts(0.01f, 0.02f, threshold, threshold + 0.01f, 1f);

        Assert.Equal(parts.Take(2), Select(parts, threshold));
    }

    [Fact]
    public void AllAlreadyRepairablePartsProduceNoCandidates()
    {
        Assert.Empty(Select(Parts(0.15f, 0.7f, 1f), 0.15f));
    }

    [Fact]
    public void FilteringMayLeaveOnlyOneCandidate()
    {
        var parts = Parts(0.1f, 0.5f, 1f);

        Assert.Same(parts[0], Assert.Single(Select(parts, 0.15f)));
    }

    [Fact]
    public void AllPartsAreUsedWhenTheirCombinedConditionCannotReachThreshold()
    {
        var parts = Parts(0.01f, 0.02f, 0.03f);

        Assert.Equal(parts, Select(parts, 0.8f));
    }

    [Fact]
    public void SelectsEnoughPartsToCrossThreshold()
    {
        var parts = Parts(0.25f, 0.25f, 0.25f, 0.25f);

        var selected = Select(parts, 0.5f);

        Assert.Equal(parts.Take(3), selected);
        AssertClose(0.578125f, ScrapRepairCalculator.GetRepairedCondition(selected.Select(p => p.Condition)));
    }

    [Fact]
    public void MinimizesExcessConditionRatherThanNumberOfConsumedParts()
    {
        var parts = Parts(0.7f, 0.7f, 0.2f, 0.2f, 0.2f);

        var selected = Select(parts, 0.8f);

        Assert.Equal(3, selected.Count);
        Assert.Equal(1, selected.Count(p => p.Condition == 0.7f));
        AssertClose(0.808f, ScrapRepairCalculator.GetRepairedCondition(selected.Select(p => p.Condition)));
    }

    [Fact]
    public void EqualConditionPartsRemainDistinctAndAreNeverReused()
    {
        var parts = Parts(0.1f, 0.1f, 0.1f, 0.1f);

        var selected = Select(parts, 0.15f);

        Assert.Equal(2, selected.Count);
        Assert.Same(parts[0], selected[0]);
        Assert.Same(parts[1], selected[1]);
    }

    [Fact]
    public void ZeroConditionPartsDoNotGetConsumedWhenTheyAddNoImprovement()
    {
        var parts = Parts(0f, 0.1f, 0f, 0.1f);

        Assert.Equal(new[] { parts[1], parts[3] }, Select(parts, 0.15f));
    }

    [Fact]
    public void AllZeroConditionPartsReturnWithoutLogarithmOrReconstructionErrors()
    {
        var parts = Parts(0f, 0f, 0f);

        Assert.Equal(parts, Select(parts, 0.8f));
    }

    [Fact]
    public void SelectionPreservesInputOrderAndDoesNotMutateInventory()
    {
        var parts = Parts(0.2f, 0.7f, 0.2f, 0.7f, 0.2f);
        var original = parts.ToArray();
        var conditions = parts.Select(p => p.Condition).ToArray();

        var selected = Select(parts, 0.8f);

        Assert.Equal(original, parts);
        Assert.Equal(conditions, parts.Select(p => p.Condition));
        Assert.Equal(selected.OrderBy(parts.IndexOf), selected);
        Assert.Equal(selected.Count, selected.Distinct().Count());
    }

    [Theory]
    [InlineData(0.15f)]
    [InlineData(0.8f)]
    public void SelectionMatchesExhaustiveSearchOnSmallInventories(float threshold)
    {
        var random = new Random(1979);
        for (var sample = 0; sample < 64; sample++)
        {
            // All inputs are below the repair threshold, so every part is eligible.
            var parts = Enumerable.Range(0, 8)
                .Select(_ => new Part((float)(random.NextDouble() * threshold * 0.95)))
                .ToList();
            var selected = Select(parts, threshold);

            // Independent oracle: enumerate subsets using multiplication, without the
            // production algorithm's logarithms, integer weights or parent map.
            var bestDamage = -1d;
            for (var mask = 1; mask < (1 << parts.Count); mask++)
            {
                var damage = 1d;
                for (var i = 0; i < parts.Count; i++)
                {
                    if ((mask & (1 << i)) != 0)
                        damage *= 1f - parts[i].Condition;
                }
                if (damage < 1f - threshold)
                    bestDamage = Math.Max(bestDamage, damage);
            }

            if (bestDamage < 0)
            {
                Assert.Equal(parts, selected);
            }
            else
            {
                var actualDamage = selected.Aggregate(1d, (d, p) => d * (1f - p.Condition));
                // The selector rounds log weights at 1e-5 precision.
                Assert.InRange(Math.Abs(bestDamage - actualDamage), 0d, 0.00002d);
                Assert.True(actualDamage < 1f - threshold);
            }

            Assert.Equal(selected.Count, selected.Distinct().Count());
            Assert.All(selected, part => Assert.Contains(part, parts));
        }
    }

    private static List<Part> Parts(params float[] conditions) =>
        [.. conditions.Select(condition => new Part(condition))];

    private static List<Part> Select(List<Part> parts, float threshold, bool isNonRepairable = false) =>
        ScrapRepairCalculator.FilterOptimalItems(parts, part => part.Condition, threshold, isNonRepairable);

    private static void AssertClose(float expected, float actual) =>
        Assert.InRange(Math.Abs(expected - actual), 0f, 0.000001f);

    private sealed class Part(float condition)
    {
        public float Condition { get; } = condition;
    }
}
