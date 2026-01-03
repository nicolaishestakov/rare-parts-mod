using CMS.UI;
using CMS.UI.Logic;
using CMS.UI.Windows;
using MelonLoader;
using UnityEngine;

namespace RareParts;

internal static class Debug
{
    /// <summary>
    /// Debug method to show the current scene.
    /// </summary>
    public static void ShowSceneName(string currentScene)
    {
        UIManager.Get().ShowPopup(BuildInfo.Name, $"Scene: {currentScene}", PopupType.Normal);
        MelonLogger.Msg($"Scene: {currentScene}");
    }
    
    /// <summary>
    /// Debug method to show the current open window's name.
    /// </summary>
    public static void ShowWindowName()
    {
        var windowManager = WindowManager.Instance;
        if (windowManager != null)
        {
            if (windowManager.activeWindows.Count > 0)
            {
                UIManager.Get().ShowPopup(BuildInfo.Name, $"Window: {windowManager.GetLastOpenedWindow().name}",
                    PopupType.Normal);
                MelonLogger.Msg($"Window: {windowManager.GetLastOpenedWindow().name}");
            }
        }
    }

    public static void LogWindowsState()
    {
        var windowManager = WindowManager.Instance;

        var activeWindows = windowManager.activeWindows;
        
        MelonLogger.Msg($"Active Windows count: {activeWindows.Count}");

        foreach (var activeWindow in activeWindows)
        {
            MelonLogger.Msg($"{activeWindow}");
        }
        
        foreach (var windowID in activeWindows)
        {
            var window = windowManager.GetWindowByID(windowID);
            MelonLogger.Msg($"{windowID} name: {window.name}, enabled: {window.enabled}, is active: {window.IsActive}");

            if (windowID == WindowID.Inventory)
            {
                LogInventoryWindowsState(windowManager.GetWindowByID<InventoryWindow>(windowID));
            }
            else if (windowID == WindowID.ItemsExchange)
            {
                LogItemsExchangeWindowsState(windowManager.GetWindowByID<ItemsExchangeWindow>(windowID));
            }
        }
        
        GameObject shopCarWindow = GameObject.Find("ShopCarWindow");
        if (shopCarWindow is not null)
        {
            LogUnityObject(shopCarWindow);
        }
    }

    public static void LogInventoryState()
    {
        var gameManager = Singleton<GameManager>.Instance;
        var tempInventory = gameManager.TempInventory;
        
        var profileManager = Singleton<ProfileManager>.Instance;
        var profileData = profileManager.GetSelectedProfileData();
        var userInventoryData = profileData.inventoryData;
        
        MelonLogger.Msg($"User Inventory lastUId: {userInventoryData.lastUId}, items count: {userInventoryData.items.Count}, group count: {userInventoryData.groups.Count}");
        MelonLogger.Msg("Items:");
        foreach (var item in userInventoryData.items)
        {
            LogItemState(item);
        }

        MelonLogger.Msg("Groups:");
        foreach (var item in userInventoryData.groups)
        {
            LogItemState(item);
        }
        
        MelonLogger.Msg($"Temp Inventory items count: {tempInventory.items.Count}");
        MelonLogger.Msg("Items:");
        foreach (var item in tempInventory.items)
        {
            LogItemState(item);
        }
    }

    public static void LogInventoryWindowsState(InventoryWindow window)
    {
        if (window is null)
        {
            MelonLogger.Warning("Not inventory window");
            return;
        }
        MelonLogger.Msg($"Inventory Window, current category: {window.currentCategory}, current page: {window.currentPage}, is garage: {window.isGarage}, was collected: {window.WasCollected}, tag: {window.tag}");

        MelonLogger.Msg("Items:");

        foreach (var item in window.items)
        {
            LogItemState(item);
        }
    }

    public static void LogItemsExchangeWindowsState(ItemsExchangeWindow window)
    {
        if (window is null)
        {
            MelonLogger.Warning("Not items exchange window");
            return;
        }
        MelonLogger.Msg($"Items Exchange Window, current tab: {window.currentTab}, was collected: {window.WasCollected}, tag: {window.tag}");

        var junk = window.Junk;
        if (junk is not null)
        {
            MelonLogger.Msg(
                $"Junk, items count: {junk.ItemsInTrash?.Count}, enabled: {junk.enabled}, active and enabled: {junk.isActiveAndEnabled}.");
        }
        
        var foundTab = window.foundTab;
        if (foundTab is not null)
        {
            MelonLogger.Msg($"Found Tab, items count: {foundTab.items?.Count}, enabled: {foundTab.enabled}, active: {foundTab.IsActive}, inventory items count: {foundTab.itemObjects?.Count}.");

            if (foundTab.items is not null)
            {
                foreach (var item in foundTab.items)
                {
                    LogItemState(item);
                }
            }
        }

        var collectedTab = window.collectedTab;
        if (collectedTab is not null)
        {
            MelonLogger.Msg($"Collected Tab, items count: {collectedTab.items?.Count}, enabled: {collectedTab.enabled}, active: {collectedTab.IsActive}, inventory items count: {collectedTab.itemObjects?.Count}.");
            if (collectedTab.items is not null)
            {
                foreach (var item in collectedTab.items)
                {
                    LogItemState(item);
                }
            }
        }
    }

    public static void LogItemState(BaseItem item)
    {
        var asItem = item.TryCast<Item>();
        MelonLogger.Msg($"{item.ID}, name: {item.GetLocalizedName()}, UID: {item.UID}, Pointer: {item.Pointer}, non-repairable: {item.IsNonRepairable}, rare: {item.IsRare}, special repairable: {item.IsSpecialRepairable}, repair amount: {asItem?.RepairAmount}");
    }
    
    public static void LogWarehouseItems()
    {
        var warehouse = Singleton<Warehouse>.Instance;

        MelonLogger.Msg($"Warehouse selected option: {warehouse.SelectedOption}");

        MelonLogger.Msg($"Warehouse group list count: {warehouse.warehouseGroupList.Count}");

        for (var i = 0; i < warehouse.warehouseGroupList.Count; i++)
        {
            var groupList = warehouse.warehouseGroupList[i];
            for (var j = 0; j < groupList.Count; j++)
            {
                var groupItem = groupList[j];
                MelonLogger.Msg($"Warehouse group [{i}, {j}]: {groupItem.ID} with {groupItem.ItemList.Count} items");
            }
        }

        MelonLogger.Msg($"Warehouse item list count: {warehouse.warehouseList.Count}");
        for (var i = 0; i < warehouse.warehouseList.Count; i++)
        {
            var itemList = warehouse.warehouseList[i];
            for (var j = 0; j < itemList.Count; j++)
            {
                var item = itemList[j];
                MelonLogger.Msg(
                    $"Warehouse item [{i}, {j}]: {item.ID} with repair amount: {item.RepairAmount}, condition: {item.Condition}");
            }
        }
    }
    
    /// <summary>
    /// Debug method to show the current selected category.
    /// </summary>
    public static void ShowCurrentCategory()
    {
        string categoryName = string.Empty;
        var windowManager = WindowManager.Instance;
        if (windowManager != null)
        {
            if (windowManager.activeWindows.count > 0)
            {
                if (windowManager.IsWindowActive(WindowID.Warehouse))
                {
                    var warehouseWindow = windowManager.GetWindowByID<WarehouseWindow>(WindowID.Warehouse);
                    if (warehouseWindow.currentTab == 0)
                    {
                        var inventoryTab = warehouseWindow.warehouseInventoryTab;
                        categoryName = inventoryTab.currentCategory.ToString();
                    }
                    else
                    {
                        var warehouseTab = warehouseWindow.warehouseTab;
                        categoryName = warehouseTab.currentCategory.ToString();
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            UIManager.Get().ShowPopup(BuildInfo.Name, $"Selected Category: {categoryName}", PopupType.Normal);
            MelonLogger.Msg($"Selected Category: {categoryName}");
        }
    }

    /// <summary>
    /// Debug method to show if maps or cases are in any of the inventories.
    /// </summary>
    public static void ShowMapsAndCases(Scene currentScene)
    {
        if (currentScene.IsGarage)
        {
            var inventory = Singleton<Inventory>.Instance;
            var invItems = inventory.GetAllItemsAndGroups();
            if (invItems != null)
            {
                var (mapCount, caseCount) = invItems.GetMapAndCaseCounts();

                if (mapCount > 0 || caseCount > 0)
                {
                    MelonLogger.Msg($"Inventory Maps: {mapCount} Cases: {caseCount}");
                }
            }

            var warehouse = Singleton<Warehouse>.Instance;
            var wareItems = warehouse.GetAllItemsAndGroups();
            if (wareItems != null)
            {
                var (mapCount, caseCount) = wareItems.GetMapAndCaseCounts();

                if (mapCount > 0 || caseCount > 0)
                {
                    MelonLogger.Msg($"Warehouse Maps: {mapCount} Cases: {caseCount}");
                }
            }
        }

        if (currentScene.IsBarnOrJunkyard)
        {
            var junks = UnityEngine.Object.FindObjectsOfType<Junk>();
            if (junks != null)
            {
                int mapCount = 0;
                int caseCount = 0;
                
                foreach (var junk in junks)
                {
                    var junkItems = junk.ItemsInTrash;
                    if (junkItems != null)
                    {
                        var mapCaseCount = junkItems.GetMapAndCaseCounts();
                        mapCount += mapCaseCount.MapCount;
                        caseCount += mapCaseCount.CaseCount;
                    }
                }

                if (mapCount > 0 || caseCount > 0)
                {
                    MelonLogger.Msg($"Stash Maps: {mapCount} Cases: {caseCount}");
                }
            }

            var tempInvItems = Singleton<GameManager>.Instance?.TempInventory?.items;

            if (tempInvItems != null)
            {
                var (mapCount, caseCount) = tempInvItems.GetMapAndCaseCounts();

                if (mapCount > 0 || caseCount > 0)
                {
                    MelonLogger.Msg($"Shopping Cart Maps: {mapCount} Cases: {caseCount}");
                }
            }
        }
    }
    
    public static void LogUnityObject(GameObject parent)
    {
        LogUnityObject(parent?.transform);
    }

    public static void LogUnityObject(Transform obj, bool recursive = true)
    {
        if (obj == null) return;

        int childCount = obj.childCount;

        for (int i = 0; i < childCount; i++)
        {
            Transform currentChild = obj.GetChild(i);
            MelonLogger.Msg($"[{i}]: {currentChild.gameObject.name}, type: {currentChild.gameObject.GetIl2CppType().Name}");

            // foreach (var component in currentChild.GetComponents<Component>())
            // {
            //     MelonLogger.Msg($"  Component: {component.name}, type: {component.GetIl2CppType().Name}");
            // }

            var showroomCarItem = currentChild.GetComponent<ShowroomCarItem>();
            if (showroomCarItem != null)
            {
                MelonLogger.Msg($"{showroomCarItem.ID} - {showroomCarItem.carName?.text}");
            }
            
            if (recursive)
            {
                LogUnityObject(currentChild, false);
            }
        }
    }
}