using System.Reflection;

namespace Gopnik.Engine;

/// <summary>
/// Unicode &lt;-&gt; CP866 mapping. The game draws only glyphs that really exist in the
/// code page, so the whole screen could be dumped into a real DOS text mode unchanged.
/// </summary>
public static class Cp866
{
    // Unicode for byte values 0x80..0xFF, in order.
    private const string High =
        "АБВГДЕЖЗИЙКЛМНОП" +
        "РСТУФХЦЧШЩЪЫЬЭЮЯ" +
        "абвгдежзийклмноп" +
        "░▒▓│┤╡╢╖╕╣║╗╝╜╛┐" +
        "└┴┬├─┼╞╟╚╔╩╦╠═╬╧" +
        "╨╤╥╙╘╒╓╫╪┘┌█▄▌▐▀" +
        "рстуфхцчшщъыьэюя" +
        "ЁёЄєЇїЎў°∙·√№¤■\u00A0";

    // Declaration order matters: Build() fills ToUnicode, so it must exist first.
    public static readonly char[] ToUnicode = new char[256];
    private static readonly Dictionary<char, byte> Map = Build();

    private static Dictionary<char, byte> Build()
    {
        var m = new Dictionary<char, byte>(256);
        for (int i = 0; i < 128; i++)
        {
            ToUnicode[i] = (char)i;
            m[(char)i] = (byte)i;
        }
        for (int i = 0; i < 128; i++)
        {
            char u = High[i];
            byte b = (byte)(0x80 + i);
            ToUnicode[b] = u;
            if (!m.ContainsKey(u)) m[u] = b;
        }
        // Convenience aliases for characters that are easy to type but absent from CP866.
        m['ё'] = 0xF1; m['Ё'] = 0xF0;
        m['«'] = (byte)'"'; m['»'] = (byte)'"';
        m['—'] = (byte)'-'; m['–'] = (byte)'-';
        m['…'] = (byte)'.';
        m['№'] = 0xFC;
        return m;
    }

    /// <summary>Encodes one character; anything unmappable becomes '?'.</summary>
    public static byte Encode(char c) => Map.TryGetValue(c, out var b) ? b : (byte)'?';
}

/// <summary>The 8x12 CP866 bitmap font lifted from Windows' own vga866.fon.</summary>
public sealed class VgaFont
{
    public const int CharW = 8;
    public const int CharH = 12;          // glyph rows actually stored in the font

    // The cell is two rows taller than the glyph. That single pixel of leading above and
    // below does two things: it gives the type room to breathe, and it makes the frame
    // 640x350 - the real EGA text resolution, which is near enough 16:9 that a fullscreen
    // window is almost entirely picture instead of letterbox.
    public const int CellH = 14;
    public const int GlyphTop = 1;

    private readonly byte[] _cells;    // 256 * CellH bytes, one byte per pixel row

    private VgaFont(byte[] cells) => _cells = cells;

    public static VgaFont Load() => new(Expand(FontSource.Load()));

    /// <summary>
    /// Grows every 12-row glyph into a 14-row cell.
    /// Letters simply gain blank leading. Box-drawing and block glyphs (0xB0..0xDF) must
    /// keep tiling across the seam, so their first and last rows are repeated into the
    /// padding instead - that is what keeps '│' unbroken and '▀' exactly half a cell.
    /// </summary>
    private static byte[] Expand(byte[] glyphs)
    {
        var cells = new byte[256 * CellH];
        for (int code = 0; code < 256; code++)
        {
            bool tiling = code >= 0xB0 && code <= 0xDF;
            int src = code * CharH, dst = code * CellH;
            cells[dst] = tiling ? glyphs[src] : (byte)0;
            for (int y = 0; y < CharH; y++) cells[dst + GlyphTop + y] = glyphs[src + y];
            cells[dst + CellH - 1] = tiling ? glyphs[src + CharH - 1] : (byte)0;
        }
        return cells;
    }

    public byte Row(byte code, int y) => _cells[code * CellH + y];
}

/// <summary>Standard 16-colour VGA palette, as 0xAARRGGBB.</summary>
public static class Vga
{
    public const byte Black = 0, Blue = 1, Green = 2, Cyan = 3, Red = 4, Magenta = 5,
                      Brown = 6, LightGray = 7, DarkGray = 8, LightBlue = 9, LightGreen = 10,
                      LightCyan = 11, LightRed = 12, LightMagenta = 13, Yellow = 14, White = 15;

    public static readonly int[] Palette =
    {
        unchecked((int)0xFF000000), unchecked((int)0xFF0000AA), unchecked((int)0xFF00AA00),
        unchecked((int)0xFF00AAAA), unchecked((int)0xFFAA0000), unchecked((int)0xFFAA00AA),
        unchecked((int)0xFFAA5500), unchecked((int)0xFFAAAAAA), unchecked((int)0xFF555555),
        unchecked((int)0xFF5555FF), unchecked((int)0xFF55FF55), unchecked((int)0xFF55FFFF),
        unchecked((int)0xFFFF5555), unchecked((int)0xFFFF55FF), unchecked((int)0xFFFFFF55),
        unchecked((int)0xFFFFFFFF),
    };
}

/// <summary>
/// An 80x25 character cell buffer with VGA attributes - the only surface the game draws on.
/// Everything above it is composition; everything below it is one blit.
/// </summary>
public sealed class TextScreen
{
    public const int Cols = 80;
    public const int Rows = 25;
    public const int PixW = Cols * VgaFont.CharW;   // 640
    public const int PixH = Rows * VgaFont.CellH;   // 350 - EGA text mode, near enough 16:9

    private readonly byte[] _ch = new byte[Cols * Rows];
    private readonly byte[] _at = new byte[Cols * Rows];
    private readonly VgaFont _font;

    public TextScreen(VgaFont font)
    {
        _font = font;
        Clear();
    }

    public static byte Attr(byte fg, byte bg = Vga.Black) => (byte)((bg << 4) | (fg & 0x0F));

    public void Clear(byte bg = Vga.Black)
    {
        Array.Fill(_ch, (byte)' ');
        Array.Fill(_at, Attr(Vga.LightGray, bg));
    }

    public void Put(int x, int y, byte code, byte fg, byte bg = Vga.Black)
    {
        if ((uint)x >= Cols || (uint)y >= Rows) return;
        int i = y * Cols + x;
        _ch[i] = code;
        _at[i] = Attr(fg, bg);
    }

    public void Put(int x, int y, char c, byte fg, byte bg = Vga.Black)
        => Put(x, y, Cp866.Encode(c), fg, bg);

    /// <summary>Reads a cell back. The offscreen harness uses this to assert on what is drawn.</summary>
    public char CharAt(int x, int y)
        => (uint)x < Cols && (uint)y < Rows ? Cp866.ToUnicode[_ch[y * Cols + x]] : ' ';

    /// <summary>Writes plain text; returns the column just past the last character.</summary>
    public int Write(int x, int y, string s, byte fg, byte bg = Vga.Black)
    {
        foreach (char c in s)
        {
            if (x >= Cols) break;
            Put(x++, y, c, fg, bg);
        }
        return x;
    }

    /// <summary>
    /// The original's markup used ^0..^6 as *semantic* colours, not VGA indices. Taken
    /// literally they land in the dark half of the palette and the prose becomes unreadable,
    /// so codes 0-7 are mapped to their bright counterparts. Codes 8-F stay raw VGA, which
    /// is what the interface chrome uses.
    /// </summary>
    private static readonly byte[] TextPalette =
    {
        Vga.LightGray,     // ^0 plain
        Vga.LightCyan,     // ^1 loot, blessings, good news
        Vga.LightGreen,    // ^2 your successes
        Vga.Cyan,          // ^3 structure
        Vga.LightRed,      // ^4 damage, danger, the other guy
        Vga.LightMagenta,  // ^5 the girl
        Vga.Yellow,        // ^6 attention
        Vga.White,         // ^7 emphasis
    };

    /// <summary>
    /// Writes text with the original game's own inline colour markup: '^' followed by a
    /// hex digit switches the foreground. '^^' emits a literal caret.
    /// </summary>
    public int Markup(int x, int y, string s, byte fg = Vga.LightGray, byte bg = Vga.Black)
    {
        byte cur = fg;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '^' && i + 1 < s.Length)
            {
                char n = s[i + 1];
                if (n == '^') { Put(x++, y, '^', cur, bg); i++; continue; }
                int v = HexVal(n);
                if (v >= 0) { cur = v < 8 ? TextPalette[v] : (byte)v; i++; continue; }
            }
            if (x >= Cols) break;
            Put(x++, y, c, cur, bg);
        }
        return x;
    }

    /// <summary>Visible length of a markup string, ignoring colour codes.</summary>
    public static int VisibleLength(string s)
    {
        int n = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '^' && i + 1 < s.Length)
            {
                if (s[i + 1] == '^') { n++; i++; continue; }
                if (HexVal(s[i + 1]) >= 0) { i++; continue; }
            }
            n++;
        }
        return n;
    }

    private static int HexVal(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'A' and <= 'F' => c - 'A' + 10,
        >= 'a' and <= 'f' => c - 'a' + 10,
        _ => -1,
    };

    public void Fill(int x, int y, int w, int h, char c, byte fg, byte bg = Vga.Black)
    {
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
                Put(x + i, y + j, c, fg, bg);
    }

    public void HLine(int x, int y, int w, byte fg, bool dbl = false, byte bg = Vga.Black)
    {
        char c = dbl ? '═' : '─';
        for (int i = 0; i < w; i++) Put(x + i, y, c, fg, bg);
    }

    /// <summary>Single- or double-ruled frame; an optional title is inlaid into the top edge.</summary>
    public void Box(int x, int y, int w, int h, byte fg, bool dbl = false,
                    string? title = null, byte titleFg = Vga.White, byte bg = Vga.Black)
    {
        char tl = dbl ? '╔' : '┌', tr = dbl ? '╗' : '┐';
        char bl = dbl ? '╚' : '└', br = dbl ? '╝' : '┘';
        char hz = dbl ? '═' : '─', vt = dbl ? '║' : '│';

        Put(x, y, tl, fg, bg); Put(x + w - 1, y, tr, fg, bg);
        Put(x, y + h - 1, bl, fg, bg); Put(x + w - 1, y + h - 1, br, fg, bg);
        for (int i = 1; i < w - 1; i++) { Put(x + i, y, hz, fg, bg); Put(x + i, y + h - 1, hz, fg, bg); }
        for (int j = 1; j < h - 1; j++) { Put(x, y + j, vt, fg, bg); Put(x + w - 1, y + j, vt, fg, bg); }

        if (!string.IsNullOrEmpty(title))
        {
            int tx = x + 2;
            Put(tx - 1, y, ' ', fg, bg);
            int end = Write(tx, y, title, titleFg, bg);
            Put(end, y, ' ', fg, bg);
        }
    }

    /// <summary>A horizontal rule that ties into an existing frame's side walls.</summary>
    public void Tee(int x, int y, int w, byte fg, bool dbl = false, byte bg = Vga.Black)
    {
        Put(x, y, dbl ? '╠' : '├', fg, bg);
        Put(x + w - 1, y, dbl ? '╣' : '┤', fg, bg);
        for (int i = 1; i < w - 1; i++) Put(x + i, y, dbl ? '═' : '─', fg, bg);
    }

    /// <summary>Filled/empty bar built from block glyphs - the game's only gauge.</summary>
    public void Bar(int x, int y, int w, double frac, byte on, byte off = Vga.DarkGray, byte bg = Vga.Black)
    {
        frac = Math.Clamp(frac, 0, 1);
        int f = (int)Math.Round(frac * w);
        if (f == 0 && frac > 0) f = 1;
        if (f == w && frac < 1) f = w - 1;
        for (int i = 0; i < w; i++) Put(x + i, y, i < f ? '█' : '░', i < f ? on : off, bg);
    }

    /// <summary>Renders the cell buffer into a 32-bit ARGB pixel buffer of PixW x PixH.</summary>
    public void Blit(int[] dst)
    {
        for (int cy = 0; cy < Rows; cy++)
        {
            for (int cx = 0; cx < Cols; cx++)
            {
                int i = cy * Cols + cx;
                byte code = _ch[i], at = _at[i];
                int fg = Vga.Palette[at & 0x0F];
                int bg = Vga.Palette[(at >> 4) & 0x0F];
                int px0 = cx * VgaFont.CharW;
                int py0 = cy * VgaFont.CellH;
                for (int gy = 0; gy < VgaFont.CellH; gy++)
                {
                    byte bits = _font.Row(code, gy);
                    int row = (py0 + gy) * PixW + px0;
                    for (int gx = 0; gx < VgaFont.CharW; gx++)
                        dst[row + gx] = ((bits >> (7 - gx)) & 1) != 0 ? fg : bg;
                }
            }
        }
    }
}
