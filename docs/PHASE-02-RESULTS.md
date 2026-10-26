# PHASE 02 verification / 第二阶段验证

Phase: P2 / Domain, diff engine and SQLite

Implemented: stable keys, immutable validated inventory contracts, canonical field diffs, UTC observation windows, versioned SQLite repository, transactional category baselines and events, origin isolation, demo action, saved history reload, overview/timeline/baseline-list integration. Real Windows collectors remain out of scope.

已实现稳定键、只读契约、字段差异、UTC 观察窗口、SQLite 迁移及事务、类别独立基线、幂等事件、演示来源隔离、演示操作与历史重载。真实 Windows 采集不在本阶段范围内。

Changed files: Core inventory models/keys/diff engine/interfaces; Infrastructure SQLite repository and DemoSession; App composition, generated commands, three data-bound pages and localized resources; domain/repository/view-model/WPF tests; dependency locks and bilingual documentation. Obsolete static demo provider removed. PRODUCT.md, PHASES.md and the user's concise-UI rule are retained.

## Acceptance / 验收

- [x] First successful baseline emits zero changes / 首次成功不报新增
- [x] Added, removed and modified with complete evidence / 增删改及字段证据
- [x] Canonical item/field ordering and missing/null/empty distinctions / 规范化及空值区分
- [x] Idempotent retries and concurrent duplicate submission / 重试及并发去重
- [x] Partial/Failed/Skipped never advance baselines or imply removal / 失败保护
- [x] Mixed per-category outcomes and recovery against last success / 混合状态及恢复
- [x] SQL failure after partial writes rolls back the entire run / 全事务回滚
- [x] Migration, restart, cancellation, origin/schema guard, parameterized SQL / 迁移、重载、取消与库保护
- [x] Demo UI uses real repository; generated command handles busy/error/cancel / 界面接入实际演示库
- [x] Simplified Chinese / English, light / dark, five-page render regression
- [ ] Manual DPI 100/150/200%, complete keyboard audit / DPI 及完整键盘手测
- [ ] Remote CI result / 远端 CI 结果

## Executed checks / 实际检查

Windows x64, .NET SDK 10.0.403, Release. Local commands use the existing task-only SDK/environment wrapper and verified official NuGet package feed because Windows TLS in the command environment fails; certificates were not bypassed.

| Check | Result |
| --- | --- |
| `dotnet restore --locked-mode` (local feed) | PASS |
| `dotnet build --configuration Release --no-restore` | PASS, 0 warnings / 0 errors |
| `dotnet test --configuration Release --no-build --logger trx` | PASS, 33 passed / 0 failed / 0 skipped |
| `dotnet format --verify-no-changes --no-restore` | PASS |
| WPF render combinations at 960×620 | PASS, 20 combinations after two actual demo commits |
| Executable launch and normal close | PASS |
| Resolved dependencies vs NuGet advisory feed | PASS, 26 resolved packages, 0 matches on 2026-10-26 |
| GitHub Windows CI | Pending PR run / 等待 PR 运行 |

The dependency check found an older native SQLite advisory before delivery. The implementation now uses Microsoft.Data.Sqlite 10.0.14 with SQLitePCLRaw 2.1.13, whose official package versions and licenses were verified and locks regenerated. This is an advisory-feed check, not a complete security audit.

依赖检查发现旧原生库公告后，已改用官方补丁版并重建锁文件；当前对照 NuGet 公告无匹配项。该结果不是完整安全审计。

Manual verification: sampled real WPF renders for overview and timeline, both themes/languages, plus the real executable startup/exit test. Physical DPI and full keyboard checks were NOT RUN. No system inventory API is present in the Collectors project.

Known limitations: only synthetic data is collected. Current history view loads the newest 200 changes, with cumulative counts from all stored changes. Pagination, production comparison, field-detail UI and time-window search belong to PHASE 04. Settings are session-only. No automatic migration of hypothetical external databases, export, tray, startup or retention workflow is introduced. Missing original release-test specification still needs completion before PHASE 06.

Next phase proposal: PHASE 03, four read-only Windows collectors, after reviewing this stage. 第二阶段不自动跨入第三阶段。
