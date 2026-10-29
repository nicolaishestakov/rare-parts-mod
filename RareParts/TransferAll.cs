using CMS.Helpers;
using CMS.UI;
using CMS.UI.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CMS.Containers;
using CMS.UI.Logic.Warehouse;
using RareParts.Extensions;
using RareParts.Logging;
using UnityEngine.UI;

// based on https://github.com/mannly01/TransferAll

namespace RareParts;

public class TransferAll
{
    public TransferAll(ITransferAllConfig config, TempInventoryManager tempInventoryManager, IQolPopupsBlocker qolPopupsBlocker, ILogger<TransferAll> logger)
    {
        _logger = logger;
        _config = config;
        _tempInventoryManager = tempInventoryManager;
        _qolPopupsBlocker = qolPopupsBlocker;
    }

    private readonly ILogger<TransferAll> _logger;
    private readonly ITransferAllConfig _config;
    private TransferSettingsPanel _transferSettingsPanel;

    /// <returns>True when the modal panel is shown.</returns>
    public bool Update()
    {
        if (_transferSettingsPanel is null)
        {
            return false;
        }
        
        // Keep shortcuts suppressed for the closing frame as well.
        var blockTransferShortcuts = _transferSettingsPanel.IsOpen;
        _transferSettingsPanel.Update();
        if (!_transferSettingsPanel.IsEditingText && UnityEngine.Input.GetKeyUp(_config.TransferSettingsKey))
        {
            _transferSettingsPanel.Toggle();
            blockTransferShortcuts = true;
        }
        return blockTransferShortcuts || _transferSettingsPanel.IsOpen;
    }

    public void LateUpdate() => _transferSettingsPanel?.LateUpdate();

    public void DrawGUI() => _transferSettingsPanel?.Draw();

    public void CloseSettingsPanel() => _transferSettingsPanel?.Close();

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
    private readonly IQolPopupsBlocker _qolPopupsBlocker;

    /// <summary>
    /// Global reference used to temporarily hold the individual junk stashes in the Barn and Junkyard.
    /// This allows the individual stashes to be "undone" and transferred back.
    /// </summary>
    private readonly Dictionary<IntPtr, List<Item>> _tempItems = new ();

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
    private readonly Dictionary<IntPtr, List<GroupItem>> _tempGroups = new ();

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
        OnlyRareRepairable,
        ShopListItemsOnly
    }

    public Mode TransferMode { get; private set; } = Mode.OnlyRareRepairable;

    public bool IsBarnOrJunkyard => _currentScene.IsBarnOrJunkyard;

    // The panel always collects; unlike the legacy hotkey it never infers an undo
    // operation from whichever inventory tab happens to be open.
    public void CollectEntireJunkyardOrBarn(string nameFilter = null)
    {
        if (IsBarnOrJunkyard && !CheckIfInputIsFocused())
            MoveEntireBarnOrJunkyard(GetShopListItems(), collectOnly: true,
                nameFilter: nameFilter?.Trim());
    }

    /// <summary>Return items collected by this mod to their original piles, regardless of the active tab.</summary>
    public void ReturnEntireJunkyardOrBarn()
    {
        if (!IsBarnOrJunkyard || CheckIfInputIsFocused())
            return;

        var count = 0;
        foreach (var junk in UnityEngine.Object.FindObjectsOfType<Junk>())
            count += MoveCollectedInventoryItemsBackToJunkStash(junk, false);

        Popup(count > 0 ? $"Items returned to junk piles: {count}" : "No collected items to return.");
        _logger.Information($"Items returned from Shopping Cart to junk piles: {count}");
        if (count > 0)
        {
            GetInventoryWindow(true)?.Refresh();
            var exchange = GetItemsExchangeWindow(true);
            exchange?.foundTab?.Refresh();
            exchange?.collectedTab?.Refresh();
        }
    }

    public void EnterScene(int buildIndex, string sceneName)
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
            _transferSettingsPanel = new TransferSettingsPanel(this, _config);
            _logger.Information($"Entered {sceneName}");
        }
    }

    public void LeaveScene(int buildIndex, string sceneName)
    {
        if (buildIndex == -1)
        {
            return;
        }

        // Clear the temporary dictionaries when the user leaves the scene.
        if (new Scene(sceneName).IsBarnOrJunkyard)
        {
            _currentScene.UpdateScene(string.Empty);
            _tempItems.Clear();
            _tempGroups.Clear();
            _tempInventoryManager.Clear();
            _transferSettingsPanel?.Close();
            _transferSettingsPanel = null;
            _logger.Information($"Leaving {sceneName}");
        }
    }
    
    public void TransferAllItemsAndGroups(bool moveOnlyDuplicateItems)
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
                    MoveInventoryOrWarehouseItems(moveOnlyDuplicateItems);
                }
                else
                {
                    // The user hasn't upgraded their Garage, so show them a message.
                    Popup("You must unlock the Warehouse Expansion first.");
                    _logger.Information("Warehouse has not been unlocked");
                }
            }
            // Check that the user is in the Barn or Junkyard.
            else if (_currentScene.IsBarnOrJunkyard)
            {
                // Do work on the Junk and TempInventory.
                MoveBarnOrJunkyardStashItems(GetShopListItems());
            }
            else
            {
                // The user is not at the Garage, so show them a message.
                Popup("This function only works at the Garage, Barn or Junkyard");
                _logger.Information("TransferEntireJunkyardOrBarn key pressed outside of Garage, Barn or Junkyard");
            }
        }
        else
        {
            _logger.Information("Search box has focus");
        }
    }

    public void TransferEntireJunkyardOrBarn()
    {
        // Check if the user is currently using the Seach box.
        if (!CheckIfInputIsFocused())
        {
            // Check that the user is in the Barn or Junkyard.
            if (_currentScene.IsBarnOrJunkyard)
            {
                // Do work on the Junk and TempInventory.
                MoveEntireBarnOrJunkyard(GetShopListItems());
            }
            else
            {
                // The user is not at the Barn or Junkyard, so show them a message.
                Popup("This function only works at the Barn or Junkyard");
                _logger.Information("TransferEntireJunkyardOrBarn key pressed outside of Barn or Junkyard");
            }
        }
        else
        {
            _logger.Information("Search box has focus");
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
            _logger.Information("shopListWindow is null");
            return;
        }

        var moved = false;
        var shoppingListModified = false;

        foreach (var shopItem in shopListWindow.items.ToEnumerable().ToList())
        {
            _logger.Information($"Looking for {shopItem.ID}");
            var found = warehouse.FindItems(shopItem.ID, _config.MoveShoppingListWarehouses);
            _logger.Information($"Found {found.Count} items");

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
                        _logger.Information($"Item {item.ID} condition {item.Condition * 100f} is not repairable so will not be moved");
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
            _logger.Information("shopListWindow is active, trying to refresh");
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
        _logger.Information($"Moving Item {itemName}" + (deleteMovedFromShoppingList? "": ", keep in the shopping list"));

        if (deleteMovedFromShoppingList)
        {
            shopListWindow.RemoveFromShopList(shopListItem.ID, shopListItem.AdditionalData, false);
        }

        inventory.Add(warehouseItem);
        if (!warehouse.DeleteRobust(warehouseItem))
        {
            _logger.Information($"Failed to delete item {itemName} from warehouse");
        }
        else
        {
            var leftItems = warehouse.FindItems(warehouseItem.ID, _config.MoveShoppingListWarehouses);
            if (leftItems.Count == 0)
            {
                _logger.Information($"No more {itemName} left in movable warehouse stores");
            }
            else
            {
                _logger.Information($"Still {leftItems.Count} are in movable warehouse stores");
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
            _logger.Information("shopListWindow is null");
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

    public void SwitchTransferMode(bool previous = false)
    {
        TransferMode = previous ? TransferMode switch
        {
            Mode.ByCondition => Mode.ShopListItemsOnly,
            Mode.ByConditionSkipUnrepairable => Mode.ByCondition,
            Mode.OnlyRareRepairable => Mode.ByConditionSkipUnrepairable,
            Mode.ShopListItemsOnly => Mode.OnlyRareRepairable,
            _ => Mode.ByCondition
        } : TransferMode switch
        {
            Mode.ByCondition => Mode.ByConditionSkipUnrepairable,
            Mode.ByConditionSkipUnrepairable => Mode.OnlyRareRepairable,
            Mode.OnlyRareRepairable => Mode.ShopListItemsOnly,
            Mode.ShopListItemsOnly => Mode.ByCondition,
            _ => Mode.ByCondition
        };

        string transferMode = TransferMode switch
        {
            Mode.ByCondition => $"Starting from {_config.MinPartCondition}% condition",
            Mode.ByConditionSkipUnrepairable =>
                $"Starting from {_config.MinPartCondition}% condition and skip unrepairable",
            Mode.OnlyRareRepairable => "Only rare repairable parts",
            Mode.ShopListItemsOnly => "Shopping-list items only",
            _ => "Unknown"
        };

        Popup($"Transfer Mode: {transferMode}");
    }

    /// <summary>
    /// This mod only works if the Warehouse has been unlocked
    /// as an Expansion, so check if it has been unlocked.
    /// </summary>
    /// <returns>(bool) True if the Warehouse Expansion has been unlocked.</returns>
    private bool CheckIfWarehouseIsUnlocked()
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
            _logger.Information($"Warehouse Unlocked: {warehouseUpgrade.IsUnlocked}");
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
    internal bool CheckIfInputIsFocused()
    {
        if (TransferSettingsPanel.IsTextInputFocused)
            return true;

        var inputFields = UnityEngine.Object.FindObjectsOfType<InputField>();
        foreach (var inputField in inputFields)
        {
            if (inputField != null)
            {
                if (inputField.isFocused)
                {
#if DEBUG
                    _logger.Information("Input Field Focused");
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
        using var guard = _qolPopupsBlocker.BlockPopups();

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
        HashSet<string> includeShopListItems)
    {
        var items = new List<Item>();
        var groups = new List<GroupItem>();

        foreach (var junkItem in junkItems)
        {
            if (IsToBeMoved(junkItem, includeShopListItems))
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

    private bool IsToBeMoved(BaseItem baseItem, HashSet<string> includeShopListItems)
    {
        //_logger.Information($"Checking {baseItem.ID}");

        // These independent options apply in every mode. Disabled categories must
        // not fall through to shopping-list or condition-based part selection.
        if (baseItem.IsMap)
            return _config.MoveMaps;
        if (baseItem.IsCase)
            return _config.MoveCases;

        if (includeShopListItems.Contains(baseItem.ID))
        {
            Popup($"Shop list item found: {baseItem.GetLocalizedName()}");
            return true;
        }

        if (TransferMode == Mode.ShopListItemsOnly)
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
                _logger.Information($"{id} moved as a group?");
                return true;
            }

            // Check if non-repairable
            if (baseItem.IsNonRepairable ||
                (baseItem.IsSpecialRepairable &&
                 condition < PartsInfo.SpecialRepairablePartsMinCondition))
            {
                _logger.Information($"{id} not included as non-repairable");
                return false;
            }

            return true;
        }

        _logger.Error($"Unknown transfer mode {TransferMode}. Item {id} not moved");
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
        _logger.Information($"Called");
#endif
        // Disable QoLmod settings temporarily.
        using var guard = _qolPopupsBlocker.BlockPopups();
        
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
            _logger.Information("Warehouse is not open");
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
                _logger.Information($"TransferByCategory enabled");

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
                _logger.Information("No items to move");
                return;
            }

            void Move()
            {
                (var tempItems, var tempGroups) = MoveInventoryItemsToWarehouse(items, inventory, warehouse);
                // Show the user the number of items and groups that were moved.
                Popup($"Items Moved From Inventory: {tempItems}");
                Popup($"Groups Moved From Inventory: {tempGroups}");
                _logger.Information($"{tempItems} Item(s) and {tempGroups} Group(s) moved from Inventory");

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
                        _logger.Information($"User accepted MinNumOfItemsWarning dialog");
                        Move();
                    }
                    else
                    {
                        _logger.Information($"User cancelled MinNumOfItemsWarning dialog");
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
                _logger.Information($"moveOnlyDuplicateItems enabled");
            }
            
            var items = GetItemsToMoveFromWarehouseTabToInventory(warehouse, warehouseWindow.warehouseTab, moveOnlyDuplicateItems);

            if (items.Count == 0)
            {
                Popup("No items to move");
                _logger.Information("No items to move");
                return;
            }

            void Move()
            {
                (var tempItems, var tempGroups) = MoveWarehouseItemsToInventory(items, inventory, warehouse);
                // Show the user the number of items and groups that were moved.
                Popup($"Items Moved From Warehouse: {tempItems}");
                Popup($"Groups Moved From Warehouse: {tempGroups}");
                _logger.Information($"{tempItems} Item(s) and {tempGroups} Group(s) moved from Warehouse");

                // Refresh the Warehouse Tab
                warehouseWindow.warehouseTab.Refresh(true);
            }
            
            // Check if the Warehouse has more items/groups than the user has configured in Settings.
            if (items.Count >= _config.MinNumOfItemsWarning)
            {
                _logger.Information($"MinNumOfItemsWarning threshold reached");

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
    private void MoveBarnOrJunkyardStashItems(HashSet<string> includeShopListItems)
    {
        _logger.Information($"MoveBarnOrJunkyardItems");

        var itemsExchangeWindow = GetItemsExchangeWindow(true);

        if (itemsExchangeWindow is null)
        {
            Popup("Please open a junk pile first.");
            _logger.Information("Junk window not open");
            return;
        }

        // Disable QoLmod settings temporarily.
        using var guard = _qolPopupsBlocker.BlockPopups();
        
        // Check which Tab is currently being displayed.
        if (itemsExchangeWindow.currentTab == 0)  // This is the Junk (Found) Tab.
        {
            var count = MoveJunkStashToInventory(itemsExchangeWindow.junk, includeShopListItems, true);

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


    private int MoveJunkStashToInventory(Junk junk, HashSet<string> includeShopListItems, bool showPopup,
        string nameFilter = null)
    {
        _logger.Information($"MoveJunkStashToInventory from {junk.name}");
        
        // Get the list of junk items/groups.
        var junkItems = GetItemsToMoveFromJunk(junk.ItemsInTrash, includeShopListItems);
        // Apply to every selection path, including maps/cases, shopping-list
        // exceptions and config categories. Groups match their displayed name.
        if (!string.IsNullOrEmpty(nameFilter))
        {
            bool Matches(BaseItem item) => item.GetLocalizedName()?.IndexOf(nameFilter,
                StringComparison.CurrentCultureIgnoreCase) >= 0;
            junkItems = (junkItems.Items.Where(item => Matches(item)).ToList(),
                junkItems.Groups.Where(group => Matches(group)).ToList());
        }
        // Store the number of junk in the Stash.
        int junkCount = junkItems.Items.Count + junkItems.Groups.Count;

        if (junkCount == 0)
        {
            if (showPopup)
            {
                Popup("No items to move");
            }

            _logger.Information("No items to move");
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
        
        _logger.Information(
            $"{junkItems.Items.Count} Item(s) and {junkItems.Groups.Count} Group(s) moved from Junk to Shopping Cart.");

        return junkCount;
    }

    private int MoveCollectedInventoryItemsBackToJunkStash(Junk junk, bool showPopup)
    {
        var cachedJunkItems = GetJunkTempStorageItems(junk.Pointer);
        var cachedJunkGroups = GetJunkTempStorageGroups(junk.Pointer);

        if (cachedJunkItems.Count == 0 && cachedJunkGroups.Count == 0)
        {
            if (showPopup)
                Popup("No items to move");
            _logger.Information("No items to move");
            return 0;
        }

        var itemCount = 0;
        var groupCount = 0;
        
        foreach (var item in cachedJunkItems)
        {
            if (!_tempInventoryManager.Contains(item))
            {
                Popup( $"Can't move back {item.GetLocalizedName()}");
                continue;
            }
            junk.ItemsInTrash.Add(item);
            _tempInventoryManager.Remove(item);
            itemCount++;
        }
        
        foreach (var group in cachedJunkGroups)
        {
            if (!_tempInventoryManager.Contains(group))
            {
                Popup( $"Can't move back {group.GetLocalizedName()}");
                continue;
            }
            junk.ItemsInTrash.Add(group);
            _tempInventoryManager.Remove(group);
            groupCount++;
        }
        
        cachedJunkItems.Clear();
        cachedJunkGroups.Clear();

        if (showPopup)
        {
            // Show the user the number of items that were moved.
            Popup($"Items Moved to Junk: {itemCount}");
            // Show the user the number of groups that were moved.
            Popup($"Groups Moved to Junk: {groupCount}");
            _logger.Information($"{itemCount} Item(s) and {groupCount} Group(s) moved to Junk");
        }

        return itemCount + groupCount;
    }


    /// <summary>
    /// Method to move all items and groups from all the Junk Stashes to the Temp Inventory of the Barn or Junkyard.
    /// Or back to the stashes, if temp inventory (aka shopping cart) is opened.
    /// </summary>
    private void MoveEntireBarnOrJunkyard(HashSet<string> includeShopListItems, bool collectOnly = false,
        string nameFilter = null)
    {
        _logger.Information($"MoveEntireBarnOrJunkyard, including seeking for {includeShopListItems.Count} shop list items");
        
        var itemsExchangeWindow = GetItemsExchangeWindow(true);
        var inventoryWindow = GetInventoryWindow(true);

        // Check if the Junk Items Window (ItemsExchangeWindow) is displayed.
        // This means the user wants to move all the junk from the open junk pile.
        if (!collectOnly && itemsExchangeWindow is not null &&
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
        if (!collectOnly && inventoryWindow is not null)
        {
            ReturnEntireJunkyardOrBarn();
            return;
        }
        
        // Loop through each Junk object and move the items and groups.
        foreach (var junk in junks)
        {
            junkTotalCount += MoveJunkStashToInventory(junk, includeShopListItems, false, nameFilter);
        }

        // Show the user the number of items and groups that were moved.
        if (junkTotalCount > 0)
        {
            Popup($"Junk items moved to Shopping Cart: {junkTotalCount}.");
            _logger.Information($"Junk items moved to Shopping Cart: {junkTotalCount}");
            
            itemsExchangeWindow?.foundTab?.Refresh();
            itemsExchangeWindow?.collectedTab?.Refresh();
            inventoryWindow?.Refresh();
        }
        else
        {
            Popup("Junk piles are empty or items do not meet move criteria.");
            _logger.Information("Junk piles are empty or items do not meet move criteria");
        }
    }

    private static void Popup(string message, string title = null, PopupType type = PopupType.Normal)
    {
        UIManager.Get().ShowPopup(title ?? "Transfer", message, type);
    }
}
