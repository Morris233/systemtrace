# SystemTrace

在问题出现之前，知道系统发生过什么变化。

[English](README.md) · [产品需求](docs/PRODUCT.md) · [开发阶段](docs/PHASES.md)

面向 Windows 11 x64 的本地桌面项目，计划记录软件、已监测启动来源、PnP 驱动与网络适配器变化，默认使用普通用户权限。

## 当前阶段

PHASE 02：快照契约、稳定键、字段级差异、SQLite 事务存储及可操作演示。点击一次“运行演示”建立基线，再次点击生成变化；重开程序可读取已保存的演示历史。保留五页面、中英文及深浅主题，所有数据显著标记 **演示 / DEMO**。真实系统采集、正式比较与检索、托盘监测、定时扫描及登录启动留待后续阶段。

## 构建与运行

在 Windows 11 x64 安装微软官方 .NET 10 SDK，然后在仓库根目录运行：

```powershell
dotnet restore --locked-mode
dotnet build --no-restore
dotnet test --no-build
dotnet run --project src/SystemTrace.App
```

无需管理员权限，关闭窗口即退出。语言及主题仅保存在内存中。仅打开程序不创建新库；主动运行演示后，虚构历史保存到 `%LOCALAPPDATA%/SystemTrace/demo-v1.db`，与未来真实数据隔离。请勿将数据库提交到 Git。

## 结构及文档来源

`src/` 包含 App、Core、Collectors、Infrastructure 四层；`tests/` 为 xUnit 测试；`docs/` 为设计与验收记录；`.github/workflows/ci.yml` 在 Windows 构建测试。

从下载目录导入了 `AGENTS.md`、`docs/PRODUCT.md`、`docs/PHASES.md`。产品及阶段文档保持原样；AGENTS.md 后续追加了用户明确要求的简洁界面准则。未取得原 ZIP、README 及其余设计原稿。README 和架构、模型、界面、测试补充说明由本次初始化新增，仅定义第一阶段所需边界，不声称恢复了缺失原稿。原 PRD 与阶段要求仍为依据。

参见[第二阶段验证](docs/PHASE-02-RESULTS.md)、[第一阶段历史](docs/PHASE-01-RESULTS.md)、[依赖许可](docs/THIRD-PARTY.md)及[隐私说明](docs/PRIVACY.md)。使用 MIT 许可证。
