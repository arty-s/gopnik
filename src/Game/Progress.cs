using System.Text.Json;

namespace Gopnik.Game;

/// <summary>
/// What carries across runs rather than inside one. The original kept a save per district
/// and let you open a fresh game at any of them - "Нажми цифру с какого района начать" -
/// so once a district has been reached it stays unlocked for every future character.
/// </summary>
public static class Progress
{
    private static string Path =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "gopnik.progress.json");

    private sealed class Dto
    {
        public int MaxDistrict { get; set; }
    }

    private static int _max = -1;

    /// <summary>Highest district index ever reached, across all characters.</summary>
    public static int MaxDistrict
    {
        get
        {
            if (_max >= 0) return _max;
            try
            {
                _max = File.Exists(Path)
                    ? Math.Clamp(JsonSerializer.Deserialize<Dto>(File.ReadAllText(Path))?.MaxDistrict ?? 0,
                                 0, Data.Districts.Length - 1)
                    : 0;
            }
            catch { _max = 0; }
            return _max;
        }
    }

    public static void Reached(int district)
    {
        if (district <= MaxDistrict) return;
        _max = Math.Clamp(district, 0, Data.Districts.Length - 1);
        try
        {
            File.WriteAllText(Path, JsonSerializer.Serialize(new Dto { MaxDistrict = _max },
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* an unlock that fails to stick is not worth taking the game down for */ }
    }
}
