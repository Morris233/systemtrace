# Privacy / 隐私

## PHASE 05 / 第五阶段

The opt-in sign-in toggle is the only production registry write: it manages the current user's SystemTrace Run value and removes it only on an exact command match. Background and close-to-tray are separate, visible opt-ins; defaults are off. Settings and metadata-only bounded diagnostics stay under the same local application directory. No network upload, telemetry, command execution or automatic repair exists.

JSON/CSV exports are user-initiated. Default redaction hides free-text names/descriptions, paths, commands and original stable identities; identities are pseudonymous hashes. Only approved factual fields (such as versions and state) remain, with user-path normalization. Raw exports require an explicit confirmation. CSV additionally neutralizes formula-leading cells, but redaction and formula guarding have different purposes.

History retention defaults to 90 days (0 means keep all). Pruning happens after scans or a manual Remove expired action, retaining current category baselines/backing evidence and the latest attempt. Delete all history requires confirmation and resets baselines. SQLite secure_delete reduces recoverable freed content, but exports, backups and storage snapshots remain outside the app's deletion scope.

只有用户启用登录启动时，生产代码才写自己的当前用户 Run 值。没有修改其他启动项、系统设置或网络配置。默认脱敏报告不含原始命令、路径或稳定键；原始导出由用户再次确认。自动清理保留必要基线，删除全部记录由用户明确确认。以下内容保留为较早阶段历史。


## PHASE 04 current behavior / 第四阶段现状

The app reads and stores only the system inventory described in PHASE 03 below. Production demo mode/generation has been removed; old demo-v1.db is ignored and never automatically deleted. Opening an existing system database may atomically migrate its schema from 1 to 2, even without a new scan; opening without an existing store still creates no database and starts no collector.

Search text, clipped summaries and item counts are derived from evidence already in the same local database. Searching, detail loading, snapshot comparison and troubleshooting stay local. Snapshot comparison does not save new events; there is no export, automatic retention deletion, schedule, tray or login startup in this phase. Render fixtures are generated only inside tests. Schema migration and queries add no telemetry or network access.

第四阶段查询索引和摘要只复制本机已有证据，不增加采集范围或上传。首次打开旧系统库会原子迁移，但不会自动扫描。旧演示库不再访问，不会自动清理。截图与源码包不包含真实清单、命令、路径、设备标识或数据库。以下较早阶段说明保留为开发历史，由本节更新当前行为。


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
