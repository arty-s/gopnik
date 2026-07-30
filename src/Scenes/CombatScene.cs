using Gopnik.Engine;
using Gopnik.Game;

namespace Gopnik.Scenes;

/// <summary>
/// A fight. Both sides show the same five numbers so the trade can be judged before it is
/// made - the one thing the 2003 version never let you see.
/// </summary>
public sealed class CombatScene : IScene
{
    private readonly World _w;
    private readonly Fight _f;
    private readonly CommandLine _cmd = new();
    private bool _settled;
    private bool _fatal;

    public CombatScene(World w, Foe foe)
    {
        _w = w;
        _f = new Fight(w.P, foe);
    }

    public void Update(double dt, InputState input, SceneHost host)
    {
        _cmd.Update(input);
        string? c = _cmd.Submitted?.ToLowerInvariant();

        if (_f.End != FightEnd.None)
        {
            if (!_settled) Settle();
            if (c is not null)
            {
                _w.Foe = null;
                if (_w.Won) { host.Go(new EndScene(_w, victory: true)); return; }
                Save.Write(_w);
                host.Go(_fatal ? new EndScene(_w, victory: false) : new StreetScene(_w));
            }
            return;
        }

        if (c is null) return;
        switch (c)
        {
            case "": break;
            case "k": _f.Attack(); break;
            case "f": _f.Shoot(); break;
            case "kos": _f.SmokeJoint(); break;
            case "h": _f.DrinkBeer(); break;
            case "v": _f.CallBackup(); break;
            case "q":
                if (_f.TryFlee()) { _w.Foe = null; host.Go(new StreetScene(_w)); return; }
                break;
            case "sv":
                _f.Say($"^8{_f.F.Name}: здоровье {_f.F.Hp}/{_f.F.MaxHp} ∙ урон {_f.F.DamageMin}-{_f.F.DamageMax} ∙ броня {_f.F.Armour}");
                break;
            default:
                _f.Say("^6Не понял. ^Fk^6 пнуть, ^Fq^6 свалить.");
                break;
        }
    }

    private void Settle()
    {
        _settled = true;
        if (_f.End == FightEnd.Won)
        {
            bool wasRector = _f.F.Index == Data.Foes.Length - 1;
            foreach (var line in _f.Spoils()) _f.Say(line);
            if (wasRector) { _w.Won = true; _fatal = false; }
            _f.Say("");
            _f.Say(wasRector ? "^E- жми ввод -" : "^E- жми ввод, идём дальше -");
        }
        else if (_f.End == FightEnd.Lost)
        {
            _fatal = _w.Die();
            foreach (var line in _w.Log.TakeLast(2)) _f.Say(line);
            _f.Say("");
            _f.Say("^E- жми ввод -");
        }
    }

    public void Update() { }

    public void Draw(TextScreen s, double t)
    {
        Hud.Status(s, _w.P);
        var p = _w.P;
        var f = _f.F;

        // --- opponent ------------------------------------------------------------
        int x = s.Write(1, 5, f.Name.ToUpperInvariant(), Vga.LightRed);
        x = s.Write(x, 5, $" ур.{f.Level}", Vga.LightGray);
        Hud.WriteRight(s, 78, 5, $"раунд {_f.Round}", Vga.DarkGray);

        s.Bar(1, 6, 24, f.MaxHp == 0 ? 0 : Math.Max(0, f.Hp) / (double)f.MaxHp, Vga.LightRed);
        s.Write(26, 6, $"{Math.Max(0, f.Hp)}/{f.MaxHp}", Vga.White);
        s.Write(36, 6, $"урон {f.DamageMin}-{f.DamageMax} ∙ броня {f.Armour} ∙ точность {f.Accuracy}%", Vga.LightGray);

        int sx = 1;
        sx = State(s, sx, 7, "ЧЕЛЮСТЬ", f.JawBroken);
        sx = State(s, sx, 7, "НОГА", f.LegBroken);
        if (_f.BackupHere) s.Write(sx, 7, " БРАТВА ЗДЕСЬ ", Vga.Black, Vga.LightGreen);

        s.HLine(0, 8, 80, Vga.Blue);

        // --- blow-by-blow --------------------------------------------------------
        Hud.DrawLog(s, _f.Log, 9, 19);

        // --- the crowd -----------------------------------------------------------
        s.HLine(0, 20, 80, Vga.Blue);
        int cx = s.Write(1, 21, "Зрители: ", Vga.Cyan);
        s.Write(cx, 21, _f.Spectator, Vga.LightGray);

        Hud.Hints(s, BuildHints());
        _cmd.Draw(s, 1, 24, t);
    }

    private static int State(TextScreen s, int x, int y, string label, bool broken)
    {
        if (!broken) return x;
        return s.Write(x, y, $" {label} СЛОМАНА ", Vga.White, Vga.Red) + 1;
    }

    private string BuildHints()
    {
        var p = _w.P;
        if (_f.End != FightEnd.None) return "^Fввод^8 - дальше";

        var parts = new List<string> { $"^Fk^8 пнуть ^A{_f.MyHitChance}%" };
        if (p.Pistol && p.Ammo > 0) parts.Add($"^Ff^8 стрелять ({p.Ammo})");
        if (p.Joints > 0) parts.Add($"^Fkos^8 косяк ({p.Joints})");
        if (p.Beer >= 0.5) parts.Add($"^Fh^8 пиво ({p.Beer:0.0}л)");
        if (_f.CanCallBackup) parts.Add("^Fv^8 братва");
        parts.Add($"^Fq^8 свалить ^E{_f.MyFleeChance}%");
        return string.Join("^8 ∙ ", parts);
    }
}
