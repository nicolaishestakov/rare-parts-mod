using System.Collections.Generic;
using System.Linq;
using RareParts.Extensions;
using RareParts.Logging;

namespace RareParts;

public class TempInventoryManager
{
    private readonly ILogger<TempInventoryManager> _logger;

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
    
    public TempInventoryManager(ILogger<TempInventoryManager> logger)
    {
        _logger = logger;
        
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
        _logger.Information($"Temp Inventory contains {TempInventory.items.Count} items:");
        foreach (var tempInventoryItem in TempInventory.items)
        {
            Debug.LogItemState(tempInventoryItem);
        }
        
        _logger.Information($"Custom items contains {_customItems.Count} items:");
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
        
        _logger.Information($"BeforeInternalClear: internal TempInventory contains {TempInventory.items.Count} items");
        
        var updatedItems = new List<BaseItem>(); 
        foreach (var item in TempInventory.items)
        {
            if (_customItems.Any(x => x.Pointer == item.Pointer)) // _customItems.Contains does not work
            {
                updatedItems.Add(item);
            }
        }
        
        _logger.Information($"BeforeInternalClear: adjusting custom items from {_customItems.Count} to {updatedItems.Count} items");
        
        _customItems.Clear();
        _customItems.AddRange(updatedItems);
    }

    private void OnInternalClear(TempInventory tempInventory)
    {
        foreach (var item in _customItems)
        {
            TempInventory.AddItem(item);
        }
        
        _logger.Information($"OnInternalClear: restored {_customItems.Count} items");
    }
}