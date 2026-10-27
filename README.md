# SystemTrace

[简体中文](README.zh-CN.md) · [Product requirements](docs/PRODUCT.md) · [Development phases](docs/PHASES.md)

SystemTrace is a Windows 11 x64 desktop app for reviewing changes to software, monitored startup sources, present PnP drivers, and network adapters. Inventory stays local and runs with ordinary user privileges.

## Current milestone

PHASE 04: production dashboard, searchable/paged change history, complete before/after details, successful-snapshot comparison and observation-window troubleshooting. Click **Scan now** to establish baselines, then scan again to detect changes. Partial/failed categories retain their last successful baseline. Demo data and mode have been removed from the app; synthetic fixtures exist only in tests.

Search supports literal names, field names and before/after values plus a category filter. Details preserve null, empty, missing and unreadable values. Snapshot comparison is read-only. Troubleshooting matches observation intervals and provides clues, not a diagnosis. Tray monitoring, scheduling, login startup, exports and retention remain PHASE 05.

## Build and run

Install the official .NET 10 SDK on Windows 11 x64. From the repository root:

```powershell
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
dotnet run --project src/SystemTrace.App
```

No administrator rights are requested. Closing the window exits. Language/theme choices are session-only. Opening reads existing history without scanning or creating a new database. A manual scan writes `%LOCALAPPDATA%/SystemTrace/system-v1.db`; existing schema 1 migrates atomically to schema 2. The old demo database is ignored, never deleted. Never commit inventory databases. Startup commands are stored as evidence only and are never executed. Network history excludes IP/MAC/SSID/passwords.

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

See [PHASE 04 verification](docs/PHASE-04-RESULTS.md), [PHASE 03](docs/PHASE-03-RESULTS.md), [third-party notices](docs/THIRD-PARTY.md) and [privacy](docs/PRIVACY.md). MIT licensed.
