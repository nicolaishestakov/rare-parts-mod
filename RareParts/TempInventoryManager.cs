using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using RareParts.Extensions;

namespace RareParts;

public class TempInventoryManager
{
    private TempInventory _tempInventory;
    private TempInventory TempInventory
    {
        get
        {
            _tempInventory ??= Singleton<GameManager>.Instance.TempInventory;
            return _tempInventory;
        }
    }

    private readonly List<BaseItem> _customItems = [];
    
    public TempInventoryManager()
    {
        
        Patched.OnTempInventoryCleared += OnInternalClear;
        Patched.BeforeTempInventoryCleared += BeforeInternalClear;
    }

    public IEnumerable<BaseItem> Items => TempInventory.items.ToEnumerable(); 

    public int Count => TempInventory.items.Count;
    
    public void AddItem(BaseItem item)
    {
        _customItems.Add(item);
        TempInventory.AddItem(item);
    }

    public void Remove(BaseItem item)
    {
        _customItems.RemoveAll(x => x.Pointer == item.Pointer);
        TempInventory.RemoveItem(item);
    }
    
    public void Clear()
    {
        _customItems.Clear();
    }

    public void Invalidate()
    {
        _customItems.Clear();
        _tempInventory = null;
    }

    public bool Contains(BaseItem item)
    {
        return TempInventory.items.Contains(item);
    }

    public void LogState()
    {
        MelonLogger.Msg($"Temp Inventory contains {TempInventory.items.Count} items:");
        foreach (var tempInventoryItem in TempInventory.items)
        {
            Debug.LogItemState(tempInventoryItem);
        }
        
        MelonLogger.Msg($"Custom items contains {_customItems.Count} items:");
        foreach (var customItem in _customItems)
        {
            Debug.LogItemState(customItem);
        }
    }
    
    private void BeforeInternalClear(TempInventory tempInventory)
    {
        if (_customItems.Count == 0)
        {
            return;
        }
        
        MelonLogger.Msg($"BeforeInternalClear: internal TempInventory contains {TempInventory.items.Count} items");
        
        var updatedItems = new List<BaseItem>(); 
        foreach (var item in TempInventory.items)
        {
            if (_customItems.Any(x => x.Pointer == item.Pointer)) // _customItems.Contains does not work
            {
                updatedItems.Add(item);
            }
        }
        
        MelonLogger.Msg($"BeforeInternalClear: adjusting custom items from {_customItems.Count} to {updatedItems.Count} items");
        
        _customItems.Clear();
        _customItems.AddRange(updatedItems);
    }

    private void OnInternalClear(TempInventory tempInventory)
    {
        foreach (var item in _customItems)
        {
            TempInventory.AddItem(item);
        }
        
        MelonLogger.Msg($"OnInternalClear: restored {_customItems.Count} items");
    }
}