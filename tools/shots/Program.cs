using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using Gopnik.Engine;
using Gopnik.Game;
using Gopnik.Scenes;

namespace Gopnik.Shots;

/// <summary>
/// Offscreen test harness. It drives the real scenes in memory and writes the cell buffer
/// straight to PNG - no window, no focus, no synthetic keystrokes. Nothing here can touch a
/// copy of the game somebody is actually playing.
/// </summary>
public static class Program
{
    private sealed class Host : SceneHost
    {
        public IScene Scene;
        private IScene? _next;
        public bool Quitted;
        public double Time { get; set; }

        public Host(IScene start) => Scene = start;
        public void Go(IScene next) => _next = next;
        public void Quit() => Quitted = true;
        public void Commit() { if (_next is not null) { Scene = _next; _next = null; } }
    }

    private const double Dt = 1.0 / 60.0;

    private static Host _host = null!;
    private static InputState _input = null!;
    private static TextScreen _screen = null!;
    private static int[] _pixels = null!;
    private static string _outDir = "";
    private static int _shot;
    private static readonly StringBuilder _trace = new();

    public static int Main(string[] args)
    {
        // --classic renders the same scenes under the 2003 arithmetic, where the numbers on
        // screen get noticeably wider - a boss carries three digits of health there.
        Rules.Classic = args.Any(a => a.Equals("--classic", StringComparison.OrdinalIgnoreCase));

        // The scenario travels districts, which unlocks them for future runs and would put
        // a "which district" screen in front of the next one - the harness has to start
        // from the same blank slate every time or its scripted keys land on other scenes.
        try { File.Delete(Path.Combine(AppContext.BaseDirectory, "gopnik.progress.json")); }
        catch { }

        _outDir = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal))
            ?? Path.Combine(AppContext.BaseDirectory, "shots");
        Directory.CreateDirectory(_outDir);

        // The harness keeps its own save next to its own binary, so a run here can never
        // overwrite the save of the game you are playing.
        Console.WriteLine($"каталог снимков : {_outDir}");
        Console.WriteLine($"сейв стенда     : {AppContext.BaseDirectory}gopnik.sav.json");

        _screen = new TextScreen(VgaFont.Load());
        _pixels = new int[TextScreen.PixW * TextScreen.PixH];
        _input = new InputState();
        _host = new Host(new TitleScene());

        int seed = args.Length > 1 && int.TryParse(args[1], out var s) ? s : 20260730;
        Rules.Reseed(seed);
        Console.WriteLine($"зерно           : {seed}");
        Console.WriteLine();

        Scenario();

        File.WriteAllText(Path.Combine(_outDir, "trace.txt"), _trace.ToString());
        Console.WriteLine();
        Console.WriteLine($"снимков: {_shot}, трасса: {Path.Combine(_outDir, "trace.txt")}");
        return 0;
    }

    // ---------------------------------------------------------------- primitives
    /// <summary>Advances one frame with whatever input has been queued.</summary>
    private static void Frame()
    {
        _host.Time += Dt;
        _host.Scene.Update(Dt, _input, _host);
        _input.Clear();
        _host.Commit();
    }

    private static void Frames(int n) { for (int i = 0; i < n; i++) Frame(); }

    /// <summary>Types a character, then lets a frame run.</summary>
    private static void Key(char c) { _input.PushChar(c); Frame(); }

    /// <summary>Types a whole command and presses enter.</summary>
    private static void Line(string text)
    {
        foreach (char c in text) _input.PushChar(c);
        _input.PushChar('\r');
        Frame();
        _trace.AppendLine($"> {text}");
    }

    private static void Snap(string name)
    {
        _screen.Clear();
        _host.Scene.Draw(_screen, _host.Time);
        _screen.Blit(_pixels);

        using var frame = new Bitmap(TextScreen.PixW, TextScreen.PixH, PixelFormat.Format32bppPArgb);
        var rect = new Rectangle(0, 0, TextScreen.PixW, TextScreen.PixH);
        var bits = frame.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        Marshal.Copy(_pixels, 0, bits.Scan0, _pixels.Length);
        frame.UnlockBits(bits);

        const int scale = 2;
        using var big = new Bitmap(TextScreen.PixW * scale, TextScreen.PixH * scale);
        using (var g = Graphics.FromImage(big))
        {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(frame, 0, 0, big.Width, big.Height);
        }

        string file = Path.Combine(_outDir, $"{++_shot:00}_{name}.png");
        big.Save(file, ImageFormat.Png);
        Console.WriteLine($"  {Path.GetFileName(file)}   [{_host.Scene.GetType().Name}]");
        _trace.AppendLine($"--- снимок {_shot:00} {name} [{_host.Scene.GetType().Name}] ---");
    }

    private static bool In<T>() => _host.Scene is T;

    // ---------------------------------------------------------------- scenario
    private static void Scenario()
    {
        Frames(30);
        Snap("title");

        Key('1');                                   // новая игра -> интро
        Frames(2);
        Snap("intro");

        Key(' ');                                   // -> выбор класса
        Frames(2);
        Snap("class");

        Key('2');                                   // Гопник
        foreach (char c in "11223344") Key(c);      // 8 очков
        Frames(2);
        Snap("skills");

        Key('\r');                                  // -> имя
        Frames(2);
        Line("Вован");                              // -> улица
        Frames(2);
        Snap("street");

        // Побродить, пока не встретится противник.
        for (int i = 0; i < 40 && !HasFoe(); i++) Line("w");
        Snap("encounter");

        if (HasFoe())
        {
            Line("sv");                             // оценить противника
            Snap("inspect");

            Line("k");                              // в драку
            Frames(2);
            Snap("combat_start");

            for (int i = 0; i < 40 && In<CombatScene>(); i++)
            {
                Line("k");
                if (i == 3) Snap("combat_mid");
            }
            Snap("combat_end");
            if (In<CombatScene>()) Line("");        // закрыть итоги
        }

        Line("s");
        Snap("stats");
        Key(' ');

        Places();

        Line("i");
        Frames(2);
        Snap("help");
        Key(' ');

        // Долгий прогон: сотня ходов с драками, чтобы посмотреть поздний экран.
        for (int i = 0; i < 220; i++)
        {
            if (In<CombatScene>()) { Line("k"); continue; }
            if (In<StreetScene>())
            {
                if (HasFoe()) Line("k");
                else Line("w");
                continue;
            }
            Line("w");                              // из любой локации - наружу
        }
        Snap("late");

        Finale();
    }

    /// <summary>
    /// The indoor screens, drawn from a built character rather than a played one. Reaching
    /// them through the scenario now means grinding reputation past the priton's door, and
    /// a harness that only ever presses "kick" dies on the way - which cost four screens of
    /// coverage. Built state also lets the fence actually have something to buy.
    /// </summary>
    private static void Places()
    {
        var w = new World();
        var p = w.P;
        p.Klass = Klass.Gopnik;
        p.Level = 8;
        p.Str = 8; p.Agi = 6; p.Vit = 7; p.Luck = 5;
        p.Rep = 45; p.Money = 320; p.Junk = 3; p.Beer = 1.5; p.Joints = 2;
        p.Weapon = 2; p.BootsIdx = 2; p.SuitIdx = 2; p.JacketIdx = 1;

        // Owns the rungs below what he is wearing, so "продать ненужные вещи" has stock.
        p.Own(ref p.WeaponsOwned, 1); p.Own(ref p.WeaponsOwned, 2);
        p.Own(ref p.BootsOwned, 1); p.Own(ref p.BootsOwned, 2);
        p.Own(ref p.SuitsOwned, 1); p.Own(ref p.SuitsOwned, 2);
        p.Own(ref p.JacketsOwned, 1);

        p.GirlKnown = true;
        foreach (var pl in Data.Places) p.Discover(pl.P);
        p.Hp = p.MaxHp;

        var tour = new[]
        {
            (Place.Priton, "priton"), (Place.Trenaj, "trenaj"),
            (Place.Barygi, "barygi"), (Place.Bazar, "bazar"),
        };
        foreach (var (place, name) in tour)
        {
            _host = new Host(new LocationScene(w, place));
            Frames(2);
            Snap(name);
            if (place == Place.Barygi) { Line("wes"); Snap("barygi_sold"); }
        }

        Line("w");                                  // обратно на улицу, дальше сценарий
        Frames(2);
    }

    /// <summary>
    /// Drives the two-stage ending directly. Wandering into it through the scenario would
    /// take a winning run, so the harness builds an end-game character and drops him into
    /// the office instead - the point is to see both halves of the joke render.
    /// </summary>
    private static void Finale()
    {
        var w = new World();
        var p = w.P;
        p.Klass = Klass.Gopnik;
        // Deliberately overwhelming: this probe checks that both halves of the ending
        // render, not whether the fight is winnable - the balance harness answers that.
        p.Level = 20;
        p.Str = 26; p.Agi = 20; p.Vit = 18; p.Luck = 14;
        p.Weapon = 4; p.BootsIdx = 2; p.SuitIdx = 2; p.JacketIdx = 2;
        p.Press = p.PressCap;
        p.Rep = 100;
        p.Hp = p.MaxHp;
        p.DistrictIdx = Data.Districts.Length - 1;

        var stand = Rules.MakeFoe(p, Data.Foes.Length - 1, 14);
        w.Foe = stand;
        _host = new Host(new CombatScene(w, stand));
        Frames(2);

        for (int i = 0; i < 400 && !w.ProrectorDown && In<CombatScene>(); i++)
        {
            Line("k");
            Frames(1);
        }
        Snap("finale_twist");

        Line("");                                   // -> настоящий ректор
        Frames(2);
        Snap("finale_real");

        for (int i = 0; i < 400 && In<CombatScene>() && !w.Won; i++)
        {
            Line("k");
            Frames(1);
        }
        Snap("finale_settled");

        Line("");
        Frames(30);
        Snap("finale_end");
    }

    /// <summary>
    /// The street scene owns the current encounter; the harness has no back door into it,
    /// so it reads the hint bar the same way a player reads the screen.
    /// </summary>
    private static bool HasFoe()
    {
        if (!In<StreetScene>()) return false;
        _screen.Clear();
        _host.Scene.Draw(_screen, _host.Time);
        // The bar takes one or two rows depending on the scene, so read both of them
        // rather than pinning the harness to a layout that is allowed to change.
        return ScreenText(22).Contains("наехать") || ScreenText(23).Contains("наехать");
    }

    private static string ScreenText(int row)
    {
        var sb = new StringBuilder();
        for (int x = 0; x < TextScreen.Cols; x++) sb.Append(_screen.CharAt(x, row));
        return sb.ToString();
    }
}
