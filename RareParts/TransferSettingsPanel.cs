using System.Collections.Generic;
using CMS.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
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
    private static TransferSettingsPanel _activePanel;
    private static int _blockInputThroughFrame = -1;

    // A small outlined arrow, drawn with the built-in white texture. No native
    // texture allocation or game cursor/camera state needs to be managed.
    private static readonly string[] PanelCursor =
    [
        "#",
        " ###",
        " #..###",
        " #.....###",
        "  #.......###",
        "  #..........###",
        "  #.............###",
        "   #...............###",
        "   #..................###",
        "   #.....................###",
        "    #......................#",
        "    #....................##",
        "    #...................#",
        "     #................##",
        "     #...............#",
        "     #..............#",
        "      #.............#",
        "      #..............#",
        "      #...............#",
        "       #...............#",
        "       #................#",
        "       #.......##........#",
        "        #.....#  #........#",
        "        #....#    #........#",
        "        #...#      #.......#",
        "         #..#       #.....#",
        "         #.#         #...#",
        "         ##           #.#",
        "                       #"
    ];

    // IMGUI and the game's EventSystem read input independently. Consuming an
    // IMGUI event alone cannot prevent the game from also handling that click.
    internal static bool BlocksGameInput => _activePanel != null || Time.frameCount <= _blockInputThroughFrame;
    
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
        _activePanel = this;
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
        if (_activePanel == this)
            _activePanel = null;
        // OnGUI can close after the game's Update, or collection can close before
        // it. Keep the release from reaching the game in either execution order.
        _blockInputThroughFrame = Time.frameCount + 1;
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
        var previousColor = GUI.color;
        var scale = Mathf.Min(1f, Mathf.Min(Screen.width / (width + 20), Screen.height / (height + 20)));
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
        GUI.BeginGroup(new Rect((Screen.width / scale - width) / 2, (Screen.height / scale - height) / 2, width, height));
        try
        {
            GUI.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture, ScaleMode.StretchToFill, false);
            GUI.color = Color.white;
            GUI.Box(new Rect(0, 0, width, height), "Whole barn / junkyard transfer");
            GUI.Label(new Rect(20, 30, 580, 25), "Collect from every pile into the shopping cart (purchase as usual).");
            DrawSettings();
            GUI.Label(new Rect(20, 364, 580, 25), $"Hotkeys: {_config.SwitchModeKey} mode, {_config.SetPartConditionLower} / {_config.SetPartConditionHigherKey} condition");
            if (GUI.Button(new Rect(20, 408, 400, 32), "Transfer whole location to shopping cart"))
                _collectRequested = true;
            if (GUI.Button(new Rect(440, 408, 160, 32), $"Close ({_config.TransferSettingsKey})"))
                Close();

            // Also consume unused clicks/scrolls on the background for other
            // IMGUI handlers. Game UI input is blocked separately below.
            if (Event.current.isMouse || Event.current.type == EventType.ScrollWheel)
                Event.current.Use();
        }
        finally
        {
            GUI.EndGroup();
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        // Draw last, outside the group so the arrow is not clipped at its edges.
        DrawPanelCursor(new Rect((Screen.width - width * scale) / 2,
            (Screen.height - height * scale) / 2, width * scale, height * scale));
    }

    private void DrawPanelCursor(Rect panelBounds)
    {
        if (!IsOpen || Event.current.type != EventType.Repaint)
            return;

        var mouse = Input.mousePosition;
        var position = new Vector2(mouse.x, Screen.height - mouse.y);
        // Outside the opaque panel the original game cursor is still visible.
        if (!panelBounds.Contains(position))
            return;

        var previousMatrix = GUI.matrix;
        var previousColor = GUI.color;
        try
        {
            GUI.matrix = Matrix4x4.identity;
            const float pixelSize = 1f;
            for (var y = 0; y < PanelCursor.Length; y++)
            {
                var row = PanelCursor[y];
                for (var x = 0; x < row.Length;)
                {
                    var start = x;
                    var color = row[x++];
                    while (x < row.Length && row[x] == color)
                        x++;
                    if (color == ' ')
                        continue;

                    GUI.color = color == '#' ? new Color(0.4f, 0.4f, 0.36f, 1f) : Color.white;
                    GUI.DrawTexture(new Rect(position.x + start * pixelSize,
                        position.y + y * pixelSize, (x - start) * pixelSize, pixelSize),
                        Texture2D.whiteTexture);
                }
            }
        }
        finally
        {
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
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

// Suppress dispatch rather than changing player/camera/raycaster state. There is
// no captured game object to restore after a scene unload or during saving.
[HarmonyPatch(typeof(EventSystem), "Update")]
internal static class TransferPanelEventSystemPatch
{
    [HarmonyPrefix]
    private static bool Prefix() => !TransferSettingsPanel.BlocksGameInput;
}

[HarmonyPatch]
internal static class TransferPanelActionInputPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        // These actions can be polled directly, outside Unity's EventSystem.
        string[] actions =
        [
            "GameplayActionButtonDown", "GameplayActionButtonUp", "GameplayActionButton",
            "GameplayMechanicActionButtonDown", "GameplayMechanicActionButtonUp", "GameplayMechanicActionButton",
            "UIEnterButtonDown", "UISubmitButtonDown", "UISubmitAltDown", "UICancelButtonDown"
        ];
        foreach (var action in actions)
            yield return AccessTools.Method(typeof(InputManager), action);
    }

    [HarmonyPrefix]
    private static bool Prefix(ref bool __result)
    {
        if (!TransferSettingsPanel.BlocksGameInput)
            return true;
        __result = false;
        return false;
    }
}
