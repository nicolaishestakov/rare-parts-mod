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
    public void Transfer()
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
        var task = tab?.orderTabPageManager?.currentJobTask;
        var car = tab?.carLoader;
        var ui = UIManager.Get();
        var shopList = ui?.ShopListWindow;
        if (task?.Parts == null || car == null || shopList == null)
            return;

        // JobPart.ID identifies a position on the car, not a catalog item.
        // Include every page of the selected issue, but no other issue's parts.
        var issuePaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in task.Parts)
            issuePaths.Add(part.ID);

        var selectedItems = new List<SelectedItem>();
        var parts = car.GetPartScriptCache();
        var inventory = Singleton<GameInventory>.Instance;
        if (parts != null && inventory != null)
        {
            var pathBuilder = new Il2CppSystem.Text.StringBuilder();
            foreach (var part in parts)
            {
                // The order tab's star action updates this flag immediately.
                if (part == null || !part.markImportantPart || !part.IsExamined)
                    continue;

                try
                {
                    pathBuilder.Clear();
                    if (!issuePaths.Contains(part.GetGameObjectPathWithoutRoot(pathBuilder)))
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
                    
                    var selected = selectedItems.Find(item => item.ID == id && item.Data.Equals(data));
                    if (selected == null)
                        selectedItems.Add(new SelectedItem { ID = id, Data = data, Amount = 1 });
                    else
                        selected.Amount++;
                }
                catch (Exception exception)
                {
                    MelonLogger.Warning($"Could not add marked part '{part.id}' to shopping list: {exception}");
                }
            }
        }

        var added = 0;
        foreach (var selected in selectedItems)
        {
            try
            {
                added += ShoppingListQuantity.TopUp(selected.Amount,
                    () => GetAmount(shopList, selected.ID, selected.Data),
                    () => shopList.AddToShopList(selected.ID, string.Empty, selected.Data));
            }
            catch (Exception exception)
            {
                MelonLogger.Warning($"Could not top up '{selected.ID}' in shopping list: {exception}");
            }
        }

        ui.ShowPopup("Shopping list", $"{added} items added to shopping list", PopupType.Normal);
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
            MelonLogger.Warning($"Cannot determine wheel dimensions for marked part '{part.id}'.");
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
