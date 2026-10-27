# Contributing / 贡献

Use Windows 11 x64, the official .NET 10 SDK and PowerShell 7 for packaging. Read AGENTS.md and docs/PRODUCT.md before changing behavior.

```powershell
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
dotnet format --verify-no-changes --no-restore
./scripts/Build-Portable.ps1
```

Normal and win-x64 publish restores have separate lock files. Update both deliberately when changing packages. Production collectors are read-only; only the explicitly enabled SystemTrace current-user login value can be managed. Partial/Failed cannot create removals. Keep UTC observation intervals, privacy-filtered exports, atomic transactions and cancellation semantics covered by tests.

Keep UI copy short and functional. Every interface key needs English and Simplified Chinese. Style uses spacing/type/color rather than slogans. Provide before/after screenshots with synthetic fixtures when changing layout.

Report reproducible steps, expected/actual behavior, app version and Windows build. Use fake fixtures or a carefully reviewed redacted report; never attach an inventory database, raw startup commands, personal paths, credentials or a user's complete diagnostic directory to a public issue.

Small PRs should explain behavior and include actual verification. Do not claim manual/DPI/clean-machine checks that were not performed. See docs/TEST-PLAN.md for the release matrix. Contributions are under MIT.

提交前阅读规范，使用隔离测试，不修改真实软件、启动项、驱动或网卡。界面少字、双语同步、失败不误报；提交说明写清行为与实际验证。公开 issue 不附原始清单库或个人信息。
