# SystemTrace

Know what changed before something broke.

[简体中文](README.zh-CN.md) · [Product requirements](docs/PRODUCT.md) · [Development phases](docs/PHASES.md)

SystemTrace is a Windows 11 x64 desktop project for reviewing changes to software, monitored startup sources, PnP drivers, and network adapters. It is local first and designed to run without administrator privileges.

## Current milestone

PHASE 02: snapshot contracts, canonical identity, field-level diffs, transactional SQLite storage and an interactive demo in the existing bilingual WPF shell. Click **Run demo** once to create a baseline, then again to generate changes. Restarting reloads the saved demo. All data remains explicitly **DEMO**. Real system collection, production comparison/search, tray monitoring, scheduling and login startup belong to later phases.

## Build and run

Install the official .NET 10 SDK on Windows 11 x64. From this repository root:

```powershell
dotnet restore --locked-mode
dotnet build --no-restore
dotnet test --no-build
dotnet run --project src/SystemTrace.App
```

No administrator rights are requested. Closing the window exits the application. Language and theme changes are in memory. Opening the app does not create a new database; the demo action writes synthetic history to `%LOCALAPPDATA%/SystemTrace/demo-v1.db`, separated from future real inventory. Never commit that database.

## Repository

```text
AGENTS.md
SystemTrace.slnx
src/
  SystemTrace.App/             # WPF views, view models, composition and resources
  SystemTrace.Core/            # Platform independent contracts and diff engine
  SystemTrace.Collectors/      # Reserved for PHASE 03 Windows collectors
  SystemTrace.Infrastructure/  # Transactional SQLite repository and demo session
tests/SystemTrace.Tests/       # xUnit contracts, resources and WPF shell verification
docs/                         # Requirements, design constraints and verification records
.github/workflows/ci.yml       # Windows build and tests
```

## Document provenance

`AGENTS.md`, `docs/PRODUCT.md`, and `docs/PHASES.md` were imported from the user's downloads. The product and phase documents remain unchanged; AGENTS.md subsequently received the user's explicit concise-UI rule. The original package ZIP, README and other referenced design documents were unavailable. The README and architecture, data-model, UI and test notes were added during initialization, are explicitly scoped to PHASE 01, and do not claim to recover the missing originals. The supplied PRD and phase constraints remain authoritative.

See [PHASE 02 verification](docs/PHASE-02-RESULTS.md), [PHASE 01 history](docs/PHASE-01-RESULTS.md), [third-party notices](docs/THIRD-PARTY.md), and [privacy](docs/PRIVACY.md). MIT licensed.
