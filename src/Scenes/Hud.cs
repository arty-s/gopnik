using Gopnik.Engine;
using Gopnik.Game;

namespace Gopnik.Scenes;

/// <summary>Shared chrome: the status strip and the text utilities every screen needs.</summary>
public static class Hud
{
    public const int Top = 5;        // first content row below the status strip
    public const int Bottom = 21;    // last content row above the command bar

    /// <summary>
    /// Word wrap that survives inline colour codes: the active colour is re-stated at the
    /// start of every continuation line, so a wrapped sentence keeps its colour.
    /// </summary>
    public static List<string> Wrap(string markup, int width)
    {
        var lines = new List<string>();
        string cur = "", pending = "";
        byte colour = 0xFF;                      // 0xFF = "no colour stated yet"
        int len = 0;

        void Flush()
        {
            if (cur.Length > 0 || len > 0) lines.Add(cur);
            cur = colour == 0xFF ? "" : "^" + "0123456789ABCDEF"[colour];
            len = 0;
        }

        for (int i = 0; i < markup.Length; i++)
        {
            char c = markup[i];
            if (c == '^' && i + 1 < markup.Length && Hex(markup[i + 1]) >= 0)
            {
                colour = (byte)Hex(markup[i + 1]);
                pending += markup.Substring(i, 2);
                i++;
                continue;
            }
            if (c == ' ')
            {
                cur += pending + " ";
                len += 1;
                pending = "";
                continue;
            }
            // Accumulate a word, breaking the line before it if it will not fit.
            int wordLen = 0;
            string word = "";
            int j = i;
            for (; j < markup.Length && markup[j] != ' '; j++)
            {
                if (markup[j] == '^' && j + 1 < markup.Length && Hex(markup[j + 1]) >= 0)
                {
                    colour = (byte)Hex(markup[j + 1]);
                    word += markup.Substring(j, 2);
                    j++;
                    continue;
                }
                word += markup[j];
                wordLen++;
            }
            i = j - 1;

            if (len + wordLen > width && len > 0)
            {
                cur = cur.TrimEnd();
                Flush();
                pending = "";
            }
            cur += pending + word;
            len += wordLen;
            pending = "";
        }
        if (cur.Length > 0) lines.Add(cur);
        if (lines.Count == 0) lines.Add("");
        return lines;
    }

    private static int Hex(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'A' and <= 'F' => c - 'A' + 10,
        >= 'a' and <= 'f' => c - 'a' + 10,
        _ => -1,
    };

    /// <summary>Right-aligned write.</summary>
    public static void WriteRight(TextScreen s, int rightCol, int y, string text, byte fg)
        => s.Write(rightCol - text.Length + 1, y, text, fg);

    /// <summary>The four-row status strip. Everything the player must never have to ask for.</summary>
    public static void Status(TextScreen s, Player p)
    {
        // Rows 0-3 carry the strip; row 4 is the rule that closes it off.
        int x = s.Write(1, 0, p.Name.ToUpperInvariant(), Vga.White);
        x = s.Write(x, 0, " ∙ ", Vga.DarkGray);
        x = s.Write(x, 0, Data.Classes[(int)p.Klass].Name, Vga.LightGray);
        x = s.Write(x, 0, $" ур.{p.Level}", Vga.LightGray);
        x = s.Write(x, 0, " ∙ ", Vga.DarkGray);
        x = s.Write(x, 0, "\"" + p.Rank + "\"", Vga.Yellow);
        string where = $"{p.District.Name}, район {p.DistrictIdx + 1} из {Data.Districts.Length}  {p.Money} руб.";
        WriteRight(s, 78, 0, where, Vga.LightGray);

        // Row 1 - the three gauges.
        s.Write(1, 1, "Здор", Vga.LightGray);
        s.Bar(6, 1, 10, p.MaxHp == 0 ? 0 : p.Hp / (double)p.MaxHp,
              p.Hp * 3 <= p.MaxHp ? Vga.LightRed : Vga.LightGreen);
        s.Write(17, 1, $"{p.Hp}/{p.MaxHp}", Vga.White);

        s.Write(27, 1, "Понты", Vga.LightGray);
        s.Bar(33, 1, 10, p.Rep / 126.0, Vga.Yellow);
        s.Write(44, 1, $"{p.Rep}", Vga.White);

        s.Write(50, 1, "Опыт", Vga.LightGray);
        s.Bar(55, 1, 10, Math.Clamp(p.Xp / (double)p.XpToLevel, 0, 1), Vga.LightCyan);
        s.Write(66, 1, $"{p.Xp}/{p.XpToLevel}", Vga.White);

        // Row 2 - stats, each shown with what it actually buys you.
        x = 1;
        x = Stat(s, x, 2, "Сила", p.EStr, $"урон {p.DamageMin}-{p.DamageMax}");
        x = Stat(s, x, 2, "Ловк", p.EAgi, $"точн {p.Accuracy}%");
        x = Stat(s, x, 2, "Жив", p.EVit, $"здор {p.MaxHp}");
        x = Stat(s, x, 2, "Удача", p.ELuck, $"крит {Rules.CritChance(p.ELuck)}%");
        Stat(s, x, 2, "Броня", p.Armour, $"-{p.Armour}");

        // Row 3 - what you are wearing and carrying, plus whatever is currently broken.
        x = s.Write(1, 3, Data.Weapons[p.Weapon].Name, Vga.LightGreen);
        if (p.BootsIdx > 0) { x = s.Write(x, 3, " ∙ ", Vga.DarkGray); x = s.Write(x, 3, Data.Boots[p.BootsIdx].Name, Vga.LightGreen); }
        if (p.SuitIdx > 0) { x = s.Write(x, 3, " ∙ ", Vga.DarkGray); x = s.Write(x, 3, Data.Suits[p.SuitIdx].Name, Vga.LightGreen); }
        if (p.JacketIdx > 0) { x = s.Write(x, 3, " ∙ ", Vga.DarkGray); x = s.Write(x, 3, Data.Jackets[p.JacketIdx].Name, Vga.LightGreen); }

        if (p.JawBroken || p.LegBroken || p.Stoned > 0)
        {
            int bx = Math.Max(x + 2, 34);
            if (p.JawBroken) bx = s.Write(bx, 3, " ЧЕЛЮСТЬ ", Vga.White, Vga.Red) + 1;
            if (p.LegBroken) bx = s.Write(bx, 3, " НОГА ", Vga.White, Vga.Red) + 1;
            if (p.Stoned > 0) s.Write(bx, 3, " ОБДОЛБАН ", Vga.Black, Vga.Brown);
        }

        string kit = $"пиво {p.Beer:0.0}л  косяки {p.Joints}  хлам {p.Junk}"
                   + (p.Pistol ? $"  патроны {p.Ammo}" : "");
        WriteRight(s, 78, 3, kit, Vga.LightGray);

        s.HLine(0, 4, 80, Vga.Cyan, dbl: true);
    }

    private static int Stat(TextScreen s, int x, int y, string name, int value, string effect)
    {
        x = s.Write(x, y, name, Vga.LightGray);
        x = s.Write(x, y, " ", Vga.Black);
        x = s.Write(x, y, value.ToString(), Vga.White);
        x = s.Write(x, y, ">", Vga.DarkGray);
        x = s.Write(x, y, effect, Vga.LightCyan);
        return x + 2;
    }

    /// <summary>Renders the tail of a message log into the content area.</summary>
    public static void DrawLog(TextScreen s, IReadOnlyList<string> log, int top, int bottom, int width = 78)
    {
        var lines = new List<string>();
        for (int i = Math.Max(0, log.Count - 40); i < log.Count; i++)
            lines.AddRange(Wrap(log[i], width));

        int rows = bottom - top + 1;
        int start = Math.Max(0, lines.Count - rows);
        for (int i = start; i < lines.Count; i++)
            s.Markup(1, top + (i - start), lines[i], Vga.LightGray);
    }

    /// <summary>Bottom command hint bar.</summary>
    public static void Hints(TextScreen s, string hints)
    {
        s.HLine(0, 22, 80, Vga.Cyan, dbl: true);
        s.Markup(1, 23, hints, Vga.DarkGray);
    }
}
