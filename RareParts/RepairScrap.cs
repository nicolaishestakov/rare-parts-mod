using System.Collections.Generic;
using System.Linq;
using System.Text;
using CMS.UI;
using CMS.UI.Windows;
using Il2CppSystem;
using MelonLoader;
using RareParts.Extensions;

namespace RareParts;

public class RepairScrap
{
    public void Repair()
    {
        var scrapWindow = GetScrapWindowIfActive();

        if (scrapWindow is null)
        {
            MelonLogger.Msg($"Scrap window is not active, exiting repair.");
            return;
        }
        else if (scrapWindow.topMenu.currentSelected != 1) // upgrade menu 
        {
            MelonLogger.Msg($"Scrap window upgrade tab is not active, exiting repair.");
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
        var repairableItems = GetRepairableItems(inventory);
        var uiManager = UIManager.Get();
        
        if (repairableItems.Count < 2)
        {
            uiManager.ShowInfoWindow("No repairable items found. You need several identical parts to repair one item.");
            return;
        }
        
        MelonLogger.Msg($"Items to scrap-repair:");
        foreach (var item in repairableItems)
        {
            Debug.LogItemState(item);
        }
        
        var chosenItem = repairableItems.OrderByDescending(x => x.Quality).First();
        var repairedCondition = GetRepairedCondition(repairableItems.Select(x => x.Condition));

        var description = new StringBuilder();
        description.AppendLine($"Repairing {chosenItem.GetLocalizedName()} parts:");
        description.Append(ConditionToPercent(repairableItems[0].Condition));
        
        for (var i = 2; i <= repairableItems.Count; i++)
        {
            description.Append(" + ");
            description.Append(ConditionToPercent(repairableItems[i-1].Condition));
        }

        description.Append($" to one {ConditionToPercent(repairedCondition)} part.");

        var repairAction = new System.Action<bool>(confirmed =>
        {
            if (!confirmed)
            {
                return;
            }

            chosenItem.Condition = repairedCondition;

            foreach (var item in repairableItems.Where(x => x != chosenItem))
            {
                MelonLogger.Msg($"Deleting UID: {item.UID}");
                inventory.Delete(item);
            }

            uiManager.ShowPopup("SCRAP-REPAIR",
                $"{chosenItem.GetLocalizedName()} repaired from {repairableItems.Count} parts to {ConditionToPercent(chosenItem.Condition)}.", PopupType.Normal);
            
            scrapWindow.scrapUpgrade.UpdateItems();
        });
        
        uiManager.ShowAskWindow("SCRAP-REPAIR ITEMS", description.ToString(), repairAction);
    }

    private static string ConditionToPercent(float condition) => $"{(int)(condition * 100)}%";
    
    private static float GetRepairedCondition(IEnumerable<float> conditions)
    {
        float q = 1;

        foreach (var condition in conditions)
        {
            q *= (1 - condition);
        }
        
        return 1 - q;
    }

    private List<Item> GetRepairableItems(Inventory inventory)
    {
        var itemsGroup = inventory.items.ToEnumerable()
            .GroupBy(x => x.ID)
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Sum(y => y.Condition))
            .FirstOrDefault();

        return itemsGroup is null ? [] : itemsGroup.ToList();
    }
}