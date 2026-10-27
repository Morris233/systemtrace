# SystemTrace

Know what changed before something broke.

[简体中文](README.zh-CN.md) · [Product requirements](docs/PRODUCT.md) · [Development phases](docs/PHASES.md)

SystemTrace is a Windows 11 x64 desktop project for reviewing changes to software, monitored startup sources, PnP drivers, and network adapters. It is local first and designed to run without administrator privileges.

## Current milestone

PHASE 03: four read-only Windows collectors with stable identities, per-category results, field-level diffs and atomic SQLite history. The app opens in **System** mode: click **Scan now** to establish category baselines, then scan again to detect changes. Partial/failed categories retain their previous baseline. **Demo** switches to separate synthetic history. Full comparison/search/detail UI, tray monitoring, scheduling and login startup remain later phases.

## Build and run

Install the official .NET 10 SDK on Windows 11 x64. From this repository root:

```powershell
dotnet restore --locked-mode
dotnet build --no-restore
dotnet test --no-build
dotnet run --project src/SystemTrace.App
```

No administrator rights are requested. Closing the window exits the application. Language/theme/mode choices are session-only. Opening the app reads existing history without scanning or creating a new database. A manual system scan writes `%LOCALAPPDATA%/SystemTrace/system-v1.db`; demo actions use `demo-v1.db`. Never commit either database. Startup commands are stored as evidence only and are never executed. Network history excludes IP/MAC/SSID/passwords.

## Repository

```text
AGENTS.md
SystemTrace.slnx
src/
  SystemTrace.App/             # WPF views, view models, composition and resources
  SystemTrace.Core/            # Platform independent contracts and diff engine
  SystemTrace.Collectors/      # Four read-only collectors and fakeable Windows sources
  SystemTrace.Infrastructure/  # Transactional SQLite repository and system/demo sessions
tests/SystemTrace.Tests/       # xUnit contracts, resources and WPF shell verification
docs/                         # Requirements, design constraints and verification records
.github/workflows/ci.yml       # Windows build and tests
```

## Document provenance

`AGENTS.md`, `docs/PRODUCT.md`, and `docs/PHASES.md` were imported from the user's downloads. The product and phase documents remain unchanged; AGENTS.md subsequently received the user's explicit concise-UI rule. The original package ZIP, README and other referenced design documents were unavailable. The README and architecture, data-model, UI and test notes were added during initialization, are explicitly scoped to PHASE 01, and do not claim to recover the missing originals. The supplied PRD and phase constraints remain authoritative.

See [PHASE 03 verification](docs/PHASE-03-RESULTS.md), [PHASE 02 history](docs/PHASE-02-RESULTS.md), [third-party notices](docs/THIRD-PARTY.md), and [privacy](docs/PRIVACY.md). MIT licensed.
