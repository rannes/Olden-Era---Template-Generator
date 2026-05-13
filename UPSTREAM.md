# Upstream Tracking

This fork tracks `KhanDevelopsGames/Olden-Era---Template-Generator`. Use this file to
record what we have synced, what we consciously skipped, and where the cursor sits.

## Cursor

| Field | Value |
| --- | --- |
| Upstream remote | `https://github.com/KhanDevelopsGames/Olden-Era---Template-Generator.git` |
| Last reviewed upstream commit | `edb64cb` ("zone customization UI improvements", 2026-05-11) |
| Last common merge base | `1bf6fd7` ("fix random tournament layout + preview") |
| Last review date | 2026-05-11 |

To recompute divergence:

```bash
git fetch upstream
git rev-list --left-right --count upstream/main...main
git log --oneline $(git merge-base upstream/main main)..upstream/main
```

## Triage table

Status legend: **have** = we already have an equivalent change; **port** = should be
brought over; **skip** = consciously not bringing over (reason given); **deferred** =
intend to port but tracked as its own initiative.

| Upstream | Subject | Status | Notes |
| --- | --- | --- | --- |
| `49b88d1` | made bridge gaps smoother | have | `TemplatePreviewRenderer.cs:623-624` already at `FadeExtraPx=14.0`, `FadeSteps=50` (came in `aa5ff5d`). |
| `2ae2c56` | prevent connection lines from going out of bounds | have | Deflection cap + canvas clamp at `TemplatePreviewRenderer.cs:407-419`. |
| `7d99e5b` | ensure guards in cities | have | All four city/spawn `GuardChance` sites set to `1.0` in `TemplateGenerator.cs`. |
| `aa88894` | hide preview in UI | skip | Their motivation was renderer bugs; ours are fixed. We rely on the preview in both Web and WPF. |
| `dded712` | refactored texts | skip | XAML string tweaks against a `MainWindow.xaml` we have since decomposed into per-panel UserControls. |
| `8a077aa` | small ui refactor | skip | Same — touches the monolithic XAML we replaced. |
| `6cab0ae` | removed outdated road hint | port (verify) | Check the equivalent hint did not survive in our panel split; remove if so. |
| `c1106d6` | add link to community discord | deferred | Port to WPF + Web header. Use upstream's Discord URL. |
| `2c4f619` | add github button | deferred | Port to WPF + Web header. Point at this fork. |
| `364d435` | fix dropdown issues on some devices | skip | Patch targets `Themes/MedievalTheme.xaml` which we deleted (`65777cd`). Our `ModernDarkTheme` ComboBox style is unaffected. Re-evaluate if users report the symptom. |
| `0c45c42` | Content Item manager (initial abstraction) | deferred (PR #17) | Part of "customizable player starting zone content"; track as one feature. |
| `55a31a4` | Distance ruleset abstraction | deferred (PR #17) | Same feature group. |
| `f47fae3` | Customizable starting zone content (impl) | deferred (PR #17) | Adds `PlayerZoneMandatoryContent` setting + WPF UI + `Services/ContentManagement/` layer. See `docs/plans/2026-05-11-upstream-sync.md` for design notes. |
| `4a76ce2` | Added guards to default content | deferred (PR #17) | Tail end of the same feature. |
| `edb64cb` | zone customization UI improvements | deferred (PR #17) | Polish on top of the above. |
| `b062d10` | add patch notes button | port | Idea ported: Patch Notes ghost button in `MainWindow.xaml`/`.cs` action toolbar (reuses `GitHubReleasesPage` constant) and matching link in `OldenEra.Web/Pages/Home.razor` header. |

## Sync workflow

When syncing a new round of upstream commits:

1. `git fetch upstream`
2. List new commits since the cursor:
   `git log --oneline edb64cb..upstream/main`
3. For each new commit, append a row to the triage table with status.
4. Update the **Last reviewed upstream commit** + **Last review date** to the newest hash you classified.
5. For `port` rows, open a PR. For `deferred`, open a plan in `docs/plans/`.
6. Commit this file alongside the work it records.
