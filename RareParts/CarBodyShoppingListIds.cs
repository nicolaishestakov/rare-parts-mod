using System;
using System.Collections.Generic;
using System.Reflection;
using CMS.UI.Helpers;
using CMS.UI.Logic.CarInfo;
using HarmonyLib;
using MelonLoader;

namespace RareParts;

// The game's body display models discard their catalog IDs and have no marking
// callback. Each body helper calls GetBodyLocalizedName once per emitted row, in
// result order. Capture those IDs instead of reverse-matching translated names or
// duplicating the game's filtering, sorting, and interior/body variant rules.
[HarmonyPatch]
internal static class CarBodyShoppingListIds
{
    private static readonly Dictionary<IntPtr, BodyRow> Rows = new();

    [ThreadStatic]
    private static List<string> _capturedIds;

    public static bool TryGetId(CarInfoPart part, out string id)
    {
        if (part != null && Rows.TryGetValue(part.Pointer, out var row))
        {
            id = row.ID;
            return true;
        }

        id = null;
        return false;
    }

    // GetParts has a ref List<CarInfoPart> parameter whose generated interop
    // wrapper contains an open-generic constructor. Harmony cannot compile that
    // wrapper. Clear at its UI caller instead, immediately before GetParts runs.
    [HarmonyPatch(typeof(PartsTabPageManager), nameof(PartsTabPageManager.PrepareFirstPage))]
    [HarmonyPrefix]
    private static void ClearRows()
    {
        Rows.Clear();
    }

    [HarmonyPatch(typeof(GameInventory), nameof(GameInventory.GetBodyLocalizedName))]
    [HarmonyPrefix]
    private static void CaptureId(string __0)
    {
        _capturedIds?.Add(__0);
    }

    private static void SaveRows(Il2CppSystem.Collections.Generic.List<CarInfoPart> result)
    {
        if (result == null || _capturedIds == null || result.Count != _capturedIds.Count)
        {
            MelonLogger.Warning("Could not associate car body rows with their shopping-list IDs.");
            return;
        }

        for (var i = 0; i < result.Count; i++)
        {
            var part = result[i];
            var id = _capturedIds[i];
            if (part != null && !string.IsNullOrEmpty(id))
                Rows[part.Pointer] = new BodyRow { Part = part, ID = id };
        }
    }

    [HarmonyPatch]
    private static class BodyCapture
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(CarInfoPartsHelper), nameof(CarInfoPartsHelper.GetMountedBodyParts));
            yield return AccessTools.Method(typeof(CarInfoPartsHelper), nameof(CarInfoPartsHelper.GetUnmountedBodyParts));
        }

        [HarmonyPrefix]
        private static void BeginCapture(out List<string> __state)
        {
            __state = _capturedIds;
            _capturedIds = new List<string>();
        }

        [HarmonyPostfix]
        private static void FinishCapture(Il2CppSystem.Collections.Generic.List<CarInfoPart> __result)
        {
            SaveRows(__result);
        }

        [HarmonyFinalizer]
        private static void EndCapture(List<string> __state)
        {
            _capturedIds = __state;
        }
    }

    private sealed class BodyRow
    {
        // Keep the native model alive while its pointer is used as a key. The next
        // Parts-page preparation releases all models, including the previous car.
        public CarInfoPart Part;
        public string ID;
    }
}
