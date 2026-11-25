using System.IO;
using System.Windows;

namespace demo;

/// <summary>
///     Journal d'un auto-test et code de sortie du processus : 0 seulement si au moins une ligne est écrite et qu'aucune ne
///     commence par <c>ECHEC</c>, <c>ERROR</c> ou <c>WATCHDOG</c>. Un délai dépassé tue le processus (code 2), même si le
///     thread d'interface est bloqué. Le journal est écrit au fil de l'eau, donc lisible après un arrêt brutal.
/// </summary>
internal sealed class SelfTestReport
{
    private readonly string _path;
    private readonly Lock _gate = new();
    private int _lines;
    private bool _failed;

    private SelfTestReport(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, "");
    }

    private void Log(string line)
    {
        lock (_gate)
        {
            _lines++;
            _failed |= line.StartsWith("ECHEC") || line.StartsWith("ERROR") || line.StartsWith("WATCHDOG");
            File.AppendAllText(_path, line + Environment.NewLine);
        }
    }

    /// <summary>Exécute le scénario, puis <paramref name="after" /> (capture d'écran) et ferme l'application avec le code de sortie.</summary>
    public static async Task Run(Window window, string logPath, TimeSpan timeout, Func<Action<string>, Task> scenario, Action? after = null)
    {
        var report = new SelfTestReport(logPath);
        // La souris réelle ne doit atteindre que la fenêtre testée.
        window.Topmost = true;
        window.Activate();
        using var watchdog = new Timer(_ =>
        {
            report.Log($"WATCHDOG: délai de {timeout.TotalSeconds:0} s dépassé, arrêt forcé");
            Environment.Exit(2);
        }, null, timeout, Timeout.InfiniteTimeSpan);

        try
        {
            await scenario(report.Log);
            after?.Invoke();
        }
        catch (Exception ex)
        {
            report.Log("ERROR " + ex);
        }

        var code = report._failed || report._lines == 0 ? 1 : 0;
        Environment.ExitCode = code;
        Application.Current.Shutdown(code);
    }

    /// <summary>La ligne d'une vérification.</summary>
    public static string Line(string name, bool ok, string detail = "") =>
        $"{(ok ? "OK    " : "ECHEC ")} {name}{(detail.Length > 0 ? " — " + detail : "")}";
}
