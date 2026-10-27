# Test plan / 测试计划

Initialization supplement, not the missing original T001–T026 plan. 本次新增；未取得原 T001–T026，不冒充原测试清单。

## PHASE 01 automated acceptance

| ID | Contract / 验收要求 |
| --- | --- |
| P01-01 | Build all four layers and xUnit on Windows / Windows 构建四层及测试工程 |
| P01-02 | Five pages navigate and invalid selection has safe fallback / 五页面切换及无效选择回退 |
| P01-03 | English and Chinese keys match, values nonempty, real dictionaries load / 双语资源完整且可加载 |
| P01-04 | Both themes load and preserve localization on replacement / 主题切换不破坏语言资源 |
| P01-05 | All pages render under both languages and themes with no binding errors / 四种组合渲染无绑定错误 |
| P01-06 | Demo data remains explicit, fixed, and limited to four categories / 演示数据标志及四类别 |
| P01-07 | Core has no platform/persistence dependency / Core 无平台及数据库依赖 |
| P01-08 | Manifest requests asInvoker; no startup/tray/collector/persistence registration / 普通权限，无越阶段功能 |

## Manual acceptance

Open the built Windows application without elevation. Visit all five pages, change theme and language, inspect focus using Tab/Shift+Tab, resize to 960×620 and inspect 100/150/200% DPI, then close and confirm the process exits. Check there is no SystemTrace database, registry change, background scan, telemetry or auto-start. Automated render tests supplement, but do not replace, these checks.

以普通权限启动，切换五页面、双语及主题；检查键盘焦点、最小尺寸及三种 DPI；关闭后确认进程退出。检查无数据库、自启、后台扫描和遥测。自动渲染测试不能代替完整手工验收。

## PHASE 02 automated acceptance / 第二阶段自动验收

- Initial successful baseline has no events; same state and different enumeration order yield no changes.
- Added/removed/modified evidence and UTC observation windows round-trip; missing/null/empty/whitespace differ.
- Stable keys normalize Windows identity casing/separators, preserve source scope and avoid delimiter collision; Core stays platform independent.
- Exactly four unique category outcomes, valid time intervals, duplicate key and unreadable-field validation.
- Partial/Failed/Skipped preserve last successful category baseline, retain available evidence, and never cause false removals; recovery compares against the last success. Successful empty result may remove.
- Reusing scan ID with identical canonical input is idempotent; different input is rejected; concurrent duplicate connections commit once.
- Injected SQL failure after an earlier category write rolls back run, results, snapshots, changes and baseline movement.
- Cancelled commit, backwards observation, origin mismatch and future schema fail without overwriting history; SQL-looking field/key values remain data.
- Restart persistence, version-1 migration, no database creation on empty load, recoverable UI failure, command reentry/cancellation.
- WPF runs two real synthetic commits before rendering all five pages × two themes × two languages; startup and close are exercised separately.

初始基线、规范化、字段差异、混合采集状态、故障恢复、事务回滚、并发幂等、取消、来源隔离、迁移、重开读取及双语主题渲染均通过真实 SQLite 临时库测试；测试只清理自己的隔离目录。

## PHASE 03 executed coverage / 第三阶段覆盖

- Software source scopes, shared HKCU view, separate HKLM views, skipped unnamed entries and absence of uninstall/install-path fields.
- Inert Run/RunOnce commands, folder metadata, source identities and transient warnings; commands are not expanded or executed.
- PnP fake version changes / absence / missing values / enumeration failures; adapter fake rename/state with stable GUID and approved fields only.
- Collector cancellation and unexpected-failure isolation; failed/partial driver/network results cannot remove old baselines.
- Real non-elevated Windows read-only scan and baseline creation; only counts/status retained outside the temporary database.
- Isolated current-user registry add/version-edit/remove detection and HKCU view sharing; isolated startup file/value reads; all fixtures cleaned up.
- Both modes, separate persisted histories, localized status/warnings, not-detected wording, 40 page/language/theme render combinations and actual app startup/exit.
- Older collection JSON defaults, schema/idempotency/diff/rollback tests from PHASE 02 remain active.

Windows integration tests intentionally create only random test-owned registry keys. A constrained execution context may deny these writes; rerun the fixture suite in the normal user context rather than weakening system permissions or skipping tests. 受限环境初次拒绝测试键写入时，应使用正常用户上下文重验，并记录实际权限；不修改系统权限，不假装测试通过。

## Later-phase coverage / 后续阶段

PHASE 02 requires initial baseline, add/remove/modify, canonical ordering, null values, duplicate scans, mixed category statuses, partial/failed baseline protection and transaction rollback tests. PHASE 03 adds isolated fake Windows sources and read-only integration checks. Later phases add privacy-safe export, opt-in scheduling/startup, recovery, performance and clean-machine packaging checks per PRODUCT.md. Recover or replace the missing full release test plan before PHASE 06.

第二阶段覆盖基线、差异、规范化、去重、失败保护与事务回滚；第三阶段加入隔离的采集测试。后续补齐隐私导出、调度、自启、恢复、性能与干净机器验证。第六阶段前须补全正式发布测试计划。
