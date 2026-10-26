# Architecture / 架构

Initialization supplement, not the missing original design. 本次初始化补充，非缺失原稿。

## PHASE 01 contract

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
