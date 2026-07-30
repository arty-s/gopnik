namespace Gopnik.Engine;

/// <summary>
/// Text-mode demo effects. Everything here works inside a character cell - no pixel access,
/// no alpha, sixteen colours. The tricks are the old ones: colour ramps standing in for
/// gradients, half-blocks doubling the vertical resolution, and motion by index shifting.
/// </summary>
public static class Retro
{
    // Ramps run dark -> bright. Index 0 is the edge of a bar, the last entry is its hot core.
    public static readonly byte[] SteelRamp =
        { Vga.Black, Vga.Blue, Vga.Blue, Vga.LightBlue, Vga.Cyan, Vga.LightCyan, Vga.White };

    public static readonly byte[] GoldRamp =
        { Vga.Black, Vga.Red, Vga.Brown, Vga.Brown, Vga.Yellow, Vga.Yellow, Vga.White };

    public static readonly byte[] BloodRamp =
        { Vga.Black, Vga.Red, Vga.Red, Vga.LightRed, Vga.Yellow, Vga.White };

    private static byte Pick(byte[] ramp, double d)
    {
        d = Math.Abs(d);
        if (d >= 1.0) return Vga.Black;
        int i = (int)Math.Round((1.0 - d) * (ramp.Length - 1));
        return ramp[Math.Clamp(i, 0, ramp.Length - 1)];
    }

    /// <summary>
    /// A copper bar: a horizontal band lit from its own centre line. Each text row carries
    /// two gradient steps by drawing '▀' with different foreground and background colours,
    /// which is how you get a smooth band out of twenty-five rows and sixteen colours.
    /// </summary>
    public static void CopperBar(TextScreen s, double centerY, double halfHeight, double t,
                                 byte[] ramp, double waveAmp = 0.0, double waveFreq = 0.16,
                                 double waveSpeed = 2.0, int x = 0, int w = TextScreen.Cols)
    {
        double span = halfHeight + Math.Abs(waveAmp) + 1;
        int y0 = (int)Math.Floor(centerY - span);
        int y1 = (int)Math.Ceiling(centerY + span);
        for (int i = 0; i < w; i++)
        {
            // The whole ribbon rides one smooth sine. Perturbing the colour index per cell
            // instead would just look like noise - the motion has to be in the geometry.
            double c = centerY + waveAmp * Math.Sin(t * waveSpeed + i * waveFreq);
            for (int y = y0; y <= y1; y++)
            {
                if ((uint)y >= TextScreen.Rows) continue;
                byte fg = Pick(ramp, (y - c) / halfHeight);
                byte bg = Pick(ramp, (y + 0.5 - c) / halfHeight);
                if (fg == Vga.Black && bg == Vga.Black) continue;
                s.Put(x + i, y, '▀', fg, bg);
            }
        }
    }

    /// <summary>
    /// Character-cell plasma. Four cheap sine fields summed, then quantised twice:
    /// once into a density glyph and once into a colour, which is what gives the
    /// old demos their banded, dithered look.
    /// </summary>
    public static void Plasma(TextScreen s, int x, int y, int w, int h, double t, byte[] ramp)
    {
        const string Density = " ░▒▓█";
        for (int j = 0; j < h; j++)
        {
            for (int i = 0; i < w; i++)
            {
                double v = Math.Sin(i * 0.30 + t * 1.7)
                         + Math.Sin(j * 0.45 - t * 1.1)
                         + Math.Sin((i + j) * 0.22 + t * 0.9)
                         + Math.Sin(Math.Sqrt(i * i * 0.6 + j * j * 2.2) * 0.35 - t * 1.5);
                double n = (v + 4.0) / 8.0;
                int di = (int)Math.Clamp(n * (Density.Length - 1), 0, Density.Length - 1);
                int ci = (int)Math.Clamp(n * (ramp.Length - 1), 0, ramp.Length - 1);
                s.Put(x + i, y + j, Density[di], ramp[ci]);
            }
        }
    }

    /// <summary>Draws multi-row block art with one colour per row - the "chrome text" look.</summary>
    public static void MetalText(TextScreen s, int x, int y, string[] rows, byte[] rowColors)
    {
        for (int j = 0; j < rows.Length; j++)
        {
            byte c = rowColors[Math.Min(j, rowColors.Length - 1)];
            for (int i = 0; i < rows[j].Length; i++)
                if (rows[j][i] != ' ')
                    s.Put(x + i, y + j, rows[j][i], c);
        }
    }

    /// <summary>The six-letter block logo, 8 cells wide per glyph.</summary>
    public static readonly string[] Logo =
    {
        "████████  ████████  ████████  ██    ██  ██    ██  ██   ███",
        "███       ██    ██  ██    ██  ██    ██  ██  ████  ██  ██  ",
        "███       ██    ██  ██    ██  ████████  ██ ██ ██  █████   ",
        "███       ██    ██  ██    ██  ██    ██  ████  ██  ██  ██  ",
        "███       ████████  ██    ██  ██    ██  ██    ██  ██   ███",
    };

    public static readonly byte[] LogoSheen =
        { Vga.White, Vga.LightCyan, Vga.Cyan, Vga.Cyan, Vga.Blue };
}

/// <summary>A bottom-of-screen message scroller, the way every trainer and cracktro had one.</summary>
public sealed class Scroller
{
    private readonly string _text;
    private readonly int _width;
    private double _pos;

    public double Speed { get; set; } = 14.0;   // cells per second

    public Scroller(string text, int width = TextScreen.Cols)
    {
        _text = new string(' ', width) + text + new string(' ', width);
        _width = width;
    }

    public void Update(double dt)
    {
        _pos += Speed * dt;
        if (_pos >= _text.Length - _width) _pos = 0;
    }

    public void Draw(TextScreen s, int y, byte fg, int x = 0)
    {
        int start = (int)_pos;
        for (int i = 0; i < _width && start + i < _text.Length; i++)
            s.Put(x + i, y, _text[start + i], fg);
    }
}

/// <summary>
/// Fade-to-black for a 16-colour palette. There is no blending, so each colour walks
/// down a hand-built ladder towards black - the same table trick the demos used.
/// </summary>
public static class Fader
{
    private static readonly byte[] Dim =
    {
        Vga.Black, Vga.Black, Vga.Black, Vga.Blue, Vga.Black, Vga.Black, Vga.Black, Vga.DarkGray,
        Vga.Black, Vga.Blue, Vga.Green, Vga.Cyan, Vga.Red, Vga.Magenta, Vga.Brown, Vga.LightGray,
    };

    public static byte Step(byte colour, int steps)
    {
        for (int i = 0; i < steps; i++) colour = Dim[colour];
        return colour;
    }
}
