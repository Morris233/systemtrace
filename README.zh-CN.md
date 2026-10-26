# SystemTrace

在问题出现之前，知道系统发生过什么变化。

[English](README.md) · [产品需求](docs/PRODUCT.md) · [开发阶段](docs/PHASES.md)

面向 Windows 11 x64 的本地桌面项目，计划记录软件、已监测启动来源、PnP 驱动与网络适配器变化，默认使用普通用户权限。

## 当前阶段

PHASE 01：.NET 10 / WPF 界面框架，包括五个占位页面、源生成 MVVM、依赖注入、日志接口、中英文资源及深浅色主题。示例数据均显著标记 **演示 / DEMO**。真实采集、数据库、托盘监测、定时扫描和登录启动尚未实现，属于后续阶段。

## 构建与运行

在 Windows 11 x64 安装微软官方 .NET 10 SDK，然后在仓库根目录运行：

```powershell
dotnet restore --locked-mode
dotnet build --no-restore
dotnet test --no-build
dotnet run --project src/SystemTrace.App
```

无需管理员权限，关闭窗口即退出。当前阶段的语言及主题选择仅保存在内存中。

## 结构及文档来源

`src/` 包含 App、Core、Collectors、Infrastructure 四层；`tests/` 为 xUnit 测试；`docs/` 为设计与验收记录；`.github/workflows/ci.yml` 在 Windows 构建测试。

从下载目录导入了 `AGENTS.md`、`docs/PRODUCT.md`、`docs/PHASES.md`。产品及阶段文档保持原样；AGENTS.md 后续追加了用户明确要求的简洁界面准则。未取得原 ZIP、README 及其余设计原稿。README 和架构、模型、界面、测试补充说明由本次初始化新增，仅定义第一阶段所需边界，不声称恢复了缺失原稿。原 PRD 与阶段要求仍为依据。

参见[验证记录](docs/PHASE-01-RESULTS.md)、[依赖许可](docs/THIRD-PARTY.md)及[隐私说明](docs/PRIVACY.md)。使用 MIT 许可证。
