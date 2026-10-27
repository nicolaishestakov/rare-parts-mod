using System;
using System.Collections.Generic;
using CMS.Containers;
using CMS.UI;
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
        if (window == null || !window.IsActive || window.currentTab != CarInfoTabs.Order)
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

                    var previousAmount = GetAmount(shopList, id, data);
                    shopList.AddToShopList(id, string.Empty, data);
                    var added = GetAmount(shopList, id, data) - previousAmount;
                    ui.ShowPopup("Shopping list", $"{added} item added to shopping list", PopupType.Normal);
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
