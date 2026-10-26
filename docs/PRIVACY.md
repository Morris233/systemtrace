# Privacy / 隐私

PHASE 01 uses synthetic data only. It does not enumerate installed software, registry startup entries, devices or adapters. It does not create a database, write application logs, upload telemetry, register startup tasks, or remain in the tray. Language/theme choices last only for the current process. Building/restoring uses NuGet; CI runs on GitHub, independently of the shipped application.

第一阶段仅展示固定演示数据，不读取软件、启动项、设备或网卡，不创建数据库或文件日志，不上传遥测，不登记自启，不在托盘常驻。语言与主题仅保存在当前进程内。开发构建需要 NuGet，CI 使用 GitHub，与应用运行行为分开。

Future collection and local storage must follow PRODUCT.md. Any export must be user initiated and default to redacting user paths; credentials, SSID, IP and MAC must not enter inventory history by default.

后续采集及本地存储须遵循 PRD，导出由用户主动触发并默认脱敏；历史默认不记录凭证、SSID、IP 或 MAC。
