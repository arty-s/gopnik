namespace Gopnik.Game;

public enum FightEnd { None, Won, Lost, Fled }

/// <summary>
/// One street fight. The original resolved a whole exchange per keypress and printed the
/// result; this keeps that rhythm - you act, the other guy answers, the round closes.
/// </summary>
public sealed class Fight
{
    public readonly Player P;
    public readonly Foe F;
    public readonly List<string> Log = new();
    public int Round = 1;
    public FightEnd End;
    public string Spectator = "";

    private int _backupIn = -1;      // rounds until the lads show up
    public bool BackupHere;

    // Read-only tallies for the balance harness. They never affect play.
    public int MySwings, MyHits, HisSwings, HisHits, DamageDealt, DamageTaken;

    public Fight(Player p, Foe f)
    {
        P = p; F = f;
        F.Reset();
        Say(f.Greeting);
        Say("^7Начинают собираться зрители.");
        NextSpectator();
    }

    public void Say(string line) => Log.Add(line);
    public void NextSpectator() => Spectator = Texts.Pick(Texts.Spectators);

    public int MyHitChance => Rules.HitChance(P.Accuracy, P.EAgi, F.Agi);
    public int HisHitChance => Rules.HitChance(F.Accuracy, F.Agi, P.EAgi);
    public int MyFleeChance => Rules.FleeChance(P.LegBroken, P.EAgi, F.Agi);
    public bool CanCallBackup => _backupIn < 0 && !BackupHere && P.Rep >= 15;

    // ---- player actions ---------------------------------------------------------

    public void Attack()
    {
        if (End != FightEnd.None) return;
        Swing();
        if (F.Hp > 0 && Rules.ExtraSwing(P.EAgi, F.Agi))
        {
            Say("^2Из-за большой ловкости ты можешь пнуть ещё раз");
            Swing();
        }
        CloseRound();
    }

    public void Shoot()
    {
        if (End != FightEnd.None) return;
        if (!P.Pistol) { Say("^6Нету пушки."); return; }
        if (P.Ammo <= 0) { Say("^6Чё за батва? Блин патроны кончились!"); return; }
        if (!P.District.Bandit && !P.Silencer)
        {
            Say("^6Нельзя тут стрелять! Менты накроют!");
            return;
        }
        P.Ammo--;
        if (Rules.Chance(55 + P.ELuck * 2))
        {
            int dmg = Rules.Roll(8, 16);
            F.Hp -= dmg;
            Say($"^2Ты выстрелил и ранил врага на {dmg}з. У него осталось {Math.Max(0, F.Hp)}з., патронов {P.Ammo}");
        }
        else Say("^2Это был хреновый выстрел.");
        CloseRound();
    }

    public void SmokeJoint()
    {
        if (End != FightEnd.None) return;
        if (P.JawBroken) { Say("^4Ты не можешь схавать колёса из-за сломаной челюсти."); return; }
        if (P.Joints <= 0) { Say("^4У тебя нет косяков"); return; }
        if (P.Stoned > 0) { Say("^6Ты неможешь схавать ещё один косяк."); return; }
        P.Joints--;
        int heal = Rules.Roll(6, 14);
        P.Heal(heal);
        P.Stoned = Rules.Roll(4, 7);
        P.Str += 2;
        Say($"^2Колёса прибавляют {heal}з. Здоровья:{P.Hp}/{P.MaxHp}. Осталось {P.Joints} косяков");
        Say("^2Сила +2.");
        CloseRound();
    }

    public void DrinkBeer()
    {
        if (End != FightEnd.None) return;
        if (P.JawBroken) { Say("^4Ты не можешь пить пиво из-за сломаной челюсти."); return; }
        if (P.Beer < 0.5) { Say("^4Пива нету"); return; }
        P.Beer -= 0.5;
        int heal = Rules.Roll(3, 7);
        P.Heal(heal);
        Say($"^2Пиво прибавляет {heal}з. Здоровья:{P.Hp}/{P.MaxHp}. Осталось {P.Beer:0.0}л. пива");
        CloseRound();
    }

    public void CallBackup()
    {
        if (End != FightEnd.None) return;
        if (BackupHere || _backupIn >= 0) { Say("^6Уже позвал."); return; }
        if (P.Rep < 15)
        {
            Say("^4Ни кто не хочет за тебя впрягаться.");
            Say("^6Сначала надо скорешиться с местной гопотой.");
            return;
        }
        _backupIn = P.Mobile ? 2 : 4;
        Say("^2Подошли пацаны - Ща начнется!");
        Say($"^7надо продержатся до подхода братвы - {_backupIn} раунда");
        CloseRound();
    }

    public bool TryFlee()
    {
        if (End != FightEnd.None) return false;
        if (F.Index == Data.Foes.Length - 1)
        {
            Say("^4Ректор: Куда? Стоять! Бейся до конца трусливый урод!");
            return false;
        }
        if (P.LegBroken) { Say("^4Ты не можешь убежать на сломаной ноге."); return false; }
        if (Rules.Chance(MyFleeChance))
        {
            Say("^2Ты смылся.");
            P.AddRep(-2);
            Say("^4Враг: Трусливый засранец!");
            End = FightEnd.Fled;
            return true;
        }
        Say("^4Не вышло смыться.");
        CloseRound();
        return false;
    }

    // ---- resolution -------------------------------------------------------------

    private void Swing()
    {
        MySwings++;
        if (!Rules.Chance(MyHitChance)) { Say("^4Ты промазал"); return; }
        MyHits++;

        int dmg = Rules.Roll(P.DamageMin, P.DamageMax);
        if (Rules.Chance(Rules.CritChance(P.ELuck)))
        {
            dmg *= 2;
            Say(Texts.Pick(Texts.PlayerHits));
        }
        // Strength shoulders through armour. Without this, agility was the only stat that
        // kept paying off late, because accuracy multiplies every point of damage while
        // raw strength just got eaten by the other guy's jacket.
        dmg = Rules.Soak(dmg, Math.Max(0, F.Armour - P.EStr / 4));
        F.Hp -= dmg;
        DamageDealt += dmg;
        Say($"^2Ты пнул врага на {dmg}з. У него осталось {Math.Max(0, F.Hp)}");

        if (!F.JawBroken && Rules.Chance(6 + P.EStr))
        {
            F.JawBroken = true;
            Say("^2Ты сломал врагу челюсть. ^4Враг: А! козёл!");
        }
        else if (!F.LegBroken && Rules.Chance(4 + P.EStr / 2))
        {
            F.LegBroken = true;
            Say("^2Ты сломал врагу ногу. ^4Враг: Ну что за урод!");
        }
    }

    private void FoeSwing()
    {
        HisSwings++;
        if (!Rules.Chance(HisHitChance)) { Say("^2Враг промазал"); return; }
        HisHits++;

        int dmg = Rules.Roll(F.DamageMin, F.DamageMax);
        if (Rules.Chance(8)) { dmg *= 2; Say(Texts.Pick(Texts.FoeTaunts)); }
        dmg = Rules.Soak(dmg, Math.Max(0, P.Armour - F.Str / 4));
        P.Hp -= dmg;
        DamageTaken += dmg;
        Say($"^4Он пнул тебя на {dmg}з. У тебя осталось {Math.Max(0, P.Hp)}");

        if (!P.JawBroken && Rules.Chance(5 + F.Str))
        {
            if (P.Guard && Rules.Chance(75)) Say("^2Защита спасла твои кривые клыки.");
            else { P.JawBroken = true; Say("^4Враг сломал тебе челюсть."); }
        }
        else if (!P.LegBroken && Rules.Chance(3 + F.Str / 2))
        {
            P.LegBroken = true;
            Say("^4Враг сломал тебе ногу.");
        }
    }

    /// <summary>The other guy answers, the lads may arrive, and the round ticks over.</summary>
    private void CloseRound()
    {
        if (CheckDead()) return;

        FoeSwing();
        if (F.Agi > P.EAgi && Rules.ExtraSwing(F.Agi, P.EAgi))
        {
            Say("^4Из-за большой ловкости враг может пнуть ещё раз");
            FoeSwing();
        }
        if (CheckDead()) return;

        if (_backupIn > 0)
        {
            _backupIn--;
            if (_backupIn == 0)
            {
                BackupHere = true;
                Say("^2Они уже здесь.");
            }
        }
        if (BackupHere)
        {
            int d = Rules.Roll(3, 9);
            F.Hp -= d;
            Say($"^2Врага отпинали на {d}з. У него осталось {Math.Max(0, F.Hp)}");
            if (CheckDead()) return;
        }

        if (P.Stoned > 0 && --P.Stoned == 0)
        {
            P.Str = Math.Max(1, P.Str - 2);
            Say("^6Глюки прошли. Сила -2.");
        }

        Round++;
        if (Round % 2 == 0) NextSpectator();
    }

    private bool CheckDead()
    {
        if (F.Hp <= 0) { F.Hp = 0; Say("^2Враг сдох."); End = FightEnd.Won; return true; }
        if (P.Hp <= 0) { P.Hp = 0; End = FightEnd.Lost; return true; }
        return false;
    }

    // ---- spoils -----------------------------------------------------------------

    /// <summary>Applies the winnings and returns the lines to print.</summary>
    public List<string> Spoils()
    {
        var outp = new List<string>();
        int xp = Rules.XpFor(P, F);
        if (xp == 0)
        {
            outp.Add("^6Ты запинал слишком слабого мудака для увеличения понтовости");
        }
        else
        {
            P.Xp += xp;
            outp.Add($"^6За отпин врага ты получаешь {xp} качков опыта");
            int rep = Math.Max(1, (F.Level - P.Level + 3));
            P.AddRep(rep);
            outp.Add($"^2Ты отпинал этого мудака - пацаны этого незабудут. Понтовость улутшилась на {rep}.");
        }

        P.Money += F.Money;
        outp.Add($"^EОпа бабки! {F.Money} рублей на пиво!");
        if (Rules.Chance(35)) { P.Junk++; outp.Add("^7Прихватил с него какой-то хлам."); }

        outp.AddRange(Loot());

        while (P.Xp >= P.XpToLevel)
        {
            outp.Add("^1Понтовость увеличивается:");
            outp.AddRange(Rules.LevelUp(P));
            outp.Add($"^6Теперь ты {P.Level} уровня.");
        }
        return outp;
    }

    private IEnumerable<string> Loot()
    {
        var got = new List<string>();
        int luck = P.ELuck;

        if (!P.Cross && Rules.Chance(3 + luck)) { P.Cross = true; got.Add("^1Ты нашёл крестик: удача +2"); }
        else if (!P.RingLuck && Rules.Chance(3 + luck)) { P.RingLuck = true; got.Add("^1Ты нашёл кольцо \"Господи спаси\": удача +1"); }
        else if (!P.Mobile && Rules.Chance(4 + luck)) { P.Mobile = true; got.Add("^1Ты нашёл мобилу"); }
        else if (!P.Shades && Rules.Chance(4 + luck)) { P.Shades = true; got.Add("^1Ты нашёл тёмные очки."); }

        if (F.Index == 2 && Rules.Chance(45)) { P.Joints++; got.Add("^1А у нарка был косячок"); }

        int wep = F.WeaponBonus switch { >= 9 => 4, >= 6 => 3, >= 4 => 2, >= 2 => 1, _ => 0 };
        if (wep > 0 && Rules.Chance(30 + luck))
        {
            if (wep > P.Weapon)
            {
                P.Weapon = wep;
                got.Add($"^1Ты отобрал у врага: {Data.Weapons[wep].Name}(урон+{Data.Weapons[wep].Bonus})");
            }
            else if (Rules.Chance(50))
            {
                P.Junk++;
                got.Add("^6Но у тебя есть более мощное оружие - забрал как хлам.");
            }
        }
        return got;
    }
}
