using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CMS.UI;
using CMS.UI.Windows;
using RareParts.Extensions;
using RareParts.Logging;

namespace RareParts;

public class RepairScrap
{
    private readonly ILogger<RepairScrap> _logger;

    public RepairScrap(ILogger<RepairScrap> logger)
    {
        _logger = logger;
    }

    public void Repair()
    {
        var scrapWindow = GetScrapWindowIfActive();

        if (scrapWindow is null)
        {
            _logger.Information("Scrap window is not active, exiting repair.");
            return;
        }
        
        var inventory = Singleton<Inventory>.Instance;
        RepairFromInventory(inventory, scrapWindow);
    }

    private static ScrapWindow GetScrapWindowIfActive()
    {
        var windowManager = WindowManager.Instance;

        if (!windowManager.IsWindowActive(WindowID.Scrap))
        {
            return null;
        }

        return windowManager.GetWindowByID<ScrapWindow>(WindowID.Scrap);
    }
    
    public void RepairFromInventory(Inventory inventory, ScrapWindow scrapWindow)
    {
        var currentItem = GetCurrentItem(scrapWindow); // if an item is selected, it has priority to repair

        if (currentItem is not null)
        {
            _logger.Information("Currently selected item:");
            Debug.LogItemState(currentItem);
        }
        
        var repairableItems = GetRepairableItems(inventory, currentItem?.ID);
        var uiManager = UIManager.Get();
        
        if (repairableItems.Count < 2)
        {
            uiManager.ShowInfoWindow("No repairable items found. You need several identical parts to repair one item.");
            return;
        }
        
        _logger.Information("Items to scrap-repair:");
        foreach (var item in repairableItems)
        {
            Debug.LogItemState(item);
        }
        
        repairableItems = repairableItems
            .OrderBy(x => x.UID == currentItem?.UID ? 0 : 1) // if an item is selected, it will be taken as primary and will keep additional properties like quality or wheel size
            .ThenByDescending(x => x.Quality)
            .ToList();
        
        var chosenItem = repairableItems[0];
        
        var repairedCondition = ScrapRepairCalculator.GetRepairedCondition(repairableItems.Select(x => x.Condition));

        var description = new StringBuilder();
        description.AppendLine($"Repairing {chosenItem.GetLocalizedName()} parts:");
        description.Append(ConditionToPercent(repairableItems[0].Condition));
        
        for (var i = 2; i <= repairableItems.Count; i++)
        {
            description.Append(" + ");
            description.Append(ConditionToPercent(repairableItems[i-1].Condition));
        }

        description.Append($" to one {ConditionToPercent(repairedCondition)} part.");

        var repairAction = new Action<bool>(confirmed =>
        {
            if (!confirmed)
            {
                return;
            }

            chosenItem.Condition = repairedCondition;

            foreach (var item in repairableItems.Where(x => x != chosenItem))
            {
                _logger.Information($"Deleting UID: {item.UID}");
                inventory.Delete(item);
            }

            uiManager.ShowPopup("SCRAP-REPAIR",
                $"{chosenItem.GetLocalizedName()} repaired from {repairableItems.Count} parts to {ConditionToPercent(chosenItem.Condition)}.", PopupType.Normal);

            // Rebuild the active tab's item list after consuming inventory items.
            if (scrapWindow.scrapProduction?.IsActive == true)
            {
                scrapWindow.UpdateItemsForProduction();
            }
            else if (scrapWindow.scrapUpgrade?.IsActive == true)
            {
                scrapWindow.scrapUpgrade.UpdateItems();
            }
        });
        
        uiManager.ShowAskWindow("SCRAP-REPAIR ITEMS", description.ToString(), repairAction);
    }

    private static BaseItem GetCurrentItem(ScrapWindow scrapWindow)
    {
        return scrapWindow.choosePartDownWindow?.GetCurrentItem(out var _)?.BaseItem;
    }

    private static string ConditionToPercent(float condition) => $"{(int)(condition * 100)}%";

    private static IEnumerable<List<Item>> GetRepairableItemGroups(Inventory inventory, string priorId)
    {
        return inventory.items.ToEnumerable()
            .Where(x => x.Condition < 1)
            .GroupBy(x => x.ID)
            .OrderBy(x => x.Key == priorId ? 0 : 1)
            .ThenByDescending(x => x.Count())
            .ThenBy(x => x.Sum(y => y.Condition))
            .Select(x => FilterOptimalItems(x.ToList()));
    }

    private List<Item> GetRepairableItems(Inventory inventory, string priorId)
    {
        var itemsGroup = GetRepairableItemGroups(inventory, priorId)
            .FirstOrDefault(x => x.Count > 1);

        return itemsGroup ?? [];
    }

    private static List<Item> FilterOptimalItems(List<Item> items)
    {
        if (items.Count < 2)
        {
            return items;
        }

        var sample = items[0];
        return ScrapRepairCalculator.FilterOptimalItems(
            items, item => item.Condition, sample.MinConditionToRepair,
            PartsInfo.NonRepairableParts.Contains(sample.ID));
    }
}
