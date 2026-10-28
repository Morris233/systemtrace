# Release procedure / 发布流程

1. Complete docs/TEST-PLAN.md and record exact actual results in PHASE-06-RESULTS.md. Keep NOT RUN environment experiments explicit.
2. Run locked restore, Release build, complete tests and formatting. Build locally with PowerShell 7: ./scripts/Build-Portable.ps1. Verify the ZIP and checksums. Packaging preserves source lock files and uses separate win-x64 locks.
3. Review tracked source/history for secrets, inventory, logs and personal paths. Update release notes, version, bilingual README and dependency notices. Do not package SDKs, caches, PDBs or real inventory.
4. Integrate reviewed stage PRs into main without rewriting history. A main-branch commit whose message contains **[release]** intentionally starts the Portable release workflow. Alternatively run that workflow manually on main. Ordinary commits do not publish.
5. The workflow rebuilds/tests/formats, packages/licenses/smoke-tests on Windows, uploads immutable artifacts, verifies SHA256 again, and creates the version tag plus GitHub Release once. Publishing is restricted to main; the publish job alone has contents-write permission. An existing version fails rather than silently replacing assets.
6. Verify the final commit, Actions success, tag target, Release assets/checksums and public repository visibility through GitHub. Record exact links. If access/visibility is blocked, report that step as pending.

The user explicitly authorized public publication, followed by continued project improvements and GitHub updates for v0.1.1. Future releases still require an intentional workflow dispatch or [release] marker from an authorized maintainer. Repository visibility is a separate GitHub settings action, not something the release token can change.

The package is a multi-file self-contained win-x64 ZIP. Extract the complete folder; do not copy the executable alone. It includes .NET runtime/native SQLite and corresponding licenses/notices. Deterministic managed compilation and locked package versions make the procedure reproducible; ZIP timestamps/SDK patch changes mean byte-identical builds across machines are not promised. BUILD-INFO.json records the actual commit/SDK/runtime.

The portable smoke explicitly removes shared runtime discovery, checks the loaded coreclr.dll is in the payload, uses an isolated test profile, checks second-instance handoff and clean exit, and asserts no inventory DB was created by opening. This is meaningful runtime isolation, not certification on every clean Windows installation.

打包与发布不会启用本机监测或自启，不会上传实际清单。公开仓库设置单独验证；不能把私有 Release、仅上传源码或工作流触发当作“公开发布完成”。包未签名，实际环境实验边界写入 Release Notes。
