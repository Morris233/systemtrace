# SystemTrace

[English](README.md) · [产品需求](docs/PRODUCT.md) · [开发阶段](docs/PHASES.md)

面向 Windows 11 x64 的本地桌面应用，记录软件、已监测启动来源、当前 PnP 驱动与网络适配器的变化，使用普通用户权限。

## 当前阶段

PHASE 05：已实现自愿后台监测、托盘、可选登录启动、进度取消、设置保存、JSON/CSV 导出及历史保留。后台、关闭到托盘和登录启动默认关闭，需在设置中启用并保存。默认扫描间隔 30 分钟，历史保留 90 天（0 为全部保留）。

手动扫描建立基线，失败或部分成功不推进基线。导出包含当前已应用搜索的全部页，默认隐藏个人信息。快照比较只读，时间回溯仅提供观察区间线索。发布加固属于第六阶段。

## 构建与运行

在 Windows 11 x64 安装微软官方 .NET 10 SDK，在仓库根目录运行：

```powershell
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
dotnet run --project src/SystemTrace.App
```

无需管理员权限。未启用关闭到托盘时，关闭窗口即退出；托盘“退出”始终结束进程。语言与主题会保存。打开只载入已有历史，不自动扫描或创建新库。手动扫描保存到 `%LOCALAPPDATA%/SystemTrace/system-v1.db`，旧 schema 1 原子迁移到 schema 2；旧演示库不读取、不删除。不要提交数据库。启动命令只作为本地证据，不会执行；网卡历史不含 IP、MAC、SSID 或密码。

## 结构及文档来源

`src/` 包含 App、Core、Collectors、Infrastructure 四层；`tests/` 包含领域、隔离采集、查询迁移及 WPF 测试；`docs/` 包含设计与验收记录；Windows CI 执行锁定还原、Release 构建、测试和格式检查。

从下载目录导入了 `AGENTS.md`、`docs/PRODUCT.md`、`docs/PHASES.md`。产品与阶段文档保持原样；AGENTS 追加了用户的简洁界面准则。未取得原 ZIP、README 及其余设计原稿，补充文档不声称恢复缺失内容。原 PRD 与阶段约束仍为依据。

参见[第五阶段验证](docs/PHASE-05-RESULTS.md)、[第四阶段验证](docs/PHASE-04-RESULTS.md)、[第三阶段历史](docs/PHASE-03-RESULTS.md)、[依赖许可](docs/THIRD-PARTY.md)及[隐私说明](docs/PRIVACY.md)。MIT 许可证。
