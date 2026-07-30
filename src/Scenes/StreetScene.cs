using Gopnik.Engine;
using Gopnik.Game;

namespace Gopnik.Scenes;

/// <summary>The main loop: wander, meet somebody, decide what to do about it.</summary>
public sealed class StreetScene : IScene
{
    private readonly World _w;
    private readonly CommandLine _cmd = new();

    public StreetScene(World w) => _w = w;

    public void Update(double dt, InputState input, SceneHost host)
    {
        _cmd.Update(input);
        string? c = _cmd.Submitted?.ToLowerInvariant();
        if (c is null) return;
        var p = _w.P;

        // A forced encounter pins you in place: you may look, drink or smoke, but the only
        // way out of the street is through him.
        if (_w.FoeForced && _w.Foe is not null &&
            (c is "w" or "go" || Data.Places.Any(pl => pl.Cmd == c)))
        {
            _w.Blank();
            _w.Say("^4Поздно рыпаться - он уже прёт на тебя. ^Ek^4 - в драку.");
            return;
        }

        switch (c)
        {
            case "": return;

            case "w": _w.Wander(); Save.Write(_w); return;

            case "k":
                if (_w.Foe is null) { _w.Blank(); _w.Say("^6Чё машешь копытами? Ищи мудака которого будешь пинать!"); return; }
                host.Go(new CombatScene(_w, _w.Foe));
                return;

            case "sv":
                if (_w.Foe is null) { _w.Say("^6Не на кого пялиться."); return; }
                Inspect();
                return;

            case "go": _w.Travel(); Save.Write(_w); return;

            case "kos": Joint(); Save.Write(_w); return;
            case "h": Beer(); Save.Write(_w); return;

            case "s": host.Go(new StatsScene(_w)); return;
            case "i":
            case "help": host.Go(new HelpScene(_w)); return;

            case "e": host.Go(new TitleScene()); return;

            default:
                var place = Data.Places.FirstOrDefault(pl => pl.Cmd == c);
                if (place.Cmd is not null)
                {
                    _w.Blank();
                    if (place.P == Place.Girl && !p.GirlKnown) { _w.Say("^4У тебя пока нет девчонки."); return; }
                    if (!p.Knows(place.P)) { _w.Say($"^6Ты ещё не знаешь, где в этом районе {place.Name}."); return; }
                    host.Go(new LocationScene(_w, place.P));
                    return;
                }
                _w.Say($"^6Не понял: \"{c}\". Введи ^Ei^6 - там все команды.");
                return;
        }
    }

    private void Inspect()
    {
        var f = _w.Foe!;
        _w.Blank();
        _w.Say($"^7{f.Name}, {f.Level} уровня.");
        _w.Say($"^8здоровье {f.MaxHp} ∙ урон {f.DamageMin}-{f.DamageMax} ∙ броня {f.Armour} ∙ точность {f.Accuracy}%");
        int mine = Rules.HitChance(_w.P.Accuracy, _w.P.EAgi, f.Agi);
        int his = Rules.HitChance(f.Accuracy, f.Agi, _w.P.EAgi);
        _w.Say($"^Fтвой шанс попасть ^A{mine}%^F ∙ его по тебе ^C{his}%^F ∙ свалить ^E{Rules.FleeChance(_w.P.LegBroken, _w.P.EAgi, f.Agi)}%");
    }

    private void Joint()
    {
        var p = _w.P;
        _w.Blank();
        if (p.JawBroken) { _w.Say("^4Ты не можешь схавать колёса из-за сломаной челюсти."); return; }
        if (p.Joints <= 0) { _w.Say("^4У тебя нет косяков"); return; }
        if (p.Stoned > 0) { _w.Say("^6Ты неможешь схавать ещё один косяк."); return; }
        p.Joints--;
        int heal = Rules.Roll(6, 14);
        p.Heal(heal);
        p.Stoned = Rules.Roll(4, 7);
        p.Str += 2;
        _w.Say($"^2Колёса прибавляют {heal}з. Здоровья:{p.Hp}/{p.MaxHp}. Осталось {p.Joints} косяков");
        _w.Say("^2Сила +2.");
    }

    private void Beer()
    {
        var p = _w.P;
        _w.Blank();
        if (p.JawBroken) { _w.Say("^4Ты не можешь пить пиво из-за сломаной челюсти."); return; }
        if (p.Beer < 0.5) { _w.Say("^4Пива нет"); return; }
        if (p.Hp >= p.MaxHp) { _w.Say("^6Блин только тупить не надо - и так здоровья до фига."); return; }
        p.Beer -= 0.5;
        int heal = Rules.Roll(3, 7);
        p.Heal(heal);
        _w.Say($"^2Пиво прибавляет {heal}з. Здоровья:{p.Hp}/{p.MaxHp}. Осталось {p.Beer:0.0}л. пива");
    }

    public void Draw(TextScreen s, double t)
    {
        Hud.Status(s, _w.P);

        int bottom = Hud.Bottom;
        if (_w.Foe is not null) bottom = Hud.Bottom - 5;
        Hud.DrawLog(s, _w.Log, Hud.Top, bottom);

        if (_w.Foe is not null) DrawFoeCard(s, bottom + 1);

        Hud.Hints(s, BuildHints());
        _cmd.Draw(s, 1, 24, t);
    }

    private void DrawFoeCard(TextScreen s, int y)
    {
        var f = _w.Foe!;
        var p = _w.P;
        s.Box(2, y, 62, 4, Vga.Cyan, title: $"{f.Name} ∙ {f.Level} уровня");
        s.Write(4, y + 1, $"здоровье {f.MaxHp}  урон {f.DamageMin}-{f.DamageMax}  броня {f.Armour}", Vga.LightGray);

        int mine = Rules.HitChance(p.Accuracy, p.EAgi, f.Agi);
        int his = Rules.HitChance(f.Accuracy, f.Agi, p.EAgi);
        int flee = Rules.FleeChance(p.LegBroken, p.EAgi, f.Agi);
        int x = s.Write(4, y + 2, "твой шанс ", Vga.White);
        x = s.Write(x, y + 2, $"{mine}%", Vga.LightGreen);
        x = s.Write(x, y + 2, "   его по тебе ", Vga.White);
        x = s.Write(x, y + 2, $"{his}%", Vga.LightRed);
        x = s.Write(x, y + 2, "   свалить ", Vga.White);
        s.Write(x, y + 2, $"{flee}%", Vga.Yellow);

        s.Write(66, y + 1, "[k] наехать", Vga.White);
        if (_w.FoeForced) s.Write(66, y + 2, "не уйдёшь", Vga.LightRed);
        else s.Write(66, y + 2, "[w] мимо", Vga.LightGray);
    }

    private string BuildHints()
    {
        var p = _w.P;
        var parts = new List<string> { "^Fw^8 шататься" };
        if (_w.Foe is not null) parts.Add("^Fk^8 наехать");
        foreach (var pl in Data.Places)
            if (p.Knows(pl.P) && (pl.P != Place.Girl || p.GirlKnown))
                parts.Add($"^F{pl.Cmd}^8 {pl.Name}");
        if (_w.CanTravel) parts.Add("^Ego^8 дальше");
        parts.Add("^Fs^8 себя");
        parts.Add("^Fi^8 команды");
        return string.Join("^8 ∙ ", parts);
    }
}
