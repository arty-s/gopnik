using Gopnik.Engine;
using Gopnik.Game;

namespace Gopnik.Scenes;

/// <summary>The setup: why you are out here, who you are, and what you are made of.</summary>
public sealed class CreateScene : IScene
{
    private readonly World _w = new();
    private int _stage;
    private int _spent;
    private readonly CommandLine _name = new();

    // The original hands you twelve points across the four skills. Start each at one and
    // let the player place the remaining eight, so the total lands exactly on twelve.
    public CreateScene() => _w.P.Str = _w.P.Agi = _w.P.Vit = _w.P.Luck = 1;

    private static readonly string[] Intro =
    {
        Texts.Intro1, Texts.Intro2, Texts.Intro3, Texts.Intro4, Texts.Intro5, "",
        Texts.Intro6, Texts.Intro7, Texts.Intro8, "", Texts.Intro9, "",
        Texts.Intro10, Texts.Intro11,
    };

    public void Update(double dt, InputState input, SceneHost host)
    {
        var p = _w.P;
        switch (_stage)
        {
            case 0:
                if (input.AnyKey) _stage = 1;
                return;

            case 1:
                foreach (char c in input.Typed)
                    if (c >= '0' && c <= '3')
                    {
                        p.Klass = (Klass)(c - '0');
                        if (p.Klass == Klass.Pacan) p.GirlKnown = true;
                        if (p.Klass == Klass.Gopnik) p.Discover(Place.Priton);
                        if (p.Klass == Klass.Vor) p.Discover(Place.Barygi);
                        _stage = 2;
                        return;
                    }
                return;

            case 2:
                foreach (char c in input.Typed)
                {
                    if (c >= '1' && c <= '4' && _spent < 8)
                    {
                        switch (c) { case '1': p.Str++; break; case '2': p.Agi++; break;
                                     case '3': p.Vit++; break; default: p.Luck++; break; }
                        _spent++;
                    }
                    else if (c == '0')
                    {
                        p.Str = p.Agi = p.Vit = p.Luck = 1; _spent = 0;
                    }
                    else if ((c == '\r' || c == '\n') && _spent == 8)
                    {
                        _stage = 3;
                    }
                }
                if (input.Hit(Keys.Back) && _spent > 0) { }
                return;

            default:
                _name.Update(input);
                if (_name.Submitted is not null)
                {
                    if (_name.Submitted.Length > 0) p.Name = _name.Submitted;
                    p.Hp = p.MaxHp;
                    _w.Say("^1" + Data.Districts[0].Arrival);
                    _w.Say("^1" + Data.Districts[0].Flavour);
                    _w.Say("^7Доказать свою крутизну ты можешь, отпинывая разных мудаков.");
                    _w.Say("^7Тебе придётся поработать над сабой, чтобы стать крутым.");
                    _w.Say("^7Введи ^Ei^7 чтобы посмотреть команды, ^Ew^7 - чтобы шататься по окрестностям.");
                    Save.Write(_w);
                    host.Go(new StreetScene(_w));
                }
                return;
        }
    }

    public void Draw(TextScreen s, double t)
    {
        var p = _w.P;
        switch (_stage)
        {
            case 0:
                for (int i = 0; i < Intro.Length; i++)
                    s.Markup(6, 3 + i, Intro[i], Vga.LightGray);
                s.Write(6, 22, "- жми любую кнопку -", Vga.Yellow);
                return;

            case 1:
                s.Box(4, 3, 72, 12, Vga.Cyan, title: "ВЫБЕРИ КЕМ ТЫ БУДЕШЬ");
                for (int i = 0; i < Data.Classes.Length; i++)
                {
                    s.Write(7, 5 + i * 2, $"{i}", Vga.White);
                    s.Write(10, 5 + i * 2, Data.Classes[i].Name, Vga.LightGreen);
                    s.Write(24, 5 + i * 2, Data.Classes[i].Bonus, Vga.LightGray);
                }
                s.Write(6, 16, "жми цифру", Vga.Yellow);
                return;

            case 2:
                s.Box(4, 3, 72, 13, Vga.Cyan, title: "НАВЫКИ - ВСЕГО 12 ОЧКОВ");
                s.Write(6, 5, $"осталось раскидать: {8 - _spent}", Vga.Yellow);
                Row(s, 7, "1", "Сила", p.Str, $"урон {p.DamageMin}-{p.DamageMax}, +1 здоровья за очко");
                Row(s, 8, "2", "Ловкость", p.Agi, $"точность {p.Accuracy}%, шанс ударить дважды");
                Row(s, 9, "3", "Живучесть", p.Vit, $"здоровье {p.MaxHp}, +5 за очко");
                Row(s, 10, "4", "Удача", p.Luck, $"крит {Rules.CritChance(p.ELuck)}%, находки, карты");
                s.Write(6, 12, "Чем больше навык - тем чаще он растёт на новом уровне:", Vga.DarkGray);
                s.Write(6, 13, "шанс роста = значение навыка из 12.", Vga.DarkGray);
                s.Write(6, 14, "0 - сбросить", Vga.DarkGray);
                if (_spent == 8) s.Write(6, 17, "- жми ВВОД -", Vga.LightGreen);
                return;

            default:
                s.Box(4, 8, 72, 5, Vga.Cyan, title: "А ЗОВУТ ТЕБЯ");
                s.Write(6, 10, "погоняло:", Vga.LightGray);
                _name.Draw(s, 16, 10, t);
                s.Write(6, 14, "пусто - будешь Раздолбаем", Vga.DarkGray);
                return;
        }
    }

    private static void Row(TextScreen s, int y, string key, string name, int val, string what)
    {
        s.Write(7, y, key, Vga.White);
        s.Write(10, y, name, Vga.LightGray);
        s.Write(22, y, val.ToString(), Vga.White);
        s.Write(26, y, what, Vga.LightCyan);
    }
}

/// <summary>The full character sheet - everything the status strip has no room for.</summary>
public sealed class StatsScene : IScene
{
    private readonly World _w;
    public StatsScene(World w) => _w = w;

    public void Update(double dt, InputState input, SceneHost host)
    {
        if (input.AnyKey) host.Go(new StreetScene(_w));
    }

    public void Draw(TextScreen s, double t)
    {
        var p = _w.P;
        Hud.Status(s, p);
        s.Box(0, Hud.Top, 40, 16, Vga.Cyan, title: "ЧТО НА ТЕБЕ");
        int y = Hud.Top + 1;
        s.Write(2, y++, $"Оружие:  {Data.Weapons[p.Weapon].Name} (+{Data.Weapons[p.Weapon].Bonus})", Vga.LightGray);
        s.Write(2, y++, $"Обувь:   {Data.Boots[p.BootsIdx].Name} (+{Data.Boots[p.BootsIdx].Bonus})", Vga.LightGray);
        s.Write(2, y++, $"Костюм:  {Data.Suits[p.SuitIdx].Name} (+{Data.Suits[p.SuitIdx].Bonus})", Vga.LightGray);
        s.Write(2, y++, $"Кожанка: {Data.Jackets[p.JacketIdx].Name} (+{Data.Jackets[p.JacketIdx].Bonus})", Vga.LightGray);
        s.Write(2, y++, $"Пресс:   броня +{p.Press}", Vga.LightGray);
        y++;
        s.Write(2, y++, "Феньки:", Vga.Cyan);
        if (p.Cross) s.Write(4, y++, "Крестик (удача +2)", Vga.LightGreen);
        if (p.RingLuck) s.Write(4, y++, "Кольцо \"Гс\" (удача +1)", Vga.LightGreen);
        if (p.RingAll) s.Write(4, y++, "Кольцо \"Пг\" (всё +1)", Vga.LightGreen);
        if (p.MegaRing) s.Write(4, y++, "Мега Кольцо (всё +4)", Vga.LightGreen);
        if (p.RingHeal) s.Write(4, y++, "Кольцо \"Гп\" (самолечение)", Vga.LightGreen);

        s.Box(40, Hud.Top, 40, 16, Vga.Cyan, title: "ПРОЧЕЕ");
        y = Hud.Top + 1;
        Flag(s, 42, ref y, "мобильник", p.Mobile);
        Flag(s, 42, ref y, "тёмные очки", p.Shades);
        Flag(s, 42, ref y, "зоновская наколка", p.Tattoo);
        Flag(s, 42, ref y, "пистолет", p.Pistol);
        Flag(s, 42, ref y, "глушитель", p.Silencer);
        Flag(s, 42, ref y, "зубная защита", p.Guard);
        y++;
        s.Write(42, y++, $"Понтовость: {p.Rep} из 126", Vga.Yellow);
        s.Write(42, y++, $"\"{p.Rank}\"", Vga.White);
        y++;
        s.Write(42, y++, _w.CanTravel ? "Можешь ехать дальше: go" : $"Дальше - с {_w.NextDistrictLevel} уровня",
                _w.CanTravel ? Vga.LightGreen : Vga.DarkGray);

        Hud.Hints(s, "^Fлюбая кнопка^8 - назад на улицу");
    }

    private static void Flag(TextScreen s, int x, ref int y, string name, bool has)
    {
        s.Write(x, y, has ? "■" : "·", has ? Vga.LightGreen : Vga.DarkGray);
        s.Write(x + 2, y, name, has ? Vga.LightGray : Vga.DarkGray);
        y++;
    }
}

/// <summary>The original's help screen, with the formulas it actually printed.</summary>
public sealed class HelpScene : IScene
{
    private readonly World? _w;
    public HelpScene(World? w = null) => _w = w;

    public void Update(double dt, InputState input, SceneHost host)
    {
        if (input.AnyKey) host.Go(_w is null ? new TitleScene() : new StreetScene(_w));
    }

    public void Draw(TextScreen s, double t)
    {
        s.Box(0, 0, 80, 25, Vga.Cyan, dbl: true, title: "ЧЁ ЗА БАТВА");
        int y = 2;
        s.Markup(2, y++, "^FМАТЕМАТИКА^7 - ровно та, что была в оригинале 2003 года:");
        s.Markup(4, y++, "^BЗдоровье^7 = 10 + Живучесть×5 + Сила");
        s.Markup(4, y++, "^BУрон^7 = от Сила/2 до Сила, плюс оружие и бутсы");
        s.Markup(4, y++, "^BТочность^7 = (20 + Ловкость×5)%, минус ловкость врага");
        s.Markup(4, y++, "^BБроня^7 снимает урон врага; ^BУдача^7 - криты, находки, карты");
        s.Markup(4, y++, "^BНа уровне^7 навык растёт с шансом \"своё значение из 12\"");
        y++;
        s.Markup(2, y++, "^FМЕСТА^7 - в каждом новом районе их надо искать заново:");
        s.Markup(4, y++, "^Emar^7  базар - жратва, пиво, шмотки, можно тискать кошельки");
        s.Markup(4, y++, "^Ebmar^7 барыги - косяки, ствол, кастет; сюда же сдавать хлам");
        s.Markup(4, y++, "^Erep^7  ветеринар - лечит царапины и переломы");
        s.Markup(4, y++, "^Egirl^7 подруга - отдых, полное здоровье, покажет клуб");
        s.Markup(4, y++, "^Epr^7   притон - понтовость, займы, дела; отсюда приходит братва");
        s.Markup(4, y++, "^Ekl^7   клуб - дискотека, приёмы, карты на бабло");
        s.Markup(4, y++, "^Etrn^7  качалка - сила, живучесть, пресс, зубная защита");
        y++;
        s.Markup(2, y++, "^FНА УЛИЦЕ:^7 ^Ew^7 шататься ∙ ^Ek^7 наехать ∙ ^Esv^7 оценить ∙ ^Es^7 себя ∙ ^Ego^7 в след. район");
        s.Markup(2, y++, "^FВ ДРАКЕ:^7 ^Ek^7 пнуть ∙ ^Ef^7 стрелять ∙ ^Ekos^7 косяк ∙ ^Eh^7 пиво ∙ ^Ev^7 братва ∙ ^Eq^7 свалить");
        s.Markup(2, y++, "^8Alt+Enter - на весь экран.");
        s.Write(2, 23, "- жми любую кнопку -", Vga.Yellow);
    }
}

/// <summary>Curtain call.</summary>
public sealed class EndScene : IScene
{
    private readonly World _w;
    private readonly bool _victory;
    private readonly Scroller _scroll;

    public EndScene(World w, bool victory)
    {
        _w = w; _victory = victory;
        _scroll = new Scroller(victory
            ? "ТЫ ЗАМОЧИЛ САМОГО РЕКТОРА  ∙  ну, почти  ∙  спасибо за игру  ∙∙∙   "
            : "ТУТ ТВОЯ ИСТОРИЯ И КОНЧИЛАСЬ  ∙  бывает  ∙∙∙   ");
    }

    public void Update(double dt, InputState input, SceneHost host)
    {
        _scroll.Update(dt);
        if (input.AnyKey) host.Go(new TitleScene());
    }

    public void Draw(TextScreen s, double t)
    {
        var p = _w.P;
        Retro.CopperBar(s, 1.5, 1.5, t, _victory ? Retro.GoldRamp : Retro.BloodRamp,
                        waveAmp: 0.8, waveSpeed: 1.5);

        int y = 5;
        if (_victory)
        {
            s.Markup(6, y++, Texts.WinLine1);
            s.Markup(6, y++, Texts.WinLine2);
            s.Markup(6, y++, Texts.WinLine3);
            y++;
            s.Markup(6, y++, Texts.WinLine4);
            s.Markup(6, y++, Texts.WinLine5);
            s.Markup(6, y++, Texts.WinLine6);
        }
        else
        {
            s.Markup(6, y++, "^4Ты сдох.");
            s.Markup(6, y++, "^6Блин не быть тебе нормальным пацаном.");
        }

        y += 2;
        s.Markup(6, y++, "^1А результат:");
        s.Write(8, y++, $"{p.Name} - {p.Rank}", Vga.White);
        s.Write(8, y++, $"уровень {p.Level}, понтовость {p.Rep}, район {p.DistrictIdx + 1} из {Data.Districts.Length}", Vga.LightGray);
        s.Write(8, y++, $"сила {p.EStr}  ловкость {p.EAgi}  живучесть {p.EVit}  удача {p.ELuck}", Vga.LightGray);
        s.Write(8, y++, $"в кармане {p.Money} руб.", Vga.LightGray);

        Retro.CopperBar(s, 21.6, 1.4, t + 1.1, _victory ? Retro.GoldRamp : Retro.BloodRamp,
                        waveAmp: 0.7, waveSpeed: -1.3);
        _scroll.Draw(s, 24, _victory ? Vga.Yellow : Vga.LightRed);
    }
}
