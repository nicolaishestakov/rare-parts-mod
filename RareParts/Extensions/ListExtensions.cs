using Il2CppSystem.Collections.Generic;

namespace RareParts.Extensions;

internal static class ListExtensions
{
    public static System.Collections.Generic.IEnumerable<T> ToEnumerable<T>(this List<T> list)
    {
        foreach (var item in list) yield return item;
    }

    public static int RemoveItem(this List<Item> list, Item item)
    {
        var predicate = (Item x) => x.Pointer == item.Pointer;
        
        return list.RemoveAll(predicate);
    }
}