using System.Text.Json;

namespace Gopnik.Game;

/// <summary>
/// Save games. The original wrote a 694-byte Pascal record next to the executable; this
/// writes readable JSON next to it instead, which costs nothing and can be repaired by hand.
/// </summary>
public static class Save
{
    private static string Path =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "gopnik.sav.json");

    public static bool Exists => File.Exists(Path);

    private sealed class Dto
    {
        public string Name { get; set; } = "";
        public int Klass { get; set; }
        public int Level { get; set; }
        public int Str { get; set; }
        public int Agi { get; set; }
        public int Vit { get; set; }
        public int Luck { get; set; }
        public int Hp { get; set; }
        public int Xp { get; set; }
        public int Rep { get; set; }
        public int Money { get; set; }
        public int Junk { get; set; }
        public int Joints { get; set; }
        public double Beer { get; set; }
        public int Ammo { get; set; }
        public int Weapon { get; set; }
        public int Boots { get; set; }
        public int Suit { get; set; }
        public int Jacket { get; set; }
        public int Press { get; set; }
        public bool Cross { get; set; }
        public bool RingLuck { get; set; }
        public bool RingAll { get; set; }
        public bool MegaRing { get; set; }
        public bool RingHeal { get; set; }
        public bool Mobile { get; set; }
        public bool Shades { get; set; }
        public bool Tattoo { get; set; }
        public bool Pistol { get; set; }
        public bool Silencer { get; set; }
        public bool Guard { get; set; }
        public bool Jaw { get; set; }
        public bool Leg { get; set; }
        public int Stoned { get; set; }
        public int Knockouts { get; set; }
        public int District { get; set; }
        public bool GirlKnown { get; set; }
        public bool[] Found { get; set; } = Array.Empty<bool>();
    }

    public static void Write(World w)
    {
        var p = w.P;
        int places = 8;
        var flat = new bool[Data.Districts.Length * places];
        for (int d = 0; d < Data.Districts.Length; d++)
            for (int i = 0; i < places; i++)
                flat[d * places + i] = p.Found[d, i];

        var dto = new Dto
        {
            Name = p.Name, Klass = (int)p.Klass, Level = p.Level,
            Str = p.Str, Agi = p.Agi, Vit = p.Vit, Luck = p.Luck,
            Hp = p.Hp, Xp = p.Xp, Rep = p.Rep, Money = p.Money, Junk = p.Junk,
            Joints = p.Joints, Beer = p.Beer, Ammo = p.Ammo,
            Weapon = p.Weapon, Boots = p.BootsIdx, Suit = p.SuitIdx, Jacket = p.JacketIdx,
            Press = p.Press, Cross = p.Cross, RingLuck = p.RingLuck, RingAll = p.RingAll,
            MegaRing = p.MegaRing, RingHeal = p.RingHeal, Mobile = p.Mobile, Shades = p.Shades,
            Tattoo = p.Tattoo, Pistol = p.Pistol, Silencer = p.Silencer, Guard = p.Guard,
            Jaw = p.JawBroken, Leg = p.LegBroken, Stoned = p.Stoned, Knockouts = p.Knockouts,
            District = p.DistrictIdx, GirlKnown = p.GirlKnown, Found = flat,
        };

        try
        {
            File.WriteAllText(Path, JsonSerializer.Serialize(dto,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* a failed save must never take the game down mid-fight */ }
    }

    public static World? Read()
    {
        try
        {
            if (!Exists) return null;
            var dto = JsonSerializer.Deserialize<Dto>(File.ReadAllText(Path));
            if (dto is null) return null;

            var w = new World();
            var p = w.P;
            p.Name = dto.Name; p.Klass = (Klass)dto.Klass; p.Level = dto.Level;
            p.Str = dto.Str; p.Agi = dto.Agi; p.Vit = dto.Vit; p.Luck = dto.Luck;
            p.Xp = dto.Xp; p.Rep = dto.Rep; p.Money = dto.Money; p.Junk = dto.Junk;
            p.Joints = dto.Joints; p.Beer = dto.Beer; p.Ammo = dto.Ammo;
            p.Weapon = dto.Weapon; p.BootsIdx = dto.Boots; p.SuitIdx = dto.Suit;
            p.JacketIdx = dto.Jacket; p.Press = dto.Press;
            p.Cross = dto.Cross; p.RingLuck = dto.RingLuck; p.RingAll = dto.RingAll;
            p.MegaRing = dto.MegaRing; p.RingHeal = dto.RingHeal;
            p.Mobile = dto.Mobile; p.Shades = dto.Shades; p.Tattoo = dto.Tattoo;
            p.Pistol = dto.Pistol; p.Silencer = dto.Silencer; p.Guard = dto.Guard;
            p.JawBroken = dto.Jaw; p.LegBroken = dto.Leg; p.Stoned = dto.Stoned;
            p.Knockouts = dto.Knockouts;
            p.DistrictIdx = Math.Clamp(dto.District, 0, Data.Districts.Length - 1);
            p.GirlKnown = dto.GirlKnown;
            p.Hp = Math.Clamp(dto.Hp, 1, p.MaxHp);

            int places = 8;
            for (int d = 0; d < Data.Districts.Length; d++)
                for (int i = 0; i < places; i++)
                {
                    int k = d * places + i;
                    if (k < dto.Found.Length) p.Found[d, i] = dto.Found[k];
                }

            w.Say($"^1Загружено: {p.Name}, {p.Rank}, {p.District.Name}.");
            w.Say("^7Введи ^Ew^7 чтобы шататься по окрестностям.");
            return w;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Short line for the title screen.</summary>
    public static string Describe()
    {
        try
        {
            var dto = JsonSerializer.Deserialize<Dto>(File.ReadAllText(Path));
            if (dto is null) return "сейв битый";
            string rank = Data.Ranks[Math.Clamp(dto.Rep / 3, 0, Data.Ranks.Length - 1)];
            return $"{dto.Name}, ур.{dto.Level}, \"{rank}\", {Data.Districts[Math.Clamp(dto.District, 0, 4)].Name}";
        }
        catch { return "сейв битый"; }
    }
}
