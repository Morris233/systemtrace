# SystemTrace

[简体中文](README.zh-CN.md) · [Product requirements](docs/PRODUCT.md) · [Development phases](docs/PHASES.md)

SystemTrace is a Windows 11 x64 desktop app for reviewing changes to software, monitored startup sources, present PnP drivers, and network adapters. Inventory stays local and runs with ordinary user privileges.

## Current milestone

PHASE 05: opt-in background monitoring, tray controls, sign-in start, progress/cancellation, persisted settings, privacy-first JSON/CSV export and retention are implemented alongside the dashboard/search/detail/comparison UI. Background, close-to-tray and sign-in start default off. Use Settings → Save to enable them. The scan interval defaults to 30 minutes and history to 90 days (0 keeps all).

Click **Scan now** to establish baselines. Failed/partial categories keep their last successful baseline. Exports cover the applied change search, all pages; personal details are hidden by default. Snapshot comparison is read-only and troubleshooting shows observation-window clues rather than a diagnosis. Release hardening is PHASE 06.

## Build and run

Install the official .NET 10 SDK on Windows 11 x64. From the repository root:

```powershell
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
dotnet run --project src/SystemTrace.App
```

No administrator rights are requested. Closing exits unless you explicitly enabled close-to-tray; Tray → Exit always exits. Language/theme choices are persisted. Opening reads existing history without scanning or creating a new database. A manual scan writes `%LOCALAPPDATA%/SystemTrace/system-v1.db`; existing schema 1 migrates atomically to schema 2. The old demo database is ignored, never deleted. Never commit inventory databases. Startup commands are stored as evidence only and are never executed. Network history excludes IP/MAC/SSID/passwords.

## Repository

```text
AGENTS.md
SystemTrace.slnx
src/
  SystemTrace.App/             # WPF views, view models, composition and resources
  SystemTrace.Core/            # Platform-independent inventory, diff and query contracts
  SystemTrace.Collectors/      # Four read-only collectors and fakeable Windows sources
  SystemTrace.Infrastructure/  # Transactional SQLite history, paging and system session
tests/SystemTrace.Tests/       # xUnit, isolated fixtures, integration and WPF verification
docs/                         # Requirements, design and phase verification records
.github/workflows/ci.yml       # Windows locked restore, Release build, tests and formatting
```

## Document provenance

`AGENTS.md`, `docs/PRODUCT.md`, and `docs/PHASES.md` came from the user's downloads. PRODUCT and PHASES remain unchanged; AGENTS received the explicit concise-UI rule. The original ZIP, README and other design originals were unavailable. The README and design/test notes were added during development and do not claim to recover missing originals. The supplied PRD and phase constraints remain authoritative.

See [PHASE 05 verification](docs/PHASE-05-RESULTS.md), [PHASE 04 verification](docs/PHASE-04-RESULTS.md), [PHASE 03](docs/PHASE-03-RESULTS.md), [third-party notices](docs/THIRD-PARTY.md) and [privacy](docs/PRIVACY.md). MIT licensed.
