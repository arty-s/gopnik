namespace Gopnik.Game;

/// <summary>The street: everything that happens between fights.</summary>
public sealed class World
{
    public readonly Player P = new();
    public readonly List<string> Log = new();
    public Foe? Foe;                 // whoever is standing in front of you right now
    public bool FoeIsCop;
    public bool Won;

    /// <summary>
    /// Some encounters cannot be walked away from. Without this the optimal play is simply
    /// to decline every unfavourable fight, and a patient player becomes unbeatable - the
    /// game stops being a game and turns into a waiting exercise.
    /// </summary>
    public bool FoeForced;

    /// <summary>Level needed before the next district will have you.</summary>
    private static readonly int[] Gate = { 3, 5, 8, 11, 99 };
    public int NextDistrictLevel => Gate[P.DistrictIdx];
    public bool CanTravel => P.Level >= NextDistrictLevel && P.DistrictIdx < Data.Districts.Length - 1;

    public void Say(string s)
    {
        // Repeating the same refusal ("ищи мудака которого будешь пинать") on every stray
        // keypress buries the actual events, so identical consecutive lines collapse.
        if (Log.Count > 0 && Log[^1] == s && s.Length > 0) return;
        Log.Add(s);
        if (Log.Count > 200) Log.RemoveRange(0, Log.Count - 200);
    }

    public void Blank() { if (Log.Count > 0 && Log[^1] != "") Log.Add(""); }

    // ---------------------------------------------------------------- wandering
    public void Wander()
    {
        Blank();
        Foe = null; FoeIsCop = false; FoeForced = false;

        if (P.Stoned > 0 && Rules.Chance(30))
        {
            Say("^6Все куда-то плывёт - совсем башню заклинило");
            Say("^6Чё за ботва? ни кого нет.. глюки какие-то...");
            Tick();
            return;
        }

        int r = Rules.Rng.Next(100);

        if (r < 34) { MeetFoe(); Tick(); return; }
        if (r < 42) { MeetCop(); Tick(); return; }
        if (r < 52) { DiscoverPlace(); Tick(); return; }
        if (r < 60) { Say(Texts.Pick(Texts.Phone)); Tick(); return; }
        if (r < 66)
        {
            int m = Rules.Roll(2, 6 + P.ELuck);
            P.Money += m;
            Say($"^EОпа бабки! {m} рублей на пиво!");
            Tick(); return;
        }
        if (r < 72) { Chick(); Tick(); return; }
        if (r < 78) { Temple(); Tick(); return; }
        if (r < 84 && P.Klass == Klass.Vor)
        {
            P.Junk++;
            Say("^2Ты подрезал у зазевавшегося лоха какую-то вещь. Хлама стало больше.");
            Tick(); return;
        }

        Say(Texts.Pick(Texts.Wander));
        Tick();
    }

    private void MeetFoe()
    {
        // In the last district the Rector has to be findable again. He is placed once on
        // arrival, and if you lose that fight and your mates drag you out, there would
        // otherwise be no way left to finish the game.
        if (P.DistrictIdx == Data.Districts.Length - 1 && Rules.Chance(35))
        {
            Foe = Rules.MakeFoe(P, Data.Foes.Length - 1, Math.Max(12, P.Level + 1));
            Say("^1Ты снова пробрался в тёмный ректорский кабинет...");
            Say($"^C{Foe.Name} {Foe.Level} уровня. Отступать некуда.");
            return;
        }

        var pool = Data.Spawns[P.DistrictIdx];
        int idx = pool[Rules.Rng.Next(pool.Length)];

        // Every so often somebody well out of your weight class turns up, and the deeper
        // you go the more of them there are. Ordinary street trash is meant to fold in two
        // or three kicks - that is the whole power fantasy - so the danger has to come from
        // the fights you should look at before taking them, not from every passer-by.
        // None of this in the opening district - that one is where you learn the ropes with
        // nothing in your pockets, and an unavoidable heavyweight there just ends the run.
        bool elite = P.DistrictIdx > 0 && Rules.Chance(8 + P.DistrictIdx * 7);
        var f = Rules.MakeFoe(P, idx, Rules.SpawnLevel(P) + (elite ? 4 : 0));
        if (elite)
        {
            f.Name = "матёрый " + f.Name;
            f.WeaponBonus += 2;
            f.Armour += 1;
            f.Money += 15 + P.DistrictIdx * 10;
        }

        if (P.Tattoo && idx < 7 && !elite && Rules.Chance(50))
        {
            Say($"^7Мимо прошёл {f.Name} {f.Level} уровня - на твою наколку покосился и не полез.");
            return;
        }
        Foe = f;
        if (elite && Rules.Chance(35))
        {
            FoeForced = true;
            Say($"^4Идёт {f.Name} {f.Level} уровня.");
            Say("^4Он тебя заметил. Не разойтись.");
            return;
        }
        Say(elite
            ? $"^4Идёт {f.Name} {f.Level} уровня. Такого лучше сперва оценить - ^Esv^4."
            : $"^CИдёт {f.Name} {f.Level} уровня, ищущий кого отпинать. Хочешь наехать?");
    }

    private void MeetCop()
    {
        var f = Rules.MakeFoe(P, 8, Rules.SpawnLevel(P) + 1);
        Say($"^6Идет ментяра {f.Level} уровня гроза гопов.");
        if (P.Shades)
        {
            Say("^2Ты напялил тёмные очки и мент не узнал твою рожу, которая весит на почётном");
            Say("^2стенде \"Разыскиваются за гопничество\"");
            return;
        }
        if (Rules.Chance(35 + P.ELuck * 3))
        {
            Say("^2Ты затаился, прикинулся не гопом... Мент вроде не заметил");
            return;
        }
        Say("^4Запалил! Уже не разойтись.");
        Foe = f; FoeIsCop = true; FoeForced = true;
    }

    private void Chick()
    {
        Say("^5Идет типа клёвая цыпа. Хочешь её зацепить?");
        if (Rules.Chance(25 + P.ELuck * 4 + P.Rep / 4))
        {
            Say("^5Ты такой подкатываешь, а она:\"Глянулся ты мне парниша\"");
            P.GirlKnown = true;
            P.Discover(Place.Girl);
            P.AddRep(2);
        }
        else Say("^4Ты ещё подкатить неуспел - а она:\"Отдыхай урод\". - Тебя обломали кент");
    }

    private void Temple()
    {
        Say("^7Бродя по окрестностям с самыми грязными намериниями...");
        Say("^7Ты наткнулся на храм Божий.");
        Say("^2\"Господи, Братан, прости грешника\". - Начал было ты...");
        if (Rules.Chance(45))
        {
            Say("^1Громовой голос: \"Да пошел ты на хрен!\"");
            Say("^2 - Эээ.. типа.. а чё?..");
            return;
        }
        Say("^1Ладно насылаю на тебя, типа, \"моё благословление\"");
        switch (Rules.Rng.Next(5))
        {
            case 0: P.AddRep(4); Say(Texts.Blessings[0]); break;
            case 1: P.Str++; Say(Texts.Blessings[1]); break;
            case 2: P.Agi++; Say(Texts.Blessings[2]); break;
            case 3: P.Vit++; P.Heal(10); Say(Texts.Blessings[3]); break;
            default: P.Luck++; Say(Texts.Blessings[4]); break;
        }
        if (P.JawBroken && Rules.Chance(50)) { P.JawBroken = false; Say("^2Твоя челюсть залечилась с Божей помощью."); }
        if (P.LegBroken && Rules.Chance(50)) { P.LegBroken = false; Say("^2Твоя нога залечилась с Божей помощью."); }
        if (!P.RingAll && Rules.Chance(12)) { P.RingAll = true; Say("^1Дарю тебе феньку! Кольцо \"Помоги Господи\""); }
        else if (!P.MegaRing && Rules.Chance(5)) { P.MegaRing = true; Say("^1\"Мега Кольцо\"! со своего, можно сказать, пальца"); }
        else if (!P.RingHeal && Rules.Chance(8)) { P.RingHeal = true; Say("^1Ваще полезное кольцо \"Господи помилуй\" - раны затягиваются сами"); }
        Say("^1А теперь вали отсюда и никогда здесь не появляйся!");
    }

    private void DiscoverPlace()
    {
        var unknown = Data.Places.Where(pl => !P.Knows(pl.P) &&
                                              (pl.P != Place.Girl || P.GirlKnown)).ToList();
        if (unknown.Count == 0) { Say(Texts.Pick(Texts.Wander)); return; }
        var pick = unknown[Rules.Rng.Next(unknown.Count)];
        P.Discover(pick.P);
        Say(pick.P switch
        {
            Place.Bazar => "^1Ты нашел базар.",
            Place.Vet => "^1Ты спросил у прохожего где больница.",
            Place.Klub => "^1Ты увидел объявление \"Типа заходи в наш понтовый клуб\".",
            Place.Trenaj => "^1На стене реклама \"Жизнь тяжела. Если не хочешь сдохнуть качайся!\".",
            Place.Barygi => "^1Ты приметил, где сидят барыги.",
            Place.Priton => "^1Тебе показали, где местный притон.",
            _ => "^1Ты запомнил дорогу к своей девчонке.",
        });
    }

    /// <summary>One step of wall-clock: passive regeneration and sobering up.</summary>
    private void Tick()
    {
        if (P.RingHeal && P.Hp < P.MaxHp) P.Heal(3);
        if (P.Klass == Klass.Otmoroz && P.Hp < P.MaxHp && Rules.Chance(50)) P.Heal(2);
        if (P.RingHeal && Rules.Chance(5))
        {
            if (P.JawBroken) { P.JawBroken = false; Say("^2Челюсть срослась сама."); }
            else if (P.LegBroken) { P.LegBroken = false; Say("^2Нога срослась сама."); }
        }
        if (P.Stoned > 0 && --P.Stoned == 0)
        {
            P.Str = Math.Max(1, P.Str - 2);
            Say("^6Глюки прошли. Сила -2.");
        }
    }

    // ---------------------------------------------------------------- travel
    public void Travel()
    {
        Blank();
        if (!CanTravel)
        {
            Say($"^6Рано тебе дальше. Дорасти до {NextDistrictLevel} уровня, сейчас ты {P.Level}.");
            return;
        }
        P.DistrictIdx++;
        Foe = null;
        var d = P.District;
        Say("^1Ты доказал, что ты самый крутой в этом районе - отправляйся в следующий");
        Say("^1" + d.Arrival);
        Say("^1" + d.Flavour);
        Say("^6В новом районе ты не знаешь, где что находится - ищи заново.");

        if (P.DistrictIdx == Data.Districts.Length - 1)
        {
            Say("^4А вот и он...");
            Foe = Rules.MakeFoe(P, Data.Foes.Length - 1, Math.Max(12, P.Level + 2));
            Say($"^C{Foe.Name} {Foe.Level} уровня. Отступать некуда.");
        }
    }

    /// <summary>Death is not always the end - the original let your mates haul you out.</summary>
    public bool Die()
    {
        P.Knockouts++;

        if (P.DistrictIdx == Data.Districts.Length - 1)
        {
            Say(Texts.DeadByRector);
            return true;
        }

        // The very first beating never kills. At level one you have no reputation, so the
        // rescue below cannot fire, and one unlucky opening fight ended the whole run -
        // which reads as unfair rather than hard.
        if (P.Knockouts == 1)
        {
            Say(Texts.SavedByFriends);
            P.Hp = Math.Max(1, P.MaxHp / 3);
            P.AddRep(-2);
            return false;
        }

        if (P.Rep >= 20 && Rules.Chance(60))
        {
            Say(Texts.SavedByFriends);
            P.Hp = Math.Max(1, P.MaxHp / 4);
            P.Money = Math.Max(0, P.Money - 15);
            P.AddRep(-4);
            return false;
        }
        Say("^4Ты сдох.");
        return true;
    }
}
