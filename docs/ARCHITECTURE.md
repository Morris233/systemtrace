# Architecture / 架构

Initialization supplement, not the missing original design. 本次初始化补充，非缺失原稿。

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
