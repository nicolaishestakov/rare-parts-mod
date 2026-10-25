// Preview-only stand-ins. This project never loads game assemblies or touches saves.
using System;
using System.Reflection;
using UnityEngine;

namespace RareParts
{
    internal sealed class PreviewConfig : ITransferAllConfig
    {
        public KeyCode TransferAllItemsAndGroupsKey => KeyCode.K;
        public KeyCode TransferEntireJunkyardOrBarnKey => KeyCode.L;
        public KeyCode TransferSettingsKey => KeyCode.F7;
        public KeyCode SetPartConditionLower => KeyCode.Minus;
        public KeyCode SetPartConditionHigherKey => KeyCode.Equals;
        public KeyCode SwitchModeKey => KeyCode.KeypadMultiply;
        public int MinPartCondition { get; set; }
        public int MinNumOfItemsWarning => 500;
        public bool TransferByCategory => true;
        public bool TransferPartsOnlyAtBarnOrJunkyard { get; set; }
        public bool TransferMapsOrCasesOnlyAtBarnOrJunkyard { get; set; }
        public int[] MoveShoppingListWarehouses => [1, 2];
        public int SetPartConditionLowerBy10() => MinPartCondition = Math.Max(0,
            MinPartCondition - (MinPartCondition is 90 or 85 ? 5 : 10));
        public int SetPartConditionHigherBy10() => MinPartCondition = Math.Min(100,
            MinPartCondition + (MinPartCondition is 80 or 85 ? 5 : 10));
    }

    public sealed class TransferAll
    {
        public enum Mode { ByCondition, ByConditionSkipUnrepairable, OnlyRareRepairable }
        public Mode TransferMode { get; private set; } = Mode.OnlyRareRepairable;
        public bool IsBarnOrJunkyard { get; set; } = true;
        public static bool CheckIfInputIsFocused() => false;
        public string LastAction { get; private set; } = "No simulated transfers yet.";
        public int CollectionCalls { get; private set; }
        public int ReturnCalls { get; private set; }
        public bool LastShoppingListOnly { get; private set; }
        public void SwitchTransferMode() => TransferMode = (Mode)(((int)TransferMode + 1) % 3);
        public void CollectEntireJunkyardOrBarn(bool onlyShopListItems)
        {
            CollectionCalls++;
            LastShoppingListOnly = onlyShopListItems;
            LastAction = $"Simulated collection #{CollectionCalls}: {TransferMode}, shopping-list-only = {onlyShopListItems}.";
        }
        public void ReturnEntireJunkyardOrBarn()
        {
            ReturnCalls++;
            LastAction = $"Simulated return #{ReturnCalls} to original junk piles.";
        }
    }
}

namespace CMS.UI
{
    public enum WindowID { Inventory, ItemsExchange }
    public class WindowManager
    {
        // No game font is available: let the panel use its fallback font path.
        public static WindowManager Instance => null;
        public T GetWindowByID<T>(WindowID id) where T : Component => null;
    }
}
namespace CMS.UI.Windows
{
    public class InventoryWindow : Component { }
    public class ItemsExchangeWindow : Component { }
}
namespace UnityEngine.UI
{
    public class Text : Component { public Font font; }
}
namespace UnityEngine.EventSystems { public class EventSystem { } }
namespace MelonLoader
{
    public static class MelonLogger { public static void Msg(string message) => System.Diagnostics.Debug.WriteLine(message); }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class HarmonyPatch : Attribute
    {
        public HarmonyPatch() { }
        public HarmonyPatch(Type type, string name) { }
    }
    public class HarmonyPrefix : Attribute { }
    public class HarmonyTargetMethods : Attribute { }
    public static class AccessTools
    {
        public static MethodInfo Method(Type type, string name) => type.GetMethod(name);
    }
}
public class InputManager { }
