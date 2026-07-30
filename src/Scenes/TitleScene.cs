using Gopnik.Engine;
using Gopnik.Game;

namespace Gopnik.Scenes;

/// <summary>
/// The attract screen. Copper bars top and bottom, a chrome logo, a scroller -
/// the vocabulary of a 1990s trainer, because that is what this game deserves.
/// </summary>
public sealed class TitleScene : IScene
{
    private readonly Scroller _scroller = new(
        "ГОПНИК v2.0  ∙  реставрация оригинала V.P., 2003  ∙  " +
        "весь текст, все 43 звания и вся математика драки взяты из оригинального g.exe  ∙  " +
        "шрифт - настоящий VGA CP866  ∙  жми цифру и погнали  ∙∙∙   ");

    private readonly string[] _menu =
    {
        "1  Начать сначала",
        "2  Продолжить",
        "3  Как в это играть",
        "0  Свалить",
    };

    private int _hover;

    public void Update(double dt, InputState input, SceneHost host)
    {
        _scroller.Update(dt);

        if (input.Hit(Keys.Down)) _hover = (_hover + 1) % _menu.Length;
        if (input.Hit(Keys.Up)) _hover = (_hover + _menu.Length - 1) % _menu.Length;

        foreach (char c in input.Typed)
        {
            switch (c)
            {
                case '1': host.Go(new CreateScene()); return;
                case '2': Continue(host); return;
                case '3': host.Go(new HelpScene()); return;
                case '0': host.Quit(); return;
                case '\r':
                    switch (_hover)
                    {
                        case 1: Continue(host); return;
                        case 2: host.Go(new HelpScene()); return;
                        case 3: host.Quit(); return;
                        default: host.Go(new CreateScene()); return;
                    }
            }
        }
        if (input.Hit(Keys.Escape)) host.Quit();
    }

    private static void Continue(SceneHost host)
    {
        var w = Save.Read();
        host.Go(w is null ? new CreateScene() : new StreetScene(w));
    }

    public void Draw(TextScreen s, double t)
    {
        Retro.CopperBar(s, 1.5, 1.5, t, Retro.SteelRamp, waveAmp: 0.8, waveSpeed: 1.7);

        Retro.MetalText(s, 11, 5, Retro.Logo, Retro.LogoSheen);

        s.Write(37, 10, "версия 2.0  ∙  реставрация 2026", Vga.DarkGray);

        const int bx = 20, by = 12, bw = 40, bh = 6;
        s.Box(bx, by, bw, bh, Vga.Cyan, dbl: true);
        for (int i = 0; i < _menu.Length; i++)
        {
            bool on = i == _hover;
            byte fg = on ? Vga.White : Vga.LightGray;
            if (on) s.Fill(bx + 1, by + 1 + i, bw - 2, 1, ' ', Vga.White, Vga.Blue);
            s.Write(bx + 3, by + 1 + i, _menu[i], fg, on ? Vga.Blue : Vga.Black);
        }

        string save = Save.Exists ? "сейв: " + Save.Describe() : "сейва нет - начинай сначала";
        s.Write(Math.Max(1, (80 - save.Length) / 2), 19, save, Vga.DarkGray);

        Retro.CopperBar(s, 21.6, 1.4, t + 1.1, Retro.SteelRamp, waveAmp: 0.7, waveSpeed: -1.4);

        _scroller.Draw(s, 24, Vga.Yellow);
    }
}

/// <summary>Temporary landing pad while the rest of the game is being built.</summary>
public sealed class PlaceholderScene : IScene
{
    private readonly string _what;
    public PlaceholderScene(string what) => _what = what;

    public void Update(double dt, InputState input, SceneHost host)
    {
        if (input.AnyKey) host.Go(new TitleScene());
    }

    public void Draw(TextScreen s, double t)
    {
        Retro.Plasma(s, 0, 0, TextScreen.Cols, TextScreen.Rows, t, Retro.SteelRamp);
        int w = _what.Length + 8;
        int x = (TextScreen.Cols - w) / 2;
        s.Fill(x, 11, w, 3, ' ', Vga.White, Vga.Black);
        s.Box(x, 11, w, 3, Vga.LightCyan);
        s.Write(x + 4, 12, _what, Vga.White);
        s.Write(24, 16, "тут пока пусто - жми любую кнопку", Vga.LightGray);
    }
}
