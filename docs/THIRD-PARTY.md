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

WPF/.NET are MIT licensed. Resolved package license metadata is checked during restore/initialization; refer to the package's included license and upstream notices for full terms. Tooling dependencies stay private to the test project. SDK and build outputs are not committed.

WPF/.NET 使用 MIT。以各包自带许可及上游声明为准。测试依赖不向生产应用传递，SDK 与编译产物不提交。
