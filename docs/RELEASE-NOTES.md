# SystemTrace v0.1.0

Windows 11 x64 · portable · self-contained .NET 10 · MIT

Download the win-x64 ZIP, verify its SHA256 against SHA256SUMS.txt, extract it and open SystemTrace.App.exe. No SDK, installer or administrator account is required. This build is unsigned.

## Included

- Four read-only collectors: registry-listed software, monitored Run/RunOnce/startup folders, present PnP drivers and adapter metadata.
- Reliable successful-category baselines, field evidence, local history, search/paging, snapshot comparison and observation-window troubleshooting.
- English / Simplified Chinese, light / dark, opt-in tray scheduling and sign-in start, scan progress/cancellation.
- JSON/CSV reports with conservative privacy filtering on by default; CSV formula guards; configurable retention and confirmed history clearing.
- Local settings and bounded metadata-only diagnostics. No telemetry or application network requests.

## Use

Start with **Scan now**. The first successful result per category creates a baseline without events. Enable background monitoring in Settings only if wanted; the default interval is 30 minutes. Close-to-tray and sign-in start are separate opt-ins, off by default. Tray → Exit always exits. Retention defaults to 90 days; 0 keeps all. Current baselines and the latest attempt remain available.

## Limits

Software coverage omits portable/some Store apps; startup coverage omits services, scheduled tasks and other users; PnP absence is “not detected”, not proof of uninstall. Adapter history omits IP/MAC/SSID/password and is operational state rather than administrative enable state. Partial results freeze that category's baseline. Observation intervals do not establish a precise occurrence time or root cause.

Raw exports may contain personal details and require confirmation. Deleting app history does not remove external exports/backups. Render validation covers 100/150/200% scale; physical mixed-monitor transitions and actual sign-out/sign-in or third-party-install experiments have not been performed. A self-contained runtime-isolated Windows smoke test is included; no clean-machine VM certification is claimed.

## 简体中文

下载 win-x64 ZIP，核对 SHA256SUMS.txt 后解压运行 SystemTrace.App.exe，无需安装 .NET 或管理员权限。此版本未签名。

首次手动扫描建立基线。后台监测、关闭到托盘、登录启动默认关闭，需在设置启用并保存；默认间隔 30 分钟、历史 90 天，0 为全部保留。托盘“退出”会结束进程。导出默认隐藏个人信息；原始导出另行确认。

采集范围有限，设备未检测到不等于卸载，观察区间不代表确切发生时间或原因。实际跨显示器 DPI、注销再登录、第三方软件安装实验未执行，详细验收与测试边界见 docs/TEST-PLAN.md 和 docs/PHASE-06-RESULTS.md。
