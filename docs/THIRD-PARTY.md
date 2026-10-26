# Dependencies / 第三方依赖

Versions below were confirmed available through NuGet's official flat-container index before use. Lock files record the complete resolved graph; packages are open source, with no paid UI package.

以下版本在引入前已通过 NuGet 官方索引确认存在。完整依赖图见各项目 packages.lock.json，无商业 UI 包。

| Dependency | Version | License | Purpose / 用途 |
| --- | --- | --- | --- |
| CommunityToolkit.Mvvm | 8.4.0 | MIT | Generated MVVM / MVVM 源生成 |
| Microsoft.Extensions.DependencyInjection | 10.0.0 | MIT | Composition / 依赖组装 |
| Microsoft.Extensions.Logging | 10.0.0 | MIT | Logging abstractions / 日志接口 |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT | Test execution / 测试运行 |
| xunit | 2.9.3 | Apache-2.0 | Unit tests / 单元测试 |
| xunit.runner.visualstudio | 3.1.5 | Apache-2.0 | Test adapter / 测试适配器 |
| Microsoft.Data.Sqlite | 10.0.14 | MIT | SQLite connection, transactions and migrations / 本地事务存储 |
| SQLitePCLRaw bundle/core/provider/native | 2.1.13 | Apache-2.0 (SQLite engine public domain) | Native SQLite runtime / SQLite 原生运行时 |

PHASE 02 uses the verified official 10.0.14 SQLite package, whose resolved native dependency is 2.1.13. This avoids the older <=2.1.11 advisory [GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q). Resolved packages are checked against NuGet's vulnerability feed during local validation; this is a package-advisory check, not a full security audit. 第二阶段选用官方补丁版，锁定图已更新，并对照 NuGet 漏洞源核对。该检查不代表完整安全审计。

WPF/.NET are MIT licensed. Resolved package license metadata is checked during restore/initialization; refer to the package's included license and upstream notices for full terms. Tooling dependencies stay private to the test project. SDK and build outputs are not committed.

WPF/.NET 使用 MIT。以各包自带许可及上游声明为准。测试依赖不向生产应用传递，SDK 与编译产物不提交。
