using Gopnik.Engine;
using Gopnik.Game;

namespace Gopnik.Scenes;

/// <summary>
/// Any of the seven places. One list, three columns: what it costs, what it is, what it
/// actually does to you. Greyed rows are the ones not worth your money right now.
/// </summary>
public sealed class LocationScene : IScene
{
    private sealed record Opt(string Key, int Price, string Title, string Note,
                              bool Useful, Action Do);

    private readonly World _w;
    private readonly Place _place;
    private readonly CommandLine _cmd = new();
    private readonly List<string> _log = new();

    public LocationScene(World w, Place place)
    {
        _w = w; _place = place;
        _log.Add(Greeting());
    }

    private Player P => _w.P;

    private string Title => _place switch
    {
        Place.Bazar => "БАЗАР",
        Place.Barygi => "БАРЫГИ",
        Place.Vet => "ВЕТЕРИНАР",
        Place.Girl => "ПОДРУГА",
        Place.Priton => "ПРИТОН",
        Place.Klub => "КЛУБ",
        _ => "КАЧАЛКА",
    };

    private string Greeting() => _place switch
    {
        Place.Bazar => "^7Ты пришел на базар.",
        Place.Barygi => "^7Ты пришел к барыгам.",
        Place.Vet => "^0Док: не волнуйся всё зарастёт как на собаке.",
        Place.Girl => "^2Ты пришел к своей подруге.",
        Place.Priton => "^7Ты пришел в притон - " + (P.Klass == Klass.Gopnik ? "гоповский притон." : "общагу ВКИ."),
        Place.Klub => "^7Ты пришел в клуб.",
        _ => "^7Ты пришел в качалку.",
    };

    private void Say(string s)
    {
        _log.Add(s);
        if (_log.Count > 60) _log.RemoveRange(0, _log.Count - 60);
    }

    /// <summary>The gym's refusal, in its own words. True when it will not train you.</summary>
    private bool Outgrown()
    {
        if (!_w.CanTravel) return false;
        Say("^6Ты слишком крутой чтобы тренироваться здесь.");
        Say("^6Качай дальше в следующем районе");
        return true;
    }

    private bool Pay(int cost)
    {
        if (P.Money < cost) { Say("^4Не хватает бабок."); return false; }
        P.Money -= cost;
        return true;
    }

    // ------------------------------------------------------------------ options
    private List<Opt> Options()
    {
        var o = new List<Opt>();
        switch (_place)
        {
            case Place.Bazar:
                o.Add(new("1", Data.PriceHotdog, "Хотдог", "здоровье +3..4", P.Hp < P.MaxHp, () =>
                {
                    if (P.JawBroken) { Say("^4Ты не можешь хавать из-за сломаной челюсти."); return; }
                    if (!Pay(Data.PriceHotdog)) return;
                    P.Heal(Rules.Roll(3, 4)); Say("^2Ты сожрал хот-дог.");
                }));
                o.Add(new("2", Data.PriceBeer, "Пиво, 0.5л", "лечит понемногу, можно взять в драку", true, () =>
                {
                    if (!Pay(Data.PriceBeer)) return;
                    P.Beer += 0.5; Say("^2Пивко. Холодненькое.");
                }));
                o.Add(new("3", Data.PriceShades, "Затемнённые очки", P.Shades ? "уже есть" : "менты не узнают", !P.Shades, () =>
                {
                    if (P.Shades) { Say("^6У тебя есть очки от солнца."); return; }
                    if (!Pay(Data.PriceShades)) return;
                    P.Shades = true; Say("^2Модные такие очки от солнца.");
                }));
                AddGear(o, "4", Data.Suits, 1, () => P.SuitIdx, v => P.SuitIdx = v, "броня", () => P.SuitsOwned, v => P.SuitsOwned = v);
                AddGear(o, "5", Data.Suits, 2, () => P.SuitIdx, v => P.SuitIdx = v, "броня", () => P.SuitsOwned, v => P.SuitsOwned = v);
                AddGear(o, "6", Data.Boots, 1, () => P.BootsIdx, v => P.BootsIdx = v, "урон", () => P.BootsOwned, v => P.BootsOwned = v);
                AddGear(o, "7", Data.Boots, 2, () => P.BootsIdx, v => P.BootsIdx = v, "урон", () => P.BootsOwned, v => P.BootsOwned = v);
                AddGear(o, "8", Data.Jackets, 1, () => P.JacketIdx, v => P.JacketIdx = v, "броня", () => P.JacketsOwned, v => P.JacketsOwned = v);
                AddGear(o, "9", Data.Jackets, 2, () => P.JacketIdx, v => P.JacketIdx = v, "броня", () => P.JacketsOwned, v => P.JacketsOwned = v);
                o.Add(new("t", 0, "Потискать кошельки", P.Klass == Klass.Vor ? "ты вор, тебе можно" : "рискованно", true, Pickpocket));
                break;

            case Place.Barygi:
                o.Add(new("1", Data.PriceJoint, "Косяк", "лечит в драке, потом сила -2", true, () =>
                {
                    if (!Pay(Data.PriceJoint)) return;
                    P.Joints++; Say("^2Ты купил косяк.");
                }));
                o.Add(new("2", Data.PriceMobile, "Краденый мобильник", P.Mobile ? "уже есть" : "братва приходит быстрее", !P.Mobile, () =>
                {
                    if (P.Mobile) { Say("^6У тебя уже есть мобила."); return; }
                    if (!Pay(Data.PriceMobile)) return;
                    P.Mobile = true; Say("^2Чё ты модный типа да?.");
                }));
                o.Add(new("3", Data.PriceBigJoint, "Офигенный косяк", "+1 к самой слабой характеристике", true, () =>
                {
                    if (!Pay(Data.PriceBigJoint)) return;
                    Say("^2Пошли стероиды!");
                    int lo = Math.Min(Math.Min(P.Str, P.Agi), Math.Min(P.Vit, P.Luck));
                    if (P.Str == lo) { P.Str++; Say("^1Сила +1"); }
                    else if (P.Agi == lo) { P.Agi++; Say("^1Ловкость +1"); }
                    else if (P.Vit == lo) { P.Vit++; Say("^1Живучесть +1"); }
                    else { P.Luck++; Say("^1Удача +1"); }
                }));
                o.Add(new("4", Data.PriceTattoo, "Зоновская наколка", P.Tattoo ? "уже есть" : "-50% что на тебя наедут", !P.Tattoo, () =>
                {
                    if (P.Tattoo) { Say("^2Чистый зек."); return; }
                    if (!Pay(Data.PriceTattoo)) return;
                    P.Tattoo = true; Say("^2Чистый зек.");
                }));
                AddGear(o, "5", Data.Weapons, 1, () => P.Weapon, v => P.Weapon = v, "урон", () => P.WeaponsOwned, v => P.WeaponsOwned = v);
                AddGear(o, "6", Data.Weapons, 2, () => P.Weapon, v => P.Weapon = v, "урон", () => P.WeaponsOwned, v => P.WeaponsOwned = v);
                o.Add(new("7", Data.PricePistol, "Самопальный пистолет", P.Pistol ? "уже есть" : "стрельба в бандитских районах", !P.Pistol, () =>
                {
                    if (P.Pistol) { Say("^6Да купил уже, купил."); return; }
                    if (!Pay(Data.PricePistol)) return;
                    P.Pistol = true; Say("^2Спасайся кто может!!!");
                    Say("^0Только помни стреляй в бандитских районах - там менты не накроют");
                }));
                o.Add(new("8", Data.PriceAmmo, "Патроны, 6 штук", P.Pistol ? $"сейчас у тебя {P.Ammo}" : "сначала купи пушку", P.Pistol, () =>
                {
                    if (!P.Pistol) { Say("^6Нету пушки. Сначала купи пистолет."); return; }
                    if (!Pay(Data.PriceAmmo)) return;
                    P.Ammo += 6; Say("^2Получи пять пуль.. на руки");
                }));
                o.Add(new("9", Data.PriceSilencer, "Глушитель", P.Silencer ? "уже есть" : "стреляй где хочешь", P.Pistol && !P.Silencer, () =>
                {
                    if (!P.Pistol) { Say("^6Нету пушки."); return; }
                    if (P.Silencer) { Say("^6Да купил уже."); return; }
                    if (!Pay(Data.PriceSilencer)) return;
                    P.Silencer = true; Say("^2Теперь стреляй где хочешь!");
                }));
                o.Add(new("x", 0, "Толкнуть хлам", P.Junk > 0 ? $"{P.Junk} шт - примерно {P.Junk * 7} руб." : "нечего спихнуть", P.Junk > 0, () =>
                {
                    if (P.Junk <= 0) { Say("^4Тебе нечего спихнуть."); return; }
                    int paid = P.Junk * Rules.Roll(5, 9);
                    P.Money += paid; P.Junk = 0;
                    Say($"^6Барыги дали тебе за хлам {paid} руб.");
                }));
                int spare = SpareWorth();
                o.Add(new("wes", 0, "Продать ненужные вещи",
                          spare > 0 ? $"выручишь {spare} руб." : "всё, что есть, при деле",
                          spare > 0, SellSpare));
                break;

            case Place.Vet:
                o.Add(new("1", Data.VetScratches, "Залатать царапины", P.Hp < P.MaxHp ? "здоровье под завязку" : "ты и так целый", P.Hp < P.MaxHp, () =>
                {
                    if (P.Hp >= P.MaxHp) { Say("^0Док: вали отсюда ты здоров."); return; }
                    if (!Pay(Data.VetScratches)) return;
                    P.Hp = P.MaxHp;
                    Say("^0Щас гайки подтянем и будешь как новый!");
                    Say($"^2Здоровья {P.Hp}/{P.MaxHp}");
                }));
                o.Add(new("2", Data.VetBones, "Починить переломы", P.JawBroken || P.LegBroken ? "челюсть и нога" : "ломать нечего", P.JawBroken || P.LegBroken, () =>
                {
                    if (!P.JawBroken && !P.LegBroken) { Say("^0Док: вали отсюда ты здоров."); return; }
                    if (!Pay(Data.VetBones)) return;
                    P.JawBroken = P.LegBroken = false;
                    Say("^0Ого! да тебя не иначе как грузовик откатал!");
                    Say("^2Твои переломы залечены.");
                }));
                break;

            case Place.Girl:
                o.Add(new("1", Data.GirlGift, "Завалиться на пару дней", "полное здоровье и узнаешь, где клуб", true, () =>
                {
                    if (!Pay(Data.GirlGift)) return;
                    Say("^6Ты купил ей чё-то, потратив 12 рублей.");
                    P.Hp = P.MaxHp;
                    Say("^2Ты расслабился, отдохнул и снова можешь творить свои гоповские дела.");
                    if (!P.Knows(Place.Klub))
                    {
                        P.Discover(Place.Klub);
                        Say("^2Она вытащила тебя в клуб и теперь ты знаешь где он находиться.");
                    }
                }));
                break;

            case Place.Priton:
                o.Add(new("p", 0, "Угостить пацанов пивом", P.Beer >= 1 ? "понтовость +5" : "пива нет", P.Beer >= 1, () =>
                {
                    if (P.Beer < 1) { Say("^6А нет у тебя пива."); return; }
                    P.Beer -= 1; P.AddRep(5);
                    Say("^2Ты угостил пацанов пивом. Понтовость улутшилась на 5.");
                }));
                o.Add(new("r", 0, "Занять 2 рубля", "понтовость -2", P.Rep >= 4, () =>
                {
                    if (P.Rep < 4) { Say("^6Ты не можешь занять денег."); return; }
                    P.Money += 2; P.AddRep(-2);
                    Say("^2Ты занял 2 рубля на пиво. Понтовость уменьшилась на 2.");
                }));
                o.Add(new("a", 0, "Спросить чё-то", "покажут места района", true, () =>
                {
                    Say("^0Тут у нас есть пара мест куда тебе стоит сходить.");
                    bool any = false;
                    foreach (var pl in new[] { Place.Trenaj, Place.Barygi, Place.Bazar })
                        if (!P.Knows(pl)) { P.Discover(pl); any = true; }
                    Say(any ? "^2Ты узнал где находится качалка и где сидят барыги."
                            : "^6Да ты и так всё тут знаешь.");
                }));
                o.Add(new("d", 0, "Пойти на дело", "риск: деньги и хлам, или драка", P.Rep >= 12, () =>
                {
                    if (P.Rep < 12) { Say("^4Тебя мудака такого туда не пустят - поднимай понтовость."); return; }
                    Say("^0Давай быстрее..");
                    Say("^2Ты пришел воровать деньги.");
                    if (Rules.Chance(35 + P.ELuck * 3))
                    {
                        int m = Rules.Roll(10, 25 + P.ELuck * 2);
                        P.Money += m; P.Junk += Rules.Roll(1, 2); P.AddRep(3);
                        Say($"^2Ты наваровал денег: {m} руб. и прихватил хлама.");
                    }
                    else
                    {
                        Say("^4Шухер менты!");
                        Say("^6Пора валить!");
                        if (Rules.Chance(40 + P.EAgi * 5)) Say("^2Ты смылся от ментов.");
                        else { P.Money = Math.Max(0, P.Money - 10); P.AddRep(-3); Say("^4Отобрали десятку и надавали по шее."); }
                    }
                }));
                break;

            case Place.Klub:
                o.Add(new("1", Data.KlubDisco, "Потусоваться на дискотеке", "понтовость +4", true, () =>
                {
                    if (!Pay(Data.KlubDisco)) return;
                    P.AddRep(4); Say("^2Ну весь на понтах. Понтовость +4.");
                }));
                o.Add(new("2", Data.KlubTricks, "Разузнать приёмы", "ловкость или удача +1", true, () =>
                {
                    if (!Pay(Data.KlubTricks)) return;
                    if (Rules.Chance(50)) { P.Agi++; Say("^2Ты прокачиваешь ловкость."); }
                    else { P.Luck++; Say("^2Ты прокачиваешь удачу."); }
                }));
                o.Add(new("3", 10, "Сыграть в карты", "ставка 10 руб.", P.Money >= 10, Cards));
                break;

            case Place.Trenaj:
                // The local gym is done with you the moment you are ready to move on:
                // "Ты слишком крутой чтобы тренироваться здесь" / "Качай дальше в следующем
                // районе". It is what stops the first district from being farmed for stats.
                bool outgrown = _w.CanTravel;
                string gymNote = outgrown ? "ты перерос этот район" : null!;

                o.Add(new("1", Data.TrainStat, "Качаться гантелями", gymNote ?? "сила +1", !outgrown, () =>
                {
                    if (Outgrown()) return;
                    if (!Pay(Data.TrainStat)) return;
                    P.Str++; Say("^2Ты прокачиваешь силу. ^1Сила +1");
                }));
                o.Add(new("2", Data.TrainStat, "Тренажёры", gymNote ?? "живучесть +1", !outgrown, () =>
                {
                    if (Outgrown()) return;
                    if (!Pay(Data.TrainStat)) return;
                    P.Vit++; Say("^2Ты прокачиваешь выносливость. ^1Живучесть +1");
                }));
                o.Add(new("3", Data.TrainXp, "Погонять до седьмого пота", "немного опыта", true, () =>
                {
                    if (!Pay(Data.TrainXp)) return;
                    int xp = Rules.Roll(4, 10);
                    P.Xp += xp; Say($"^2Ты тренируешься. ^1+{xp} качков опыта");
                }));
                o.Add(new("4", Data.TrainGuard, "Зубная защита боксёров", P.Guard ? "уже есть" : "-75% что сломают челюсть", !P.Guard, () =>
                {
                    if (P.Guard) { Say("^6У тебя есть эта штучка."); return; }
                    if (!Pay(Data.TrainGuard)) return;
                    P.Guard = true; Say("^2Ты купил защиту.");
                }));
                o.Add(new("5", Data.TrainPress, "Прокачать пресс", P.Press < P.PressCap ? "броня +1" : "для своего уровня хватит", P.Press < P.PressCap, () =>
                {
                    if (P.Press >= P.PressCap) { Say("^6Ты максимально прокачал пресс для своего уровня."); return; }
                    if (!Pay(Data.TrainPress)) return;
                    P.Press++; Say("^2Ты прокачиваешь пресс. ^1Броня +1");
                }));
                break;
        }
        return o;
    }

    private void AddGear(List<Opt> o, string key, (string Name, int Bonus, int Price, int Sale)[] table,
                         int idx, Func<int> get, Action<int> set, string what, Func<int> owned,
                         Action<int> setOwned)
    {
        var it = table[idx];
        bool useful = get() < idx;
        string note = get() >= idx ? $"у тебя {table[get()].Name}" : $"{what} +{it.Bonus}";
        o.Add(new(key, it.Price, it.Name, note, useful, () =>
        {
            if (get() >= idx) { Say("^6У тебя уже есть это или получше."); return; }
            if (!Pay(it.Price)) return;
            int mask = owned();
            P.Own(ref mask, idx);
            setOwned(mask);
            set(idx);
            Say($"^2Взял: {it.Name}. {what} +{it.Bonus}.");
        }));
    }

    /// <summary>What the spare gear in your bag is worth, so the menu can say it up front.</summary>
    private int SpareWorth()
    {
        int sum = 0;
        void Slot((string Name, int Bonus, int Price, int Sale)[] table, int worn, int mask)
        {
            for (int i = 1; i < worn; i++)
                if (Player.Owns(mask, i)) sum += table[i].Sale;
        }
        Slot(Data.Suits, P.SuitIdx, P.SuitsOwned);
        Slot(Data.Boots, P.BootsIdx, P.BootsOwned);
        Slot(Data.Jackets, P.JacketIdx, P.JacketsOwned);
        Slot(Data.Weapons, P.Weapon, P.WeaponsOwned);
        return sum;
    }

    /// <summary>
    /// Everything you own on a rung below the one you are wearing. The original walked
    /// these one at a time - "У тебя есть ненужный костюм хочешь продать?" - and this keeps
    /// its per-piece lines while spending a single keypress on the lot.
    /// </summary>
    private void SellSpare()
    {
        var slots = new (string What, (string Name, int Bonus, int Price, int Sale)[] Table,
                         Func<int> Worn, Func<int> Owned, Action<int> SetOwned)[]
        {
            ("костюм",    Data.Suits,   () => P.SuitIdx,   () => P.SuitsOwned,   v => P.SuitsOwned = v),
            ("кроссовки", Data.Boots,   () => P.BootsIdx,  () => P.BootsOwned,   v => P.BootsOwned = v),
            ("кожанку",   Data.Jackets, () => P.JacketIdx, () => P.JacketsOwned, v => P.JacketsOwned = v),
            ("железку",   Data.Weapons, () => P.Weapon,    () => P.WeaponsOwned, v => P.WeaponsOwned = v),
        };

        int total = 0;
        foreach (var slot in slots)
        {
            int mask = slot.Owned();
            for (int i = 1; i < slot.Worn(); i++)
            {
                if (!Player.Owns(mask, i)) continue;
                mask &= ~(1 << i);
                int paid = slot.Table[i].Sale;
                P.Money += paid;
                total += paid;
                Say($"^2Ты продал {slot.Table[i].Name} за {paid}.");
            }
            slot.SetOwned(mask);
        }

        if (total == 0) Say("^6У тебя нет ненужных вещей.");
    }

    private void Pickpocket()
    {
        int skill = 25 + P.ELuck * 4 + (P.Klass == Klass.Vor ? 20 : 0);
        if (Rules.Chance(skill))
        {
            int m = Rules.Roll(4, 12 + P.ELuck);
            P.Money += m;
            Say($"^2Потискал лоха - {m} руб. в кармане.");
            if (Rules.Chance(30)) { P.Luck++; Say("^1Удача +1"); }
        }
        else
        {
            Say("^6Блин менты запалят сматывайся!.");
            P.AddRep(-1);
            P.BazarClosed = true;
        }
    }

    private void Cards()
    {
        if (P.Money < 10) { Say("^6Не хватает денег - надо 10."); return; }
        P.Money -= 10;
        Say("^7Ты поставил 10 рублей");
        if (Rules.Chance(38 + P.ELuck * 3))
        {
            int win = Rules.Roll(15, 30);
            P.Money += win;
            Say($"^2Ты выиграл {win} рублей");
            if (Rules.Chance(20))
            {
                Say("^4Козёл! Да ты мухлевал!");
                Say("^6Уноси ноги, пока не отобрали деньги другие канадидаты");
            }
        }
        else Say("^4Ты проиграл 10 рублей");
    }

    // ------------------------------------------------------------------ scene
    public void Update(double dt, InputState input, SceneHost host)
    {
        _cmd.Update(input);
        string? c = _cmd.Submitted?.ToLowerInvariant();
        if (c is null) return;
        if (c is "w" or "e") { Save.Write(_w); host.Go(new StreetScene(_w)); return; }
        if (c == "") return;

        var opt = Options().FirstOrDefault(x => x.Key == c);
        if (opt is null) { Say($"^6Не понял: \"{c}\". ^Fw^6 - уйти."); return; }
        opt.Do();
    }

    public void Draw(TextScreen s, double t)
    {
        Hud.Status(s, P);

        var opts = Options();
        s.Box(0, Hud.Top, 80, opts.Count + 3, Vga.Cyan, title: $"{Title} ∙ {P.District.Name}");
        Hud.WriteRight(s, 77, Hud.Top, $"у тебя {P.Money} руб.", Vga.Yellow);

        for (int i = 0; i < opts.Count; i++)
        {
            var o = opts[i];
            bool afford = o.Price <= P.Money;
            bool live = o.Useful && afford;
            byte key = live ? Vga.White : Vga.DarkGray;
            byte name = live ? Vga.LightGray : Vga.DarkGray;
            byte note = live ? Vga.DarkGray : Vga.DarkGray;

            int y = Hud.Top + 1 + i;
            s.Write(2, y, o.Key, key);
            s.Write(5, y, o.Price > 0 ? $"{o.Price,4} р." : "     ", live ? Vga.Yellow : Vga.DarkGray);
            s.Write(13, y, o.Title, name);
            // The note column stops short of the price warning, otherwise a long line runs
            // straight into "не хватит" and the two read as one word.
            bool warn = !afford && o.Price > 0;
            int room = warn ? 24 : 33;
            s.Write(45, y, o.Note.Length > room ? o.Note[..(room - 1)] + "." : o.Note, note);
            if (warn) s.Write(70, y, "не хватит", Vga.Red);
        }

        int logTop = Hud.Top + opts.Count + 4;
        Hud.DrawLog(s, _log, logTop, Hud.Bottom);

        Hud.Hints(s, "^Fw^8 уйти ∙ жми клавишу слева и ввод");
        _cmd.Draw(s, 1, 24, t);
    }
}
