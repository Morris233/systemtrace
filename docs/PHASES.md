# SystemTrace — Codex 分阶段执行清单

每个阶段是独立、可提交的里程碑。完成阶段需编译、测试、更新文档；不得跨阶段抢做。开发中的说明可以直接复制到 Codex 并加上“执行 PHASE XX”。

## PHASE 01 — Bootstrap + UI shell

**前置**：Windows 11 + .NET 10 SDK、Git。
**实现**：创建解决方案及 App/Core/Collectors/Infrastructure、xUnit 测试工程、WPF 主窗口、MVVM/DI/ILogger、ResourceDictionary、中文英文资源、五页面占位、模拟数据显著 Demo 标志、CI Windows 构建。
**验收**：`dotnet build` `dotnet test` 成功；页面和主题切换；UI 不读取系统内容；所有文案双语；无管理权限。
**禁止**：真实采集/数据库写入/后台常驻。
**提交建议**：`feat: bootstrap SystemTrace WPF shell`。

## PHASE 02 — Domain, diff engine and SQLite

**前置**：01 Pass。
**实现**：稳定键、规范化模型、Snapshot/Change/CollectionResult/ScanRun；DiffEngine；schema migrations；SQLite repository；模拟采集数据与 UI 联通。
**验收**：至少覆盖初始基线不生成 changes，增删改，字段顺序不同无变化，重入去重，partial/failed 不推进，事务回滚，多类别不同状态。
**禁止**：直接读取 Windows API。
**提交**：`feat: add snapshot diff engine and persistence`。

## PHASE 03 — Four read-only collectors

**前置**：02 Pass。
**实现**：SoftwareCollector→StartupCollector→DriverCollector→NetworkCollector；每个通过 ISystemCollector 独立返回数据/状态/警告；依类别比较与保存；错误提示。
**验收**：真实 Windows 非管理员权限可建立基线；测试注册表项变更/软件安装后可检测；PnP/Network 通过 fake 模拟变化；权限受限/设备枚举失败不产生大量删除；不执行命令、不修改系统。
**提交**：`feat: implement Windows collectors`。

## PHASE 04 — Production UI, navigation and localization

**前置**：03 Pass。
**实现**：首页最近扫描状态、分类变化概览、时间线分页/搜索/类别筛选、字段级详情、选择两个成功快照对比、故障回溯（只检索时间和类别，不给根因断言）。
**验收**：移除 Demo 数据；能打开真实事件和 before/after；大历史不卡 UI；英文/中文不混乱；100/150/200% DPI 验证。
**提交**：`feat: deliver dashboard and timeline`。

## PHASE 05 — Tray, scheduling, login start and export

**前置**：04 Pass。
**实现**：30 分钟默认间隔的可设置调度器、单扫描锁、可选开机启动、自愿托盘常驻、手动/定时扫描进度与取消、暂停、JSON/CSV 导出、保留策略与删除数据。
**验收**：默认不后台常驻/不自启；用户关闭托盘可彻底退出；双击扫描不并发；设置关闭后恢复预期；导出默认脱敏且 Excel 公式安全；删除历史后重新基线。
**提交**：`feat: add opt-in background scan and export`。

## PHASE 06 — Release hardening

**前置**：05 Pass。
**实现**：完全检查 `docs/TEST-PLAN.md` T001–T026、性能与错误恢复、完善 README 英文主版与中文版本、隐私文件、贡献指南、issue 模板、GitHub Actions Release 工作流、SHA256、便携 win-x64 ZIP。
**验收**：全部 P0 通过；Windows CI 绿；干净机器可用；构建产物与限制清晰；不自动发布，需用户授权打 tag。
**提交**：`chore: prepare v0.1.0 release`。

## 后续迭代（不要纳入 MVP）

- v0.2：Event Log / Windows Update 事件补充与时间准确度；任务计划程序启动项。
- v0.3：更多核查线索、证据链接和高级筛选。
- v0.4：自定义监测规则及性能优化。
- v1.0：可靠性、多语言和可访问性综合完善。
