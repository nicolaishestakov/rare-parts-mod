using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;
using UnityEngine;
using Color = System.Drawing.Color;
using Screen = UnityEngine.Screen;

namespace RareParts;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var fonts = new PrivateFontCollection();
        try
        {
            var fontPath = Option(args, "--font");
            if (fontPath != null)
            {
                fonts.AddFontFile(Path.GetFullPath(fontPath));
                GUI.FontFamily = fonts.Families[0];
            }
            if (Array.IndexOf(args, "--self-test") >= 0 || Option(args, "--screenshot") != null)
            {
                using var surface = new PreviewSurface { Size = new Size(1000, 680) };
                if (Array.IndexOf(args, "--self-test") >= 0)
                    SelfTest(surface);
                var screenshot = Option(args, "--screenshot");
                if (screenshot != null)
                {
                    using var bitmap = surface.Snapshot();
                    bitmap.Save(Path.GetFullPath(screenshot), ImageFormat.Png);
                }
                return 0;
            }
            Application.Run(new PreviewWindow());
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            if (args.Length == 0) MessageBox.Show(exception.ToString(), "Panel preview error");
            return 1;
        }
    }

    private static string Option(string[] args, string option)
    {
        var index = Array.IndexOf(args, option);
        if (index < 0) return null;
        if (index + 1 >= args.Length) throw new ArgumentException($"Missing value after {option}");
        return args[index + 1];
    }

    private static void SelfTest(PreviewSurface surface)
    {
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        // These coordinates intentionally check the actual panel controls, not
        // the stand-in transfer methods. Layout changes may require updating them.
        void Click(float x, float y)
        {
            var scale = Math.Min(1f, Math.Min(surface.Width / 880f, surface.Height / 480f));
            var point = new Point((int)((surface.Width - 860 * scale) / 2 + x * scale),
                (int)((surface.Height - 460 * scale) / 2 + y * scale));
            surface.Dispatch(EventType.MouseDown, point);
            surface.Dispatch(EventType.MouseUp, point);
            surface.Tick();
        }
        using (surface.Snapshot()) { }
        Click(700, 85);
        Check(surface.Transfer.TransferMode == TransferAll.Mode.ShopListItemsOnly, "Mode button did not select shopping-list-only.");
        Click(700, 85);
        Check(surface.Transfer.TransferMode == TransferAll.Mode.ByCondition, "Mode button did not cycle.");
        Click(780, 120);
        Check(surface.Config.MinPartCondition == 10, "Condition + button failed.");
        Click(400, 295);
        Check(surface.Panel.IsEditingText, "Name filter did not receive focus.");
        foreach (var character in "oil")
            surface.Dispatch(EventType.KeyDown, Point.Empty, character);
        surface.ReleaseKey(KeyCode.L);
        surface.ReleaseKey(KeyCode.KeypadMultiply);
        surface.ReleaseKey(KeyCode.Minus);
        surface.ReleaseKey(KeyCode.F7);
        surface.Tick();
        Check(surface.Panel.IsOpen && surface.Transfer.CollectionCalls == 0 &&
              surface.Transfer.TransferMode == TransferAll.Mode.ByCondition && surface.Config.MinPartCondition == 10,
            "Typing in the name filter triggered a shortcut.");
        Click(700, 85);
        Click(700, 85);
        Click(700, 85);
        Click(250, 425);
        Check(surface.Transfer.CollectionCalls == 1 && surface.Transfer.LastShoppingListOnly,
            "Collection must execute once with the selected shopping-list mode.");
        Check(surface.Transfer.LastNameFilter == "oil", "Name filter was not passed to collection.");
        Check(!surface.Panel.IsOpen, "Collection should close the panel.");
        surface.Tick();
        Check(surface.Transfer.CollectionCalls == 1, "Collection executed twice.");
        surface.Panel.Open();
        Click(450, 425);
        Check(surface.Transfer.ReturnCalls == 1, "Return button failed.");
        surface.Panel.Open();
        surface.ReleaseKey(KeyCode.L, shift: true);
        surface.Tick();
        Check(surface.Transfer.ReturnCalls == 2, "Shift+L should return.");
        surface.Panel.Open();
        Click(700, 85);
        surface.ReleaseKey(KeyCode.L);
        surface.Tick();
        Check(surface.Transfer.CollectionCalls == 2 && !surface.Transfer.LastShoppingListOnly,
            "Leaving shopping-list mode must stop filtering to shopping-list items.");
        surface.Panel.Open();
        Click(700, 425);
        Check(!surface.Panel.IsOpen, "Close button failed.");
        surface.Panel.Open();
        surface.ReleaseKey(KeyCode.Escape);
        Check(!surface.Panel.IsOpen, "Escape should close.");
        surface.Transfer.IsBarnOrJunkyard = false;
        surface.Panel.Open();
        Check(!surface.Panel.IsOpen, "Panel opened outside barn/junkyard.");
        surface.Transfer.IsBarnOrJunkyard = true;
        surface.Panel.Open();
        surface.Size = new Size(640, 480);
        using (surface.Snapshot()) { }
        Click(780, 120);
        Check(surface.Config.MinPartCondition == 20, "Scaled hit testing failed.");
        surface.Config.TransferMapsOrCasesOnlyAtBarnOrJunkyard = true;
        using (surface.Snapshot()) { }
        surface.Config.TransferMapsOrCasesOnlyAtBarnOrJunkyard = false;
        surface.Size = new Size(1000, 680);
        Console.WriteLine("Panel preview self-test passed.");
    }
}

internal sealed class PreviewWindow : Form
{
    private readonly PreviewSurface _surface = new() { Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 33 };

    public PreviewWindow()
    {
        Text = "Rare Parts — TransferSettingsPanel preview (simulated game)";
        ClientSize = new Size(1040, 760);
        MinimumSize = new Size(640, 520);
        KeyPreview = true;
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 45, Padding = new Padding(8) };
        var reopen = new Button { Text = "Open / close (F7)", AutoSize = true };
        reopen.Click += (_, _) => { _surface.Panel.Toggle(); _surface.Focus(); };
        toolbar.Controls.Add(reopen);
        AddOption("Maps / cases filter", false, value => _surface.Config.TransferMapsOrCasesOnlyAtBarnOrJunkyard = value);
        AddOption("Parts filter", false, value => _surface.Config.TransferPartsOnlyAtBarnOrJunkyard = value);
        AddOption("In barn / junkyard", true, value => _surface.Transfer.IsBarnOrJunkyard = value);
        Controls.Add(_surface);
        Controls.Add(toolbar);
        _timer.Tick += (_, _) => _surface.Tick();
        _timer.Start();
        KeyDown += (_, e) =>
        {
            if (e.Shift) Input.Held.Add(KeyCode.LeftShift);
        };
        KeyUp += (_, e) =>
        {
            if (MapKey(e.KeyCode) is { } key)
            {
                _surface.ReleaseKey(key, e.Shift);
                e.Handled = true;
            }
            if (!e.Shift) Input.Held.Clear();
        };
        Deactivate += (_, _) => Input.Held.Clear();

        void AddOption(string text, bool initial, Action<bool> set)
        {
            var option = new CheckBox { Text = text, Checked = initial, AutoSize = true, Margin = new Padding(10, 6, 0, 0) };
            option.CheckedChanged += (_, _) => { set(option.Checked); _surface.Focus(); };
            toolbar.Controls.Add(option);
        }
    }
    private static KeyCode? MapKey(Keys key) => key switch
    {
        Keys.F7 => KeyCode.F7, Keys.L => KeyCode.L, Keys.Escape => KeyCode.Escape,
        Keys.Multiply => KeyCode.KeypadMultiply, Keys.OemMinus => KeyCode.Minus,
        Keys.Oemplus => KeyCode.Equals, _ => null
    };
    protected override void Dispose(bool disposing)
    {
        if (disposing) { _timer.Dispose(); _surface.Panel.Close(); }
        base.Dispose(disposing);
    }
}

internal sealed class PreviewSurface : Control
{
    internal readonly PreviewConfig Config = new();
    internal readonly TransferAll Transfer = new();
    internal readonly TransferSettingsPanel Panel;
    private Point _mouse = new(-100, -100);
    private bool _cursorHidden;

    public PreviewSurface()
    {
        DoubleBuffered = true;
        TabStop = true;
        Panel = new(Transfer, Config);
        Panel.Open();
        SetStyle(ControlStyles.ResizeRedraw, true);
    }
    internal void Tick()
    {
        Time.frameCount++;
        Panel.Update();
        Panel.LateUpdate();
        Input.Released.Clear();
        UpdateCursor();
        Invalidate();
    }
    internal void ReleaseKey(KeyCode key, bool shift = false)
    {
        Input.Held.Clear();
        if (shift) Input.Held.Add(KeyCode.LeftShift);
        Input.Released.Add(key);
        if (key == KeyCode.F7 && !Panel.IsEditingText) Panel.Toggle();
        Tick();
    }
    internal void Dispatch(EventType type, Point point, char character = '\0')
    {
        _mouse = point;
        using var bitmap = new Bitmap(Math.Max(1, Width), Math.Max(1, Height));
        using var graphics = Graphics.FromImage(bitmap);
        GUI.BeginFrame(graphics, Width, Height, type, point);
        Event.current.character = character;
        Panel.Draw();
        Invalidate();
    }
    internal Bitmap Snapshot()
    {
        var bitmap = new Bitmap(Width, Height);
        using var graphics = Graphics.FromImage(bitmap);
        Render(graphics);
        return bitmap;
    }
    protected override void OnPaint(PaintEventArgs e) => Render(e.Graphics);
    private void Render(Graphics graphics)
    {
        graphics.Clear(Color.FromArgb(42, 45, 48));
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var brush = new SolidBrush(Color.LightGray);
        graphics.DrawString("PREVIEW — actual panel source / simulated transfers / approximate Unity rendering",
            SystemFonts.MessageBoxFont, brush, 12, 10);
        graphics.DrawString(Transfer.LastAction, SystemFonts.MessageBoxFont, brush, 12, Height - 45);
        graphics.DrawString($"Font: {GUI.FontFamily.Name}. F7 reopens the panel. Resize the window to check scaling.",
            SystemFonts.MessageBoxFont, brush, 12, Height - 25);
        GUI.BeginFrame(graphics, Width, Height, EventType.Repaint, _mouse);
        Panel.Draw();
    }
    protected override void OnMouseMove(MouseEventArgs e) { _mouse = e.Location; UpdateCursor(); Invalidate(); }
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (!Panel.IsEditingText) return;
        Dispatch(EventType.KeyDown, _mouse, e.KeyChar);
        e.Handled = true;
    }
    protected override void OnMouseLeave(EventArgs e) { _mouse = new(-100, -100); UpdateCursor(); Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        if (e.Button == MouseButtons.Left) Dispatch(EventType.MouseDown, e.Location);
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) Dispatch(EventType.MouseUp, e.Location);
    }

    private void UpdateCursor()
    {
        if (!IsHandleCreated) return;
        var scale = Math.Min(1f, Math.Min(Width / 880f, Height / 480f));
        var bounds = new RectangleF((Width - 860 * scale) / 2, (Height - 460 * scale) / 2,
            860 * scale, 460 * scale);
        var hide = Panel.IsOpen && bounds.Contains(_mouse);
        if (hide == _cursorHidden) return;
        if (hide) System.Windows.Forms.Cursor.Hide();
        else System.Windows.Forms.Cursor.Show();
        _cursorHidden = hide;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _cursorHidden)
        {
            System.Windows.Forms.Cursor.Show();
            _cursorHidden = false;
        }
        base.Dispose(disposing);
    }
}
