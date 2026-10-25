using System.Collections.Generic;
using CMS.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RareParts;

/// <summary>
/// Presentation for whole-location collection. TransferAll owns selection and transfer
/// behavior; settings are read live so this panel and the existing hotkeys stay in sync.
/// Add future filter controls in DrawSettings without duplicating transfer logic here.
/// </summary>
public sealed class TransferSettingsPanel
{
    private readonly TransferAll _transfer;
    private readonly ITransferAllConfig _config;
    private bool _onlyShoppingList;
    private bool _collectRequested;
    
    // Note: the commented properties and related code was capturing mouse/camera so the panel could work in the 3D scene
    // (when normally the mouse is fixed in the center and the movement rotates the camera)
    // This was not stable and somehow caused freezing when leaving junkyard (due to a crash when saving game)
    // It has been commented, but not deleted yet in case I want to fix this mode
    
    //private Rewired.Player _player;
    //private bool _wasPlaying;
    //private WindowManager _windows;
    //private bool _raycasterEnabled;
    //private bool _raycasterBlocked;
    //private bool _cursorVisible;
    //private CursorLockMode _cursorLock;
    //private bool _canShowCursor;
    //private bool _disableMouseEvents;
    //private Cursor3D _gameCursor;
    //private bool _gameCursorEnabled;
    //private readonly Dictionary<FPSCamera, bool> _cameraInputStates = new();
    //private readonly Dictionary<Image, bool> _cursorImageStates = new();

    public TransferSettingsPanel(TransferAll transfer, ITransferAllConfig config)
    {
        _transfer = transfer;
        _config = config;
    }

    public bool IsOpen { get; private set; }

    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void Open()
    {
        if (IsOpen)
            return;
        
        if (!_transfer.IsBarnOrJunkyard || TransferAll.CheckIfInputIsFocused())
            return;

        //_player = Singleton<GameManager>.Instance?.InputManager?.Player;
        //_windows = WindowManager.Instance;
        // if (_player == null || _windows == null)
        //     return;

        //_wasPlaying = _player.isPlaying;
        // _raycasterEnabled = _windows.isGraphicsRaycasterEnabled;
        // _raycasterBlocked = _windows.blockGraphicsRaycaster;
        // _cursorVisible = Cursor.visible;
        // _cursorLock = Cursor.lockState;
        // _canShowCursor = GameSettings.CanShowCursor;
        // _disableMouseEvents = GameSettings.DisableMouseEvents;

        IsOpen = true;
        //_player.isPlaying = false; //todo verify if needed
        // _windows.EnableGraphicsRaycasterAndBlock(false, true);
        // GameSettings.DisableMouseEvents = true;
        // GameSettings.CanShowCursor = true;
        // CaptureMouse();
        // ShowCursor();
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        _collectRequested = false;
        // if (_player != null)
        //     _player.isPlaying = _wasPlaying;
        // if (_windows != null)
        //     _windows.EnableGraphicsRaycasterAndBlock(_raycasterEnabled, _raycasterBlocked);
        // GameSettings.DisableMouseEvents = _disableMouseEvents;
        // GameSettings.CanShowCursor = _canShowCursor;
        // ReleaseMouse();
        // Cursor.lockState = _cursorLock;
        // Cursor.visible = _cursorVisible;
        // _player = null;
        // _windows = null;
    }

    public void Update()
    {
        if (!IsOpen)
            return;
        if (!_transfer.IsBarnOrJunkyard || Input.GetKeyUp(KeyCode.Escape))
        {
            Close();
            return;
        }

        if (_collectRequested)
        {
            // Execute once, outside IMGUI's multiple layout/repaint passes.
            Close();
            _transfer.CollectEntireJunkyardOrBarn(_onlyShoppingList);
            return;
        }

        if (Input.GetKeyUp(_config.SwitchModeKey))
            _transfer.SwitchTransferMode();
        if (Input.GetKeyUp(_config.SetPartConditionLower))
            _config.SetPartConditionLowerBy10();
        if (Input.GetKeyUp(_config.SetPartConditionHigherKey))
            _config.SetPartConditionHigherBy10();
        if (Input.GetKeyUp(_config.TransferEntireJunkyardOrBarnKey))
            _collectRequested = true;
        //ShowCursor();
    }

    // private void CaptureMouse()
    // {
    //     // Rewired's player flag alone does not stop the game's FPS mouse look.
    //     // Use the camera's input switch without disabling the camera itself.
    //     foreach (var camera in Object.FindObjectsOfType<FPSCamera>())
    //     {
    //         _cameraInputStates.Add(camera, camera.readInput);
    //         camera.readInput = false;
    //     }
    //
    //     // Cursor3D updates independently of IMGUI and can recenter the pointer.
    //     // Suspend it and hide its crosshair while using the OS cursor for the panel.
    //     _gameCursor = Cursor3D.Get();
    //     if (_gameCursor != null)
    //     {
    //         _gameCursorEnabled = _gameCursor.enabled;
    //         _gameCursor.enabled = false;
    //         HideCursorImage(_gameCursor.cursorImage);
    //         HideCursorImage(_gameCursor.cursorTimerImage);
    //     }
    // }

    // private void HideCursorImage(Image image)
    // {
    //     if (image == null || _cursorImageStates.ContainsKey(image))
    //         return;
    //     _cursorImageStates.Add(image, image.enabled);
    //     image.enabled = false;
    // }
    //
    // private void ReleaseMouse()
    // {
    //     foreach (var state in _cameraInputStates)
    //         if (state.Key != null)
    //             state.Key.readInput = state.Value;
    //     _cameraInputStates.Clear();
    //
    //     foreach (var state in _cursorImageStates)
    //         if (state.Key != null)
    //             state.Key.enabled = state.Value;
    //     _cursorImageStates.Clear();
    //
    //     if (_gameCursor != null)
    //         _gameCursor.enabled = _gameCursorEnabled;
    //     _gameCursor = null;
    // }

    public void LateUpdate()
    {
        // if (IsOpen)
        //     ShowCursor();
    }

    // private static void ShowCursor()
    // {
    //     Cursor.lockState = CursorLockMode.None;
    //     Cursor.visible = true;
    // }

    public void Draw()
    {
        if (!IsOpen)
            return;

        //ShowCursor();

        const float width = 620, height = 460;
        var previousMatrix = GUI.matrix;
        var scale = Mathf.Min(1f, Mathf.Min(Screen.width / (width + 20), Screen.height / (height + 20)));
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
        GUI.BeginGroup(new Rect((Screen.width / scale - width) / 2, (Screen.height / scale - height) / 2, width, height));
        try
        {
            GUI.Box(new Rect(0, 0, width, height), "Whole barn / junkyard transfer");
            GUI.Label(new Rect(20, 30, 580, 25), "Collect from every pile into the shopping cart (purchase as usual).");
            DrawSettings();
            GUI.Label(new Rect(20, 364, 580, 25), $"Hotkeys: {_config.SwitchModeKey} mode, {_config.SetPartConditionLower} / {_config.SetPartConditionHigherKey} condition");
            if (GUI.Button(new Rect(20, 408, 400, 32), "Transfer whole location to shopping cart"))
                _collectRequested = true;
            if (GUI.Button(new Rect(440, 408, 160, 32), $"Close ({_config.TransferSettingsKey})"))
                Close();
        }
        finally
        {
            GUI.EndGroup();
            GUI.matrix = previousMatrix;
        }
    }

    private void DrawSettings()
    {
        var mode = _transfer.TransferMode switch
        {
            TransferAll.Mode.ByCondition => "By condition",
            TransferAll.Mode.ByConditionSkipUnrepairable => "By condition, skip unrepairable",
            _ => "Only rare repairable"
        };
        Row(70, "Mode", mode);
        if (GUI.Button(new Rect(500, 70, 100, 25), "Change"))
            _transfer.SwitchTransferMode();

        Row(105, "Minimum condition", $"{_config.MinPartCondition}%");
        if (GUI.Button(new Rect(500, 105, 45, 25), "-"))
            _config.SetPartConditionLowerBy10();
        if (GUI.Button(new Rect(555, 105, 45, 25), "+"))
            _config.SetPartConditionHigherBy10();

        _onlyShoppingList = GUI.Toggle(new Rect(20, 145, 580, 25), _onlyShoppingList,
            "Shopping-list items only (maps and cases still included)");
        Row(185, "Maps / cases filter (config)", OnOff(_config.TransferMapsOrCasesOnlyAtBarnOrJunkyard));
        Row(215, "Parts filter (config)", OnOff(_config.TransferPartsOnlyAtBarnOrJunkyard));
        Row(245, "Repair thresholds", $"Normal: {PartsInfo.NormalRepairablePartsMinCondition * 100:0}%   Special: {PartsInfo.SpecialRepairablePartsMinCondition * 100:0}%");

        var specialFilter = !_onlyShoppingList &&
            (_config.TransferMapsOrCasesOnlyAtBarnOrJunkyard || _config.TransferPartsOnlyAtBarnOrJunkyard);
        var explanation = _onlyShoppingList
            ? "Shopping-list matches of any condition, plus maps and cases.\nMode, minimum condition and config filters are ignored."
            : specialFilter
                ? "Config filters select their combined categories directly.\nMode, condition and shopping-list inclusion are bypassed."
                : "Shopping-list matches, maps and cases are always included.\n" +
                  (_transfer.TransferMode == TransferAll.Mode.OnlyRareRepairable
                      ? "Minimum condition is ignored in rare repairable mode."
                      : "Other parts use the selected mode and minimum condition.");
        GUI.Label(new Rect(20, 290, 580, 60), explanation);
    }

    private static void Row(float y, string label, string value)
    {
        GUI.Label(new Rect(20, y, 235, 25), label);
        GUI.Label(new Rect(255, y, 345, 25), value);
    }

    private static string OnOff(bool value) => value ? "On" : "Off";
}
