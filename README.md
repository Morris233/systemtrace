# SystemTrace

[简体中文](README.zh-CN.md) · [Download](https://github.com/Morris233/systemtrace/releases/latest) · [Privacy](docs/PRIVACY.md) · [Contributing](CONTRIBUTING.md)

Local Windows history for software, monitored startup sources, present PnP drivers and network adapters.

![Change details with synthetic test data](docs/images/changes.png)

## Run

Windows 11 x64. Download the win-x64 ZIP from Releases, compare its SHA256 with SHA256SUMS.txt, extract it and open **SystemTrace.App.exe**. The portable package includes .NET 10; it needs no SDK, installer or administrator account. The build is unsigned.

Click **Create baseline** first. Successful categories establish a baseline without generating events. Scan again to record changes. Failed/partial categories retain their last successful baseline.

## Features

- Dashboard with the latest complete scan, latest attempt, category status and recent changes.
- Searchable, paged history with complete field evidence, before/after values and observation intervals.
- Read-only successful-snapshot comparison and time-window troubleshooting.
- English / Simplified Chinese, light / dark, keyboard focus and accessible labels.
- Optional tray monitoring, pause/resume, sign-in start, collector progress and cancellation.
- JSON/CSV export of the applied search across all pages, with privacy filtering on by default.
- Configurable retention and confirmed history deletion; metadata-only local diagnostics.

![Overview with synthetic test data](docs/images/overview.png)

Screenshots use fictional test fixtures. The application has no demo mode.

## Defaults and data

| Setting | Default |
| --- | --- |
| Background monitoring | Off |
| Close to tray | Off |
| Start at sign-in | Off |
| Interval when enabled | 30 minutes |
| History retention | 90 days; 0 keeps all |
| Export privacy filtering | On |

Monitoring choices apply with **Save** in Settings; language/theme save immediately. Closing exits unless close-to-tray was explicitly enabled. **Tray → Exit** always exits. A second launch opens the existing window. Pausing prevents future scheduled scans; Cancel stops the current uncommitted scan. After a system sleep, at most one due scan runs, with no catch-up queue.

Data lives under `%LOCALAPPDATA%\SystemTrace\`: `system-v1.db` (schema 2), `settings.json`, and bounded `diagnostics*.log`. Opening reads history without scanning or creating an inventory database. Old system schema 1 migrates atomically. Legacy demo files are ignored and never automatically removed.

Retention runs after scans or **Remove expired**; it preserves current baselines/backing evidence and the latest attempt even if older than the chosen age. **Delete all history** requires confirmation and establishes new baselines on the next successful scan. External exports/backups are outside that deletion.

The app makes no network requests or telemetry uploads. Startup commands are evidence and never execute. The only configuration write is the explicitly enabled, exact-owned current-user SystemTrace login entry. Network history excludes IP/MAC/SSID/passwords. Reports hide free-text identities, commands and paths by default; unfiltered export needs confirmation. See [privacy](docs/PRIVACY.md).

## Limits

Software coverage omits portable/some Store apps. Startup coverage includes Run/RunOnce and startup folders, omitting tasks/services/other users. PnP absence means “not detected”, not proof of uninstall. Adapter operational state is distinct from administrative enable state. Observation windows are evidence intervals, not exact occurrence times or a diagnosis.

Rendering was checked at 100/150/200% scale. Physical mixed-monitor transitions, a full manual screen-reader session, actual sign-out/sign-in and third-party installation experiments remain unperformed. Portable runtime-isolated smoke tests do not claim clean-VM certification. See the explicit [T001–T026 matrix](docs/TEST-PLAN.md) and [release verification](docs/PHASE-06-RESULTS.md).

## Build and verify

Windows 11 x64, official .NET 10 SDK. Packaging uses PowerShell 7.

```powershell
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build --logger trx
dotnet format --verify-no-changes --no-restore
dotnet run --project src/SystemTrace.App
./scripts/Build-Portable.ps1
```

The package script performs a separate win-x64 locked restore, publishes the self-contained app, includes dependency licenses/notices, verifies runtime isolation and process exit, and writes a ZIP, SHA256SUMS.txt and BUILD-INFO.json under artifacts/release. Tests create only isolated fixture keys/directories. SYSTEMTRACE_DATA_DIR provides explicit test-process data isolation.

Normal and win-x64 publish lock files are separate. Source/payload excludes inventory databases, logs, credentials, build caches and private test results. See [dependencies](docs/THIRD-PARTY.md) and [release procedure](docs/RELEASING.md).

## Layout and provenance

App (WPF/MVVM), Core (platform-independent contracts/diff/query/schedule), Collectors (read-only Windows adapters), Infrastructure (SQLite/settings/exports), tests, docs and scripts.

AGENTS.md, PRODUCT.md and PHASES.md were supplied from the user's downloads. PRODUCT/PHASES remain unchanged; AGENTS carries the user's concise-UI rule. Missing design originals were replaced by clearly identified development notes, not claimed as recovered. Six implementation milestones are recorded in docs/PHASE-01-RESULTS.md through docs/PHASE-06-RESULTS.md.

MIT. Third-party components retain their respective notices.
