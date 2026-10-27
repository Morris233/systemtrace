# Release acceptance / 发布验收

This T001–T026 matrix was reconstructed from PRODUCT.md because the original full test plan was unavailable. It does not claim to reproduce that missing document. All rows are P0 for the stated automated scope. Optional physical/environment experiments are listed separately with their actual status.

本表根据原 PRD 补齐，非缺失原稿的恢复。P0 结论只覆盖表内明确的自动化范围；实机环境实验单列，不以截图替代。

| ID | P0 acceptance / 验收 | Evidence / 证据 |
| --- | --- | --- |
| T001 | Self-contained Windows x64 package starts without shared .NET and exits / 无系统 .NET 启动退出 | scripts/Test-Portable.ps1 verifies loaded coreclr path, isolated profile, second launch and exit |
| T002 | No scan/DB creation/background/self-start by default / 默认不扫描、不建清单库、不常驻自启 | MonitoringTests defaults; portable smoke; App DI/manual scan |
| T003 | First successful category result establishes baseline only / 首次成功仅基线 | InventoryTests, RepositoryTests, WindowsIntegrationTests |
| T004 | Stable identities preserve scope and ignore display-name identity / 稳定键与范围 | StableKeys and collector identity tests |
| T005 | Add/remove/modify, ordering and null/error evidence / 差异与空值错误 | InventoryTests; HistoryReaderTests full evidence; presenter tests |
| T006 | Partial/Failed/Skipped cannot imply removal or advance baseline / 失败保护 | RepositoryTests mixed results and collector failure/recovery |
| T007 | Atomic commit, idempotency, rollback and cancellation / 事务幂等回滚 | RepositoryTests trigger failure, duplicate, concurrent and cancelled cases |
| T008 | All four collectors use documented read-only sources / 四类采集 | CollectorTests and 4 isolated Windows integration cases |
| T009 | Network/history scope excludes prohibited identifiers / 数据范围 | Collector contracts/field allowlists and source inspection |
| T010 | Commands never execute; only owned login value may be written / 不执行命令及自启归属 | Inert startup fixtures; WindowsLoginStart isolated ownership test |
| T011 | Latest complete and attempted scan are distinct / 完整成功与最近尝试区分 | HistoryReaderTests dashboard and baseline retention |
| T012 | Search/category/UTC overlap and deterministic bounded paging / 检索与分页 | HistoryReaderTests 2,400 records; HardeningTests 12,000 records |
| T013 | Full before/after detail preserves missing/null/empty/unreadable / 完整详情 | HistoryReaderTests, ViewModelTests, WPF render |
| T014 | Successful snapshot selection/comparison is read-only / 成功快照只读比较 | Snapshot validation and 43-header pinned-selection tests |
| T015 | Troubleshooting filters observation windows without causal claims / 回溯不归因 | Inclusive-overlap/timezone tests, local time validation, bilingual caution |
| T016 | Chinese/English and light/dark switch without losing state / 双语主题与状态 | ShellTests resource parity and selection/detail preservation |
| T017 | Minimum 960×620 DIP and 96/144/192-DPI raster output remain usable / 最小尺寸与三种渲染缩放 | 60 WPF page/language/theme/scale renders plus normal/empty cases, visual inspection |
| T018 | Keyboard focus traversal and meaningful accessible control names / 焦点与可访问名称 | Five-page WPF traversal; field/row/record labels; F5 and Enter bindings |
| T019 | One pipeline, collector progress and cancellable uncommitted work / 单扫描进度取消 | MonitoringTests gate/progress/cancellation, view-model busy tests |
| T020 | Opt-in timer, pause/resume, tray hide/show/exit / 调度托盘 | MonitorSchedule fake clock, actual WPF tray lifecycle and process smoke |
| T021 | Login start only manages exact owned current-user entry / 登录启动 | Random HKCU key test with foreign-value preservation; no real login entry enabled |
| T022 | Atomic persisted settings and bounded metadata-only diagnostics / 设置日志 | MonitoringTests JSON cancellation/future version/rollback and log privacy |
| T023 | All-page JSON/CSV, default redaction, formula-safe CSV / 隐私导出 | Export filter/field-state/redaction/CSV tests; atomic sibling-file save path |
| T024 | Retention keeps needed baseline; confirmed delete re-baselines / 保留与删除 | Retention/recovery, rollback/cancellation, confirmation and cleared-view tests |
| T025 | Corruption/clock reversal fail safely; large history stays bounded / 恢复与性能 | HardeningTests corruption preservation, clock guard, 12,000-change paging/stream export |
| T026 | Locked CI, dependency notices, SHA256, public source and verified Release / 发布 | CI and Build-Portable validation; final public visibility/tag/assets verified at delivery |

Local automated scope: PASS after the final complete run recorded in PHASE-06-RESULTS.md. Remote CI and publication are recorded by exact commit/run/release URLs in that report and the final delivery. A pending external publication step is not a passed T026.

## Optional experiments / 单列实验

| Experiment | Status / 状态 |
| --- | --- |
| Physical mixed-monitor DPI transitions / 物理跨显示器 DPI | NOT RUN; automated raster scales are not this test |
| Full manual keyboard/screen-reader session / 完整人工键盘与读屏 | NOT RUN; automated WPF focus/name traversal passed |
| Actual Windows sign-out/sign-in / 注销再登录 | NOT RUN; isolated registration and command construction passed |
| Actual third-party install/uninstall / 第三方安装卸载 | NOT RUN; isolated registry add/version/remove passed |
| Fresh clean-machine VM certification / 全新虚拟机认证 | NOT RUN; bundled-runtime/empty-profile smoke runs locally and on Windows CI |
| Forced power loss / 硬断电 | NOT RUN; SQLite transaction failure/rollback tested |

Do not alter system display settings, actual startup entries or real installed software merely to turn a NOT RUN row green. Preserve those boundaries in release notes.

## Reproduce / 复现

```powershell
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build --logger trx
dotnet format --verify-no-changes --no-restore
./scripts/Build-Portable.ps1
```

Windows tests create and remove only random test-owned HKCU keys/directories. Run them in a normal user desktop context when constrained execution denies fixture writes or desktop messages. The real inventory smoke uses an isolated database and retains only aggregate status/count/timing metadata. SYSTEMTRACE_DATA_DIR is a documented test-process isolation override; ordinary runs use LOCALAPPDATA/SystemTrace.
