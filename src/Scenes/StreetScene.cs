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
            // No Blank() here on purpose: a blank line between two identical refusals
            // defeats the collapsing in World.Say, and somebody pinned by a forced
            // encounter presses this key a lot. One copy of the line is the whole message.
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

            case "da":
                _w.Blank();
                if (_w.BlavoPrice <= 0) { _w.Say("^6Кому ты дакаешь?"); return; }
                if (p.Money < _w.BlavoPrice) { _w.Say("^6Парень, все стоит бабок!"); return; }
                p.Money -= _w.BlavoPrice;
                _w.BlavoPrice = 0;
                Save.Write(_w);
                _w.Say("^0Сохранено! ^1Можешь беспредельничать дальше.");
                return;

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
                    if (place.P == Place.Bazar && p.BazarClosed)
                    {
                        _w.Say("^6На базар пока нельзя там менты бродят, тебя ищут.");
                        _w.Say("^7Жди звонка - скажут, когда свалят.");
                        return;
                    }
                    if (place.P == Place.Priton && p.Rep < Data.PritonRep)
                    {
                        _w.Say("^4Такого конявого непустят в местный притон!");
                        _w.Say("^6Сначала надо заработать понтовости - отпинай кого-нибудь.");
                        return;
                    }
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

        // The street is the one screen with somewhere to go, so its command bar takes two
        // rows - verbs on one, the district's places on the other - and pays a row of log
        // for it.
        int bottom = Hud.Bottom - 1;
        if (_w.Foe is not null) bottom -= 5;
        Hud.DrawLog(s, _w.Log, Hud.Top, bottom);

        if (_w.Foe is not null) DrawFoeCard(s, bottom + 1);

        Hud.Hints(s, BuildVerbs(), BuildPlaces());
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

    private string BuildVerbs()
    {
        var parts = new List<string> { "^Fw^8 шататься" };
        if (_w.Foe is not null) parts.Add("^Fk^8 наехать");
        if (_w.BlavoPrice > 0) parts.Add($"^Eda^8 сохраниться ({_w.BlavoPrice})");
        if (_w.CanTravel) parts.Add("^Ego^8 дальше");
        parts.Add("^Fs^8 себя");
        parts.Add("^Fi^8 команды");

        // How much of the district is still unwalked. It rides with the verbs rather than
        // with the stats: this is not something you spend, it is a reason to press w. It
        // leaves the screen the moment the district holds nothing new - a counter sitting
        // at its own maximum is just noise.
        string found = Counter();
        if (found.Length > 0) parts.Add(found);

        return Hud.JoinFitting(parts, 77);
    }

    /// <summary>
    /// Only what walking can turn up. The girl is left out on purpose - she is not a place
    /// you find, she is somebody you have to meet first, and counting her would leave the
    /// tally stuck one short with nothing the player could do about it.
    /// </summary>
    private string Counter()
    {
        var p = _w.P;
        var walkable = Data.Places.Where(pl => pl.P != Place.Girl).ToList();
        int known = walkable.Count(pl => p.Knows(pl.P));
        return known >= walkable.Count ? "" : $"^8места {known}/{walkable.Count}";
    }

    private string BuildPlaces()
    {
        var p = _w.P;
        var parts = Data.Places
            .Where(pl => p.Knows(pl.P) && (pl.P != Place.Girl || p.GirlKnown))
            .Select(pl => $"^F{pl.Cmd}^8 {pl.Name}")
            .ToList();

        if (parts.Count == 0) return "^8ты тут ещё ничего не знаешь";
        return Hud.JoinFitting(parts, 77);
    }
}
