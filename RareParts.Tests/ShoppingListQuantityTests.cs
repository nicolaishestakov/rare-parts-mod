using Xunit;

namespace RareParts.Tests;

public class ShoppingListQuantityTests
{
    [Theory]
    [InlineData(0, 2, 2, 2)]
    [InlineData(1, 2, 1, 2)]
    [InlineData(2, 2, 0, 2)]
    [InlineData(3, 2, 0, 3)]
    [InlineData(0, 0, 0, 0)]
    public void AddsOnlyTheMissingQuantity(int existing, int selected, int expectedAdded, int expectedTotal)
    {
        var amount = existing;
        var calls = 0;

        var added = ShoppingListQuantity.TopUp(selected, () => amount, () => { amount++; calls++; });

        Assert.Equal(expectedAdded, added);
        Assert.Equal(expectedAdded, calls);
        Assert.Equal(expectedTotal, amount);
    }

    [Fact]
    public void RepeatingTheCommandDoesNotAddMore()
    {
        var amount = 0;
        Assert.Equal(2, ShoppingListQuantity.TopUp(2, () => amount, () => amount++));
        Assert.Equal(0, ShoppingListQuantity.TopUp(2, () => amount, () => amount++));
        Assert.Equal(2, amount);
    }

    [Fact]
    public void StopsAtTheGameLimitAndCountsOnlySuccessfulAdditions()
    {
        var amount = 98;
        var calls = 0;
        var added = ShoppingListQuantity.TopUp(101, () => amount, () =>
        {
            calls++;
            if (amount < 99)
                amount++;
        });

        Assert.Equal(1, added);
        Assert.Equal(99, amount);
        Assert.Equal(2, calls);
    }
}
