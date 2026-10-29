using UnityEngine;

namespace RareParts
{
    public interface ITransferAllConfig
    {
        /// <summary>
        /// User setting for the key to transfer the Inventory/Warehouse/Junk items.
        /// </summary>
        KeyCode TransferAllItemsAndGroupsKey { get; }
    
        /// <summary>
        /// User setting for the key to transfer all the junk from the Barn or Junkyard.
        /// </summary>
        KeyCode TransferEntireJunkyardOrBarnKey { get; }

        KeyCode TransferSettingsKey { get; }
        
        /// <summary>
        /// User setting for the key to set the condition of parts percentage lower by 10%.
        /// </summary>
        KeyCode SetPartConditionLower { get; }
        
        /// <summary>
        /// User setting for the key to set the condition of parts percentage higher by 10%.
        /// </summary>
        KeyCode SetPartConditionHigherKey { get; }
        
        /// <summary>
        /// User setting for the key to switch between the ByCondition/SkipUnrepairable/OnlyRareRepairable modes.
        /// </summary>
        KeyCode SwitchModeKey { get; }
        
        /// <summary>
        /// User setting for the minimum part condition to transfer.
        /// </summary>
        int MinPartCondition { get; set; }
        
        /// <summary>
        /// User setting for the minimum number of items to show a warning message before transfer.
        /// </summary>
        int MinNumOfItemsWarning { get; }
        
        /// <summary>
        /// User setting to select whether the TransferAll key uses the current category or not.
        /// </summary>
        bool TransferByCategory { get; }
        
        /// <summary>
        /// Include maps alongside parts selected by the current transfer mode.
        /// </summary>
        bool MoveMaps { get; set; }
        
        /// <summary>
        /// Include cases alongside parts selected by the current transfer mode.
        /// </summary>
        bool MoveCases { get; set; }

        /// <summary>
        /// Method to change the MinPartCondition setting lower by 10%.
        /// </summary>
        public int SetPartConditionLowerBy10();
         
        /// <summary>
        /// Method to change the MinPartCondition setting higher by 10%.
        /// </summary>
        public int SetPartConditionHigherBy10();
        
        public int[] MoveShoppingListWarehouses { get; }
    }
}
