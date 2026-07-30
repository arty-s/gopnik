namespace Gopnik.Game;

/// <summary>Everything the player carries between turns.</summary>
public sealed class Player
{
    public string Name = "Раздолбай";
    public Klass Klass;
    public int Level = 1;

    // Base stats. The original starts you with twelve points across the four.
    public int Str = 3, Agi = 3, Vit = 3, Luck = 3;

    public int Hp;
    public int Xp;
    public int Rep;                 // "понтовость" 0..126, drives the 43 rank titles
    public int Money = 10;
    public int Junk;                // "хлам" - loot to fence at the dealers
    public int Joints;              // "косяки"
    public double Beer;             // litres, the original tracked tenths
    public int Ammo;

    public int Weapon, BootsIdx, SuitIdx, JacketIdx;
    public int Press;               // abs training -> armour

    public bool Cross, RingLuck, RingAll, MegaRing, RingHeal;   // "феньки"
    public bool Mobile, Shades, Tattoo, Pistol, Silencer, Guard;

    public bool JawBroken, LegBroken;
    public int Stoned;              // turns left while high

    public int DistrictIdx;
    public readonly bool[,] Found = new bool[Data.Districts.Length, 8];
    public bool GirlKnown;
    public int Knockouts;           // how many times you have been put on the ground

    /// <summary>Abs training is capped at half your level - see the note in Rules.Soak.</summary>
    public int PressCap => (Level + 1) / 2;

    // ---- trinket-adjusted stats ------------------------------------------------
    private int Bonus => (RingAll ? 1 : 0) + (MegaRing ? 4 : 0);
    public int EStr => Str + Bonus;
    public int EAgi => Agi + Bonus;
    public int EVit => Vit + Bonus;
    public int ELuck => Luck + Bonus + (Cross ? 2 : 0) + (RingLuck ? 1 : 0);

    public int MaxHp => 10 + EVit * 5 + EStr;
    public int WeaponBonus => Data.Weapons[Weapon].Bonus + Data.Boots[BootsIdx].Bonus;

    // The original's "Урон = Сила/2 .. Сила" gives 1-3 at a starting strength of three,
    // which against even the weakest enemy meant a dozen rounds. A flat point on both
    // ends fixes the opening without mattering at all by the endgame.
    public int DamageMin => Math.Max(1, EStr / 2 + 1 + WeaponBonus);
    public int DamageMax => Math.Max(DamageMin, EStr + 1 + WeaponBonus);
    public int Armour => Data.Suits[SuitIdx].Bonus + Data.Jackets[JacketIdx].Bonus + Press;

    /// <summary>
    /// Base to-hit. The original printed "(20 + Ловкость×5)%", which at a starting agility
    /// of three is 35% - and with both sides missing two swings in three, an opening fight
    /// ran past twenty rounds of nothing. The base is lifted to 30; the ×5 per point and the
    /// 90% ceiling are the original's.
    /// </summary>
    /// <summary>
    /// A flat base with a shallow slope. At 5% per point the spread between a clumsy build
    /// (38%) and an agile one (80%) was so wide that agility was the only stat worth having;
    /// at 3% every build can land a punch and agility is an edge rather than the whole game.
    /// </summary>
    public int Accuracy => Math.Clamp(48 + EAgi * 2, 30, 72);

    public int XpToLevel => 60 + (Level - 1) * 45;
    public string Rank => Data.Ranks[Math.Clamp(Rep / 3, 0, Data.Ranks.Length - 1)];
    public Data.District District => Data.Districts[DistrictIdx];

    public bool Knows(Place p) => p == Place.Street || Found[DistrictIdx, (int)p];
    public void Discover(Place p) => Found[DistrictIdx, (int)p] = true;

    public void Heal(int n) => Hp = Math.Min(MaxHp, Hp + n);
    public void AddRep(int n) => Rep = Math.Clamp(Rep + n, 0, 126);
}

/// <summary>
/// The rules, kept in one place so the numbers stay auditable against the original.
/// Every formula below is the one printed in the 2003 help screen; only the parts the
/// original never explained (crit odds, break odds, flee odds) are new.
/// </summary>
public static class Rules
{
    /// <summary>
    /// Reseedable so the balance harness can replay an identical run. The game itself
    /// never touches <see cref="Reseed"/>.
    /// </summary>
    public static Random Rng { get; private set; } = new();

    public static void Reseed(int seed) => Rng = new Random(seed);

    public static int Roll(int lo, int hi) => Rng.Next(lo, hi + 1);
    public static bool Chance(int percent) => Rng.Next(100) < percent;

    /// <summary>
    /// Help screen: "Ловкость - +5% попадания если больше" than the other guy's. The
    /// opponent's agility is a modifier on that comparison, not a flat subtraction -
    /// subtracting it outright turned early fights into a dozen rounds of whiffing.
    /// The 90% ceiling is the original's own ("Точность 90%").
    /// </summary>
    public static int HitChance(int accuracy, int myAgi, int hisAgi)
        => Math.Clamp(accuracy + Math.Clamp((myAgi - hisAgi) * 5, -20, 20), 15, 90);

    /// <summary>
    /// A second swing when you clearly out-move your opponent. Capped low on purpose:
    /// accuracy already multiplies damage, and a second swing multiplies it again, so an
    /// uncapped bonus makes agility the only stat worth buying.
    /// </summary>
    public static bool ExtraSwing(int myAgi, int hisAgi)
        => myAgi > hisAgi && Chance(Math.Min(22, (myAgi - hisAgi) * 7));

    /// <summary>Luck's only direct combat payoff, so it has to be worth the points.</summary>
    public static int CritChance(int luck) => Math.Min(35, 5 + luck * 3);

    /// <summary>
    /// Armour soaks flat damage, but a third of every blow always gets through.
    /// Pure subtraction is what made the late game an autopilot: by level ten a player
    /// carried more armour than an enemy could roll, so every hit landed for exactly one
    /// point and beer, joints and the brothers stopped mattering. The floor keeps armour
    /// strongly worth buying without ever making you untouchable.
    /// </summary>
    public static int Soak(int damage, int armour)
        => Math.Max(Math.Max(1, (int)Math.Ceiling(damage * 0.34)), damage - armour);

    public static int FleeChance(bool legBroken, int myAgi, int hisAgi)
        => legBroken ? 0 : Math.Clamp(45 + (myAgi - hisAgi) * 8, 10, 90);

    /// <summary>
    /// Level-up. Straight from the original: "# из 12 шансов что увеличится" - a stat's
    /// own value is its chance out of twelve, so specialists get more specialised.
    /// </summary>
    public static List<string> LevelUp(Player p)
    {
        p.Level++;
        p.Xp -= p.XpToLevel;
        if (p.Xp < 0) p.Xp = 0;

        var grew = new List<string>();
        if (Rng.Next(12) < p.Str) { p.Str++; grew.Add("^1Сила +1"); }
        if (Rng.Next(12) < p.Agi) { p.Agi++; grew.Add("^1Ловкость +1"); }
        if (Rng.Next(12) < p.Vit) { p.Vit++; grew.Add("^1Живучесть +1"); }
        if (Rng.Next(12) < p.Luck) { p.Luck++; grew.Add("^1Удача +1"); }
        p.Hp = p.MaxHp;
        return grew;
    }

    /// <summary>
    /// Builds an opponent scaled to the district and to the player's level.
    /// Scaling is additive, not multiplicative: the player's own stats grow by about one
    /// point per level, so multiplying an enemy's base by the level made the high-base
    /// ones (Мент, Маньячок, Ректор) run away from the player and become unbeatable.
    /// </summary>
    public static Foe MakeFoe(Player p, int foeIdx, int level)
    {
        var t = Data.Foes[foeIdx];
        int step = (int)Math.Round((level - 1) * 0.6);
        return new Foe
        {
            Index = foeIdx,
            Name = t.Name,
            Level = level,
            Str = Math.Max(1, t.Str + step),
            Agi = Math.Max(1, t.Agi + step),
            Vit = Math.Max(1, t.Vit + step),
            Armour = t.Armour + level / 5,
            WeaponBonus = t.Weapon,
            Money = t.Money + Roll(0, level * 3),
            Greeting = t.Greeting,
            // The last man standing is a boss, and a boss needs a health pool you have to
            // work through rather than stats that simply outclass you.
            BossHp = foeIdx == Data.Foes.Length - 1 ? 1.5 : 1.0,
        };
    }

    public static int SpawnLevel(Player p)
        => Math.Max(1, p.Level + Roll(-1, 1) + (p.DistrictIdx > 0 ? 1 : 0));

    /// <summary>Experience for a kill - nothing for beating up somebody far weaker.</summary>
    public static int XpFor(Player p, Foe f)
    {
        int gap = f.Level - p.Level;
        if (gap < -2) return 0;
        return Math.Max(1, (int)((12 + f.Level * 6) * (1.0 + 0.15 * gap)));
    }
}

/// <summary>A live opponent in a fight.</summary>
public sealed class Foe
{
    public int Index;
    public string Name = "";
    public int Level;
    public int Str, Agi, Vit, Armour, WeaponBonus, Money;
    public string Greeting = "";
    public int Hp;
    public bool JawBroken, LegBroken;
    public double BossHp = 1.0;

    /// <summary>
    /// Deliberately NOT the player's formula. Street trash with the player's health pool
    /// turns every encounter into a war of attrition; these are people you drop in a
    /// handful of good kicks.
    /// </summary>
    public int MaxHp => (int)Math.Round((3 + Vit * 2 + Str) * BossHp);
    public int DamageMin => Math.Max(1, Str / 2 + WeaponBonus);
    public int DamageMax => Math.Max(DamageMin, Str + WeaponBonus);

    /// <summary>A shade below the player's, so a competent character keeps the edge.</summary>
    public int Accuracy => Math.Clamp(42 + Agi * 2, 20, 70);

    public void Reset() => Hp = MaxHp;
}
