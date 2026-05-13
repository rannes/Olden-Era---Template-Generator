# WPF auto-update v2 — design

Date: 2026-05-13

## Premise

Upstream commits `4ae647c` + `bc93dc5` add a download-and-replace auto-update flow with
a progress window and Cancel button. We want the capability, but the upstream
implementation has rough edges we want to avoid: `catch { /* silent */ }`, the entire
flow lives in `MainWindow`, no user opt-out, and a monolithic XAML edit (`76ee588`)
that conflicts with our decomposed `MainWindow`.

The fork ships releases. Confirmed via `gh release list --repo
rannes/Olden-Era---Template-Generator`: each release since `v0.6.3` carries a single
self-contained asset named `OldenEraTemplateGenerator-v{VERSION}-win-x64.exe`. Older
releases (v0.6.0–v0.6.1) shipped `.zip`. We support `.exe` only — `.zip` is not in
scope.

## Goals

- Replace the existing "open releases page in browser" prompt with an in-app download +
  install flow when a matching asset is available.
- Keep the browser-fallback path for missing assets, download failures, and user
  cancellation.
- Let the user opt out of the startup check ("Check for updates on startup", default
  on), persisted across launches.
- Log update activity to `%LOCALAPPDATA%/OldenEraTemplateGenerator/update.log` so
  silent failures are diagnosable.
- Keep PR scope tight: no Patch Notes button (sibling PR), no opportunistic refactors.

## Non-goals

- Code-signing verification of the downloaded exe.
- SHA256 / digest verification.
- Delta updates, retry-with-backoff, MSI / installer support.
- `.zip` asset support.
- Cross-platform (this is a WPF-only feature).

## Architecture

New namespace: `OldenEra.TemplateEditor.Services.AutoUpdate`.

```
IUpdateChecker
  Task<UpdateInfo?> CheckAsync(Version current, CancellationToken)

IUpdateDownloader
  Task<string> DownloadAsync(UpdateInfo, IProgress<double>, CancellationToken)
  // returns full path to the downloaded exe

IUpdateInstaller
  void LaunchInstallAndExit(string downloadedExePath)
```

Value type: `record UpdateInfo(Version Version, string? AssetUrl, long? AssetSize)`.
A non-null `AssetUrl` means we can do an in-app install; null means "newer version
exists but no matching asset" — caller falls back to the browser path.

Default implementations:

- `GitHubUpdateChecker` — calls the existing `/releases/latest` API, parses the tag,
  selects the asset whose name matches `^OldenEraTemplateGenerator-v.*-win-x64\.exe$`.
- `HttpUpdateDownloader` — streams to
  `%LOCALAPPDATA%/OldenEraTemplateGenerator/update/<filename>.partial`, renames on
  success, deletes on cancel/error. Reports progress every ~256 KB or 100 ms.
- `BatchUpdateInstaller` — writes a self-deleting `.bat` to `%TEMP%`, launches via
  `cmd /c start`, calls `Application.Current.Shutdown()`. Resolves the running exe via
  `Process.GetCurrentProcess().MainModule!.FileName` (single-file self-contained build
  has exactly one .exe).

Plus:

- `UpdateLog` — append-only log at
  `%LOCALAPPDATA%/OldenEraTemplateGenerator/update.log`. Format
  `[ISO8601] LEVEL: msg\n{ex}\n`. Trims from the front when file exceeds 64 KB. Never
  throws — failures inside the logger are swallowed.
- `AppPreferences` (record) + `IAppPreferencesStore` /
  `JsonAppPreferencesStore` — JSON file at
  `%LOCALAPPDATA%/OldenEraTemplateGenerator/preferences.json`. Single field for now:
  `bool CheckForUpdatesOnStartup = true`. Missing file or malformed JSON returns
  defaults; never throws.

`MainWindow.xaml.cs` keeps only:

- The startup hook (gated by the new pref).
- Showing the existing "update available" Yes/No dialog.
- Owning the `UpdateProgressWindow` lifecycle.
- The browser fallback path.

The current 36-line `CheckForUpdateAsync` shrinks to ~20 lines of orchestration. The
existing `Http`, `JsonOptions`, `GitHubRelease`, `GitHubReleasesPage`,
`GitHubApiLatestRelease` constants either move into the new namespace (if only used by
update code) or stay put. `LinkWebVersion_RequestNavigate` and `FormatVersion` stay —
they have other callers.

## Flow

### Startup

1. Read `AppPreferences`.
2. If `CheckForUpdatesOnStartup == false` → skip.
3. Otherwise fire-and-forget `IUpdateChecker.CheckAsync` in the background.
4. `null` result (same/older version, network error, non-200) → silent. Exception path
   logs to `update.log`; clean "nothing to update" does not.
5. `UpdateInfo` returned → marshal to UI thread, show the existing Yes/No dialog.

### User clicks Yes

- `AssetUrl == null` → fall back to opening the releases page in the browser
  (preserves current behavior).
- `AssetUrl != null` → open `UpdateProgressWindow`, run download.

### Download

- Streams to `<prefs dir>/update/<filename>.partial`. Renames to `.exe` on success.
- `IProgress<double>` 0.0–1.0, monotonic. Window updates a `ProgressBar` and
  percent label.
- Cancel button → `cts.Cancel()`. On `OperationCanceledException`: delete `.partial`,
  close window, log "user cancelled", no error dialog.
- Other exception: log full exception, close window, show
  `MessageBox` "Update failed: {msg}. Open the releases page instead?" → falls back to
  browser. Spec's "surface failures only when the user already opted into a check" is
  satisfied — clicking Yes is opting in.

### Install

The `.bat` script (built by a pure helper `BuildInstallScript(string newExe, string
targetExe)` so it's unit-testable):

```
@echo off
timeout /t 2 /nobreak >nul
move /y "<new>" "<targetExe>"
start "" "<targetExe>"
del "%~f0"
```

Launched with `UseShellExecute = true`, `CreateNoWindow = true`. Then
`Application.Current.Shutdown()`. The standard window-closing handlers run, so the
existing dirty-template safeguards apply.

## Preferences UI

One checkable `MenuItem` under the existing menu (likely Help — confirmed during impl):

```xml
<MenuItem Header="_Check for updates on startup"
          IsCheckable="True"
          Click="MenuCheckForUpdates_Click" />
```

`IsChecked` is set from prefs on `Loaded`. The handler updates the in-memory
`AppPreferences` and calls `Save()`.

WPF Style child ordering reminder: any inline `Style` puts `Setter`s before
`Trigger`s — see `feedback_wpf_style_child_order.md`.

## Testing

New tests in `tests/OldenEra.TemplateEditor.Tests/Services/AutoUpdate/`. Test
framework follows whatever the existing test project uses (xUnit/NUnit) — no new
dependencies.

Unit-tested (TDD-driven):

- **`GitHubUpdateCheckerTests`** with an `HttpMessageHandler` test double — same/older
  version → null; newer + matching asset → `UpdateInfo` with AssetUrl; newer but no
  matching asset → `UpdateInfo` with `AssetUrl == null`; tag parsing for `v1.2`,
  `1.2`, `v1.2.3`, garbage; cancellation; non-200 → null.
- **`HttpUpdateDownloaderTests`** with the same handler double — streams to temp,
  monotonic progress, cancel deletes `.partial`, HTTP error leaves no `.partial`.
- **`AssetSelectionTests`** — pure helper testing the asset regex / version match
  logic in isolation. Covers case sensitivity, multiple matches (pick exact-version),
  no matches.
- **`BuildInstallScriptTests`** — pure helper assertion: contains `move`, `start`,
  `del`, properly quotes paths with spaces.

Skipped (per "minimal coverage is fine"):

- `BatchUpdateInstaller` end-to-end (shells out — testing it = re-implementing it).
- `UpdateProgressWindow` (WPF code-behind; manual test).
- `JsonAppPreferencesStore` (trivial JSON round-trip over a file path).
- `UpdateLog` (trivial append-with-cap).

## Upstream tracking

Add to `UPSTREAM.md`:

| Upstream | Status | Notes |
| --- | --- | --- |
| `4ae647c` | port (this PR) | Auto-update download + UpdateProgressWindow. Reimplemented under `Services/AutoUpdate/` with logging, prefs toggle, browser fallback. |
| `bc93dc5` | port (this PR) | Cancel button + CancellationToken plumbing. Folded into the v2 design. |
| `b062d10` | skip | Patch Notes — handled in sibling PR (number TBD). |
| `76ee588` | partial | UpdateProgressWindow.xaml.cs additions absorbed here. Monolithic MainWindow XAML edit and version bump rejected. |

Advance:

- Last reviewed upstream commit → `76ee588`
- Last review date → 2026-05-13

## Version

Bump `0.6.8` → `0.7.0` in
`src/OldenEra.TemplateEditor/OldenEra.TemplateEditor.csproj` (`Version`,
`AssemblyVersion`, `FileVersion`). Minor bump matches user-visible feature scope. Do
not copy upstream's number.

## Verification

- `dotnet build OldenEra.slnx` clean.
- `dotnet test` clean.
- WPF runtime can't be exercised on Mac. PR body lists Windows-reviewer test items:
  trigger dialog manually, cancel mid-download, complete a download, exercise the .bat
  handoff, toggle the menu pref and restart, verify silent failure when offline.
