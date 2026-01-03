using System.Collections.Generic;
using System.Linq;
using MelonLoader;

namespace RareParts.Extensions;

public static class WarehouseExtensions
{
    extension(Warehouse warehouse)
    {
        public bool DeleteRobust(Item item)
        {
            Item found = null;

            foreach (var subList in warehouse.warehouseList)
            {
                foreach (var warehouseItem in subList)
                {
                    if (item.Pointer == warehouseItem.Pointer)
                    {
                        found = warehouseItem;
                        break;
                    }
                }
            
                if (found is not null)
                {
                    //warehouse.Delete(found); // this only works for currently opened Warehouse
                    return subList.Remove(found);
                }
            }

            return false;
        }

        public List<Item> FindItems(string itemId, IEnumerable<int> storesToScan)
        {
            if (warehouse.warehouseList.Count == 0)
            {
                MelonLogger.Msg("Warehouse has no sections");
                return [];
            }

            var result = new List<Item>();

            foreach (var warehouseIndex in storesToScan)
            {
                var found = warehouse.warehouseList[warehouseIndex - 1].ToEnumerable().Where(item => item.ID == itemId);
                result.AddRange(found);
            }
        
            return result;
        }
    }
}