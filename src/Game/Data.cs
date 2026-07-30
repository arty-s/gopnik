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

    public static readonly Foe[] Foes =
    {
        new("Дохляк",         2, 2, 1, 2, 0, 0,  2, "^4А чё ваще?"),
        new("Нефор",          2, 3, 2, 2, 0, 0,  3, "^4Пацан ты из какого района?"),
        new("Нарк",           3, 2, 2, 1, 0, 0,  3, "^4Эй мудак?!"),
        new("Подтсан",        3, 3, 3, 2, 1, 1,  5, "^4Чё те нада козёл?!"),
        new("Отморозок",      5, 3, 4, 1, 1, 2,  7, "^4Ну ты меня достал ща урою!"),
        new("Гопник",         4, 4, 4, 3, 2, 2,  8, "^2Урыть тебя ублюдок!"),
        new("Вор",            3, 6, 3, 6, 1, 2, 14, "^4Отдай кошелёк урод!"),
        new("Беспредельщик",  7, 4, 5, 2, 2, 4, 12, "^2Ну вот мы и встретились мудак!"),
        new("Мент",           6, 5, 6, 3, 4, 4, 10, "^4Корявый! ты попался!"),
        new("Маньячок",       8, 5, 5, 4, 1, 6, 18, "^4Я МАНЬЯК!!!"),
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
    public static readonly (string Name, int Bonus, int Price)[] Weapons =
    {
        ("кулаки",  0,   0),
        ("Кастет",  2,  30),
        ("Дубинка", 4,  60),
        ("Нож",     6,   0),      // only ever found, never sold
        ("Тесак",   9,   0),
    };

    public static readonly (string Name, int Bonus, int Price)[] Boots =
    {
        ("драные кеды",     0,  0),
        ("Бутсы",           1, 35),
        ("Понтовые бутсы",  2, 70),
    };

    public static readonly (string Name, int Bonus, int Price)[] Suits =
    {
        ("своё тряпьё",    0,   0),
        ("Костюм Abibas",  1,  40),
        ("Костюм Adidas",  2,  80),
    };

    public static readonly (string Name, int Bonus, int Price)[] Jackets =
    {
        ("без кожанки",       0,   0),
        ("Кожанка",           2,  90),
        ("Крутая кожанка",    4, 160),
    };

    // ---- prices --------------------------------------------------------------------
    public const int PriceHotdog = 3;
    public const int PriceBeer = 5;
    public const int PriceShades = 30;
    public const int PriceJoint = 12;
    public const int PriceMobile = 40;
    public const int PriceBigJoint = 90;
    public const int PriceTattoo = 25;
    public const int PricePistol = 150;
    public const int PriceAmmo = 20;
    public const int PriceSilencer = 80;
    public const int VetScratches = 3;
    public const int VetBones = 7;
    public const int TrainStat = 20;
    public const int TrainXp = 10;
    public const int TrainGuard = 30;
    public const int TrainPress = 20;
    public const int KlubDisco = 15;
    public const int KlubTricks = 22;
    public const int GirlGift = 12;

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
