using System;
using System.Threading;
using System.Threading.Tasks;

namespace OldenEra.TemplateEditor.Services.AutoUpdate;

/// <summary>
/// Orchestrates the update flow: check → ask user → download (with progress + cancel)
/// → install. Falls back to opening the releases page in the browser when no asset
/// is available, when download fails, or as the user's chosen alternative.
///
/// Kept WPF-free: callers inject UI side-effects via callbacks.
/// </summary>
public sealed class UpdateOrchestrator
{
    public sealed record UiCallbacks(
        Func<UpdateInfo, bool> AskUserToInstall,
        Func<DownloadDialog> ShowDownloadDialog,
        Action<string> ShowError,
        Action OpenReleasesPage);

    public sealed record DownloadDialog(IProgress<double> Progress, CancellationToken Token, Action Close);

    private readonly IUpdateChecker _checker;
    private readonly IUpdateDownloader _downloader;
    private readonly IUpdateInstaller _installer;
    private readonly IUpdateLog _log;
    private readonly string _downloadFolder;

    public UpdateOrchestrator(
        IUpdateChecker checker,
        IUpdateDownloader downloader,
        IUpdateInstaller installer,
        IUpdateLog log,
        string? downloadFolder = null)
    {
        _checker = checker;
        _downloader = downloader;
        _installer = installer;
        _log = log;
        _downloadFolder = downloadFolder ?? UpdatePaths.DownloadFolder;
    }

    public async Task RunStartupCheckAsync(Version current, UiCallbacks ui, CancellationToken cancellationToken = default)
    {
        UpdateInfo? info;
        try
        {
            info = await _checker.CheckAsync(current, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            _log.Warn("Update check failed.", ex);
            return;
        }
        if (info == null) return;

        if (!ui.AskUserToInstall(info)) return;

        if (string.IsNullOrEmpty(info.AssetUrl))
        {
            _log.Info($"No installable asset for {info.Version}; opening releases page.");
            ui.OpenReleasesPage();
            return;
        }

        var dialog = ui.ShowDownloadDialog();
        try
        {
            string downloaded = await _downloader
                .DownloadAsync(info, _downloadFolder, dialog.Progress, dialog.Token)
                .ConfigureAwait(false);
            dialog.Close();
            _log.Info($"Downloaded {info.AssetName}; launching installer.");
            _installer.LaunchInstallAndExit(downloaded);
        }
        catch (OperationCanceledException)
        {
            _log.Info("User cancelled update download.");
            dialog.Close();
        }
        catch (Exception ex)
        {
            _log.Error("Update download failed.", ex);
            dialog.Close();
            ui.ShowError($"Update failed: {ex.Message}\nOpening the releases page instead.");
            ui.OpenReleasesPage();
        }
    }
}
