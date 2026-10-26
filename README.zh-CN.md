# SystemTrace

在问题出现之前，知道系统发生过什么变化。

[English](README.md) · [产品需求](docs/PRODUCT.md) · [开发阶段](docs/PHASES.md)

面向 Windows 11 x64 的本地桌面项目，计划记录软件、已监测启动来源、PnP 驱动与网络适配器变化，默认使用普通用户权限。

## 当前阶段

PHASE 03：四类 Windows 只读采集器、独立状态、稳定键、字段差异和 SQLite 事务历史。程序默认“本机”模式，手动扫描建立类别基线，再次扫描检测变化；部分成功或失败时保留原基线。“演示”入口切换到独立虚构历史。保留五页面、中英双语及深浅主题。完整比较、搜索、详情界面、托盘、定时扫描和登录启动留待后续阶段。

## 构建与运行

在 Windows 11 x64 安装微软官方 .NET 10 SDK，然后在仓库根目录运行：

```powershell
dotnet restore --locked-mode
dotnet build --no-restore
dotnet test --no-build
dotnet run --project src/SystemTrace.App
```

无需管理员权限，关闭窗口即退出。语言、主题和模式仅在本次运行生效。打开程序只载入已有历史，不自动扫描或创建新库。手动扫描保存到 `%LOCALAPPDATA%/SystemTrace/system-v1.db`，演示使用独立 demo-v1.db。不要提交这些数据库。启动命令仅作为本地证据保存，不会执行；网卡历史不含 IP、MAC、SSID 或密码。

## 结构及文档来源

`src/` 包含 App、Core、Collectors、Infrastructure 四层；`tests/` 为 xUnit 测试；`docs/` 为设计与验收记录；`.github/workflows/ci.yml` 在 Windows 构建测试。

从下载目录导入了 `AGENTS.md`、`docs/PRODUCT.md`、`docs/PHASES.md`。产品及阶段文档保持原样；AGENTS.md 后续追加了用户明确要求的简洁界面准则。未取得原 ZIP、README 及其余设计原稿。README 和架构、模型、界面、测试补充说明由本次初始化新增，仅定义第一阶段所需边界，不声称恢复了缺失原稿。原 PRD 与阶段要求仍为依据。

参见[第三阶段验证](docs/PHASE-03-RESULTS.md)、[第二阶段历史](docs/PHASE-02-RESULTS.md)、[依赖许可](docs/THIRD-PARTY.md)及[隐私说明](docs/PRIVACY.md)。使用 MIT 许可证。
