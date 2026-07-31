namespace Gopnik.Game;

public enum Klass { Pacan, Otmoroz, Gopnik, Vor }

public enum Place { Street = 0, Bazar, Barygi, Vet, Girl, Priton, Klub, Trenaj }

/// <summary>Static tables lifted from the original binary - names, ladders, prices.</summary>
public static class Data
{
    // ---- classes -------------------------------------------------------------------
    public static readonly (string Name, string Bonus)[] Classes =
    {
        ("Пацан",     "нормальный тип. Бонус - подруга и клуб"),
        ("Отморозок", "тупой корявый мудак. Бонус - царапины заживают сами"),
        ("Гопник",    "гоп он и есть гоп. Бонус - притон"),
        ("Вор",       "везучий ублюдок. Бонус - воровство и барыги"),
    };

    // ---- districts -----------------------------------------------------------------
    public sealed record District(string Name, string Arrival, string Flavour, bool Bandit);

    public static readonly District[] Districts =
    {
        new("Университет", "Ты стоишь у дверей университета.",
            "Отсюда ты начнёшь свой нелёгкий путь гопника.", false),
        new("Шлюз", "Ты сел на автобус и попёрся на шлюз...",
            "Там бродит шлюзовская шпана.", false),
        new("ОбьГЭС", "На маршрутке ты доехал до ОбьГЭСа...",
            "Здесь бродит уже более крутая гопота.", true),
        new("Ельцовка", "Ты приехал в Ельцовку...",
            "Ото всюду доносятся крики запинываемых.", true),
        new("Ректорат", "Пора наконец отомстить ректору...",
            "Ты пробрался в универ, в тёмный ректорский кабинет...", true),
    };

    // ---- enemies -------------------------------------------------------------------
    /// <summary>Relative power: how the enemy spends its stat budget.</summary>
    public sealed record Foe(string Name, int Str, int Agi, int Vit, int Luck, int Armour,
                             int Weapon, int Money, string Greeting);

    /// <summary>
    /// The four stats are the original's own, read out of g.exe: a 40-byte table of ten
    /// four-byte records at the very start of DGROUP, immediately in front of the array of
    /// enemy names. The field order is not a guess - the inspect routine at 0x156D loads
    /// the second field, multiplies it by five, adds twenty and prints it as "Точность #%",
    /// which is the help screen's "(20+Ловкость*5)%" exactly, and caps at a stat of 14
    /// because that is where the formula reaches the printed 90% ceiling.
    ///
    /// Armour, weapon and money are still ours - those live in tables the recovery has not
    /// pinned down yet. The Rector is not in the table at all: it stops after the ten
    /// street types, so his numbers were built in code and remain reconstructed here.
    /// </summary>
    public static readonly Foe[] Foes =
    {
        new("Дохляк",         1, 2, 1, 2, 0, 0,  2, "^4А чё ваще?"),
        new("Нефор",          2, 2, 2, 3, 0, 0,  3, "^4Пацан ты из какого района?"),
        new("Нарк",           2, 2, 2, 2, 0, 0,  3, "^4Эй мудак?!"),
        new("Подтсан",        3, 3, 3, 3, 1, 1,  5, "^4Чё те нада козёл?!"),
        new("Отморозок",      5, 2, 4, 1, 1, 2,  7, "^4Ну ты меня достал ща урою!"),
        new("Гопник",         4, 3, 3, 2, 2, 2,  8, "^2Урыть тебя ублюдок!"),
        new("Вор",            3, 3, 2, 4, 1, 2, 14, "^4Отдай кошелёк урод!"),
        new("Беспредельщик",  5, 3, 4, 2, 2, 4, 12, "^2Ну вот мы и встретились мудак!"),
        new("Мент",           5, 5, 5, 5, 4, 4, 10, "^4Корявый! ты попался!"),
        new("Маньячок",       5, 6, 8, 3, 1, 6, 18, "^4Я МАНЬЯК!!!"),
        new("Ректор НГУ",     9, 7,  9, 6, 4, 7, 90, "^4Мудак! ты тупой дебил, думал что я идиот?"),
    };

    /// <summary>Which foes roam which district (indices into <see cref="Foes"/>).</summary>
    public static readonly int[][] Spawns =
    {
        new[] { 0, 0, 1, 1, 2 },
        new[] { 0, 1, 2, 2, 3, 3 },
        new[] { 2, 3, 3, 4, 5, 5, 6 },
        new[] { 4, 5, 5, 6, 7, 7, 9 },
        new[] { 5, 7, 7, 8, 9 },
    };

    // ---- gear ladders --------------------------------------------------------------
    // Sale is what the fence gives back for a piece you have outgrown - about half, and a
    // figure of its own for the two knives, which are only ever found and so have no
    // shop price to halve.
    public static readonly (string Name, int Bonus, int Price, int Sale)[] Weapons =
    {
        ("кулаки",  0,   0,  0),
        ("Кастет",  2,  25, 12),     // цены отсюда же, DS:0x0B3C и 0x0B3D
        ("Дубинка", 4,  50, 25),
        ("Нож",     6,   0, 35),      // only ever found, never sold new
        ("Тесак",   9,   0, 50),
    };

    public static readonly (string Name, int Bonus, int Price, int Sale)[] Boots =
    {
        ("драные кеды",     0,  0,  0),
        ("Бутсы",           1, 35, 17),
        ("Понтовые бутсы",  2, 70, 35),
    };

    public static readonly (string Name, int Bonus, int Price, int Sale)[] Suits =
    {
        ("своё тряпьё",    0,   0,  0),
        ("Костюм Abibas",  1,  40, 20),
        ("Костюм Adidas",  2,  80, 40),
    };

    public static readonly (string Name, int Bonus, int Price, int Sale)[] Jackets =
    {
        ("без кожанки",       0,   0,  0),
        ("Кожанка",           2,  90, 45),
        ("Крутая кожанка",    4, 160, 80),
    };

    // ---- prices --------------------------------------------------------------------
    public const int PriceHotdog = 3;
    public const int PriceBeer = 5;
    public const int PriceShades = 30;

    // The fence's nine prices, read out of g.exe at DS:0x0B38..0x0B40 - nine consecutive
    // bytes fetched one per line by the code that prints the fence's menu at 0xC558, in
    // the same order the menu lists them. The bazaar keeps its own nine at DS:0x0B2E.
    public const int PriceJoint = 15;
    public const int PriceMobile = 30;
    public const int PriceBigJoint = 20;
    public const int PriceTattoo = 10;
    public const int PricePistol = 150;
    public const int PriceAmmo = 70;
    public const int PriceSilencer = 60;
    public const int VetScratches = 3;
    public const int VetBones = 7;
    public const int TrainStat = 20;
    public const int TrainXp = 10;
    public const int TrainGuard = 30;
    public const int TrainPress = 20;
    public const int KlubDisco = 15;
    public const int KlubTricks = 22;
    public const int GirlGift = 12;

    /// <summary>
    /// Standing needed before the priton will have you. The original refused you at the
    /// door - "Такого конявого непустят в местный притон!" - and announced the moment you
    /// were let in. It never printed the number; this one sits just under the threshold
    /// for calling the lads, so the place opens shortly before its main use does.
    /// </summary>
    public const int PritonRep = 12;

    // ---- reputation ranks: all 43, straight out of the original data segment --------
    public static readonly string[] Ranks =
    {
        "Опущеный", "Полное ЧМО", "ЧМО", "Частично не ЧМО", "Чё-то не понятное",
        "Чё-то отдалённо похожее на не ЧМО", "Вроде не ЧМО", "Не ЧМО", "Совсем не ЧМО",
        "Похожий на Чувака", "Чувак", "Нормальный Чувак", "Да нормальный такой Чувак",
        "Довольно понтовый Чувак", "Понтовый Чувак", "Вполне понтовый Чувак",
        "Очень понтовый Чувак", "Чувак отдалённо похожий на Пацана", "Похожий на Пацана",
        "Сильно похожий на Пацана", "Вроде Пацан", "Пацан", "Пацан покруче",
        "Понтоватый Пацан", "Понтовый Пацан", "Очень понтовый Пацан", "Крутой Пацан",
        "Очень крутой Пацан", "Пацан метящий в реальные", "Почти реальный Пацан",
        "Довольно реальный Пацан", "Реальный Пацан", "Пацан немного более реальный",
        "Пацан ещё реальнее", "Очень реальный Пацан", "Офигенно реальный Пацан",
        "Да типа ваще реальный Пацан", "Смотри не лопни от реальности, Реальный Пацан",
        "Крутой Реальный Пацан", "Очень крутой Реальный Пацан", "Самый Крутой Реальный Пацан",
        "Пацан, который завалил Проректора СУНЦа", "Пацан, который всех опрокинул",
    };

    public static readonly (Place P, string Cmd, string Name)[] Places =
    {
        (Place.Bazar,  "mar",  "базар"),
        (Place.Barygi, "bmar", "барыги"),
        (Place.Vet,    "rep",  "врач"),
        (Place.Girl,   "girl", "подруга"),
        (Place.Priton, "pr",   "притон"),
        (Place.Klub,   "kl",   "клуб"),
        (Place.Trenaj, "trn",  "качалка"),
    };
}
