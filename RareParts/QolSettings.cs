using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;

namespace RareParts;

public class QolSettings
{
    private FieldInfo _qolSettings;

    public QolSettings(FieldInfo qolSettings)
    {
        _qolSettings = qolSettings;
    }

    public static QolSettings CreateFromLoadedMelonAssembly(string qolAssemblyPath)
    {
        if (!File.Exists(qolAssemblyPath))
        {
            MelonLogger.Msg("QoLmod not found");
            return null;
        }
    
        // Use reflection to load the dll.
        MelonAssembly cms2021mod = MelonAssembly.LoadMelonAssembly(qolAssemblyPath);
        
        if (cms2021mod is null)
        {
            // The QoLmod was found, but it can't be loaded.
            MelonLogger.Msg("QoLmod not loaded");
            return null;
        }
        
        // Get the loaded melon (there is only 1).
        var melons = cms2021mod.LoadedMelons;
        if (melons.Count != 1)
        {
            // The number of Melons has changed and loading failed.
            MelonLogger.Msg("Number of Melons in QoLmod has changed");
            return null;
        }
        
    
        var melon = melons[0];
        // This may not be necessary, but doesn't seem to hurt.
        melon.Register();
        
        // The GetType() method of Assembly didn't seem to find the correct Main Class,
        // so use Linq to find the correct Class.
        var mainType = melon.MelonAssembly.Assembly.GetTypes()
            .First(n => n.Name.ToLower().Contains("main"));
        
        // The GetField() method had the same problem, so use Linq to get the "asetukset" (settings) field.
        var qolSettings = mainType.GetFields().FirstOrDefault(p => p.Name.ToLower().Contains("ase"));

        if (qolSettings is null)
        {
            return null;
        }

        return new QolSettings(qolSettings);
    }
    
    public bool ShowPopupForGroupAddedInventory
    {
        get
        {
            dynamic settingsValue = _qolSettings.GetValue(null);
            // Find the "showPopupforGroupAddedInventory" value from Settingssit Class and store it's value.
            // From QoLmod.cfg: When you remove wheel or suspension assembly from car, popup will show total condition
            // This happens for every item in the group that is moved and the bulk move causes errors with the PopupManager.
            return settingsValue.Value.showPopupforGroupAddedInventory;
        }
        set
        {
            dynamic settingsValue = _qolSettings.GetValue(null);
            settingsValue.Value.showPopupforGroupAddedInventory = value;
        }
    }

    public bool ShowPopupForAllPartsInGroup
    {
        get
        {
            dynamic settingsValue = _qolSettings.GetValue(null);
            
            // Find the "showPopupforAllPartsinGroup" value from Settingssit Class and store it's value.
            // From QoLmod.cfg: When you remove wheel or suspension assembly from car, popup will show parts condition
            // This happens for every item in the group that is moved and the bulk move causes errors with the PopupManager.
            return settingsValue.Value.showPopupforAllPartsinGroup;
        }
        set
        {
            dynamic settingsValue = _qolSettings.GetValue(null);
            settingsValue.Value.showPopupforAllPartsinGroup = value;
        }
    }
}

/// <summary>
/// Remembers qol settings state and restores them after the class is disposed.
/// </summary>
public class QolSettingsGuard : IDisposable
{
    private readonly QolSettings _settings;
    private readonly bool _showPopupForGroupAddedInventory;
    private readonly bool _showPopupForAllPartsInGroup;
    private bool _disposed;

    public QolSettingsGuard(QolSettings settings)
    {
        _settings = settings;
        if (settings != null)
        {
            _showPopupForGroupAddedInventory = settings.ShowPopupForGroupAddedInventory;
            _showPopupForAllPartsInGroup = settings.ShowPopupForAllPartsInGroup;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_settings != null)
        {
            try
            {
                _settings.ShowPopupForGroupAddedInventory = _showPopupForGroupAddedInventory;
                _settings.ShowPopupForAllPartsInGroup = _showPopupForAllPartsInGroup;
            }
            catch
            {
                // Ignore any reflection-related errors during disposal
            }
        }
    }
}