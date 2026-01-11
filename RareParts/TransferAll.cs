using CMS.Helpers;
using CMS.UI;
using CMS.UI.Windows;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CMS.Containers;
using CMS.UI.Logic.Warehouse;
using RareParts.Extensions;
using UnityEngine.UI;

// based on https://github.com/mannly01/TransferAll

namespace RareParts;

public class TransferAll
{
    public TransferAll(ITransferAllConfig config, TempInventoryManager tempInventoryManager)
    {
        _config = config;
        _tempInventoryManager = tempInventoryManager;
    }

    private readonly ITransferAllConfig _config;

    private readonly TempInventoryManager _tempInventoryManager;

    /// <summary>
    /// Global reference to the current scene.
    /// </summary>
    private readonly Scene _currentScene = new(string.Empty);

    /// <summary>
    /// Global reference to verify if the Warehouse Expansion is unlocked.
    /// </summary>
    private bool? _isWarehouseUnlocked = null;
    
    private bool IsWarehouseUnlocked
    {
        get
        {
            _isWarehouseUnlocked ??= CheckIfWarehouseIsUnlocked();
            return _isWarehouseUnlocked.Value;
        }
    }

    /// <summary>
    /// QoLmod shows a popup for every part inside a group as its moved and
    /// this causes some serious performance issues during bulk moves,
    /// so these references allow these settings to be disabled temporarily.
    /// </summary>
    private QolSettings _qolSettings = null;

    /// <summary>
    /// Global reference used to temporarily hold the individual junk stashes in the Barn and Junkyard.
    /// This allows the individual stashes to be "undone" and transferred back.
    /// </summary>
    private Dictionary<IntPtr, List<Item>> _tempItems = new ();

    private List<Item> GetJunkTempStorageItems(IntPtr junkStash)
    {
        if (_tempItems.TryGetValue(junkStash, out var items))
        {
            return items;
        }
        var newList = new List<Item>();
        _tempItems.Add(junkStash, newList);
        return newList;
    }

    /// <summary>
    /// Global reference used to temporarily hold the individual junk stashes in the Barn and Junkyard.
    /// This allows the individual stashes to be "undone" and transferred back.
    /// </summary>
    private Dictionary<IntPtr, List<GroupItem>> _tempGroups = new ();

    private List<GroupItem> GetJunkTempStorageGroups(IntPtr junkStash)
    {
        if (_tempGroups.TryGetValue(junkStash, out var groups))
        {
            return groups;
        }
        var newList = new List<GroupItem>();
        _tempGroups.Add(junkStash, newList);
        return newList;
    }
    
    public enum Mode
    {
        ByCondition,
        ByConditionSkipUnrepairable,
        OnlyRareRepairable
    }

    public Mode TransferMode { get; private set; } = Mode.OnlyRareRepairable;

    public void OnLateInitializeMelon() //TODO rename
    {
        _qolSettings = QolSettings.CreateFromLoadedMelonAssembly($"{Directory.GetCurrentDirectory()}\\Mods\\QoLmod.dll");
    }

    public void OnSceneWasInitialized(int buildIndex, string sceneName) // TODO rename
    {
        if (buildIndex == -1)
        {
            return;
        }

        // Save a reference to the current scene.
        _currentScene.UpdateScene(sceneName);
        
        if (_currentScene.IsBarnOrJunkyard)
        {
            _tempItems.Clear();
            _tempGroups.Clear();
            _tempInventoryManager.Invalidate();
            MelonLogger.Msg($"Leaving {sceneName}");
        }
    }

    public void OnSceneWasUnloaded(int buildIndex, string sceneName) // TODO rename
    {
        if (buildIndex == -1)
        {
            return;
        }

        // Clear the temporary dictionaries when the user leaves the scene.
        if (new Scene(sceneName).IsBarnOrJunkyard)
        {
            _tempItems.Clear();
            _tempGroups.Clear();
            _tempInventoryManager.Clear();
            MelonLogger.Msg($"Leaving {sceneName}");
        }
    }
    
    public void TransferAllItemsAndGroups(bool commandWithModifier)
    {
        // Check if the user is currently using the Search box.
        if (!CheckIfInputIsFocused())
        {
            // Check that the user is in the Garage.
            if (_currentScene.IsGarage)
            {
                if (IsWarehouseUnlocked)
                {
                    // Do work on the Inventory or Warehouse.
                    MoveInventoryOrWarehouseItems(moveOnlyDuplicateItems: commandWithModifier);
                }
                else
                {
                    // The user hasn't upgraded their Garage, so show them a message.
                    Popup("You must unlock the Warehouse Expansion first.");
                    MelonLogger.Msg("Warehouse has not been unlocked");
                }
            }
            // Check that the user is in the Barn or Junkyard.
            else if (_currentScene.IsBarnOrJunkyard)
            {
                // Do work on the Junk and TempInventory.
                MoveBarnOrJunkyardStashItems(GetShopListItems(), onlyShopListItems: commandWithModifier);
            }
            else
            {
                // The user is not at the Garage, so show them a message.
                Popup("This function only works at the Garage, Barn or Junkyard");
                MelonLogger.Msg("TransferEntireJunkyardOrBarn key pressed outside of Garage, Barn or Junkyard");
            }
        }
        else
        {
            MelonLogger.Msg("Search box has focus");
        }
    }

    public void TransferEntireJunkyardOrBarn(bool onlyShopListItems)
    {
        // Check if the user is currently using the Seach box.
        if (!CheckIfInputIsFocused())
        {
            // Check that the user is in the Barn or Junkyard.
            if (_currentScene.IsBarnOrJunkyard)
            {
                // Do work on the Junk and TempInventory.
                MoveEntireBarnOrJunkyard(GetShopListItems(), onlyShopListItems);
            }
            else
            {
                // The user is not at the Barn or Junkyard, so show them a message.
                Popup("This function only works at the Barn or Junkyard");
                MelonLogger.Msg("TransferEntireJunkyardOrBarn key pressed outside of Barn or Junkyard");
            }
        }
        else
        {
            MelonLogger.Msg("Search box has focus");
        }
    }

    public enum MoveShoppingListMode
    {
        MoveNewOrRepairableAndDeleteFromShoppingList,
        MoveRareUnrepairable
    }
    
    public void MoveShoppingListFromWarehouseToInventory(MoveShoppingListMode mode)
    {
        var inventory = Singleton<Inventory>.Instance;
        var warehouse = Singleton<Warehouse>.Instance;
        var uiManager = UIManager.Get();

        var shopListWindow = uiManager.ShopListWindow;

        if (shopListWindow == null)
        {
            MelonLogger.Msg("shopListWindow is null");
            return;
        }

        var moved = false;
        var shoppingListModified = false;

        foreach (var shopItem in shopListWindow.items.ToEnumerable().ToList())
        {
            MelonLogger.Msg($"Looking for {shopItem.ID}");
            var found = warehouse.FindItems(shopItem.ID, _config.MoveShoppingListWarehouses);
            MelonLogger.Msg($"Found {found.Count} items");

            var wantedAmount = shopItem.Amount;

            foreach (var item in found)
            {
                if (mode == MoveShoppingListMode.MoveNewOrRepairableAndDeleteFromShoppingList)
                {

                    if (wantedAmount > 0 && item.Condition >= item.MinConditionToRepair)
                    {
                        MoveItemFromWareHouseToInventory(inventory, warehouse, shopListWindow, shopItem, item, true);    
                        wantedAmount--;
                        moved = true;
                        shoppingListModified = true;
                    }
                    else
                    {
                        MelonLogger.Msg($"Item {item.ID} condition {item.Condition * 100f} is not repairable so will not be moved");
                    }
                }
                else if (mode == MoveShoppingListMode.MoveRareUnrepairable)
                {
                    if (item.IsRare && item.Condition < item.MinConditionToRepair)
                    {
                        MoveItemFromWareHouseToInventory(inventory, warehouse, shopListWindow, shopItem, item, false);
                        moved = true;
                    }
                }
            }
        }

        if (shoppingListModified && shopListWindow.IsActive)
        {
            MelonLogger.Msg("shopListWindow is active, trying to refresh");
            shopListWindow.Hide(true);
            shopListWindow.Show();
        }

        if (!moved)
        {
            Popup("Nothing found", "Moving from warehouse to inventory");
        }
    }

    private void MoveItemFromWareHouseToInventory(
        Inventory inventory, 
        Warehouse warehouse, 
        ShopListWindow shopListWindow,
        ShopListItemData shopListItem, 
        Item warehouseItem,
        bool deleteMovedFromShoppingList)
    {
        var itemName = warehouseItem.GetLocalizedName();
        MelonLogger.Msg($"Moving Item {itemName}" + (deleteMovedFromShoppingList? "": ", keep in the shopping list"));

        if (deleteMovedFromShoppingList)
        {
            shopListWindow.RemoveFromShopList(shopListItem.ID, shopListItem.AdditionalData, false);
        }

        inventory.Add(warehouseItem);
        if (!warehouse.DeleteRobust(warehouseItem))
        {
            MelonLogger.Msg($"Failed to delete item {itemName} from warehouse");
        }
        else
        {
            var leftItems = warehouse.FindItems(warehouseItem.ID, _config.MoveShoppingListWarehouses);
            if (leftItems.Count == 0)
            {
                MelonLogger.Msg($"No more {itemName} left in movable warehouse stores");
            }
            else
            {
                MelonLogger.Msg($"Still {leftItems.Count} are in movable warehouse stores");
            }
        }

        UIManager.Get().ShowPopup("Moved from warehouse to inventory", itemName,
            PopupType.Normal);
    }


    private HashSet<string> GetShopListItems()
    {
        var shopListWindow = UIManager.Get().ShopListWindow;
        
        if (shopListWindow is null)
        {
            MelonLogger.Msg("shopListWindow is null");
        }
        else
        {
            return shopListWindow.items.ToEnumerable().Select(x => x.ID).ToHashSet();
        }

        return [];
    }

    public void OnMinPartConditionChanged()
    {
        Popup($"Transfer Part Condition: {_config.MinPartCondition}%");
    }

    public void SwitchTransferMode()
    {
        TransferMode = TransferMode switch
        {
            Mode.ByCondition => Mode.ByConditionSkipUnrepairable,
            Mode.ByConditionSkipUnrepairable => Mode.OnlyRareRepairable,
            Mode.OnlyRareRepairable => Mode.ByCondition,
            _ => Mode.ByCondition
        };

        string transferMode = TransferMode switch
        {
            Mode.ByCondition => $"Starting from {_config.MinPartCondition}% condition",
            Mode.ByConditionSkipUnrepairable =>
                $"Starting from {_config.MinPartCondition}% condition and skip unrepairable",
            Mode.OnlyRareRepairable => "Only rare repairable parts",
            _ => "Unknown"
        };

        Popup($"Transfer Mode: {transferMode}");
    }

    /// <summary>
    /// This mod only works if the Warehouse has been unlocked
    /// as an Expansion, so check if it has been unlocked.
    /// </summary>
    /// <returns>(bool) True if the Warehouse Expansion has been unlocked.</returns>
    private static bool CheckIfWarehouseIsUnlocked()
    {
        var garageLevelManager = Singleton<GarageLevelManager>.Instance;
        // The GarageLevelManager seems to be the easiest way to find out.
        if (garageLevelManager != null)
        {
            // Get a reference to the Garage and Tools Tab of the Toolbox.
            var garageAndToolsTab = garageLevelManager.garageAndToolsTab;
            // The upgradeItems list isn't populated until the Tab becomes active,
            // this fakes the manager into populating the list.
            garageAndToolsTab.PrepareItems();
            // The Warehouse Expansion is item 8 (index 7) in the list.
            var warehouseUpgrade = garageAndToolsTab.upgradeItems[7];
#if DEBUG
            // Debug to show the status.
            MelonLogger.Msg($"Warehouse Unlocked: {warehouseUpgrade.IsUnlocked}");
#endif
            return warehouseUpgrade.IsUnlocked;
        }

        // Default to failing.
        return false;
    }

    /// <summary>
    /// If the user is using the Search box,
    /// the mod should do nothing.
    /// </summary>
    /// <returns>(bool) True if the Search/Input Field is being used.</returns>
    private static bool CheckIfInputIsFocused()
    {
        var inputFields = UnityEngine.Object.FindObjectsOfType<InputField>();
        foreach (var inputField in inputFields)
        {
            if (inputField != null)
            {
                if (inputField.isFocused)
                {
#if DEBUG
                    MelonLogger.Msg("Input Field Focused");
#endif
                    return true;
                }
            }
        }

        return false;
    }
   

    /// <summary>
    /// Method to move Inventory items to a Warehouse (called from MoveInventoryOrWarehouseItems).
    /// </summary>
    /// <param name="invItems">The list of items to move.</param>
    /// <param name="inventory">The user's Inventory.</param>
    /// <param name="warehouse">The Garage Warehouse.</param>
    /// <returns>(ItemCount, GroupCount) Tuple with the number of items/groups that were moved.</returns>
    private (int items, int groupItems) MoveInventoryItemsToWarehouse(
        IEnumerable<BaseItem> invItems,
        Inventory inventory, Warehouse warehouse)
    {
        // Disable QoLmod settings temporarily.
        using var guard = new QolSettingsGuard(_qolSettings);
        _qolSettings.ShowPopupForAllPartsInGroup = false;
        _qolSettings.ShowPopupForGroupAddedInventory = false;

        int invItemCount = 0;
        int invGroupCount = 0;
        
        foreach (var baseItem in invItems)
        {
            var item = baseItem.TryCast<Item>();
            // Try to cast the BaseItem to an Item.
            if (item != null)
            {
                // The BaseItem is an Item, so add it to the current Warehouse.
                warehouse.Add(item);
                // Delete the Item from the Inventory.
                inventory.Delete(item);
                // Increment the temporary count of items.
                invItemCount++;
            }

            var group = baseItem.TryCast<GroupItem>();
            // Try to cast the BaseItem to a GroupItem.
            if (group != null)
            {
                // The BaseItem is a GroupItem, so add it to the current Warehouse.
                warehouse.Add(group);
                // Delete the GroupItem from the Inventory.
                inventory.DeleteGroup(baseItem.UID);
                // Increment the temporary count of groups.
                invGroupCount++;
            }
        }

        // Return the number of Items and Groups that were moved.
        return (invItemCount, invGroupCount);
    }

    private (IList<Item> Items, IList<GroupItem> Groups) GetItemsToMoveFromJunk(
        Il2CppSystem.Collections.Generic.List<BaseItem> junkItems, 
        HashSet<string> includeShopListItems,
        bool onlyShopListItems)
    {
        var items = new List<Item>();
        var groups = new List<GroupItem>();

        if (!onlyShopListItems && 
            (_config.TransferMapsOrCasesOnlyAtBarnOrJunkyard || _config.TransferPartsOnlyAtBarnOrJunkyard))
        {
            if (_config.TransferMapsOrCasesOnlyAtBarnOrJunkyard)
            {
                items.AddRange(junkItems.ToEnumerable()
                    .Where(x => x.IsMap || x.IsCase)
                    .Select(x => x.TryCast<Item>())
                    .Where(x => x is not null));
            }

            if (_config.TransferPartsOnlyAtBarnOrJunkyard)
            {
                items.AddRange(UIHelper.GetBodyItems(junkItems).ToEnumerable()
                    .Select(x => x.TryCast<Item>())
                    .Where(x => x is not null));
            }

            return (items, groups);
        }

        foreach (var junkItem in junkItems)
        {
            if (IsToBeMoved(junkItem, includeShopListItems, onlyShopListItems))
            {
                var item = junkItem.TryCast<Item>();
                if (item != null)
                {
                    items.Add(item);
                }
                else
                {
                    var group = junkItem.TryCast<GroupItem>();
                    if (group != null)
                    {
                        groups.Add(group);
                    }
                }
            }
        }

        return (items, groups);
    }

    private bool IsToBeMoved(BaseItem baseItem, HashSet<string> includeShopListItems, bool onlyShopListItems)
    {
        //MelonLogger.Msg($"Checking {baseItem.ID}");

        if (baseItem.IsMap || baseItem.IsCase)
        {
            // map or case - always moved
            return true;
        }

        if (includeShopListItems.Contains(baseItem.ID))
        {
            Popup($"Shop list item found: {baseItem.GetLocalizedName()}");
            return true;
        }

        if (onlyShopListItems)
        {
            return false;
        }

        var id = baseItem.ID;
        var condition = baseItem.GetCondition();

        if (TransferMode == Mode.OnlyRareRepairable)
        {
            if (!baseItem.IsRare || baseItem.IsNonRepairable)
            {
                return false;
            }

            return
                (baseItem.IsSpecialRepairable && condition >= PartsInfo.SpecialRepairablePartsMinCondition) ||
                (!baseItem.IsSpecialRepairable && condition >= PartsInfo.NormalRepairablePartsMinCondition);
        }

        if (TransferMode == Mode.ByCondition)
        {
            return condition * 100 >= _config.MinPartCondition;
        }

        if (TransferMode == Mode.ByConditionSkipUnrepairable)
        {
            if (condition * 100 < _config.MinPartCondition)
            {
                return false;
            }

            var item = baseItem.TryCast<Item>();

            if (item == null)
            {
                // Move group?
                MelonLogger.Msg($"{id} moved as a group?");
                return true;
            }

            // Check if non-repairable
            if (baseItem.IsNonRepairable ||
                (baseItem.IsSpecialRepairable &&
                 condition < PartsInfo.SpecialRepairablePartsMinCondition))
            {
                MelonLogger.Msg($"{id} not included as non-repairable");
                return false;
            }

            return true;
        }

        MelonLogger.Error($"Unknown transfer mode {TransferMode}. Item {id} not moved");
        return false;
    }

    /// <summary>
    /// Method to move Warehouse items to the user's Inventory (called from MoveInventoryOrWarehouseItems).
    /// </summary>
    /// <param name="warehouseItems">The list of items to move.</param>
    /// <param name="inventory">The user's Inventory.</param>
    /// <param name="warehouse">The Garage Warehouse.</param>
    /// <returns>(ItemCount, GroupCount) Tuple with the number of items/groups that were moved.</returns>
    private (int items, int groupItems) MoveWarehouseItemsToInventory(
        IEnumerable<BaseItem> warehouseItems,
        Inventory inventory, Warehouse warehouse)
    {
#if DEBUG
        MelonLogger.Msg($"Called");
#endif
        // Disable QoLmod settings temporarily.
        using var guard = new QolSettingsGuard(_qolSettings);
        _qolSettings.ShowPopupForAllPartsInGroup = false;
        _qolSettings.ShowPopupForGroupAddedInventory = false;
        
        // Setup temporary counts to return at the end.
        int wareItemCount = 0;
        int wareGroupCount = 0;
        
        foreach (var baseItem in warehouseItems)
        {
            // Try to cast the BaseItem to an Item.
            if (baseItem.TryCast<Item>() != null)
            {
                // The BaseItem is an Item, so add it to the user's Inventory.
                inventory.Add(baseItem.TryCast<Item>());
                // Delete the Item from the Warehouse.
                warehouse.Delete(baseItem.TryCast<Item>());
                // Increment the temporary count of items.
                wareItemCount++;
            }

            // Try to cast the BaseItem to a GroupItem.
            if (baseItem.TryCast<GroupItem>() != null)
            {
                // The BaseItem is a GroupItem, so add it to the user's Inventory.
                inventory.AddGroup(baseItem.TryCast<GroupItem>());
                // Delete the GroupItem from the Warehouse.
                warehouse.Delete(baseItem.TryCast<GroupItem>());
                // Increment the temporary count of groups.
                wareGroupCount++;
            }
        }
        
        // Return the number of Items and Groups that were moved.
        return (wareItemCount, wareGroupCount);
    }

    /// <summary>
    /// Method to move items and groups between the user's Inventory and Garage Warehouse.
    /// </summary>
    private void MoveInventoryOrWarehouseItems(bool moveOnlyDuplicateItems = false)
    {
        var inventory = Singleton<Inventory>.Instance;
        var warehouse = Singleton<Warehouse>.Instance;
        var uiManager = UIManager.Get();

        var warehouseWindow = GetWarehouseWindow(true);

        if (warehouseWindow is null)
        {
            Popup("Please open the Warehouse first.");
            MelonLogger.Msg("Warehouse is not open");
            return;
        }

        // Check which Tab is currently being displayed.
        if (warehouseWindow.currentTab == 0) // This is the Inventory Tab
        {
            // Setup a temporary List<BaseItem> to hold the items.
            List<BaseItem> items;
            // Check if the user has selected to move items
            // for the current category only.
            if (_config.TransferByCategory)
            {
                MelonLogger.Msg($"TransferByCategory enabled");

                var currentTab = warehouseWindow.warehouseInventoryTab;
                items = inventory.GetItemsForCategory(currentTab.currentCategory).ToEnumerable().ToList();
            }
            else
            {
                // The user wants to move everything, so get that list.
                items = inventory.GetAllItemsAndGroups().ToEnumerable().ToList();
            }

            if (items.Count == 0)
            {
                Popup("No items to move");
                MelonLogger.Msg("No items to move");
                return;
            }

            void Move()
            {
                (var tempItems, var tempGroups) = MoveInventoryItemsToWarehouse(items, inventory, warehouse);
                // Show the user the number of items and groups that were moved.
                Popup($"Items Moved From Inventory: {tempItems}");
                Popup($"Groups Moved From Inventory: {tempGroups}");
                MelonLogger.Msg($"{tempItems} Item(s) and {tempGroups} Group(s) moved from Inventory");

                // Refresh the Warehouse Inventory Tab.
                warehouseWindow.warehouseInventoryTab.Refresh();
            }
            
            // Check if the user's Inventory has more items/groups than the user has configured in Settings.
            if (items.Count >= _config.MinNumOfItemsWarning)
            {
                // Ask the user to confirm the move because there are a lot of items/groups.
                Action<bool> confirmMove = response =>
                {
                    if (response)
                    {
                        MelonLogger.Msg($"User accepted MinNumOfItemsWarning dialog");
                        Move();
                    }
                    else
                    {
                        MelonLogger.Msg($"User cancelled MinNumOfItemsWarning dialog");
                    }
                };

                uiManager.ShowAskWindow("Move Items",
                    $"Move {items.Count} items from inventory to {warehouse.GetCurrentSelectedWarehouseName()}?",
                    confirmMove);
            }
            else
            {
                Move();
            }
        }
        else if (warehouseWindow.currentTab == 1) // This is the Warehouse Tab
        {
            if (moveOnlyDuplicateItems)
            {
                MelonLogger.Msg($"moveOnlyDuplicateItems enabled");
            }
            
            var items = GetItemsToMoveFromWarehouseTabToInventory(warehouse, warehouseWindow.warehouseTab, moveOnlyDuplicateItems);

            if (items.Count == 0)
            {
                Popup("No items to move");
                MelonLogger.Msg("No items to move");
                return;
            }

            void Move()
            {
                (var tempItems, var tempGroups) = MoveWarehouseItemsToInventory(items, inventory, warehouse);
                // Show the user the number of items and groups that were moved.
                Popup($"Items Moved From Warehouse: {tempItems}");
                Popup($"Groups Moved From Warehouse: {tempGroups}");
                MelonLogger.Msg($"{tempItems} Item(s) and {tempGroups} Group(s) moved from Warehouse");

                // Refresh the Warehouse Tab
                warehouseWindow.warehouseTab.Refresh(true);
            }
            
            // Check if the Warehouse has more items/groups than the user has configured in Settings.
            if (items.Count >= _config.MinNumOfItemsWarning)
            {
                MelonLogger.Msg($"MinNumOfItemsWarning threshold reached");

                // Ask the user to confirm the move because there are a lot of items/groups.
                Action<bool> confirmMove = response =>
                {
                    if (response)
                    {
                        Move();
                    }
                };
                
                uiManager.ShowAskWindow("Move Items",
                    $"Move {items.Count} from {warehouse.GetCurrentSelectedWarehouseName()} to your Inventory?",
                    confirmMove);
            }
            else
            {
                Move();
            }
        }
    }

    private List<BaseItem> GetItemsToMoveFromWarehouseTabToInventory(Warehouse warehouse, WarehouseTab warehouseTab, bool moveOnlyDuplicateItems)
    {
        var items = (_config.TransferByCategory
            ? warehouse.GetItemsForCategory(warehouseTab.currentCategory)
            : warehouse.GetAllItemsAndGroups()).ToEnumerable();


        if (!moveOnlyDuplicateItems)
        {
            return items.ToList();
        }
        
        // Group by ID, select only the items with duplicates. Ignore performance. Leave the item with highest condition, select its other duplicates.

        return items.GroupBy(x => x.ID)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.OrderByDescending(x => x.GetCondition()).Skip(1))
            .ToList();
    }
    
    private static ItemsExchangeWindow GetItemsExchangeWindow(bool onlyIfActive)
    {
        var windowManager = WindowManager.Instance;

        if (onlyIfActive && (windowManager.activeWindows.count == 0 ||
                             !windowManager.IsWindowActive(WindowID.ItemsExchange)))
        {
            return null;
        }

        return windowManager.GetWindowByID<ItemsExchangeWindow>(WindowID.ItemsExchange);
    }

    private static InventoryWindow GetInventoryWindow(bool onlyIfActive)
    {
        var windowManager = WindowManager.Instance;

        if (onlyIfActive && (windowManager.activeWindows.count == 0 ||
                             !windowManager.IsWindowActive(WindowID.Inventory)))
        {
            return null;
        }

        return windowManager.GetWindowByID<InventoryWindow>(WindowID.Inventory);
    }
    
    private static WarehouseWindow GetWarehouseWindow(bool onlyIfActive)
    {
        var windowManager = WindowManager.Instance;

        if (onlyIfActive && (windowManager.activeWindows.count == 0 ||
                             !windowManager.IsWindowActive(WindowID.Warehouse)))
        {
            return null;
        }

        return windowManager.GetWindowByID<WarehouseWindow>(WindowID.Warehouse);
    }

    /// <summary>
    /// Method to move items and groups between Junk Stashes and the Temp Inventory of the Barn and Junkyard.
    /// </summary>
    /// <remarks>
    /// There are never more than 30 items/groups, so we aren't checking the user setting.
    /// This might change in the future if people set it that low.
    /// There also doesn't seem to be any groups in these stashes.
    /// </remarks>
    private void MoveBarnOrJunkyardStashItems(HashSet<string> includeShopListItems, bool onlyShopListItems)
    {
        MelonLogger.Msg($"MoveBarnOrJunkyardItems");

        var itemsExchangeWindow = GetItemsExchangeWindow(true);

        if (itemsExchangeWindow is null)
        {
            Popup("Please open a junk pile first.");
            MelonLogger.Msg("Junk window not open");
            return;
        }

        // Disable QoLmod settings temporarily.
        using var guard = new QolSettingsGuard(_qolSettings);
        _qolSettings.ShowPopupForAllPartsInGroup = false;
        _qolSettings.ShowPopupForGroupAddedInventory = false;
        
        // Check which Tab is currently being displayed.
        if (itemsExchangeWindow.currentTab == 0)  // This is the Junk (Found) Tab.
        {
            var count = MoveJunkStashToInventory(itemsExchangeWindow.junk, includeShopListItems, onlyShopListItems, true);

            if (count > 0)
            {
                // Refresh the Items Exchange Tab
                itemsExchangeWindow.foundTab.Refresh(true);
            }
        }
        else if (itemsExchangeWindow.currentTab == 1)  // This is the Temp Inventory (Collected) Tab
        {
            var count = MoveCollectedInventoryItemsBackToJunkStash(itemsExchangeWindow.junk, true);

            if (count > 0)
            {
                // Refresh the Items Exchange Tab
                itemsExchangeWindow.collectedTab.Refresh(true);
            }
        }
    }


    private int MoveJunkStashToInventory(Junk junk, HashSet<string> includeShopListItems, bool onlyShopListItems, bool showPopup)
    {
        MelonLogger.Msg($"MoveJunkStashToInventory from {junk.name}");
        
        // Get the list of junk items/groups.
        var junkItems = GetItemsToMoveFromJunk(junk.ItemsInTrash, includeShopListItems, onlyShopListItems);
        // Store the number of junk in the Stash.
        int junkCount = junkItems.Items.Count + junkItems.Groups.Count;

        if (junkCount == 0)
        {
            if (showPopup)
            {
                Popup("No items to move");
            }

            MelonLogger.Msg("No items to move");
            return 0;
        }
        
        foreach (var tempItem in junkItems.Items)
        {
            _tempInventoryManager.AddItem(tempItem);
            junk.ItemsInTrash.Remove(tempItem);
        }

        foreach (var tempGroup in junkItems.Groups)
        {
            _tempInventoryManager.AddItem(tempGroup);
            junk.ItemsInTrash.Remove(tempGroup);
        }

        // Add the temporary list to the Global Dictionaries.
        // This allows the user to "undo" the moves to each Junk Stash.
        GetJunkTempStorageItems(junk.Pointer).AddRange(junkItems.Items);
        GetJunkTempStorageGroups(junk.Pointer).AddRange(junkItems.Groups);

        if (showPopup)
        {
            // Show the user the number of items that were moved.
            Popup($"Items Moved From Junk to Shopping Cart: {junkItems.Items.Count}");
            // Show the user the number of groups that were moved.
            Popup($"Groups Moved From Junk to Shopping Cart: {junkItems.Groups.Count}");
        }
        
        MelonLogger.Msg(
            $"{junkItems.Items.Count} Item(s) and {junkItems.Groups.Count} Group(s) moved from Junk to Shopping Cart.");

        return junkCount;
    }

    private int MoveCollectedInventoryItemsBackToJunkStash(Junk junk, bool showPopup)
    {
        var cachedJunkItems = GetJunkTempStorageItems(junk.Pointer);
        var cachedJunkGroups = GetJunkTempStorageGroups(junk.Pointer);

        var itemCount = cachedJunkItems.Count;
        var groupCount = cachedJunkGroups.Count;
        
        if (itemCount == 0 && groupCount == 0)
        {
            Popup("No items to move");
            MelonLogger.Msg("No items to move");
        }
        
        foreach (var item in cachedJunkItems)
        {
            if (!_tempInventoryManager.Contains(item))
            {
                Popup( $"Can't move back {item.GetLocalizedName()}");
            }
            junk.ItemsInTrash.Add(item);
            _tempInventoryManager.Remove(item);
        }
        
        foreach (var group in cachedJunkGroups)
        {
            if (!_tempInventoryManager.Contains(group))
            {
                Popup( $"Can't move back {group.GetLocalizedName()}");
            }
            junk.ItemsInTrash.Add(group);
            _tempInventoryManager.Remove(group);
        }
        
        cachedJunkItems.Clear();
        cachedJunkGroups.Clear();

        if (showPopup)
        {
            // Show the user the number of items that were moved.
            Popup($"Items Moved to Junk: {itemCount}");
            // Show the user the number of groups that were moved.
            Popup($"Groups Moved to Junk: {groupCount}");
            MelonLogger.Msg($"{itemCount} Item(s) and {groupCount} Group(s) moved to Junk");
        }

        return itemCount + groupCount;
    }


    /// <summary>
    /// Method to move all items and groups from all the Junk Stashes to the Temp Inventory of the Barn or Junkyard.
    /// Or back to the stashes, if temp inventory (aka shopping cart) is opened.
    /// </summary>
    private void MoveEntireBarnOrJunkyard(HashSet<string> includeShopListItems, bool onlyShopListItems)
    {
        MelonLogger.Msg($"MoveEntireBarnOrJunkyard, including seeking for {includeShopListItems.Count} shop list items");
        
        var itemsExchangeWindow = GetItemsExchangeWindow(true);
        var inventoryWindow = GetInventoryWindow(true);

        // Check if the Junk Items Window (ItemsExchangeWindow) is displayed.
        // This means the user wants to move all the junk from the open junk pile.
        if (itemsExchangeWindow is not null &&
            itemsExchangeWindow.currentTab == 1) // Only work if the Shopping Cart Tab is displayed.
        {
            var junkInventory = itemsExchangeWindow.junk;
            var count = MoveCollectedInventoryItemsBackToJunkStash(junkInventory, true);

            if (count > 0)
            {
                // Refresh the Items Exchange Tab.
                itemsExchangeWindow.collectedTab.Refresh(true);
                itemsExchangeWindow.foundTab.Refresh(true);
            }
            
            return;
        }
        
        // Get a reference to all the objects of type Junk.
        var junks = UnityEngine.Object.FindObjectsOfType<Junk>();
        // Setup a temporary count of junk to show the user at the end.
        int junkTotalCount = 0;

        
        // Check if the Temp Inventory window is displayed.
        // This means the user wants to move all the junk back to the junk piles (undo move).
        if (inventoryWindow is not null)
        {
            // Loop through the junk piles, get the temporary list that corresponds and then
            // move the items back to their respective junk piles.
            foreach (var junk in junks)
            {
                junkTotalCount += MoveCollectedInventoryItemsBackToJunkStash(junk, false);
            }

            // Check that the Temp Inventory is empty.
            if (junkTotalCount > 0)
            {
                // Show the user the number of items and groups that were moved.
                Popup($"Junk items moved: {junkTotalCount}");
                MelonLogger.Msg($"Junk items moved: {junkTotalCount} from Shopping Cart");
                inventoryWindow.Refresh();
            }

            return;
        }
        
        // Loop through each Junk object and move the items and groups.
        foreach (var junk in junks)
        {
            junkTotalCount += MoveJunkStashToInventory(junk, includeShopListItems, onlyShopListItems, false);
        }

        // Show the user the number of items and groups that were moved.
        if (junkTotalCount > 0)
        {
            Popup($"Junk items moved to Shopping Cart: {junkTotalCount}.");
            MelonLogger.Msg($"Junk items moved to Shopping Cart: {junkTotalCount}");
            
            itemsExchangeWindow?.foundTab?.Refresh();
            itemsExchangeWindow?.collectedTab?.Refresh();
        }
        else
        {
            Popup("Junk piles are empty or items do not meet move criteria.");
            MelonLogger.Msg("Junk piles are empty or items do not meet move criteria");
        }
    }

    private static void Popup(string message, string title = null, PopupType type = PopupType.Normal)
    {
        UIManager.Get().ShowPopup(title ?? "Transfer", message, type);
    }
}