using System;
using System.Diagnostics;
using System.IO;

namespace OldenEra.TemplateEditor.Services.AutoUpdate;

public sealed class BatchUpdateInstaller : IUpdateInstaller
{
    private readonly Func<string> _resolveCurrentExePath;
    private readonly Action _shutdown;

    public BatchUpdateInstaller(Func<string> resolveCurrentExePath, Action shutdown)
    {
        _resolveCurrentExePath = resolveCurrentExePath;
        _shutdown = shutdown;
    }

    public void LaunchInstallAndExit(string downloadedExePath)
    {
        if (string.IsNullOrWhiteSpace(downloadedExePath))
            throw new ArgumentException("Downloaded path is empty.", nameof(downloadedExePath));

        string targetExe = _resolveCurrentExePath();
        string scriptPath = Path.Combine(Path.GetTempPath(),
            $"oetg-update-{Guid.NewGuid():N}.bat");
        string script = BuildInstallScript(downloadedExePath, targetExe);
        File.WriteAllText(scriptPath, script);

        var psi = new ProcessStartInfo("cmd.exe", $"/c \"\"{scriptPath}\"\"")
        {
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        try
        {
            Process.Start(psi);
        }
        catch
        {
            // Handoff failed before any work happened — clean up the orphan script
            // and let the caller surface the error. Don't shut the app down.
            try { File.Delete(scriptPath); } catch { /* swallow */ }
            throw;
        }
        _shutdown();
    }

    /// <summary>
    /// Builds the batch script that waits, copies the new exe over the current one,
    /// relaunches it, and deletes itself. Pure for testability.
    /// </summary>
    public static string BuildInstallScript(string newExe, string targetExe)
    {
        if (string.IsNullOrWhiteSpace(newExe))    throw new ArgumentException("newExe");
        if (string.IsNullOrWhiteSpace(targetExe)) throw new ArgumentException("targetExe");
        // Quote both paths so spaces (e.g. Program Files) work.
        return
            "@echo off\r\n" +
            "timeout /t 2 /nobreak >nul\r\n" +
            $"move /y \"{newExe}\" \"{targetExe}\"\r\n" +
            $"start \"\" \"{targetExe}\"\r\n" +
            "del \"%~f0\"\r\n";
    }
}
