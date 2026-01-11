using System;
using CMS.Containers;
using CMS.UI.Windows;
using HarmonyLib;
using MelonLoader;

namespace RareParts;

[HarmonyPatch]
public class Patched
{
    [HarmonyPatch(typeof (RepairPartWindow), "GetItemsToRepair")]
    [HarmonyPostfix]
    public static void RepairPartWindow_GetItemsToRepair_Postfix(RepairPartWindow __instance, ref Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> __result)
    {
        MelonLogger.Msg($"RepairPartWindow_GetItemsToRepair_Postfix");

        if (__result is null)
        {
            MelonLogger.Msg("RepairPartWindow_GetItemsToRepair_Postfix: __result is null");
            return;
        }
        
        foreach (var item in __result)
        {
            MelonLogger.Msg($"Item: {item.BaseItem.ID}, Condition: {item.BaseItem.GetCondition()}, IsLocked: {item.IsLocked}, Checked: {item.Checked}");
                
            var itemId = item.BaseItem.ID;
            
            if (item.BaseItem.GetCondition() < item.BaseItem.MinConditionToRepair)
            {
                MelonLogger.Msg("Trying to lock it");
                item.IsLocked = true;
            }
        }
    }
    
    [HarmonyPatch(typeof(TempInventory), "ClearListOfItems")]
    [HarmonyPrefix]
    public static void TempInventory_ClearListOfItems_Prefix(TempInventory __instance)
    {
        if (_skipTempInventoryClearPrefix)
        {
            // for some reason this method is always called twice
            MelonLogger.Msg($"TempInventory_ClearListOfItems_Prefix: skipped");
        }
        else
        {
            MelonLogger.Msg($"TempInventory_ClearListOfItems_Prefix: event call");
            BeforeTempInventoryCleared?.Invoke(__instance);
        }

        _skipTempInventoryClearPrefix = !_skipTempInventoryClearPrefix;
    }
    
    private static bool _skipTempInventoryClearPrefix = false;
    
    [HarmonyPatch(typeof(TempInventory), "ClearListOfItems")]
    [HarmonyPostfix]
    public static void TempInventory_ClearListOfItems_Postfix(TempInventory __instance)
    {
        if (_skipTempInventoryClearPostfix)
        {
            // for some reason this method is always called twice
            MelonLogger.Msg($"TempInventory_ClearListOfItems_Postfix: skipped");
        }
        else
        {
            MelonLogger.Msg($"TempInventory_ClearListOfItems_Postfix: event call");
            OnTempInventoryCleared?.Invoke(__instance);
        }

        _skipTempInventoryClearPostfix = !_skipTempInventoryClearPostfix;
    }

    private static bool _skipTempInventoryClearPostfix = true;

    public static event Action<TempInventory> OnTempInventoryCleared;
    public static event Action<TempInventory> BeforeTempInventoryCleared;

    // [HarmonyPatch(typeof(CMS.UI.Logic.ShowroomCarItem), "SetupForCarConfigData")]
    // [HarmonyPrefix]
    // public static void ShowroomCarItem_SetupForCarConfigData_Prefix( CMS.UI.Logic.ShowroomCarItem __instance, CarConfigData data)
    // {
    //     MelonLogger.Msg($"ShowroomCarItem_SetupForCarConfigData_Prefix: {data?.CarID} - {data?.CarName}");
    // }
}