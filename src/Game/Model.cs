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

    /// <summary>
    /// What you own per slot, one bit per rung of the ladder, as opposed to what you are
    /// wearing. The original let gear pile up - that is what its "продать ненужные вещи"
    /// counter was for, and it named the pieces one by one - so an upgrade cannot simply
    /// overwrite the old one. Rung zero (bare fists, own rags) is free and never tracked.
    /// </summary>
    public int WeaponsOwned, BootsOwned, SuitsOwned, JacketsOwned;

    public void Own(ref int mask, int idx) { if (idx > 0) mask |= 1 << idx; }
    public static bool Owns(int mask, int idx) => idx > 0 && (mask & (1 << idx)) != 0;
    public int Press;               // abs training -> armour

    public bool Cross, RingLuck, RingAll, MegaRing, RingHeal;   // "феньки"
    public bool Mobile, Shades, Tattoo, Pistol, Silencer, Guard;

    /// <summary>
    /// Heat at the market after a lifted wallet went wrong. The original locked you out -
    /// "На базар пока нельзя там менты бродят, тебя ищут" - and lifted it with a phone
    /// call telling you the police had gone.
    /// </summary>
    public bool BazarClosed;

    public bool JawBroken, LegBroken;
    public int Stoned;              // turns left while high

    public int DistrictIdx;
    public readonly bool[,] Found = new bool[Data.Districts.Length, 8];
    public bool GirlKnown;
    public int Knockouts;           // how many times you have been put on the ground

    /// <summary>Abs training is capped at half your level - see the note in Rules.Soak.</summary>
    public int PressCap => Rules.Classic ? Level : (Level + 1) / 2;

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
    public int DamageMin => Math.Max(1, EStr / 2 + (Rules.Classic ? 0 : 1) + WeaponBonus);
    public int DamageMax => Math.Max(DamageMin, EStr + (Rules.Classic ? 0 : 1) + WeaponBonus);
    public int Armour => Data.Suits[SuitIdx].Bonus + Data.Jackets[JacketIdx].Bonus + Press;

    /// <summary>
    /// Base to-hit. Classic is the original help screen verbatim - "(20 + Ловкость×5)%" with
    /// the 90% ceiling - which at a starting agility of three is 35%, so both sides miss two
    /// swings in three and the opening fight runs long.
    /// Otherwise: a flat base with a shallow slope. At 5% per point the spread between a
    /// clumsy build (38%) and an agile one (80%) was so wide that agility was the only stat
    /// worth having; at 2% every build can land a punch and agility is an edge, not the game.
    /// </summary>
    public int Accuracy => Rules.Classic
        ? Math.Clamp(20 + EAgi * 5, 5, 90)
        : Math.Clamp(48 + EAgi * 2, 30, 72);

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
    /// Play by the 2003 arithmetic instead of the reworked one.
    ///
    /// Everything this flag switches is marked "Classic" at the formula itself. Two grades
    /// of fidelity live under the one name, and it matters which is which:
    ///
    /// Restored verbatim from the original help screen - accuracy "(20 + Ловкость×5)%",
    /// damage "Сила/2 .. Сила", "+5% попадания если ловкость больше", and health
    /// "10 + Живучесть×5 + Сила" applied to everyone rather than to the player alone.
    ///
    /// Reconstructed, because the original never printed a number for it - armour as plain
    /// subtraction, enemy stats scaled by level rather than by a flat step, an uncapped
    /// press, and no elites, no forced fights, no free first knockout. These are undone
    /// because they are demonstrably ours, not because 2003 is known to have done otherwise.
    /// </summary>
    public static bool Classic;

    /// <summary>
    /// Whether the classic set also gives enemies the player's health formula.
    ///
    /// Worth its own switch because it is the weakest link in the whole restoration: the
    /// formula is the original's verbatim, but the enemy stat table it feeds on is a
    /// reconstruction of relative power, not recovered numbers. A Дохляк with a stamina of
    /// one comes out at seventeen health, which is what turns an opening fight into twenty
    /// rounds. Everything else under Classic can stand while this one is reconsidered.
    /// </summary>
    public static bool ClassicFoeHp = true;

    /// <summary>The three sets as one number, for the title screen to cycle through.</summary>
    public static int RuleSet
    {
        get => !Classic ? 2 : ClassicFoeHp ? 0 : 1;
        set { Classic = value != 2; ClassicFoeHp = value == 0; }
    }

    public static readonly string[] RuleSetNames =
    {
        "оригинал 2003",
        "оригинал, бои короче",
        "переделанные 2026",
    };

    /// <summary>One line each, in the terms a player can feel rather than audit.</summary>
    public static readonly string[] RuleSetNotes =
    {
        "вся арифметика 2003-го. Дерёшься долго, мажешь часто, дохнешь легко.",
        "то же самое, но враги не такие живучие - бой вдвое короче. Так ровнее всего.",
        "наш пересчёт: бои быстрые, промахов мало, ловкость решает.",
    };

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
        => Classic
            ? Math.Clamp(accuracy + (myAgi > hisAgi ? 5 : 0), 5, 90)
            : Math.Clamp(accuracy + Math.Clamp((myAgi - hisAgi) * 5, -20, 20), 15, 90);

    /// <summary>
    /// A second swing when you clearly out-move your opponent. Capped low on purpose:
    /// accuracy already multiplies damage, and a second swing multiplies it again, so an
    /// uncapped bonus makes agility the only stat worth buying. Used outside Classic;
    /// Classic has the original's own rule in <see cref="Strikes"/>.
    /// </summary>
    public static bool ExtraSwing(int myAgi, int hisAgi)
        => myAgi > hisAgi && Chance(Math.Min(22, (myAgi - hisAgi) * 7));

    /// <summary>
    /// How many times you swing in a round, and the odds of one more on top.
    ///
    /// Read straight out of the 2003 code at 0x15A4: once agility passes fourteen - the
    /// point where "(20+Ловкость*5)%" reaches the printed 90% ceiling and stops paying -
    /// the surplus starts buying swings instead. The surplus is counted down in blocks of
    /// eighteen, each block worth a whole extra swing, and whatever is left over is the
    /// percentage chance of one more. Below fifteen agility there are no extra swings at
    /// all, which is why the original's inspect screen only ever showed "Второй удар #%"
    /// on a nimble character.
    ///
    /// The one part not in the code: how much the other side's agility takes off. The
    /// original printed "Из-за хорошей ловкости врага ты сможешь пнуть его раз # вместо #",
    /// so a reduction existed; its size is reconstructed as a single swing.
    /// </summary>
    public static (int Count, int ExtraChance) Strikes(int myAgi, int hisAgi)
    {
        if (!Classic || myAgi <= 14) return (1, 0);

        int surplus = myAgi - 14;
        int count = 1;
        while (surplus > 18) { surplus -= 18; count++; }

        if (hisAgi > myAgi && count > 1) count--;
        return (count, surplus);
    }

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
        => Classic
            ? Math.Max(1, damage - armour)
            : Math.Max(Math.Max(1, (int)Math.Ceiling(damage * 0.34)), damage - armour);

    /// <summary>
    /// Strength shoulders through armour. Without this, agility was the only stat that kept
    /// paying off late, because accuracy multiplies every point of damage while raw strength
    /// just got eaten by the other guy's jacket. Not something the original printed, so
    /// classic leaves armour whole.
    /// </summary>
    public static int Pierce(int armour, int str)
        => Classic ? armour : Math.Max(0, armour - str / 4);

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

        // Classic scales each stat in proportion to the enemy's own base, so the heavy
        // types pull away as the levels climb. The rate is calibrated, not recovered: 0.133
        // makes an average base (~4.5) grow at the same 0.6 a level the additive step uses,
        // which puts the two side by side in the middle of the game and lets the ends differ.
        int Scale(int b) => Classic
            ? Math.Max(1, (int)Math.Round(b * (1 + (level - 1) * 0.133)))
            : Math.Max(1, b + step);

        return new Foe
        {
            Index = foeIdx,
            Name = t.Name,
            Level = level,
            Str = Scale(t.Str),
            Agi = Scale(t.Agi),
            Vit = Scale(t.Vit),
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
    /// Classic gives everyone the one health formula the original printed. Otherwise this is
    /// deliberately NOT the player's formula: street trash with the player's health pool
    /// turns every encounter into a war of attrition, and these are people you are meant to
    /// drop in a handful of good kicks.
    /// </summary>
    public int MaxHp => (int)Math.Round(
        (Rules.Classic && Rules.ClassicFoeHp ? 10 + Vit * 5 + Str : 3 + Vit * 2 + Str) * BossHp);
    public int DamageMin => Math.Max(1, Str / 2 + WeaponBonus);
    public int DamageMax => Math.Max(DamageMin, Str + WeaponBonus);

    /// <summary>
    /// Classic is the same "(20 + Ловкость×5)%" the player gets - the original had one
    /// accuracy formula. Otherwise a shade below the player's, so a competent character
    /// keeps the edge.
    /// </summary>
    public int Accuracy => Rules.Classic
        ? Math.Clamp(20 + Agi * 5, 5, 90)
        : Math.Clamp(42 + Agi * 2, 20, 70);

    public void Reset() => Hp = MaxHp;
}
