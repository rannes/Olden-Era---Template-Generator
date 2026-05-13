namespace OldenEra.TemplateEditor.Services.AutoUpdate;

public interface IUpdateInstaller
{
    /// <summary>
    /// Begins the install handoff: writes a self-deleting batch script that waits for
    /// the running process to exit, replaces the running exe with the downloaded one,
    /// relaunches it, and signals the application to shut down.
    /// </summary>
    void LaunchInstallAndExit(string downloadedExePath);
}
