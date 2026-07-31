using Gopnik.Engine;
using Gopnik.Scenes;

namespace Gopnik;

internal static class Program
{
    private static readonly string CrashLog =
        Path.Combine(AppContext.BaseDirectory, "crash.log");

    [STAThread]
    private static void Main(string[] args)
    {
        // --classic plays by the 2003 arithmetic; see the note on Rules.Classic. Adding
        // --lite keeps all of it except the enemy health formula, which is the one piece
        // that makes a classic fight twice as long. A save remembers which set it was made
        // under and puts it back on load.
        // Starts on the middle set - the original's arithmetic everywhere the original
        // actually stated it, without extrapolating its health formula onto enemies, which
        // is the one place the 2003 help screen only ever spoke about the player. The
        // title screen cycles all three, and --classic / --modern pick one outright.
        bool Has(string flag) => args.Any(a => a.Equals(flag, StringComparison.OrdinalIgnoreCase));
        Game.Rules.RuleSet = Has("--classic") ? 0 : Has("--modern") ? 2 : 1;

        AppDomain.CurrentDomain.UnhandledException += (_, e) => Dump(e.ExceptionObject as Exception);
        Application.ThreadException += (_, e) => Dump(e.Exception);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        ApplicationConfiguration.Initialize();
        Application.Run(new GopnikForm(new TitleScene()));
    }

    private static void Dump(Exception? ex)
    {
        if (ex is null) return;
        try
        {
            File.AppendAllText(CrashLog,
                $"--- {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { /* nothing sensible left to do */ }
    }
}
