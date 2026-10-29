using System;
using System.Collections.Generic;
using CMS.Containers;
using CMS.UI;
using CMS.UI.Helpers;
using CMS.UI.Logic;
using CMS.UI.Windows;
using MelonLoader;
using UnityEngine;

namespace RareParts;

public class PartsToFixToShoppingList
{
    public void Transfer(bool allMarked)
    {
        var manager = WindowManager.Instance;
        if (manager == null)
            return;

        var lastWindow = manager.GetLastOpenedWindowID();
        if (lastWindow == null || !lastWindow.HasValue || lastWindow.Value != WindowID.CarInfo)
            return;

        var window = manager.GetWindowByID<CarInfoWindow>(WindowID.CarInfo);
        if (window == null || !window.IsActive)
            return;

        if (window.currentTab == CarInfoTabs.Parts)
        {
            TransferSelectedCarPart(window);
            return;
        }

        if (window.currentTab != CarInfoTabs.Order)
            return;

        var tab = window.orderTab;
        var pages = tab?.orderTabPageManager;
        var task = pages?.currentJobTask;
        var car = tab?.carLoader;
        var ui = UIManager.Get();
        var shopList = ui?.ShopListWindow;
        if (task?.Parts == null || car == null || shopList == null)
            return;

        // JobPart.ID identifies a position on the car, not a catalog item.
        var selectedPaths = new HashSet<string>(StringComparer.Ordinal);
        if (allMarked)
        {
            // Include every page of the selected issue, as in the original bulk mode.
            foreach (var part in task.Parts)
                selectedPaths.Add(part.ID);
        }
        else
        {
            if (tab.rightGrid == null || pages.itemObjects == null || pages.IsPageAnimationInProgress())
                return;

            var row = tab.GetItemFromRightGrid();
            if (row == null || !row.gameObject.activeInHierarchy ||
                row.selected == null || !row.selected.activeInHierarchy)
                return;

            var selectedIndex = -1;
            for (var i = 0; i < pages.currentPageItemsCount && i < pages.itemObjects.Length; i++)
            {
                if (pages.itemObjects[i] == row)
                {
                    // The game uses this same offset in OrderTabPageManager.SetFixedState.
                    selectedIndex = pages.currentPage * pages.ItemsPerPage + i;
                    break;
                }
            }

            if (selectedIndex < 0 || selectedIndex >= task.Parts.Count)
                return;

            selectedPaths.Add(task.Parts[selectedIndex].ID);
        }

        var selectedItems = new List<SelectedItem>();
        var parts = car.GetPartScriptCache();
        var inventory = Singleton<GameInventory>.Instance;
        if (parts != null && inventory != null)
        {
            var pathBuilder = new Il2CppSystem.Text.StringBuilder();
            foreach (var part in parts)
            {
                if (part == null || !part.IsExamined || (allMarked && !part.markImportantPart))
                    continue;

                try
                {
                    pathBuilder.Clear();
                    if (!selectedPaths.Contains(part.GetGameObjectPathWithoutRoot(pathBuilder)))
                        continue;

                    var id = part.GetIDWithTuned();
                    if (string.IsNullOrEmpty(id) || !inventory.CheckIfItemExistInPropertyList(id))
                        continue;

                    var property = inventory.GetItemProperty(id);
                    if (property == null)
                        continue;

                    var data = TryGetAdditionalData(car, part, property.SpecialGroup);
                    if (data == null)
                        continue;

                    if (allMarked)
                    {
                        var selected = selectedItems.Find(item => item.ID == id && item.Data.Equals(data));
                        if (selected == null)
                            selectedItems.Add(new SelectedItem { ID = id, Data = data, Amount = 1 });
                        else
                            selected.Amount++;
                        continue;
                    }

                    AddOne(ui, shopList, id, data);
                    return;
                }
                catch (Exception exception)
                {
                    MelonLogger.Warning($"Could not add selected part '{part.id}' to shopping list: {exception}");
                    if (!allMarked)
                        return;
                }
            }
        }

        if (!allMarked)
            return;

        var totalAdded = 0;
        foreach (var selected in selectedItems)
        {
            try
            {
                totalAdded += ShoppingListQuantity.TopUp(selected.Amount,
                    () => GetAmount(shopList, selected.ID, selected.Data),
                    () => shopList.AddToShopList(selected.ID, string.Empty, selected.Data));
            }
            catch (Exception exception)
            {
                MelonLogger.Warning($"Could not top up '{selected.ID}' in shopping list: {exception}");
            }
        }

        if (totalAdded > 1)
        {
            ui.ShowPopup("Shopping list", $"{totalAdded} items added to shopping list", PopupType.Normal);
        }
    }

    private static void TransferSelectedCarPart(CarInfoWindow window)
    {
        MelonLogger.Msg("TransferSelectedCarPart started");
        var tab = window.partsTab;
        var pages = tab?.partsTabPageManager;
        var car = window.currentCarLoader;
        var ui = UIManager.Get();
        var shopList = ui?.ShopListWindow;
        var inventory = Singleton<GameInventory>.Instance;
        if (tab == null || tab.gridNavigationManager == null || pages == null ||
            pages.itemObjects == null || pages.parts == null || pages.IsPageAnimationInProgress() ||
            car == null || shopList == null || inventory == null)
        {
            MelonLogger.Msg("TransferSelectedCarPart invalid UI state");
            return;
        }

        var row = tab.GetItemFromGrid();
        if (row == null || !row.gameObject.activeInHierarchy ||
            row.selected == null || !row.selected.activeInHierarchy)
        {
            MelonLogger.Msg("TransferSelectedCarPart invalid row state");
            return;
        }

        var selectedIndex = -1;
        for (var i = 0; i < pages.currentPageItemsCount && i < pages.itemObjects.Length; i++)
        {
            if (pages.itemObjects[i] == row)
            {
                selectedIndex = pages.currentPage * pages.ItemsPerPage + i;
                break;
            }
        }
        if (selectedIndex < 0 || selectedIndex >= pages.parts.Count)
        {
            MelonLogger.Msg("TransferSelectedCarPart no current row");
            return;
        }

        try
        {
            var selected = pages.parts[selectedIndex];
            // Body rows have no MarkAction. Keep the exact catalog ID used by the
            // game to create this display model (including car and tuning variant).
            if (CarBodyShoppingListIds.TryGetId(selected, out var bodyId))
            {
                var bodyData = new ShopListItemDataEx();
                bodyData.Reset();
                AddOne(ui, shopList, bodyId, bodyData);
                return;
            }

            // Mechanical rows capture their PartScript even when unexamined or
            // unmounted. Examination controls condition visibility, not identity.
            var target = selected.MarkAction?.Target;
            var part = target?.TryCast<CarInfoPartsHelper.__c__DisplayClass3_0>()?.part
                ?? target?.TryCast<CarInfoPartsHelper.__c__DisplayClass4_0>()?.part
                ?? target?.TryCast<CarInfoPartsHelper.__c__DisplayClass5_0>()?.part;
            if (part == null)
            {
                MelonLogger.Msg("TransferSelectedCarPart could not resolve selected part");
                return;
            }

            var id = part.GetIDWithTuned();
            if (string.IsNullOrEmpty(id) || !inventory.CheckIfItemExistInPropertyList(id))
            {
                MelonLogger.Msg($"TransferSelectedCarPart item '{id}' not in inventory");
                return;
            }

            var property = inventory.GetItemProperty(id);
            if (property == null)
            {
                MelonLogger.Msg($"TransferSelectedCarPart item '{id}' has no property");
                return;
            }

            var data = TryGetAdditionalData(car, part, property.SpecialGroup);
            if (data != null)
            {
                AddOne(ui, shopList, id, data);
            }
            else
            {
                MelonLogger.Msg("TransferSelectedCarPart no additional data available");
            }
        }
        catch (Exception exception)
        {
            MelonLogger.Warning($"Could not add selected car-list part to shopping list: {exception}");
        }
    }

    private static void AddOne(UIManager ui, ShopListWindow shopList, string id, ShopListItemDataEx data)
    {
        var previousAmount = GetAmount(shopList, id, data);
        shopList.AddToShopList(id, string.Empty, data);
        var added = GetAmount(shopList, id, data) - previousAmount;
        ui.ShowPopup("Shopping list", $"{added} item added to shopping list", PopupType.Normal);
    }

    private sealed class SelectedItem
    {
        public string ID;
        public ShopListItemDataEx Data;
        public int Amount;
    }

    private static int GetAmount(ShopListWindow shopList, string id, ShopListItemDataEx data)
    {
        if (shopList.items == null)
            return 0;

        var amount = 0;
        foreach (var item in shopList.items)
        {
            if (item.ID == id && item.AdditionalData.Equals(data))
                amount += item.Amount;
        }

        return amount;
    }

    private static ShopListItemDataEx TryGetAdditionalData(CarLoader car, PartScript part, SpecialGroup group)
    {
        var data = new ShopListItemDataEx();
        data.Reset();
        if (group != SpecialGroup.Tire && group != SpecialGroup.Rim)
        {
            return group != SpecialGroup.OilDrainCheckFill ? data : null;
        }

        var wheel = GetWheel(car, part.transform);
        if (wheel == null)
        {
            MelonLogger.Warning($"Cannot determine wheel dimensions for selected part '{part.id}'.");
            return null;
        }

        // Match the data written by GameScript.UpdateRaycastOnItemNameBonusText.
        data.Size = (int)wheel.Size;
        if (group == SpecialGroup.Tire)
        {
            data.Tire = true;
            data.Width = (int)wheel.Width;
            data.Profile = (int)wheel.Profile;
        }
        else
        {
            data.Rim = true;
            data.ET = wheel.ET;
        }

        return data;
    }

    private static Wheel GetWheel(CarLoader car, Transform part)
    {
        if (IsUnder(part, car.GetWheelFLHandle()))
            return car.GetWheel(WheelType.FrontLeft);
        if (IsUnder(part, car.GetWheelFRHandle()))
            return car.GetWheel(WheelType.FrontRight);
        if (IsUnder(part, car.GetWheelRLHandle()))
            return car.GetWheel(WheelType.RearLeft);
        if (IsUnder(part, car.GetWheelRRHandle()))
            return car.GetWheel(WheelType.RearRight);

        return null;
    }

    private static bool IsUnder(Transform part, GameObject wheelHandle)
    {
        return wheelHandle != null && part.IsChildOf(wheelHandle.transform);
    }
}
