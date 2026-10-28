# Architecture / 架构

## PHASE 07 / History usability

Core adds an optional validated change kind to the history query. Infrastructure shares the SQL filter and parameter construction between paged reads and streaming exports. App applies a candidate query only after a successful read, provides one reset command and preserves type selection across localization changes. Existing database schema, collection sources and defaults are unchanged.

历史查询与导出共用条件；界面查询成功后才提交筛选状态，失败保持旧结果。无新依赖、无迁移。

## PHASE 06 / Release hardening

The repository now supplies a replacement T001–T026 matrix explicitly scoped to observed automated evidence and separate NOT RUN environmental experiments. Windows CI builds/tests/formats and verifies a self-contained win-x64 package. Normal and runtime-specific NuGet locks remain separate. Build-Portable gathers production/runtime licenses and attribution, records commit/SDK/runtime, validates payload and extracted-ZIP process/runtime isolation, then atomically writes ZIP/checksums. Release publishing is a gated main-only workflow with no silent version overwrite.

SYSTEMTRACE_DATA_DIR is an explicit test-process isolation override; ordinary runs continue using LOCALAPPDATA/SystemTrace. It prevents smoke tests from reading/writing a user's actual history/settings. Single-instance activation is scoped by the resolved data directory. Public screenshots are compiled only into tests.

Export hardening validates retained numeric/date/enum metadata and redacts nonstandard values rather than trusting a field name. CSV checks leading controls/Unicode-format characters as well as whitespace/formula prefixes. WPF accessible names now cover record buttons, lists, field values and identity; five-page traversal exercises them. Appearance persistence preserves existing validation errors while translating them. The first scan action directly says Create baseline.

发布阶段补齐可复现打包、许可、校验、运行时隔离与 ZIP 解压运行检查。用户已明确授权公开发布；仓库公开状态单独验证，不从提交成功推断。未执行的物理环境实验持续如实记录。


## PHASE 05 / 第五阶段

Monitoring is explicitly opt-in. AppPreferences persists language/theme, interval/retention and background/close-to-tray/sign-in flags with atomic JSON replacement. A per-user named instance lock and activation event avoid competing app instances. SystemScanSession owns a pipeline gate and reports collector progress; manual/tray/timer paths use the same generated scan command. MonitorSchedule is clock-driven, skips busy deadlines, and never queues a catch-up burst.

NotifyIcon/ContextMenuStrip come from the framework Windows Forms assembly; WPF remains the owner of DPI configuration and main window. WFO0003 is narrowly suppressed because Forms supplies only NotifyIcon; WPF retains its manifest PerMonitorV2 declaration. Tray disposal and close/initialization races are covered by real process/WPF tests. Sign-in start writes only an exact owned current-user value when explicitly enabled.

Database maintenance uses immediate transactions and secure_delete, preserving baselines/latest attempt during retention. Export streams one evidence row at a time from a consistent read transaction, converts to a dedicated redacted DTO, and does not load all history. App file saving is atomic via a sibling temporary file. CSV uses quoting and formula guards. Diagnostics intentionally omit message text, stacks and collected evidence; only metadata is retained locally. No new NuGet package was added.

后台、托盘常驻和登录启动默认关闭；手动、托盘、定时共用扫描锁。暂停与设置即时作用于调度，不积压扫描。原子设置、保留事务、默认脱敏导出均有隔离测试。WinForms 只用于托盘，主界面仍是 WPF。


## PHASE 04 current architecture / 第四阶段现状

App now composes one system repository/session and an IHistoryReader. Production contains no demo generator, demo interface or mode switch. Synthetic SampleSession and fixture inventory exist only in the test assembly. Existing demo-v1.db is ignored and never deleted. The system filename remains system-v1.db for compatibility; its schema advances transactionally to version 2.

Core owns ChangeQuery, bounded PageResult, lightweight ChangePreview, SnapshotHeader and DashboardSummary contracts. Infrastructure executes filtering/counting/paging in SQLite on worker tasks, using one consistent read transaction per operation. App holds at most 40 rows per list, 8 recent previews, one selected detail per page, and 40 snapshot headers plus at most two pinned selections. A virtualized list with recycling is bounded by the page grid; it is not nested in an outer unbounded ScrollViewer. See [WPF control performance](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-controls).

Dashboard queries distinguish the latest attempt from the latest run where all four categories succeeded. Per-category latest counts are nullable for unsuccessful outcomes; the retained baseline is shown separately. Full inventory is not deserialized for the dashboard. Detail loads persisted full evidence by change ID. Successful snapshot comparison reads two snapshots in a transaction, validates same category and chronological order, computes cancellable diffs on a worker, and returns a bounded result page without writing events or baselines.

A single view-model work gate prevents overlapping UI operations. Close cancels the lifetime token. Failed operations show a localized retryable error and retain previously loaded evidence. Search/category/window inputs apply on Search or Enter; language changes relabel existing results without a new query and preserve filters, selections and evidence. Large substring searches can still scan matching history; they are off the UI thread and do not load the whole history into WPF. Comparison work is proportional to the two selected inventories, not total history.

第四阶段仅使用本机历史，移除演示入口和生产代码中的虚构数据生成器。数据库分页与详情按需读取均在后台线程执行，界面列表每页 40 条，快照选择跨页保留已选两项。失败类别显示未知数量而非零；上次完整成功与最近尝试分别显示。快照比较只读，不产生新的持久事件。关闭窗口取消工作；不包含第五阶段的调度、托盘、导出或自启。


Initialization supplement, not the missing original design. 本次初始化补充，非缺失原稿。

## PHASE 03

Core adds ISystemCollector and ISystemScanSession plus backwards-compatible collection warning/skipped-entry diagnostics. Collectors references only Core and framework Windows APIs. Four collector classes translate fakeable registry/file/device/network sources into canonical SnapshotItems; field errors become Partial, enumeration failures Failed, cancellation propagates. SystemScanSession runs all four independently and commits one scan through the existing atomic repository. Startup defaults to the system store but loads history only; no collection starts until Scan now. Demo and system mode use different stores and never share baselines.

采集层只读，Core 不引用平台 API。四类状态独立，失败保留旧基线；新扫描完整收集各类别状态后原子提交。真实数据使用 system-v1.db，演示使用 demo-v1.db。切换模式只载入对应历史；关闭窗口取消未完成工作。所有数据库工作及 Windows 枚举在 UI 线程外执行。

Software reads HKCU 64-bit view once (shared user Software view), and HKLM 32/64 views separately, retaining SystemComponent metadata. Startup reads Run/RunOnce in the same scopes and the current/common startup folders, without resolving or executing contents. Drivers use CM_Get_Device_ID_List with PRESENT only, CM_Locate_DevNode(NORMAL), and typed CM_Get_DevNode_Property calls; absent properties are known null, errors remain unreadable. NetworkInterface supplies adapter ID/name/description/type/operational status only. No additional package was introduced.

References: [CM device enumeration](https://learn.microsoft.com/en-us/windows/win32/api/cfgmgr32/nf-cfgmgr32-cm_get_device_id_listw), [device properties](https://learn.microsoft.com/en-us/windows/win32/api/cfgmgr32/nf-cfgmgr32-cm_get_devnode_propertyw), [driver version](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/devpkey-device-driverversion), [SDK property keys](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/shared/devpkey.h).

## PHASE 02

Core now owns immutable inventory contracts, stable-key builders, diff engine, repository/session interfaces. Infrastructure implements the SQLite v1 schema/migration and synthetic DemoSession. Each operation uses a short-lived, nonpooled connection; writes use immediate transactions, and read history uses one consistent transaction. Schema creation is its own atomic migration; a cancelled first data commit can leave an empty initialized schema but no inventory, change or baseline records. Cancellation is checked before data commit. Unknown future schemas and mismatched Demo/System stores fail without overwriting data.

App composes a Demo-only repository at the local-user app-data path, loads existing history asynchronously, and runs synthetic steps only when the user chooses Run demo. Generated AsyncRelayCommand prevents UI reentry, supports cancellation, and reports recoverable I/O errors without replacing the last good view. Window close cancels pending work. SQLite's ADO async APIs are synchronous, so complete database operations run via Task.Run; no I/O blocks the UI thread. There are no Windows collectors, scheduler, tray registration or system writes.

Core 提供模型、稳定键、差异及接口；Infrastructure 提供事务库和演示会话。每次操作独立连接，关闭后释放。读历史使用一致事务，写入使用立即事务；建库迁移本身原子执行。取消首次数据写入可能留下空 schema，但不会留下清单、事件或基线。未知版本或来源不符会拒绝。App 仅在用户运行演示时生成虚构扫描，后台线程处理数据库，错误可重试、关闭可取消；真实采集层仍未实现。

References: [Microsoft SQLite async limitations](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async), [transactions](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions).

## PHASE 01 historical contract

- App targets `net10.0-windows`, WPF, x64, `asInvoker`. It composes services with Microsoft.Extensions.DependencyInjection and uses `ILogger<T>` without a file or network logging sink.
- Core targets `net10.0`, contains immutable demo contracts and language selection policy, and has no WPF, Win32, SQLite or package references.
- Collectors targets Windows and references Core. It intentionally contains no collector implementation in this phase.
- Infrastructure references Core and provides static synthetic data only. It does not read or write system inventory or application data.
- App references the other three projects. xUnit tests reference App to exercise the real WPF resources and views on STA threads.
- CommunityToolkit.Mvvm generates observable properties and commands. UI commands are immediate in-memory operations; future I/O must be async and cancellable.
- ResourceDictionary supplies all interface strings and theme brushes. DynamicResource enables live changes without reopening the window.

App 负责界面及依赖组装；Core 保持平台无关；Collectors 本阶段仅保留项目边界；Infrastructure 仅返回固定演示数据。没有采集、数据库、调度、托盘或登录启动代码。退出时释放 DI 容器，不持久化用户设置。

## Later-phase constraints

Follow PRODUCT.md and PHASES.md: collection is read only; baselines advance per successful category; partial/failed results cannot imply removal; future persistence must use migrations and atomic transactions. Design the production contracts in PHASE 02 before adding persistence. Do not infer missing original design decisions from this supplement.

后续依原 PRD 执行：按类别成功推进基线，失败与不完整结果不产生虚假删除；第二阶段先设计正式契约，再实现迁移及原子事务。
