namespace Gopnik.Engine;

/// <summary>
/// Reads the 8x12 CP866 glyph set out of Windows' own <c>vga866.fon</c>.
///
/// The file is a 16-bit NE executable carrying an FNT resource - the same bitmap font DOS
/// boxes used - and it ships with every Russian-capable Windows. Reading it at startup
/// rather than committing the extracted bitmaps keeps a proprietary font out of this
/// repository, and the glyphs are exactly the ones the original game was written for.
/// </summary>
public static class FontSource
{
    private static readonly string[] Candidates =
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", "vga866.fon"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", "ega80866.fon"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", "vgaoem.fon"),
    };

    public const int Width = 8;
    public const int Height = 12;

    /// <summary>256 glyphs, <see cref="Height"/> bytes each, one byte per pixel row.</summary>
    public static byte[] Load()
    {
        foreach (string path in Candidates)
        {
            if (!File.Exists(path)) continue;
            try
            {
                var glyphs = FromFon(File.ReadAllBytes(path));
                if (glyphs is not null) return glyphs;
            }
            catch (Exception)
            {
                // A malformed or unexpected face is not fatal - try the next candidate.
            }
        }

        throw new FileNotFoundException(
            "Не найден системный шрифт кодовой страницы 866 (ожидался " +
            string.Join(" или ", Candidates.Select(Path.GetFileName)) + " в папке шрифтов Windows). " +
            "Игра рисует текстовый режим настоящими глифами CP866 и без них работать не может.");
    }

    // ---- NE container --------------------------------------------------------------
    private static byte[]? FromFon(byte[] raw)
    {
        if (raw.Length < 0x40 || raw[0] != 'M' || raw[1] != 'Z') return null;
        int ne = BitConverter.ToInt32(raw, 0x3C);
        if (ne <= 0 || ne + 0x28 > raw.Length || raw[ne] != 'N' || raw[ne + 1] != 'E') return null;

        int table = ne + BitConverter.ToUInt16(raw, ne + 0x24);
        if (table + 2 > raw.Length) return null;
        int shift = BitConverter.ToUInt16(raw, table);

        int p = table + 2;
        while (p + 8 <= raw.Length)
        {
            int typeId = BitConverter.ToUInt16(raw, p);
            if (typeId == 0) break;
            int count = BitConverter.ToUInt16(raw, p + 2);
            p += 8;
            for (int i = 0; i < count && p + 12 <= raw.Length; i++, p += 12)
            {
                if (typeId != 0x8008) continue;                       // RT_FONT
                int at = BitConverter.ToUInt16(raw, p) << shift;
                var g = FromFnt(raw, at);
                if (g is not null) return g;
            }
        }
        return null;
    }

    // ---- FNT face ------------------------------------------------------------------
    private static byte[]? FromFnt(byte[] raw, int o)
    {
        if (o <= 0 || o + 0x80 > raw.Length) return null;

        int version = BitConverter.ToUInt16(raw, o);
        if (version != 0x0200 && version != 0x0300) return null;
        if (BitConverter.ToUInt16(raw, o + 0x56) != Width) return null;   // dfPixWidth
        if (BitConverter.ToUInt16(raw, o + 0x58) != Height) return null;  // dfPixHeight

        int first = raw[o + 0x5F], last = raw[o + 0x60];
        if (last < first) return null;

        // The glyph table follows the header; entries are width + offset, and the offset is
        // relative to the start of this FNT. Fixed-pitch faces still carry the table.
        int tbl = o + (version == 0x0200 ? 0x76 : 0x94);
        int entry = version == 0x0200 ? 4 : 6;

        var glyphs = new byte[256 * Height];
        for (int i = 0; i <= last - first; i++)
        {
            int e = tbl + i * entry;
            if (e + entry > raw.Length) break;

            int gw = BitConverter.ToUInt16(raw, e);
            int go = version == 0x0200
                ? BitConverter.ToUInt16(raw, e + 2)
                : (int)BitConverter.ToUInt32(raw, e + 2);

            int code = first + i;
            if (code > 255 || gw == 0) continue;

            int src = o + go;
            if (src + Height > raw.Length) continue;
            // Width is 8, so the glyph is simply Height consecutive bytes, top row first.
            Array.Copy(raw, src, glyphs, code * Height, Height);
        }

        // A face that decoded to nothing is a parse failure, not an empty font.
        return glyphs.Any(b => b != 0) ? glyphs : null;
    }
}
