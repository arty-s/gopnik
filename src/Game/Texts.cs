namespace Gopnik.Game;

/// <summary>
/// Lines lifted verbatim from the 2003 binary. Spelling, slang and typos are the
/// author's; correcting them would be restoring the wrong thing.
/// </summary>
public static class Texts
{
    public static readonly string[] Spectators =
    {
        "Мочи его, мочи!", "Врежь ему!", "Блин долго ты ещё будешь мудиться?",
        "Да вы только посмотрите на эти пинки!", "Чё-тут за батва?",
        "Я знаю вон того мудака, он уже нескольких запинал!",
        "Чё так слабо бьёшь?! Пинай сильнее!", "Дерьмово дерётесь придурки",
        "Это чё реслинг?", "Двинь ему в рыло!", "И куда менты смотрят?",
        "Пинай!", "Врежь гаду!", "Господа делайте ваши ставки!",
        "Ну чё там? Какой счет?",
        "Да, а помнишь мы вчера также одного пинали, пинали.. А потом подошла его братва..",
    };

    public static readonly string[] PlayerHits =
    {
        "^2Точный удар!!!", "^2Не хило приложил!!!", "^2Двойной урон!!!",
    };

    public static readonly string[] FoeTaunts =
    {
        "^4Враг:Сдохни урод!!", "^4Тебе не хило врезали!", "^4Враг:Получи гнида!!",
    };

    public static readonly string[] Wander =
    {
        "^6Ты зашел на тропинку где бродит искитимская гопота.",
        "^6Ты зашел в какие-то дебри подваротен.",
        "^6Ты зашел на планы.",
        "^6Ты зашел чёрте куда.",
        "^7Совсем ничё не происходит.",
        "^7Ничё не происходит.",
    };

    public static readonly string[] Phone =
    {
        "^EТелефон:^6 Алё, ты где? Приходи, мы вещицу для тебя раздобыли. ^A>> иди к барыгам",
        "^EТелефон:^6 Алё, ты где щас? Тут помощь нужна. ^A>> иди в притон",
        "^EТелефон:^6 Ты где? Базар есть. ^A>> иди в притон",
        "^EТелефон:^6 А Васю можно? ^2- Нет это не Вася.",
        "^EТелефон:^6 Это ты там на базаре шухер наводил? Ну короче там менты свалили.",
        "^EТелефон:^6 Ты че там, в клуб-та пойдёшь. Уже утряслось всё.",
    };

    public static readonly string[] Blessings =
    {
        "^1Да увеличится твоя понтовость!",
        "^1Да увеличиться твоя сила!",
        "^1Да уменьшиться твоя корявость!",
        "^1Да возрастут твой силы жизненные!",
        "^1Да снизойдет на тебя удача!",
    };

    public const string Intro1 = "Год 2xxx от Р.Х.";
    public const string Intro2 = "Последний день ты пришел в универ.";
    public const string Intro3 = "Ты по-страшному косил и забивал.";
    public const string Intro4 = "Ты ещё мог сдать все задания, которые ты взял у друзей.";
    public const string Intro5 = "Но тут...";
    public const string Intro6 = "^6Ректор: Ах ты урод, чёртов забивала. Вали из универа!";
    public const string Intro7 = "^2Ты: А типа чё?";
    public const string Intro8 = "^6Ректор: Ты отчислен мудак!!! Как ты был лохом так и останешься.";
    public const string Intro9 = "^4Это слышали все и ты из пацана превратился в опущенного.";
    public const string Intro10 = "Ты неможешь стерпеть такой наезд, однако ректор офигительно крутой.";
    public const string Intro11 = "Ты решил доказать свою крутизну всему миру(в твоем понимании - Городу).";

    public const string WinLine1 = "^1Ты замочил самого ректора!!! ТЫ САМЫЙ КРУТОЙ!!!";
    public const string WinLine2 = "^6 о чёрт! да это ж не ректор был.";
    public const string WinLine3 = "^6Это был проректор СУНЦа!";

    // The second half of the joke: the man you just beat was a stand-in, and the real one
    // walks in while you are still catching your breath. All four lines sit together in
    // the 2003 binary at 0x2D79-0x2DDA, in this order.
    public const string RectorEnters = "^6Тут заходит настоящий ректор.";
    public const string RectorTaunt = "^4Мудак! ты тупой дебил, думал что я идиот?";
    public const string RectorReply = "^2Я думаю ты сконил!";
    public const string RectorFinal = "^4Ну тада сдохни!";
    public const string WinLine4 = "^1Вновь сила торжествует над интелектом.";
    public const string WinLine5 = "^1После этого сразу началась анархия и полный беспредел.";
    public const string WinLine6 = "^1И не стыдно тебе гоп чёртов?";

    public const string DeadByRector = "^4Ты сдох. Ректор тебя замочил. Ты так и не доказал свою крутизну.";
    public const string SavedByFriends = "^1Тебе повезло знакомые пацаны отвезли тебя в больницу а то бы ты сдох.";

    public static string Pick(string[] pool) => pool[Rules.Rng.Next(pool.Length)];
}
