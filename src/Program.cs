using Gopnik.Engine;
using Gopnik.Scenes;

namespace Gopnik;

internal static class Program
{
    private static readonly string CrashLog =
        Path.Combine(AppContext.BaseDirectory, "crash.log");

    [STAThread]
    private static void Main()
    {
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
