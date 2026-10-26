# PHASE 01 verification / 第一阶段验证

Phase: P1 / PHASE 01

Implemented: four project layers, WPF shell, five placeholder pages, generated MVVM, DI, ILogger, bilingual ResourceDictionary resources, live theme/language switching, synthetic demo provider, xUnit and Windows CI workflow. No real collector, persistence or background registration was added.

已实现：四层项目、WPF 五页面、源生成 MVVM、DI、ILogger、双语资源、主题及语言实时切换、明确演示数据、xUnit 与 Windows CI。未实现真实采集、持久化或后台注册。

## Local verification / 本机验证

Environment: Windows x64, official .NET SDK 10.0.403, Release configuration. The SDK ZIP was checked against Microsoft's published SHA512 and extracted in the task work directory; no system installation was changed.

环境：Windows x64、微软官方 .NET SDK 10.0.403、Release。SDK 已核对官方 SHA512，仅解压到工作目录。

| Check / 检查 | Result / 结果 |
| --- | --- |
| Dependency restore with lock files / 锁定依赖还原 | PASS |
| `dotnet build --configuration Release --no-restore` | PASS, 0 warnings, 0 errors |
| `dotnet test --configuration Release --no-build --logger trx` | PASS, 12 passed, 0 failed, 0 skipped |
| `dotnet format --verify-no-changes --no-restore` | PASS |
| Five pages × two languages × two themes / 页面语言主题组合 | PASS, 20 WPF render combinations at 960×620 window size |
| Resource parity, nonempty text and live replacement / 资源完整及实时替换 | PASS |
| Real executable startup and close / 实际程序启动与关闭 | PASS, responsive main window, normal exit code 0, process exits |
| Core platform/persistence isolation / Core 平台隔离 | PASS |
| Requested execution level / 请求权限 | asInvoker, no elevation request |
| Manual DPI 100/150/200% and full keyboard audit / DPI 与完整键盘验收 | NOT RUN |
| Clean machine / 干净机器 | NOT RUN |
| GitHub Actions / 远端 CI | Pending remote run / 等待远端运行 |

Local NuGet HTTPS initially failed with Windows TLS `SEC_E_NO_CREDENTIALS`. Packages were downloaded from official NuGet endpoints using a certificate-verifying client, inspected for license metadata, then restored from a work-directory feed. The repository keeps normal nuget.org configuration; no certificate checks were disabled. Lock files capture the exact resolved package hashes. Local commands used an environment wrapper to keep SDK, cache and temporary writes inside the task workspace; the command table above shows equivalent standard SDK arguments.

本机 NuGet HTTPS 遇到 Windows TLS 凭据错误；经保留证书校验的客户端取得官方包，核查许可后从工作目录还原。仓库仍使用标准 nuget.org 配置，没有关闭证书校验。锁文件记录完整依赖及内容哈希。本机通过环境包装脚本将 SDK、缓存与临时文件保留在工作区；表中为对应标准 SDK 参数。

## Checklist / 阶段清单

- [x] Four layers and solution / 四层及解决方案
- [x] WPF + generated MVVM + DI + ILogger
- [x] Five navigable placeholder pages / 五个可切换占位页面
- [x] Explicit synthetic DEMO notice / 明确演示标识
- [x] English / Simplified Chinese and dark / light themes
- [x] User's concise functional UI rule applied in both languages / 双语落实简洁文案准则
- [x] Build, unit, resource, render and lifecycle checks / 构建、资源、渲染与生命周期验证
- [x] Windows CI workflow and dependency locks / Windows CI 与依赖锁
- [ ] Remote CI result / 远端 CI 结果
- [ ] Manual DPI / keyboard acceptance / DPI 与键盘手工验收

Changed files: solution/build configuration; App composition, services, view models, five views and resources; Core demo/language contracts; Collectors project boundary; Infrastructure synthetic provider; xUnit tests and package locks; Windows workflow; README, license, design supplements and this report. The imported PRODUCT.md and PHASES.md are unchanged. AGENTS.md retains the imported content with the user's UI rule appended.

文件变更：解决方案及构建配置、App 组装/服务/视图模型/五页面/资源、Core 演示契约及语言规则、Collectors 边界、Infrastructure 演示数据、xUnit 与依赖锁、Windows 工作流、README/许可/设计补充与报告。PRODUCT.md、PHASES.md 保持原样；AGENTS.md 追加用户界面准则。

Known limitations: only a demo shell; settings are in memory; no inventory, database, tray, scheduling, export or startup integration. The missing original architecture/data/UI/release-test documents were not recovered; initialization supplements are identified as such. A successful automated render is not proof of all physical DPI/keyboard behavior.

限制：当前是演示框架，设置只在内存生效。没有采集、数据库、托盘、调度、导出和自启。未找回原始架构/数据/UI/发布测试文档，新增补充均已注明来源。自动渲染不代表完成真实 DPI 与键盘验收。

Next phase proposal: PHASE 02 domain contracts, diff engine and SQLite, after reviewing this milestone; do not start it automatically.

建议下一阶段：确认本阶段后执行 PHASE 02 领域契约、差异引擎及 SQLite；不自动跨阶段实施。
