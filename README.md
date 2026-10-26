# SystemTrace

Know what changed before something broke.

[简体中文](README.zh-CN.md) · [Product requirements](docs/PRODUCT.md) · [Development phases](docs/PHASES.md)

SystemTrace is a Windows 11 x64 desktop project for reviewing changes to software, monitored startup sources, PnP drivers, and network adapters. It is local first and designed to run without administrator privileges.

## Current milestone

PHASE 01: a .NET 10 / WPF application shell with five placeholder pages, generated MVVM, dependency injection, logging abstractions, English and Simplified Chinese resources, and light/dark themes. All example data is explicitly marked **DEMO**. Real collection, database persistence, tray monitoring, scheduling and login startup belong to later phases and are unavailable here.

## Build and run

Install the official .NET 10 SDK on Windows 11 x64. From this repository root:

```powershell
dotnet restore --locked-mode
dotnet build --no-restore
dotnet test --no-build
dotnet run --project src/SystemTrace.App
```

No administrator rights are requested. Closing the window exits the application. Language and theme changes are in memory for this milestone.

## Repository

```text
AGENTS.md
SystemTrace.slnx
src/
  SystemTrace.App/             # WPF views, view models, composition and resources
  SystemTrace.Core/            # Platform independent contracts
  SystemTrace.Collectors/      # Reserved for PHASE 03 Windows collectors
  SystemTrace.Infrastructure/  # Synthetic demo provider; persistence starts in PHASE 02
tests/SystemTrace.Tests/       # xUnit contracts, resources and WPF shell verification
docs/                         # Requirements, design constraints and verification records
.github/workflows/ci.yml       # Windows build and tests
```

## Document provenance

`AGENTS.md`, `docs/PRODUCT.md`, and `docs/PHASES.md` were imported unchanged from the user's downloads. The original package ZIP, README and other referenced design documents were unavailable. The README and architecture, data-model, UI and test notes were added during initialization, are explicitly scoped to PHASE 01, and do not claim to recover the missing originals. The supplied PRD and phase constraints remain authoritative.

See [verification](docs/PHASE-01-RESULTS.md), [third-party notices](docs/THIRD-PARTY.md), and [privacy](docs/PRIVACY.md). MIT licensed.
