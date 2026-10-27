using System.Collections.Generic;
using CMS.UI;
using CMS.UI.Windows;
using HarmonyLib;
using RareParts.Extensions;
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
    private string _nameFilter = string.Empty;
    private bool _resetTextFocus;
    private const string NameFilterControl = "TransferNameFilter";
    private static readonly Rect NameFilterRect = new(280, 280, 560, 30);
    public bool IsEditingText { get; private set; }
    internal static bool IsTextInputFocused => _activePanel != null && _activePanel.IsEditingText;
    private enum TransferRequest { None, Collect, Return }
    private TransferRequest _transferRequested;
    private Font _gameFont;
    private float _nextFontLookup;
    private GUIStyle _labelStyle, _buttonStyle, _titleStyle, _textFieldStyle;
    private GUIStyle _actionStyle, _shortcutStyle;
    private readonly Dictionary<Texture, FilterMode> _fontFilters = new();
    private static readonly Color TitleBackground = new(49 / 255f, 49 / 255f, 49 / 255f, 0.97f);
    private static readonly Color PanelBorder = new(111 / 255f, 111 / 255f, 110 / 255f, 0.97f);
    private static readonly Color SeparatorColor = new(1f, 160 / 255f, 0f);
    private static readonly Color ShortcutBackground = new(102 / 255f, 102 / 255f, 102 / 255f);
    private static readonly Color ShortcutForeground = new(247 / 255f, 247 / 255f, 247 / 255f);
    private static readonly Color ActionForeground = new(168 / 255f, 168 / 255f, 168 / 255f);
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
        _resetTextFocus = true;
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
        IsEditingText = false;
        if (_activePanel == this)
            _activePanel = null;
        // OnGUI can close after the game's Update, or collection can close before
        // it. Keep the release from reaching the game in either execution order.
        _blockInputThroughFrame = Time.frameCount + 1;
        _transferRequested = TransferRequest.None;
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

        if (_transferRequested != TransferRequest.None)
        {
            // Execute once, outside IMGUI's multiple layout/repaint passes.
            var request = _transferRequested;
            Close();
            if (request == TransferRequest.Return)
                _transfer.ReturnEntireJunkyardOrBarn();
            else
                _transfer.CollectEntireJunkyardOrBarn(_nameFilter);
            return;
        }

        // Typing must not trigger configured letter/punctuation shortcuts.
        if (IsEditingText)
            return;

        if (Input.GetKeyUp(_config.SwitchModeKey))
            _transfer.SwitchTransferMode();
        if (Input.GetKeyUp(_config.SetPartConditionLower))
            _config.SetPartConditionLowerBy10();
        if (Input.GetKeyUp(_config.SetPartConditionHigherKey))
            _config.SetPartConditionHigherBy10();
        if (Input.GetKeyUp(_config.TransferEntireJunkyardOrBarnKey))
            _transferRequested = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
                ? TransferRequest.Return : TransferRequest.Collect;
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

        PrepareFontStyles();
        if (_resetTextFocus)
        {
            GUI.FocusControl(null);
            _resetTextFocus = false;
        }

        //ShowCursor();

        const float height = 460, titleHeight = 56, bodyBottom = 383, actionY = 408;
        const string collectLabel = "TRANSFER TO SHOPPING CART", returnLabel = "RETURN TO JUNK PILES", closeLabel = "CLOSE";
        var collectShortcut = _config.TransferEntireJunkyardOrBarnKey.ToShortcutString();
        var returnShortcut = $"SHIFT + {collectShortcut}";
        var closeShortcut = _config.TransferSettingsKey.ToShortcutString();
        var collectWidth = ActionWidth(collectShortcut, collectLabel);
        var returnWidth = ActionWidth(returnShortcut, returnLabel);
        var closeWidth = ActionWidth(closeShortcut, closeLabel);
        // Measure labels and configurable shortcuts so the left-aligned row never overlaps.
        var width = Mathf.Max(860, 40 + collectWidth + returnWidth + closeWidth + 48);
        var previousMatrix = GUI.matrix;
        var previousColor = GUI.color;
        var scale = Mathf.Min(1f, Mathf.Min(Screen.width / (width + 20), Screen.height / (height + 20)));
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
        GUI.BeginGroup(new Rect((Screen.width / scale - width) / 2, (Screen.height / scale - height) / 2, width, height));
        try
        {
            SmoothFont(_labelStyle);
            SmoothFont(_buttonStyle);
            SmoothFont(_titleStyle);
            SmoothFont(_actionStyle);
            SmoothFont(_shortcutStyle);
            SmoothFont(_textFieldStyle);
            if (Event.current.type == EventType.MouseDown && !NameFilterRect.Contains(Event.current.mousePosition))
                GUI.FocusControl(null);
            DrawPanelBackground(width, titleHeight, bodyBottom);
            GUI.color = Color.white;
            GUI.Label(new Rect(1, 1, width - 2, titleHeight - 1), "TRANSFER ALL ITEMS", _titleStyle);
            DrawSettings();
            IsEditingText = GUI.GetNameOfFocusedControl() == NameFilterControl;
            if (DrawAction(new Rect(20, actionY, collectWidth, 36), collectShortcut, collectLabel))
                _transferRequested = TransferRequest.Collect;
            if (DrawAction(new Rect(20 + collectWidth + 24, actionY, returnWidth, 36), returnShortcut, returnLabel))
                _transferRequested = TransferRequest.Return;
            if (DrawAction(new Rect(20 + collectWidth + returnWidth + 48, actionY, closeWidth, 36), closeShortcut, closeLabel))
                Close();

            // Also consume unused clicks/scrolls on the background for other
            // IMGUI handlers. Game UI input is blocked separately below.
            if (Event.current.isMouse || Event.current.type == EventType.ScrollWheel)
                Event.current.Use();
        }
        finally
        {
            foreach (var entry in _fontFilters)
                if (entry.Key != null)
                    entry.Key.filterMode = entry.Value;
            _fontFilters.Clear();
            GUI.EndGroup();
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        // Draw last, outside the group so the arrow is not clipped at its edges.
        DrawPanelCursor(new Rect((Screen.width - width * scale) / 2,
            (Screen.height - height * scale) / 2, width * scale, height * scale));
    }

    private void SmoothFont(GUIStyle style)
    {
        if (Event.current.type != EventType.Repaint)
            return;

        var font = style.font ?? GUI.skin.font;
        var texture = font != null && font.material != null ? font.material.mainTexture : null;
        if (texture == null || _fontFilters.ContainsKey(texture))
            return;

        // Font atlases belong to the game; restore their filtering after drawing.
        _fontFilters.Add(texture, texture.filterMode);
        texture.filterMode = FilterMode.Bilinear;
    }

    private void PrepareFontStyles()
    {
        // Look up the actual UI font rather than relying on an asset name or
        // shipping an extracted copy. The inventory also supplies inactive text.
        var fontChanged = false;
        if (_gameFont == null && Time.unscaledTime >= _nextFontLookup)
        {
            _nextFontLookup = Time.unscaledTime + 1f;
            var windows = WindowManager.Instance;
            if (windows != null)
            {
                _gameFont = FindMainFont(windows.GetWindowByID<InventoryWindow>(WindowID.Inventory));
                if (_gameFont == null)
                    _gameFont = FindMainFont(windows.GetWindowByID<ItemsExchangeWindow>(WindowID.ItemsExchange));
            }
            fontChanged = _gameFont != null;
            if (fontChanged)
                MelonLoader.MelonLogger.Msg($"Transfer panel font: {_gameFont.name}");
        }

        if (_labelStyle != null && !fontChanged)
            return;

        // Copies keep other IMGUI overlays and the game's own styles unchanged.
        _labelStyle = CreateFontStyle(GUI.skin.label);
        _buttonStyle = CreateFontStyle(GUI.skin.button);
        _titleStyle = CreateFontStyle(GUIStyle.none);
        _titleStyle.fontSize = 30;
        _titleStyle.alignment = TextAnchor.MiddleCenter;
        _titleStyle.normal.textColor = Color.white;
        _textFieldStyle = CreateFontStyle(GUI.skin.textField);
        _actionStyle = CreateFontStyle(GUIStyle.none);
        _actionStyle.alignment = TextAnchor.MiddleLeft;
        _shortcutStyle = CreateFontStyle(GUIStyle.none);
        _shortcutStyle.alignment = TextAnchor.MiddleCenter;
        _shortcutStyle.normal.textColor = ShortcutForeground;
    }

    private static void DrawPanelBackground(float width, float titleHeight, float bodyBottom)
    {
        if (Event.current.type != EventType.Repaint)
            return;

        FillRect(new Rect(1, 1, width - 2, titleHeight - 1), TitleBackground);
        // Use the built-in texture for the gradient, avoiding native texture lifetime management.
        var gradientHeight = (int)(bodyBottom - titleHeight - 3);
        for (var y = 0; y < gradientHeight; y++)
        {
            var t = y / (float)(gradientHeight - 1);
            var color = new Color((86 - 32 * t) / 255f, (86 - 34 * t) / 255f,
                (85 - 34 * t) / 255f, 0.97f);
            FillRect(new Rect(1, titleHeight + y, width - 2, 1), color);
        }
        FillRect(new Rect(0, 0, width, 1), PanelBorder);
        FillRect(new Rect(0, 1, 1, bodyBottom - 4), PanelBorder);
        FillRect(new Rect(width - 1, 1, 1, bodyBottom - 4), PanelBorder);
        FillRect(new Rect(0, bodyBottom - 3, width, 3), SeparatorColor);
        // The command row below the separator deliberately has no background or border.
    }

    private static void FillRect(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
    }

    private static Font FindMainFont(Component window)
    {
        if (window == null)
            return null;

        var counts = new Dictionary<System.IntPtr, int>();
        Font selected = null;
        var highestCount = 0;
        foreach (var text in window.GetComponentsInChildren<Text>(true))
        {
            var font = text.font;
            if (font == null)
                continue;
            counts.TryGetValue(font.Pointer, out var count);
            counts[font.Pointer] = ++count;
            if (count > highestCount)
            {
                selected = font;
                highestCount = count;
            }
        }
        return selected;
    }

    private GUIStyle CreateFontStyle(GUIStyle source)
    {
        var style = new GUIStyle(source);
        style.fontSize = (source.fontSize > 0 ? source.fontSize : 13) + 5;
        if (_gameFont != null)
        {
            style.font = _gameFont;
        }
        return style;
    }

    private void DrawLabel(Rect rect, string text) => GUI.Label(rect, text, _labelStyle);
    private bool DrawButton(Rect rect, string text) => GUI.Button(rect, text, _buttonStyle);

    private float ShortcutWidth(string shortcut) => Mathf.Max(30, _shortcutStyle.CalcSize(new GUIContent(shortcut)).x + 16);

    private float ActionWidth(string shortcut, string label) =>
        ShortcutWidth(shortcut) + 12 + _actionStyle.CalcSize(new GUIContent(label)).x + 4;

    private bool DrawAction(Rect rect, string shortcut, string label)
    {
        var hovered = rect.Contains(Event.current.mousePosition);
        // The invisible button retains IMGUI's press/release handling. Only the
        // shortcut badge and label are painted; the action has no background.
        var clicked = GUI.Button(rect, string.Empty, GUIStyle.none);
        var badgeWidth = ShortcutWidth(shortcut);
        var badge = new Rect(rect.x, rect.y + 3, badgeWidth, 30);
        DrawShortcutBadge(badge);
        GUI.Label(badge, shortcut, _shortcutStyle);
        _actionStyle.normal.textColor = hovered ? SeparatorColor : ActionForeground;
        GUI.Label(new Rect(badge.xMax + 12, rect.y, rect.width - badgeWidth - 12, rect.height), label, _actionStyle);
        return clicked;
    }

    private static void DrawShortcutBadge(Rect rect)
    {
        if (Event.current.type != EventType.Repaint)
            return;

        // Unity's rounded-rectangle shader smooths both the outer and inner edges.
        const float radius = 6, border = 1.5f;
        var previousColor = GUI.color;
        try
        {
            GUI.color = Color.white;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill,
                true, 0, ShortcutForeground, 0, radius);
            var interior = new Rect(rect.x + border, rect.y + border,
                rect.width - 2 * border, rect.height - 2 * border);
            GUI.DrawTexture(interior, Texture2D.whiteTexture, ScaleMode.StretchToFill,
                true, 0, ShortcutBackground, 0, radius - border);
        }
        finally
        {
            GUI.color = previousColor;
        }
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
            TransferAll.Mode.ShopListItemsOnly => "Shopping-list items only",
            _ => "Only rare repairable"
        };
        Row(70, "MODE", mode);
        if (DrawButton(new Rect(600, 70, 240, 30), $"CHANGE ({_config.SwitchModeKey.ToShortcutString()})"))
            _transfer.SwitchTransferMode();

        Row(105, "MIN CONDITION", $"{_config.MinPartCondition}%");
        if (DrawButton(new Rect(600, 105, 110, 30), "-"))
            _config.SetPartConditionLowerBy10();
        if (DrawButton(new Rect(730, 105, 110, 30), "+"))
            _config.SetPartConditionHigherBy10();

        Row(185, "Maps / cases filter (config)", OnOff(_config.TransferMapsOrCasesOnlyAtBarnOrJunkyard));
        Row(215, "Parts filter (config)", OnOff(_config.TransferPartsOnlyAtBarnOrJunkyard));
        Row(245, "Repair thresholds", $"Normal: {PartsInfo.NormalRepairablePartsMinCondition * 100:0}%   Special: {PartsInfo.SpecialRepairablePartsMinCondition * 100:0}%");
        DrawLabel(new Rect(20, 280, 250, 30), "NAME CONTAINS");
        GUI.SetNextControlName(NameFilterControl);
        _nameFilter = GUI.TextField(NameFilterRect, _nameFilter, _textFieldStyle);

        var onlyShoppingList = _transfer.TransferMode == TransferAll.Mode.ShopListItemsOnly;
        var specialFilter = !onlyShoppingList &&
            (_config.TransferMapsOrCasesOnlyAtBarnOrJunkyard || _config.TransferPartsOnlyAtBarnOrJunkyard);
        var explanation = onlyShoppingList
            ? "Shopping-list matches of any condition, plus maps and cases.\nMinimum condition and config filters are ignored."
            : specialFilter
                ? "Config filters select their combined categories directly.\nMode, condition and shopping-list inclusion are bypassed."
                : "Shopping-list matches, maps and cases are always included.\n" +
                  (_transfer.TransferMode == TransferAll.Mode.OnlyRareRepairable
                      ? "Minimum condition is ignored in rare repairable mode."
                      : "Other parts use the selected mode and minimum condition.");
        DrawLabel(new Rect(20, 320, 820, 60), explanation + "\nName filter applies to all results; blank matches all names.");
    }

    private void Row(float y, string label, string value)
    {
        DrawLabel(new Rect(20, y, 250, 30), label);
        DrawLabel(new Rect(280, y, 310, 30), value);
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
