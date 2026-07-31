using System.Globalization;
using System.Text;
using Gopnik.Game;

namespace Gopnik.Sim;

/// <summary>
/// A build the harness will play: a class plus a starting split of the twelve points.
/// "Focus" is what the bot spends gym money on later, so a build stays true to itself.
/// </summary>
public sealed record Build(string Name, Klass Klass, int Str, int Agi, int Vit, int Luck, char Focus);

/// <summary>Outcome of one complete playthrough.</summary>
public sealed class RunResult
{
    public bool Victory, Dead, Stalled;
    public int Turns, Fights, Wins, Flights, Knockouts;
    public int Rounds, MySwings, MyHits, HisSwings, HisHits, DealtTotal, TakenTotal;
    public int Level, Rep, District, Money, Deaths;
    public readonly int[] TurnsInDistrict = new int[Data.Districts.Length];
    public readonly int[] LevelAtEntry = new int[Data.Districts.Length];

    // The power curve: what a fight costs at each character level.
    public const int MaxLevel = 20;
    public readonly int[] FightsAt = new int[MaxLevel];
    public readonly int[] RoundsAt = new int[MaxLevel];
    public readonly double[] HpLostAt = new double[MaxLevel];
    public readonly int[] LostAt = new int[MaxLevel];
    public readonly int[] DmgDealtAt = new int[MaxLevel];
    public readonly int[] FoeHpAt = new int[MaxLevel];

    // Ordinary street trash versus the ones worth looking at first.
    public int NormFights, NormLost, NormRounds;
    public double NormCost;
    public int EliteFights, EliteLost, EliteRounds, EliteFled;
    public double EliteCost;
}

public static class Program
{
    private const int TurnCap = 4000;
    private const int RoundCap = 300;

    private static readonly Build[] Builds =
    {
        new Build("Сила",          Klass.Otmoroz, 6, 2, 3, 1, 'S'),
        new Build("Ловкость",      Klass.Pacan,   2, 6, 3, 1, 'A'),
        new Build("Живучесть",     Klass.Gopnik,  2, 2, 7, 1, 'V'),
        new Build("Удача",         Klass.Vor,     2, 2, 2, 6, 'L'),
        new Build("Ровный",        Klass.Gopnik,  3, 3, 3, 3, 'S'),
        new Build("Сила+Ловкость", Klass.Otmoroz, 5, 5, 1, 1, 'S'),
        new Build("Танк",          Klass.Gopnik,  4, 1, 6, 1, 'V'),
    };

    public static void Main(string[] args)
    {
        int runs = 400;
        foreach (var a in args) if (int.TryParse(a, out var n)) runs = n;

        // Every rule set on the same seeds and the same bot - the arithmetic is the only
        // thing that differs. The middle one isolates enemy health, which is the single
        // decision that separates a long classic fight from a short one.
        var modes = new List<(string Label, bool Classic, bool FoeHp)>
        {
            ("оригинал", true, true),
            ("оригинал, здоровье врагов нынешнее", true, false),
            ("текущий", false, false),
        };
        if (args.Contains("--classic")) modes.RemoveAll(m => !m.Classic);
        if (args.Contains("--modern")) modes.RemoveAll(m => m.Classic);

        var report = new StringBuilder();
        void Both(string line) { Console.WriteLine(line); report.AppendLine(line); }

        Both($"# Баланс «Гопника» - {runs} забегов на билд");
        Both("");

        var heads = new List<(string Label, List<(Build B, List<RunResult> R)> Data)>();
        foreach (var (label, classic, foeHp) in modes)
        {
            Rules.Classic = classic;
            Rules.ClassicFoeHp = foeHp;
            Both($"# Свод правил: {label}");
            Both("");
            heads.Add((label, Section(Both, runs)));
        }

        if (heads.Count > 1) HeadToHead(Both, heads);
        DiscoveryProbe(Both, 4000);

        var path = Path.Combine(AppContext.BaseDirectory, "balance-report.md");
        File.WriteAllText(path, report.ToString());
        Console.WriteLine();
        Console.WriteLine($"отчёт: {path}");
    }

    /// <summary>
    /// How long a district stays a blank map. Finding the places is the original's mechanic,
    /// but the rate is ours, and the rate is what decides whether the opening reads as
    /// exploring or as being locked out. Rule-set independent - wandering does not care.
    /// </summary>
    private static void DiscoveryProbe(Action<string> Both, int trials)
    {
        const int Horizon = 120;
        var places = Data.Places;
        var sum = new long[places.Length];
        var miss = new int[places.Length];
        var knownBy = new long[4];
        int[] marks = { 10, 20, 30, 50 };

        for (int t = 0; t < trials; t++)
        {
            Rules.Reseed(90210 + t);
            var w = new World();
            var p = w.P;
            p.Klass = Klass.Otmoroz;          // the one class that is given nothing for free
            p.Hp = p.MaxHp;

            var at = new int[places.Length];
            Array.Fill(at, -1);

            for (int turn = 1; turn <= Horizon; turn++)
            {
                w.Wander();
                w.Foe = null;                  // the probe measures walking, not fighting
                for (int i = 0; i < places.Length; i++)
                    if (at[i] < 0 && p.Knows(places[i].P)) at[i] = turn;

                for (int m = 0; m < marks.Length; m++)
                    if (turn == marks[m]) knownBy[m] += at.Count(x => x >= 0);
            }

            for (int i = 0; i < places.Length; i++)
            {
                if (at[i] < 0) miss[i]++; else sum[i] += at[i];
            }
        }

        Both("");
        Both("# Как быстро находятся заведения");
        Both("");
        Both($"Чистое шатание в первом районе, отморозок (ему ничего не дают даром), {trials} прогонов.");
        Both("");
        Both("| заведение | ходов до находки | не найдено за " + Horizon + " ходов |");
        Both("|---|---:|---:|");
        for (int i = 0; i < places.Length; i++)
        {
            int hits = trials - miss[i];
            string avg = hits == 0 ? "-" : $"{sum[i] / (double)hits:0}";
            Both($"| {places[i].Name} | {avg} | {miss[i] * 100.0 / trials:0}% |");
        }

        Both("");
        Both("| ходов прошатался | заведений известно из " + places.Length + " |");
        Both("|---:|---:|");
        for (int m = 0; m < marks.Length; m++)
            Both($"| {marks[m]} | {knownBy[m] / (double)trials:0.0} |");
    }

    /// <summary>Side-by-side on the numbers that decide whether a run is winnable.</summary>
    private static void HeadToHead(Action<string> Both,
        List<(string Label, List<(Build B, List<RunResult> R)> Data)> heads)
    {
        Both("");
        Both("# Свод правил против свода правил");
        Both("");
        Both("| билд | " + string.Join(" | ", heads.SelectMany(h =>
            new[] { $"побед, {h.Label}", $"раундов/бой, {h.Label}" })) + " |");
        Both("|---|" + string.Concat(Enumerable.Repeat("---:|", heads.Count * 2)));

        for (int i = 0; i < Builds.Length; i++)
        {
            var cells = new List<string>();
            foreach (var h in heads)
            {
                var rs = h.Data[i].R;
                cells.Add($"{rs.Count(r => r.Victory) * 100.0 / rs.Count:0.0}%");
                cells.Add($"{Avg(rs.Sum(r => (long)r.Rounds), rs.Sum(r => (long)r.Fights)):0.0}");
            }
            Both($"| {Builds[i].Name} | {string.Join(" | ", cells)} |");
        }

        Both("");
        Both("| | " + string.Join(" | ", heads.Select(h => h.Label)) + " |");
        Both("|---|" + string.Concat(Enumerable.Repeat("---:|", heads.Count)));
        string Row(string name, Func<List<RunResult>, double> f, string fmt) =>
            $"| {name} | " + string.Join(" | ", heads.Select(h =>
                f(h.Data.SelectMany(d => d.R).ToList()).ToString(fmt, CultureInfo.InvariantCulture))) + " |";

        Both(Row("побед, все билды", r => r.Count(x => x.Victory) * 100.0 / r.Count, "0.0"));
        Both(Row("нокаутов за забег", r => r.Average(x => (double)x.Knockouts), "0.00"));
        Both(Row("смертей за забег", r => r.Average(x => (double)x.Deaths), "0.00"));
        Both(Row("раундов на бой", r => Avg(r.Sum(x => (long)x.Rounds), r.Sum(x => (long)x.Fights)), "0.0"));
        Both(Row("боёв за забег", r => r.Average(x => (double)x.Fights), "0"));
        Both(Row("ходов за забег", r => r.Average(x => (double)x.Turns), "0"));
        Both(Row("точность игрока %", r => Avg(r.Sum(x => (long)x.MyHits), r.Sum(x => (long)x.MySwings)) * 100, "0"));
        Both(Row("точность врага %", r => Avg(r.Sum(x => (long)x.HisHits), r.Sum(x => (long)x.HisSwings)) * 100, "0"));
        Both(Row("зависших забегов %", r => r.Count(x => x.Stalled) * 100.0 / r.Count, "0.0"));
    }

    /// <summary>The full report for whichever rule set is currently switched on.</summary>
    private static List<(Build B, List<RunResult> R)> Section(Action<string> Both, int runs)
    {
        var builds = Builds;

        Both("| билд | класс | побед | смертей | ходов | боёв | раундов/бой | точн. я | точн. враг | ур. финал | понты |");
        Both("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");

        var all = new List<(Build B, List<RunResult> R)>();

        foreach (var b in builds)
        {
            var results = new List<RunResult>(runs);
            for (int i = 0; i < runs; i++) results.Add(Play(b, i * 7919 + 13));
            all.Add((b, results));

            double win = results.Count(r => r.Victory) * 100.0 / runs;
            double dead = results.Average(r => r.Deaths);
            double turns = results.Average(r => r.Turns);
            double fights = results.Average(r => r.Fights);
            double rounds = Avg(results.Sum(r => (long)r.Rounds), results.Sum(r => (long)r.Fights));
            double myAcc = Avg(results.Sum(r => (long)r.MyHits), results.Sum(r => (long)r.MySwings)) * 100;
            double hisAcc = Avg(results.Sum(r => (long)r.HisHits), results.Sum(r => (long)r.HisSwings)) * 100;
            double lvl = results.Average(r => r.Level);
            double rep = results.Average(r => r.Rep);

            Both($"| {b.Name} | {Data.Classes[(int)b.Klass].Name} | {win,5:0.0}% | {dead,5:0.00} | " +
                 $"{turns,5:0} | {fights,4:0} | {rounds,5:0.0} | {myAcc,4:0}% | {hisAcc,4:0}% | {lvl,4:0.0} | {rep,4:0} |");
        }

        // ---- progression pacing -------------------------------------------------
        Both("");
        Both("## Темп по районам (усреднённые ходы внутри района / уровень на входе)");
        Both("");
        Both("| билд | " + string.Join(" | ", Data.Districts.Select(d => d.Name)) + " |");
        Both("|---|" + string.Concat(Enumerable.Repeat("---:|", Data.Districts.Length)));
        foreach (var (b, rs) in all)
        {
            var cells = new List<string>();
            for (int d = 0; d < Data.Districts.Length; d++)
            {
                var seen = rs.Where(r => r.LevelAtEntry[d] > 0).ToList();
                if (seen.Count == 0) { cells.Add("-"); continue; }
                cells.Add($"{seen.Average(r => r.TurnsInDistrict[d]):0} х / ур.{seen.Average(r => r.LevelAtEntry[d]):0.0}");
            }
            Both($"| {b.Name} | {string.Join(" | ", cells)} |");
        }

        // ---- how far runs actually get ------------------------------------------
        Both("");
        Both("## Докуда доходят (доля забегов, добравшихся до района)");
        Both("");
        Both("| билд | " + string.Join(" | ", Data.Districts.Select(d => d.Name)) + " | завис |");
        Both("|---|" + string.Concat(Enumerable.Repeat("---:|", Data.Districts.Length + 1)));
        foreach (var (b, rs) in all)
        {
            var cells = new List<string>();
            for (int d = 0; d < Data.Districts.Length; d++)
                cells.Add($"{rs.Count(r => r.District >= d) * 100.0 / rs.Count:0}%");
            Both($"| {b.Name} | {string.Join(" | ", cells)} | {rs.Count(r => r.Stalled) * 100.0 / rs.Count:0}% |");
        }

        // ---- per-foe kill times --------------------------------------------------
        Both("");
        Both("## Дуэль с каждым врагом на том уровне, где его реально встречают");
        Both("");
        Both("| враг | уровень встречи | здоровье | раундов | шанс победить |");
        Both("|---|---:|---:|---:|---:|");
        for (int fi = 0; fi < Data.Foes.Length; fi++)
        {
            var (hp, rounds, win, lvl) = DuelProbe(fi, 800);
            Both($"| {Data.Foes[fi].Name} | {lvl} | {hp:0} | {rounds:0.0} | {win:0}% |");
        }

        // ---- the power curve ----------------------------------------------------
        Both("");
        Both("## Кривая силы: во что обходится драка на каждом уровне");
        Both("");
        Both("Все билды вместе. «Цена» - какую долю своего здоровья игрок теряет за бой.");
        Both("");
        Both("| уровень | боёв | раундов | цена боя | проиграно | здоровье врага | урон игрока за бой |");
        Both("|---:|---:|---:|---:|---:|---:|---:|");
        var flat = all.SelectMany(x => x.R).ToList();
        for (int L = 1; L < RunResult.MaxLevel; L++)
        {
            long fights = flat.Sum(r => (long)r.FightsAt[L]);
            if (fights < 50) continue;
            double rounds = flat.Sum(r => (long)r.RoundsAt[L]) / (double)fights;
            double cost = flat.Sum(r => r.HpLostAt[L]) / fights;
            double lost = flat.Sum(r => (long)r.LostAt[L]) * 100.0 / fights;
            double fhp = flat.Sum(r => (long)r.FoeHpAt[L]) / (double)fights;
            double dealt = flat.Sum(r => (long)r.DmgDealtAt[L]) / (double)fights;
            Both($"| {L} | {fights} | {rounds,5:0.0} | {cost * 100,5:0}% | {lost,4:0.0}% | {fhp,5:0} | {dealt,5:0} |");
        }

        // ---- where the danger actually lives -------------------------------------
        long nf = flat.Sum(r => (long)r.NormFights), ef = flat.Sum(r => (long)r.EliteFights);
        Both("");
        Both("## Шпана против матёрых");
        Both("");
        if (ef == 0)
        {
            // Classic has no elites at all, so the split has nothing to say.
            Both("Матёрых в этом своде правил нет - все встречи обычные.");
        }
        else
        {
            Both("| | доля встреч | раундов | цена боя | проиграно | сбежал |");
            Both("|---|---:|---:|---:|---:|---:|");
            Both($"| обычная шпана | {nf * 100.0 / (nf + ef):0}% | {flat.Sum(r => (long)r.NormRounds) / (double)nf:0.0} | " +
                 $"{flat.Sum(r => r.NormCost) / nf * 100:0}% | {flat.Sum(r => (long)r.NormLost) * 100.0 / nf:0.0}% | - |");
            Both($"| матёрые | {ef * 100.0 / (nf + ef):0}% | {flat.Sum(r => (long)r.EliteRounds) / (double)ef:0.0} | " +
                 $"{flat.Sum(r => r.EliteCost) / ef * 100:0}% | {flat.Sum(r => (long)r.EliteLost) * 100.0 / ef:0.0}% | " +
                 $"{flat.Sum(r => (long)r.EliteFled) * 100.0 / ef:0.0}% |");
        }

        Both("");
        return all;
    }

    private static double Avg(long num, long den) => den == 0 ? 0 : num / (double)den;

    // ---------------------------------------------------------------- one playthrough
    private static RunResult Play(Build b, int seed)
    {
        Rules.Reseed(seed);
        var res = new RunResult();
        var w = new World();
        var p = w.P;

        p.Klass = b.Klass;
        p.Str = b.Str; p.Agi = b.Agi; p.Vit = b.Vit; p.Luck = b.Luck;
        if (p.Klass == Klass.Pacan) p.GirlKnown = true;
        if (p.Klass == Klass.Gopnik) p.Discover(Place.Priton);
        if (p.Klass == Klass.Vor) p.Discover(Place.Barygi);
        p.Hp = p.MaxHp;
        res.LevelAtEntry[0] = 1;

        while (res.Turns < TurnCap)
        {
            res.Turns++;
            res.TurnsInDistrict[p.DistrictIdx]++;

            if (Manage(w, b)) continue;

            // Only push on once patched up - walking into the next district at half health
            // is how a run ends, and a real player would not do it.
            if (w.CanTravel && p.Hp >= p.MaxHp * 0.9 && !p.JawBroken && !p.LegBroken)
            {
                w.Travel();
                res.LevelAtEntry[p.DistrictIdx] = p.Level;
                if (w.Foe is null) continue;
            }

            if (w.Foe is null) { w.Wander(); continue; }

            var foe = w.Foe;
            if (!w.FoeForced && !WorthFighting(p, foe)) { w.Foe = null; continue; }

            if (!Duel(w, foe, res)) { res.Dead = true; break; }
            if (w.Won) { res.Victory = true; break; }
        }

        res.Stalled = !res.Victory && !res.Dead;
        res.Level = p.Level;
        res.Rep = p.Rep;
        res.District = p.DistrictIdx;
        res.Money = p.Money;
        return res;
    }

    /// <summary>
    /// Would a sensible player take this fight? Uses exactly the numbers the game now puts
    /// on screen - hit chance, damage band, armour - so the harness measures a player who
    /// reads the screen rather than one who mashes the attack key.
    /// </summary>
    private static bool WorthFighting(Player p, Foe f)
    {
        if (p.Hp < p.MaxHp * 0.45) return false;
        if (p.LegBroken && f.Level > p.Level) return false;

        double myHit = Rules.HitChance(p.Accuracy, p.EAgi, f.Agi) / 100.0;
        double hisHit = Rules.HitChance(f.Accuracy, f.Agi, p.EAgi) / 100.0;
        double myDmg = Rules.Soak((p.DamageMin + p.DamageMax) / 2, Rules.Pierce(f.Armour, p.EStr));
        double hisDmg = Rules.Soak((f.DamageMin + f.DamageMax) / 2, Rules.Pierce(p.Armour, f.Str));

        double roundsToKill = f.MaxHp / Math.Max(0.1, myHit * myDmg);
        double roundsToFall = p.Hp / Math.Max(0.1, hisHit * hisDmg);
        return roundsToKill < roundsToFall * 0.85;
    }

    /// <summary>Runs one fight to its end. Returns false if the run is over.</summary>
    private static bool Duel(World w, Foe foe, RunResult res)
    {
        var p = w.P;
        int lvlAtStart = Math.Clamp(p.Level, 1, RunResult.MaxLevel - 1);
        int hpBefore = p.Hp, maxBefore = p.MaxHp, foeHp = foe.MaxHp;

        var f = new Fight(p, foe);
        res.Fights++;

        int guard = 0;
        while (f.End == FightEnd.None && guard++ < RoundCap)
        {
            bool hurt = p.Hp <= p.MaxHp * 0.35;
            if (hurt && p.Joints > 0 && !p.JawBroken && p.Stoned == 0) f.SmokeJoint();
            else if (hurt && p.Beer >= 0.5 && !p.JawBroken) f.DrinkBeer();
            else if (hurt && f.CanCallBackup) f.CallBackup();
            else if (p.Hp <= p.MaxHp * 0.18 && f.MyFleeChance >= 40) { if (f.TryFlee()) break; }
            else f.Attack();
        }

        res.Rounds += f.Round;
        res.MySwings += f.MySwings; res.MyHits += f.MyHits;
        res.HisSwings += f.HisSwings; res.HisHits += f.HisHits;
        res.DealtTotal += f.DamageDealt; res.TakenTotal += f.DamageTaken;

        double cost = maxBefore == 0 ? 0 : Math.Clamp(f.DamageTaken / (double)maxBefore, 0, 1);
        res.FightsAt[lvlAtStart]++;
        res.RoundsAt[lvlAtStart] += f.Round;
        res.HpLostAt[lvlAtStart] += cost;
        res.DmgDealtAt[lvlAtStart] += f.DamageDealt;
        res.FoeHpAt[lvlAtStart] += foeHp;
        if (f.End == FightEnd.Lost) res.LostAt[lvlAtStart]++;

        if (foe.Name.StartsWith("матёрый", StringComparison.Ordinal))
        {
            res.EliteFights++; res.EliteRounds += f.Round; res.EliteCost += cost;
            if (f.End == FightEnd.Lost) res.EliteLost++;
            if (f.End == FightEnd.Fled) res.EliteFled++;
        }
        else
        {
            res.NormFights++; res.NormRounds += f.Round; res.NormCost += cost;
            if (f.End == FightEnd.Lost) res.NormLost++;
        }

        switch (f.End)
        {
            case FightEnd.Won:
                res.Wins++;
                f.Spoils();
                if (foe.Index == Data.Foes.Length - 1) w.Won = true;
                w.Foe = null;
                return true;

            case FightEnd.Fled:
                res.Flights++;
                w.Foe = null;
                return true;

            case FightEnd.Lost:
                res.Knockouts++;
                bool fatal = w.Die();
                res.Deaths++;
                w.Foe = null;
                return !fatal;

            default:                      // hit the round guard - treat as a stalemate
                w.Foe = null;
                return true;
        }
    }

    /// <summary>
    /// Off-street upkeep. Mirrors the options the shops actually offer; one action per turn,
    /// so time spent shopping is time not spent fighting - which is the point.
    /// </summary>
    private static bool Manage(World w, Build b)
    {
        var p = w.P;

        if (p.Knows(Place.Vet))
        {
            if ((p.JawBroken || p.LegBroken) && p.Money >= Data.VetBones)
            {
                p.Money -= Data.VetBones; p.JawBroken = p.LegBroken = false; return true;
            }
            if (p.Hp < p.MaxHp * 0.6 && p.Money >= Data.VetScratches)
            {
                p.Money -= Data.VetScratches; p.Hp = p.MaxHp; return true;
            }
        }
        if (p.Hp < p.MaxHp * 0.6 && p.Beer >= 0.5)
        {
            p.Beer -= 0.5; p.Heal(Rules.Roll(3, 7)); return true;
        }

        // Weapons first: damage is the cheapest way to shorten a fight.
        if (p.Knows(Place.Barygi))
        {
            if (p.Weapon < 1 && p.Money >= Data.Weapons[1].Price) { p.Money -= Data.Weapons[1].Price; p.Weapon = 1; return true; }
            if (p.Weapon < 2 && p.Money >= Data.Weapons[2].Price + 40) { p.Money -= Data.Weapons[2].Price; p.Weapon = 2; return true; }
            if (p.Junk > 0) { p.Money += p.Junk * Rules.Roll(5, 9); p.Junk = 0; return true; }
        }

        if (p.Knows(Place.Bazar))
        {
            if (p.SuitIdx < 1 && p.Money >= Data.Suits[1].Price + 20) { p.Money -= Data.Suits[1].Price; p.SuitIdx = 1; return true; }
            if (p.BootsIdx < 1 && p.Money >= Data.Boots[1].Price + 30) { p.Money -= Data.Boots[1].Price; p.BootsIdx = 1; return true; }
            if (p.JacketIdx < 1 && p.Money >= Data.Jackets[1].Price + 40) { p.Money -= Data.Jackets[1].Price; p.JacketIdx = 1; return true; }
            if (p.SuitIdx < 2 && p.Money >= Data.Suits[2].Price + 80) { p.Money -= Data.Suits[2].Price; p.SuitIdx = 2; return true; }
            if (p.Beer < 1 && p.Money >= Data.PriceBeer + 40) { p.Money -= Data.PriceBeer; p.Beer += 0.5; return true; }
        }

        if (p.Knows(Place.Trenaj) && p.Money >= Data.TrainStat + 30)
        {
            if (p.Press < p.PressCap && p.Money >= Data.TrainPress + 40) { p.Money -= Data.TrainPress; p.Press++; return true; }
            p.Money -= Data.TrainStat;
            switch (b.Focus)
            {
                case 'S': p.Str++; break;
                case 'V': p.Vit++; break;
                default: p.Str++; break;      // the gym only sells strength and stamina
            }
            return true;
        }

        if (p.Knows(Place.Klub) && p.Money >= Data.KlubTricks + 60)
        {
            p.Money -= Data.KlubTricks;
            if (b.Focus == 'A' || Rules.Chance(50)) p.Agi++; else p.Luck++;
            return true;
        }

        return false;
    }

    // ---------------------------------------------------------------- duel probe
    /// <summary>
    /// The level and kit a balanced player plausibly has when this enemy first shows up.
    /// Probing every foe against one fixed character says nothing useful - the Rector is
    /// meant to be met at twelve, not at six.
    /// </summary>
    private static (int Level, int Weapon, int Boots, int Suit, int Jacket) Era(int foeIdx) => foeIdx switch
    {
        <= 2 => (2, 0, 0, 0, 0),
        <= 4 => (4, 1, 1, 1, 0),
        <= 6 => (6, 2, 1, 1, 1),
        7    => (9, 3, 2, 2, 1),
        <= 9 => (10, 3, 2, 2, 2),
        _    => (12, 4, 2, 2, 2),
    };

    /// <summary>
    /// Strips the run away: an era-appropriate balanced character against one foe type,
    /// repeated, to read off kill time and win probability per enemy.
    /// </summary>
    private static (double Hp, double Rounds, double Win, int Lvl) DuelProbe(int foeIdx, int trials)
    {
        Rules.Reseed(1234 + foeIdx);
        var e = Era(foeIdx);
        int stat = 3 + (e.Level - 1) / 2;
        int wins = 0, rounds = 0;
        double hp = 0;

        for (int i = 0; i < trials; i++)
        {
            var p = new Player
            {
                Str = stat, Agi = stat, Vit = stat, Luck = stat, Level = e.Level,
                Weapon = e.Weapon, BootsIdx = e.Boots, SuitIdx = e.Suit, JacketIdx = e.Jacket,
                Press = Rules.Classic ? e.Level : e.Level / 2,
            };
            p.Hp = p.MaxHp;
            var foe = Rules.MakeFoe(p, foeIdx, e.Level);
            hp += foe.MaxHp;

            var f = new Fight(p, foe);
            int guard = 0;
            while (f.End == FightEnd.None && guard++ < RoundCap) f.Attack();
            rounds += f.Round;
            if (f.End == FightEnd.Won) wins++;
        }
        return (hp / trials, rounds / (double)trials, wins * 100.0 / trials, e.Level);
    }
}
