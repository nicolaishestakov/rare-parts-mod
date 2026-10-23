using System;

namespace RareParts;

public static class ShoppingListQuantity
{
    /// <summary>Top up to the selected quantity without removing existing items.</summary>
    public static int TopUp(int selectedAmount, Func<int> getAmount, Action addOne)
    {
        var amount = getAmount();
        var added = 0;
        while (amount < selectedAmount)
        {
            addOne();
            var newAmount = getAmount();
            // The game's addition API can refuse an item at its stack limit.
            if (newAmount <= amount)
                break;

            added += newAmount - amount;
            amount = newAmount;
        }

        return added;
    }
}
