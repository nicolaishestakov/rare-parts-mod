// The small IMGUI subset used by TransferSettingsPanel, backed by GDI+.
// This is a layout/interaction preview, not a Unity runtime implementation.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using DrawingColor = System.Drawing.Color;

namespace UnityEngine;

public class Component
{
    public T[] GetComponentsInChildren<T>(bool includeInactive) => [];
}
public class Font
{
    public Material material { get; } = new();
    public IntPtr Pointer => IntPtr.Zero;
    public string name => GUI.FontFamily.Name;
}
public struct Vector2(float x, float y) { public float x = x, y = y; }
public struct Vector3(float x, float y, float z)
{
    public float x = x, y = y, z = z;
    public static Vector3 zero => new(0, 0, 0);
}
public struct Quaternion { public static Quaternion identity => new(); }
public struct Matrix4x4
{
    public float Scale;
    public static Matrix4x4 identity => new() { Scale = 1 };
    public static Matrix4x4 TRS(Vector3 position, Quaternion rotation, Vector3 scale) => new() { Scale = scale.x };
}
public struct Rect(float x, float y, float width, float height)
{
    public float x = x, y = y, width = width, height = height;
    public float xMax => x + width;
    public bool Contains(Vector2 point) => point.x >= x && point.x < x + width && point.y >= y && point.y < y + height;
}
public struct Color(float r, float g, float b, float a = 1)
{
    public float r = r, g = g, b = b, a = a;
    public static Color white => new(1, 1, 1);
    public static Color black => new(0, 0, 0);
    internal DrawingColor Drawing => DrawingColor.FromArgb((int)(a * 255), (int)(r * 255), (int)(g * 255), (int)(b * 255));
}
public static class Mathf
{
    public static float Min(float a, float b) => Math.Min(a, b);
    public static float Max(float a, float b) => Math.Max(a, b);
    public static float Sqrt(float value) => MathF.Sqrt(value);
}
public static class Screen { public static int width, height; }
public static class Time
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    public static int frameCount;
    public static float unscaledTime => (float)Clock.Elapsed.TotalSeconds;
}
public enum KeyCode
{
    K, L, F7, Minus, Equals, KeypadMultiply, LeftShift, RightShift, Escape,
    Keypad0, Keypad1, Keypad2, Keypad3, Keypad4, Keypad5, Keypad6, Keypad7, Keypad8, Keypad9,
    KeypadPeriod, KeypadDivide, KeypadMinus, KeypadPlus, KeypadEnter, KeypadEquals,
    Alpha0, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Alpha9,
    Plus, Asterisk, Slash, Backslash, Period, Comma, Semicolon, Quote, BackQuote,
    LeftBracket, RightBracket, Return, UpArrow, DownArrow, LeftArrow, RightArrow
}
public static class Input
{
    internal static readonly HashSet<KeyCode> Released = [];
    internal static readonly HashSet<KeyCode> Held = [];
    public static Vector3 mousePosition;
    public static bool GetKeyUp(KeyCode key) => Released.Contains(key);
    public static bool GetKey(KeyCode key) => Held.Contains(key);
}
public enum EventType { Repaint, MouseDown, MouseUp, MouseMove, ScrollWheel, Used }
public class Event
{
    public static Event current = new();
    public EventType type;
    internal Vector2 ScreenPosition;
    public Vector2 mousePosition => new(ScreenPosition.x / GUI.matrix.Scale - GUI.GroupX,
        ScreenPosition.y / GUI.matrix.Scale - GUI.GroupY);
    public bool isMouse => type is EventType.MouseDown or EventType.MouseUp or EventType.MouseMove;
    public void Use() => type = EventType.Used;
}
public enum ScaleMode { StretchToFill }
public enum TextAnchor { UpperLeft, UpperCenter, MiddleLeft, MiddleCenter }
public enum FilterMode { Point, Bilinear, Trilinear }
public class Texture { public FilterMode filterMode; }
public class Material { public Texture mainTexture { get; } = new Texture2D(); }
public class Texture2D : Texture { public static readonly Texture2D whiteTexture = new(); }
public class RectOffset { public int top; }
public class GUIStyleState { public Color textColor = Color.white; }
public class GUIContent(string text) { public string text = text; }
public class GUIStyle
{
    internal string Kind;
    public int fontSize;
    public Font font;
    public TextAnchor alignment;
    public RectOffset padding = new();
    public GUIStyleState normal = new();
    public static GUIStyle none { get; } = new() { Kind = "none" };
    public GUIStyle() { }
    public GUIStyle(GUIStyle source)
    {
        Kind = source.Kind;
        fontSize = source.fontSize;
        font = source.font;
        alignment = source.alignment;
        padding.top = source.padding.top;
        normal.textColor = source.normal.textColor;
    }
    public Vector2 CalcSize(GUIContent content)
    {
        using var font = GUI.DrawingFont(this, 1);
        var size = GUI.Graphics.MeasureString(content.text, font, int.MaxValue, StringFormat.GenericTypographic);
        return new(size.Width, size.Height);
    }
}
public class GUISkin
{
    public Font font = new();
    public GUIStyle label = new() { Kind = "label", alignment = TextAnchor.UpperLeft };
    public GUIStyle button = new() { Kind = "button", alignment = TextAnchor.MiddleCenter };
    public GUIStyle box = new() { Kind = "box", alignment = TextAnchor.UpperCenter, padding = new() { top = 4 } };
    public GUIStyle toggle = new() { Kind = "toggle", alignment = TextAnchor.MiddleLeft };
}
public static class GUI
{
    public static Matrix4x4 matrix = Matrix4x4.identity;
    public static Color color = Color.white;
    public static readonly GUISkin skin = new();
    internal static Graphics Graphics;
    internal static FontFamily FontFamily = FontFamily.GenericSansSerif;
    internal static float GroupX, GroupY;
    private static GraphicsState _groupState;
    private static RectangleF? _pressed;

    internal static void BeginFrame(Graphics graphics, int width, int height, EventType type, Point mouse)
    {
        Graphics = graphics;
        Screen.width = width;
        Screen.height = height;
        Input.mousePosition = new(mouse.X, height - mouse.Y, 0);
        Event.current = new() { type = type, ScreenPosition = new(mouse.X, mouse.Y) };
        GroupX = GroupY = 0;
        matrix = Matrix4x4.identity;
        color = Color.white;
    }

    public static void BeginGroup(Rect rect)
    {
        _groupState = Graphics.Save();
        Graphics.SetClip(ToScreen(rect));
        GroupX = rect.x;
        GroupY = rect.y;
    }
    public static void EndGroup()
    {
        Graphics.Restore(_groupState);
        GroupX = GroupY = 0;
    }
    private static RectangleF ToScreen(Rect rect) => new((rect.x + GroupX) * matrix.Scale,
        (rect.y + GroupY) * matrix.Scale, rect.width * matrix.Scale, rect.height * matrix.Scale);
    internal static System.Drawing.Font DrawingFont(GUIStyle style, float scale) =>
        new(FontFamily, Math.Max(1, (style.fontSize > 0 ? style.fontSize : 13) * scale), FontStyle.Regular, GraphicsUnit.Pixel);

    public static void DrawTexture(Rect rect, Texture2D texture, ScaleMode mode = ScaleMode.StretchToFill, bool alphaBlend = true)
    {
        if (Event.current.type != EventType.Repaint) return;
        using var brush = new SolidBrush(color.Drawing);
        Graphics.FillRectangle(brush, ToScreen(rect));
    }

    public static void DrawTexture(Rect rect, Texture2D texture, ScaleMode mode,
        bool alphaBlend, float imageAspect, Color tint, float borderWidth, float borderRadius)
    {
        if (Event.current.type != EventType.Repaint) return;
        var bounds = ToScreen(rect);
        var diameter = borderRadius * 2 * matrix.Scale;
        using var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        var previousSmoothing = Graphics.SmoothingMode;
        try
        {
            Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(tint.Drawing);
            Graphics.FillPath(brush, path);
        }
        finally
        {
            Graphics.SmoothingMode = previousSmoothing;
        }
    }

    public static void Label(Rect rect, string text, GUIStyle style)
    {
        if (Event.current.type != EventType.Repaint) return;
        var bounds = ToScreen(rect);
        bounds.Y += style.padding.top * matrix.Scale;
        bounds.Height -= style.padding.top * matrix.Scale;
        using var font = DrawingFont(style, matrix.Scale);
        using var brush = new SolidBrush(style.normal.textColor.Drawing);
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = style.alignment is TextAnchor.UpperCenter or TextAnchor.MiddleCenter ? StringAlignment.Center : StringAlignment.Near,
            LineAlignment = style.alignment is TextAnchor.MiddleCenter or TextAnchor.MiddleLeft ? StringAlignment.Center : StringAlignment.Near,
            Trimming = StringTrimming.None
        };
        Graphics.DrawString(text, font, brush, bounds, format);
    }

    public static void Box(Rect rect, string text, GUIStyle style)
    {
        if (Event.current.type == EventType.Repaint)
        {
            using var pen = new Pen(DrawingColor.FromArgb(90, 90, 90));
            var bounds = ToScreen(rect);
            Graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        }
        Label(rect, text, style);
    }

    public static bool Button(Rect rect, string text, GUIStyle style)
    {
        var bounds = ToScreen(rect);
        var hovered = rect.Contains(Event.current.mousePosition);
        if (Event.current.type == EventType.Repaint && style.Kind != "none")
        {
            using var brush = new SolidBrush(hovered ? DrawingColor.FromArgb(85, 85, 85) : DrawingColor.FromArgb(55, 55, 55));
            using var pen = new Pen(DrawingColor.Gray);
            Graphics.FillRectangle(brush, bounds);
            Graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }
        Label(rect, text, style);
        if (Event.current.type == EventType.MouseDown && hovered)
        {
            _pressed = bounds;
            Event.current.Use();
        }
        else if (Event.current.type == EventType.MouseUp && _pressed == bounds)
        {
            _pressed = null;
            Event.current.Use();
            return hovered;
        }
        return false;
    }

    public static bool Toggle(Rect rect, bool value, string text, GUIStyle style)
    {
        if (Button(rect, "", GUIStyle.none)) value = !value;
        if (Event.current.type == EventType.Repaint)
        {
            var box = ToScreen(new(rect.x, rect.y + 7, 16, 16));
            using var pen = new Pen(DrawingColor.White);
            Graphics.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
            if (value)
            {
                using var brush = new SolidBrush(DrawingColor.Orange);
                Graphics.FillRectangle(brush, box.X + 3, box.Y + 3, box.Width - 6, box.Height - 6);
            }
        }
        Label(new(rect.x + 26, rect.y, rect.width - 26, rect.height), text, style);
        return value;
    }
}
