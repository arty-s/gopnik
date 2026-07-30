using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Gopnik.Engine;

/// <summary>Keyboard state for one frame. Scenes consume it; the form fills it.</summary>
public sealed class InputState
{
    private readonly List<char> _typed = new();
    private readonly List<Keys> _pressed = new();

    public IReadOnlyList<char> Typed => _typed;
    public IReadOnlyList<Keys> Pressed => _pressed;

    // Public rather than internal so the offscreen test harness can drive a scene directly,
    // instead of throwing synthetic keystrokes at whatever window happens to have focus.
    public void PushChar(char c) => _typed.Add(c);
    public void PushKey(Keys k) => _pressed.Add(k);
    public void Clear() { _typed.Clear(); _pressed.Clear(); }

    public bool Hit(Keys k) => _pressed.Contains(k);
    public bool AnyKey => _typed.Count > 0 || _pressed.Count > 0;
}

/// <summary>One screen of the game.</summary>
public interface IScene
{
    void Update(double dt, InputState input, SceneHost host);
    void Draw(TextScreen s, double time);
}

/// <summary>What a scene may do to the world outside itself.</summary>
public interface SceneHost
{
    void Go(IScene next);
    void Quit();
    double Time { get; }
}

/// <summary>
/// A single-line text prompt, because the original was command driven and that is
/// half of its character. Scenes drive it; it owns nothing but the caret.
/// </summary>
public sealed class CommandLine
{
    private string _text = "";
    public string Text => _text;
    public string? Submitted { get; private set; }
    public int MaxLength { get; set; } = 40;

    public void Update(InputState input)
    {
        Submitted = null;
        foreach (char c in input.Typed)
        {
            if (c == '\b')
            {
                if (_text.Length > 0) _text = _text[..^1];
            }
            else if (c == '\r' || c == '\n')
            {
                Submitted = _text.Trim();
                _text = "";
            }
            else if (!char.IsControl(c) && _text.Length < MaxLength)
            {
                _text += c;
            }
        }
        if (input.Hit(Keys.Escape)) _text = "";
    }

    public void Clear() => _text = "";

    public void Draw(TextScreen s, int x, int y, double time, byte fg = Vga.White, byte prompt = Vga.LightGreen)
    {
        int cx = s.Write(x, y, "> ", prompt);
        cx = s.Write(cx, y, _text, fg);
        // Blinking block caret, ~2 Hz, the way a DOS prompt behaved.
        if ((int)(time * 2) % 2 == 0) s.Put(cx, y, '█', fg);
    }
}

/// <summary>
/// The window. Holds the cell buffer, scales it up by whole pixels only, and pumps
/// one scene at a time. Nothing here knows anything about the game.
/// </summary>
public sealed class GopnikForm : Form, SceneHost
{
    private readonly TextScreen _screen;
    private readonly int[] _pixels = new int[TextScreen.PixW * TextScreen.PixH];
    private readonly Bitmap _frame = new(TextScreen.PixW, TextScreen.PixH, PixelFormat.Format32bppPArgb);
    private readonly InputState _input = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly System.Windows.Forms.Timer _timer = new();

    private IScene _scene;
    private IScene? _next;
    private double _last;
    private bool _fullscreen;
    private FormWindowState _savedState = FormWindowState.Normal;
    private FormBorderStyle _savedBorder = FormBorderStyle.Sizable;
    private Rectangle _savedBounds;

    public double Time => _clock.Elapsed.TotalSeconds;

    public GopnikForm(IScene start)
    {
        _screen = new TextScreen(VgaFont.Load());
        _scene = start;

        Text = "Гопник v2.0";
        BackColor = Color.Black;
        DoubleBuffered = true;
        KeyPreview = true;
        StartPosition = FormStartPosition.CenterScreen;
        // The renderer is pixel-exact, so WinForms must not rescale the client area behind
        // our back; sizing happens once the handle exists and we know the real monitor.
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(TextScreen.PixW * 2, TextScreen.PixH * 2);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                 | ControlStyles.OptimizedDoubleBuffer, true);

        _timer.Interval = 16;
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    private void Tick()
    {
        double now = Time;
        double dt = Math.Min(now - _last, 0.1);
        _last = now;

        _scene.Update(dt, _input, this);
        _input.Clear();

        if (_next is not null) { _scene = _next; _next = null; }
        Invalidate();
    }

    public void Go(IScene next) => _next = next;
    public void Quit() => Close();

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        _input.PushChar(e.KeyChar);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Alt && e.KeyCode == Keys.Enter) { ToggleFullscreen(); e.Handled = true; return; }
        _input.PushKey(e.KeyCode);
        // Stop the arrows and Tab from moving focus around instead of reaching the game.
        e.Handled = e.KeyCode is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Tab;
    }

    private void ToggleFullscreen()
    {
        _fullscreen = !_fullscreen;
        if (_fullscreen)
        {
            _savedState = WindowState;
            _savedBorder = FormBorderStyle;
            _savedBounds = Bounds;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = Screen.FromControl(this).Bounds;
        }
        else
        {
            FormBorderStyle = _savedBorder;
            Bounds = _savedBounds;
            WindowState = _savedState;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        _screen.Clear();
        _scene.Draw(_screen, Time);
        _screen.Blit(_pixels);

        var rect = new Rectangle(0, 0, TextScreen.PixW, TextScreen.PixH);
        var bits = _frame.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        Marshal.Copy(_pixels, 0, bits.Scan0, _pixels.Length);
        _frame.UnlockBits(bits);

        // Whole-pixel scaling only: half-scaled text is the fastest way to ruin this look.
        int scale = Math.Max(1, Math.Min(ClientSize.Width / TextScreen.PixW,
                                         ClientSize.Height / TextScreen.PixH));
        int w = TextScreen.PixW * scale, h = TextScreen.PixH * scale;
        int ox = (ClientSize.Width - w) / 2, oy = (ClientSize.Height - h) / 2;

        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.SmoothingMode = SmoothingMode.None;
        if (ox > 0 || oy > 0) g.Clear(Color.Black);
        g.DrawImage(_frame, ox, oy, w, h);
    }

    /// <summary>
    /// Size the window once the handle exists, using the monitor it actually landed on.
    /// Doing this in the constructor picks the wrong screen and fights per-monitor DPI.
    /// </summary>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        var wa = Screen.FromControl(this).WorkingArea;
        int chromeW = Width - ClientSize.Width, chromeH = Height - ClientSize.Height;
        int s = Math.Min((wa.Width - chromeW) / TextScreen.PixW,
                         (wa.Height - chromeH) / TextScreen.PixH);
        s = Math.Clamp(s, 1, 6);
        ClientSize = new Size(TextScreen.PixW * s, TextScreen.PixH * s);
        Location = new Point(wa.X + (wa.Width - Width) / 2, wa.Y + (wa.Height - Height) / 2);
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }

    /// <summary>
    /// Snap the window to an exact multiple of the frame when the user lets go of the edge,
    /// so windowed mode never shows a letterbox - the picture always fills the client area.
    /// </summary>
    protected override void OnResizeEnd(EventArgs e)
    {
        base.OnResizeEnd(e);
        if (_fullscreen || WindowState != FormWindowState.Normal) return;
        int s = Math.Max(1, Math.Min(
            (int)Math.Round(ClientSize.Width / (double)TextScreen.PixW),
            (int)Math.Round(ClientSize.Height / (double)TextScreen.PixH)));
        var want = new Size(TextScreen.PixW * s, TextScreen.PixH * s);
        if (ClientSize != want) ClientSize = want;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _timer.Dispose(); _frame.Dispose(); }
        base.Dispose(disposing);
    }
}
