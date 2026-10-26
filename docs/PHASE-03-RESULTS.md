# PHASE 03 verification / 第三阶段验证

Phase: P3 / Four read-only Windows collectors

Implemented: ISystemCollector contracts; software, startup, PnP-driver and adapter collectors; fakeable Windows sources; independent statuses/diagnostics; real scan orchestration and atomic persistence; system/demo isolation; minimal scan action, localized category status, scope/permission tooltips and conservative device-absence wording. No command execution, system configuration writes, automatic scans, tray, export or login-start registration.

已实现四类只读采集、独立状态、真实扫描与持久化、本机/演示隔离及简洁状态界面。未执行启动命令，也未改写系统设置。后台与完整生产界面留待后续阶段。

Changed files: Core collection/session contracts and optional diagnostics; Collectors source adapters and four collectors; Infrastructure SystemScanSession; App composition, scan/mode commands, status UI and bilingual resources; collector/Windows integration/WPF tests; CI action versions; README, privacy, data-model, architecture and verification notes. No new NuGet dependency. Original PRODUCT.md / PHASES.md and concise-UI instructions are preserved.

## Acceptance / 验收

- [x] Software: HKCU shared view once, HKLM 32/64 separately; skipped unnamed entries counted; no uninstall command/install-path reads
- [x] Startup: Run/RunOnce and current/common startup-folder metadata; no expansion, shortcut resolution or execution
- [x] Drivers: present PnP devices and typed Configuration Manager properties; known-null vs read errors; unstable enumeration protected
- [x] Network: GUID identity, name/description/type/operational status only; no IP/MAC/SSID/password fields
- [x] Each result preserves independent status, warning and evidence; failures/partial reads do not advance baselines or imply removal
- [x] Real ordinary-permission Windows scan established all four baselines without initial events
- [x] Isolated registry add/version-edit/remove and shared HKCU-view tests
- [x] Fake device version/disappearance and adapter rename/state tests; failed reads preserve prior baselines
- [x] Separate system/demo history, error/busy/cancel behavior, 40 WPF render combinations and real executable launch/exit
- [ ] Physical DPI 100/150/200% and complete keyboard audit / DPI 与完整键盘手测
- [ ] Actual third-party software installation/uninstallation experiment / 真实第三方软件安装卸载实验
- [x] Remote Windows CI / 远端 Windows CI

## Executed checks / 实际检查

Windows x64, .NET SDK 10.0.403, Release. The original restricted test invocation denied creation of test-owned registry keys. Those tests were rerun in the normal user execution context, using only randomized HKCU fixture keys and temporary directories, then cleaned up. The real read-only smoke test confirmed `Elevated=false`; no UAC elevation or administrator token was needed.

初次受限执行环境拒绝临时注册表键写入；改为正常用户上下文重跑测试后通过。只创建并清理随机测试键与临时目录，不改真实启动项。实际只读扫描记录 Elevated=false，不需要管理员令牌。

| Check | Result |
| --- | --- |
| `dotnet restore --locked-mode` (verified local package feed) | PASS |
| `dotnet build --configuration Release --no-restore` | PASS, 0 warnings / 0 errors |
| `dotnet test --configuration Release --no-build --logger trx` | PASS, 47 passed / 0 failed / 0 skipped |
| `dotnet format --verify-no-changes --no-restore` | PASS |
| Real Windows integration cases | PASS, 3 |
| Real read-only scan + initial persistence | PASS, 4 successful baselines, 0 changes; test duration about 0.55s on this machine |
| UI render regression | PASS, 40 page/language/theme/mode combinations, using fake sources for privacy |
| App startup and normal exit | PASS |
| GitHub Windows CI | PASS: [run 40304349428](https://github.com/Morris233/systemtrace/actions/runs/40304349428), commit `e717239e96472af1edefffa9fcfa3d607b6d5c80` |

Remote Windows CI restored locked dependencies, built Release, passed all 47 tests, verified formatting and uploaded results using the official Node 24 actions. The recorded run validates the identified implementation commit; subsequent documentation updates are checked through [PR #2 checks](https://github.com/Morris233/systemtrace/pull/2/checks). This PR is based on the still-unmerged PHASE 02 branch, so review PR #1 first.

远端 Windows CI 已通过锁定还原、构建、47 项测试、格式检查及结果上传。上表记录具体实现提交；后续文档提交状态见 PR 实时检查。第三阶段 PR 以尚未合并的第二阶段分支为基础，应先审阅 PR #1。

### Real read-only smoke result / 实机只读结果

Only aggregate counts and statuses are recorded here. Real item names, commands, paths, device identifiers and inventory databases are not committed or packaged. Counts describe this test run, not a fixed product expectation.

| Category / 类别 | Status | Items | Skipped |
| --- | --- | ---: | ---: |
| Software / 软件 | Succeeded | 211 | 152 |
| Startup / 启动来源 | Succeeded | 9 | 0 |
| Drivers / 当前 PnP 设备 | Succeeded | 293 | 0 |
| Network / 网络适配器 | Succeeded | 60 | 0 |

The read-only scan and its database used an isolated temporary store that the test removed. The retained local diagnostic summary has counts/status codes only. Screenshots also use synthetic source fixtures, even when showing System mode, so no actual inventory is exposed.

实测清单写入隔离临时库，测试完成后删除；保留摘要仅含数量、状态与通用错误码。界面测试截图使用虚构数据源，避免暴露本机清单。

Manual verification: visually inspected system-mode overview (including Partial state) and change timeline in both themes/languages, plus automated real-executable startup/exit. Physical DPI/keyboard and actual software-install experiments were NOT RUN. Registry mutations were confined to isolated test-owned keys, never the real Uninstall/Run keys.

Known limitations: registry software coverage omits portable apps and some Store apps; startup omits scheduled tasks/services/other users; devices are present-only, and not detected does not prove uninstallation; network records operational status, not a separate administrative-enable flag. Partial results conservatively freeze the whole category baseline. Current history UI is limited to the newest 200 changes; full detail/search/comparison/pagination belongs to PHASE 04. No background monitoring or retention/export workflow was added.

Next phase proposal: PHASE 04 production dashboard, timeline search/pagination, field details, successful-snapshot comparison and time-window review. 第三阶段不自动跨入第四阶段。
