using CMS.UI.Logic;
using Harmony;
using MelonLoader;
using System.Collections.Generic;
using System.Linq;
using RareParts.Extensions;
using RareParts.Logging;
using UnityEngine;

namespace RareParts;

public class RarePartsMod : MelonMod
{
    private Config _config;
    private ILogger<RarePartsMod> _logger;
    private bool _isInitialized = true; //todo invert value

    private TransferAll _transferAll;
    private TempInventoryManager _tempInventoryManager;
    private RepairScrap _repairScrap;

    
    public override void OnInitializeMelon()
    {
        MelonLogger.Msg("initializing...");

        HarmonyInstance harmony = this.Harmony;
        harmony.PatchAll();

        _config = new Config("Mods/RareParts.cfg");
        _config.Reload();
        
        #if DEBUG
        var logLevel = LogLevel.Trace;
        #else
        var logLevel = _config.IsDebugMode ? LogLevel.Debug : LogLevel.Information;
        #endif
        
        var loggerFactory = new LoggerFactory(logLevel);
        _logger = loggerFactory.CreateLogger<RarePartsMod>();
        
        _tempInventoryManager = new TempInventoryManager(); //todo inject logger
        _transferAll = new TransferAll(_config, _tempInventoryManager); //todo inject logger
        _repairScrap = new RepairScrap(); //todo inject logger
    }

    public override void OnLateInitializeMelon()
    {
        _transferAll.OnLateInitializeMelon();
    }

    private void GlobalInits()
    {
        List<ShopType> shopTypes =
        [
            ShopType.Main,
            ShopType.Body,
            ShopType.Interior,
            ShopType.Tire,
            ShopType.LicensePlate,
            ShopType.Tuning,
            ShopType.BodyTuning,
            ShopType.Rims,
            ShopType.Gearbox,
            ShopType.Electronics,
            ShopType.Addons,
            ShopType.Community
        ];

        shopTypes.ForEach(shopType =>
        {
            var shopItems = Singleton<GameInventory>.Instance.GetItems(shopType);
            InitAllItems(shopItems);
        });
        
        _logger.Information($"{PartsInfo.RareParts.Count} rare parts, {PartsInfo.SpecialRepairableParts.Count} special repairable parts, {PartsInfo.NonRepairableParts.Count} non repairable parts");
    }

    public override void OnUpdate()
    {
        if (Input.GetKeyUp(KeyCode.F6)) //todo key to config
        {
            _logger.Information($"Command: Move Shopping List from Warehouse to Inventory");
            _transferAll.MoveShoppingListFromWarehouseToInventory(!IsShiftPressed(), _config.MinPartCondition);
        }

        if (Input.GetKeyUp(KeyCode.Keypad1))
        {
            Debug.LogWarehouseItems();
        }

        if (Input.GetKeyUp(KeyCode.F11))
        {
            Debug.LogWindowsState();
            Debug.LogInventoryState();
            _tempInventoryManager.LogState();
        }
        
        if (Input.GetKeyUp(_config.ScrapRepairKey))
        {
            _repairScrap.Repair();
        }
        
        if (Input.GetKeyUp(_config.TransferAllItemsAndGroupsKey))
        {
            _transferAll.TransferAllItemsAndGroups(IsShiftPressed());
        }

        if (Input.GetKeyUp(_config.TransferEntireJunkyardOrBarnKey))
        {
            _transferAll.TransferEntireJunkyardOrBarn(IsShiftPressed());
        }

        if (Input.GetKeyUp(_config.SwitchModeKey))
        {
            _transferAll.SwitchTransferMode();
        }
        
        if (Input.GetKeyUp(_config.SetPartConditionLower))
        {
            _config.SetPartConditionLowerBy10();
            _transferAll.OnMinPartConditionChanged();
        }

        if (Input.GetKeyUp(_config.SetPartConditionHigherKey))
        {
            _config.SetPartConditionHigherBy10();
            _transferAll.OnMinPartConditionChanged();
        }

        UpdateButtonsState();

        bool IsShiftPressed() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }
    
    private void UpdateButtonsState()
    {
        if (!_config.IsEnableShop)
        {
            GameObject.Find("MapDestinationButton (6)")?.SetActive(false);
        }
        
        if (!_config.IsEnableAdditionalAuction)
        {
            GameObject.Find("AuctionsButton")?.SetActive(false);
        }
    }

    
    private void RemoveRarePartFromShops(PartProperty part, int priceMultiplier)
    {
        part.Price *= priceMultiplier;
        part.ShopName = "none";

        if (!PartsInfo.RareParts.Add(part.ID))
        {
            _logger.Information($"Part {part.ID} overrides already declared with price multiplier: {priceMultiplier}");
        }
        
        if (part.RepairGroup == 0)
        {
            part.RepairGroup = 6;
            PartsInfo.SpecialRepairableParts.Add(part.ID);
        }
    }

    private bool TryRemoveRarePartFromShops(PartProperty part, IReadOnlyDictionary<string, int> rareParts)
    {
        // todo check for white list
        
        if (rareParts.TryGetValue(part.ID, out var priceMultiplier))
        {
            RemoveRarePartFromShops(part, priceMultiplier);
            return true;
        }
        else if (rareParts.TryGetValue(part.CarID, out priceMultiplier))
        {
            _logger.Trace($"Disabling part {part.ID} by car {part.CarID}");
            RemoveRarePartFromShops(part, priceMultiplier);
            return true;
        }
        
        return false;
    }

    private Dictionary<string, int> GetRarePartsWithPriceMultiplier()
    {
        var result = new Dictionary<string, int>();

        AddParts(_config.ListRetroParts1950_1959, _config.PriceParts1950_1959);
        AddParts(_config.ListRetroParts1960_1969, _config.PriceParts1960_1969);
        AddParts(_config.ListRetroParts1970_1979, _config.PriceParts1970_1979);
        AddParts(_config.ListRetroParts1980_1990, _config.PriceParts1980_1990);
        AddParts(_config.ListRetroParts1991_2000, _config.PriceParts1991_2000);
        AddParts(_config.ListRetroParts2001_2005, _config.PriceParts2001_2005);
        AddParts(_config.ListSportGT, _config.PricePartsSportGT);
        AddParts(_config.ListSpecialRetro, _config.PricePartsSpecialRetro);
        
        return result;
        
        void AddParts(IEnumerable<string> parts, int multiplier)
        {
            foreach (var part in parts)
            {
                // if (result.TryGetValue(part, out var value))
                // {
                //     MelonLogger.Msg($"Part {part} already declared in other section with price multiplier: {value}");
                // }
                
                result[part] = multiplier;
            }
        }
    }
    
    private void InitAllItems(Il2CppSystem.Collections.Generic.List<PartProperty> items)
    {
        var rareParts = GetRarePartsWithPriceMultiplier();
        
        foreach (var part in rareParts.Keys.Where(x => x.StartsWith("car_")))
        {
            PartsInfo.RareCars.Add(part);
        }
        
        foreach (var item in items)
        {
            // if you want to log all the parts in the game:
            //_logger.Debug($"Item {item.ID} aka \"{item.LocalizedName}\" repair group: {item.RepairGroup}, shop: {item.ShopGroup}/{item.ShopName}, car: {item.CarID}, part group: {item.PartGroup}");

            if (TryRemoveRarePartFromShops(item, rareParts))
            {
                continue;
            }

            if (item.LocalizedName.ContainsAny(_config.ListRetroParts)) 
            {
                _logger.Information($"Item {item.ID} aka \"{item.LocalizedName}\" disabled by localized name from ListRetroParts");
                RemoveRarePartFromShops(item, _config.PricePartsOther);
                continue;
            }

            if (item.RepairGroup == 0)
            {
                if (item.ShopName is "BodyShop" or "InteriorShop" or "TireShop" or "WorkshopBodyShop")
                {
                    item.RepairGroup = 6;
                    PartsInfo.SpecialRepairableParts.Add(item.ID);
                }
                else
                {
                    PartsInfo.NonRepairableParts.Add(item.ID);
                }
            }
        }
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        {
            //todo not quite sure what this is for
            GameSettings.CanShowPopups = true;

            var gameSettingData = GameSettings.GameSettingsData;
            gameSettingData.Init();
        }
        
        if (buildIndex == 10 && _isInitialized)
        {
            _logger.Debug($"Initialization at scene {sceneName}. Game version: {GameSettings.BuildVersion}");
            GlobalInits();

            _isInitialized = false;
        }
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
        _transferAll.OnSceneWasInitialized(buildIndex, sceneName);
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        _transferAll.OnSceneWasUnloaded(buildIndex, sceneName);
    }
}