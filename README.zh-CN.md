# SystemTrace

[English](README.md) · [产品需求](docs/PRODUCT.md) · [开发阶段](docs/PHASES.md)

面向 Windows 11 x64 的本地桌面应用，记录软件、已监测启动来源、当前 PnP 驱动与网络适配器的变化，使用普通用户权限。

## 当前阶段

PHASE 04：扫描概览、可搜索与分页的变化列表、完整字段 before/after、成功快照比较、按观察区间回溯。点击“开始扫描”建立基线，之后再次扫描检测变化；部分成功或失败时保留上次成功基线。应用已移除演示入口和虚构数据，样例只存在于测试工程。

搜索支持名称、字段名及前后值的字面子串与类别筛选。详情区分缺失、空值、空字符串和不可读。快照比较只读，不新增历史事件；回溯提供线索，不断言根因。托盘、调度、自启、导出与保留策略属于第五阶段。

## 构建与运行

在 Windows 11 x64 安装微软官方 .NET 10 SDK，在仓库根目录运行：

```powershell
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
dotnet run --project src/SystemTrace.App
```

无需管理员权限，关闭窗口即退出。语言与主题仅本次运行生效。打开只载入已有历史，不自动扫描或创建新库。手动扫描保存到 `%LOCALAPPDATA%/SystemTrace/system-v1.db`，旧 schema 1 原子迁移到 schema 2；旧演示库不读取、不删除。不要提交数据库。启动命令只作为本地证据，不会执行；网卡历史不含 IP、MAC、SSID 或密码。

## 结构及文档来源

`src/` 包含 App、Core、Collectors、Infrastructure 四层；`tests/` 包含领域、隔离采集、查询迁移及 WPF 测试；`docs/` 包含设计与验收记录；Windows CI 执行锁定还原、Release 构建、测试和格式检查。

从下载目录导入了 `AGENTS.md`、`docs/PRODUCT.md`、`docs/PHASES.md`。产品与阶段文档保持原样；AGENTS 追加了用户的简洁界面准则。未取得原 ZIP、README 及其余设计原稿，补充文档不声称恢复缺失内容。原 PRD 与阶段约束仍为依据。

参见[第四阶段验证](docs/PHASE-04-RESULTS.md)、[第三阶段历史](docs/PHASE-03-RESULTS.md)、[依赖许可](docs/THIRD-PARTY.md)及[隐私说明](docs/PRIVACY.md)。MIT 许可证。
