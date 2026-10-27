# Privacy / 隐私

PHASE 03 reads the current user's and machine-wide uninstall registry entries, monitored Run/RunOnce values and startup-file metadata, present PnP driver properties and limited adapter metadata. A manual scan writes only `%LOCALAPPDATA%/SystemTrace/system-v1.db`. The app does not execute registry commands or shortcut targets, modify system settings, access other users' hives, install anything, or upload data. Demo data remains in its separate demo-v1.db. Opening the app loads existing history without scanning or creating a new database.

启动命令、注册表源路径和设备实例稳定键可能包含个人信息，均只保存在本机系统库，不提交 Git，也不进入示例截图与源码包。软件卸载命令和安装目录不读取；网络不记录 IP、MAC、SSID 或密码。采集范围有限，不宣称覆盖全部软件/启动机制，也不将设备未检测到等同于卸载。

Tests use fake sources and random, isolated HKCU test keys / temporary directories that they clean up. The real read-only smoke test stores its inventory in an isolated temporary database and removes it; its retained verification summary includes counts and status codes only. 测试不修改真实软件、启动项、驱动或适配器。没有后台扫描、自启、遥测或导出。

PHASE 02 stores synthetic demo history only after an explicit Run demo action, in `%LOCALAPPDATA%/SystemTrace/demo-v1.db`. Existing demo history reloads on startup. It contains fake item keys/fields, scan IDs and timestamps; no installed-software list, startup commands, registry reads, device/network identifiers or telemetry. Origin enforcement prevents using the same store for future system inventory. There is no export or automatic deletion in this phase. To reset demo data, close the application and remove only this demo database; this document does not authorize automated deletion of user files.

第二阶段仅在用户主动“运行演示”后保存虚构清单、扫描 ID 和处理时间；已有演示历史在启动时重载。位置为本地用户目录中的独立 demo-v1.db。没有真实系统采集、遥测、导出或自动清理。演示和未来真实来源不混用。重置需先关闭程序，再由用户处理该演示库；此说明不授权自动删除用户文件。

## PHASE 01 history / 第一阶段历史

PHASE 01 uses synthetic data only. It does not enumerate installed software, registry startup entries, devices or adapters. It does not create a database, write application logs, upload telemetry, register startup tasks, or remain in the tray. Language/theme choices last only for the current process. Building/restoring uses NuGet; CI runs on GitHub, independently of the shipped application.

第一阶段仅展示固定演示数据，不读取软件、启动项、设备或网卡，不创建数据库或文件日志，不上传遥测，不登记自启，不在托盘常驻。语言与主题仅保存在当前进程内。开发构建需要 NuGet，CI 使用 GitHub，与应用运行行为分开。

Future collection and local storage must follow PRODUCT.md. Any export must be user initiated and default to redacting user paths; credentials, SSID, IP and MAC must not enter inventory history by default.

后续采集及本地存储须遵循 PRD，导出由用户主动触发并默认脱敏；历史默认不记录凭证、SSID、IP 或 MAC。
