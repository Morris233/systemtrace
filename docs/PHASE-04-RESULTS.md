# PHASE 04 verification / 第四阶段验证

Phase: P4 / Production UI, navigation and localization

Implemented: production dashboard with distinct attempt/complete timestamps and safe per-category counts; bounded SQL search/category/overlap queries; paged change list and complete before/after details; successful-snapshot catalogue and read-only comparison; time-window troubleshooting; preserved query/selection/detail on localization; production demo removal; schema 2 migration and query summaries. No PHASE 05 scheduling, tray, login startup, export or retention deletion.

已完成概览、分页检索、完整字段详情、成功快照比较和时间回溯。失败类别保留基线、数量显示未知；观察区间不作为发生时间或根因。移除演示入口及生产代码中的样例生成器，旧演示库不读取、不删除。界面少字、功能标签直接，沿用中英双语与深浅主题。

Changed files: Core HistoryQueries, LanguagePolicy and cancellable DiffEngine; Infrastructure SqliteHistoryReader and versioned repository writes; App composition, MainViewModel, HistoryPresenter, main window and five pages plus ChangeDetailView; bilingual strings/theme/control resources; HistoryReaderTests, ViewModelTests, ShellTests and test-only SampleSession; README, architecture, data model, UI/test/privacy notes and this report. PRODUCT.md / PHASES.md and AGENTS concise-UI rule are preserved. No new package dependency.

## Acceptance / 验收

- [x] No production demo entry, generator or synthetic inventory; test-only fixtures remain isolated
- [x] Latest complete scan versus attempt, four category states, retained baseline counts, recent 8 previews
- [x] SQL pagination beyond the prior 200-item limit, literal search, category filter, full evidence by ID
- [x] Missing/null/empty/unreadable distinction, changed fields first, source identity and read-only copyable values
- [x] Select distinct same-category successful snapshots, keep pair across catalogue pages, compare without writes
- [x] Local issue time and lookback, inclusive UTC observation overlap, no root-cause assertion
- [x] Atomic schema 1 -> 2 backfill, injected failure rollback, cancellation checkpoints and origin checks
- [x] Five pages, both languages and themes, 60 scaled render cases at minimum size, 9 normal/empty renders
- [x] Real app startup/exit, WPF binding/focus/dropdown checks and ordinary-permission read-only scan
- [ ] Physical 100/150/200% Windows DPI transitions / mixed-DPI monitors / 实机 DPI 切换
- [ ] Full manual keyboard traversal / 完整键盘手工遍历
- [ ] Actual third-party software installation/uninstallation / 真实第三方软件安装卸载
- [ ] Remote Windows CI / 远端 Windows CI (pending push)

## Executed checks / 实际检查

Windows x64, .NET SDK 10.0.403, Release. Full integration tests ran in the normal user context; only random test-owned HKCU keys and temporary files were created/cleaned. The live read-only scan recorded Elevated=false. No administrator token or system configuration change was required.

| Check | Result |
| --- | --- |
| Locked restore using verified local package feed | PASS |
| Release build, no restore | PASS, 0 warnings / 0 errors |
| Full xUnit suite, Release, no build, TRX | PASS, 56 passed / 0 failed / 0 skipped |
| Formatting verification, no restore | PASS |
| Final XAML/resource cleanup: rebuild + WPF shell subset | PASS, 11 passed |
| Resolved package advisory audit | PASS, 26 resolved packages, no advisory matches |
| 2,400-change query fixture | PASS, deterministic bounded pages and literal/Unicode/category searches |
| Migration failure injection and 128-row batch backfill | PASS, atomic rollback and retry |
| 43-snapshot selector fixture | PASS, selected pair survives catalogue paging |
| Raster-scale WPF matrix | PASS, 5 pages × 2 languages × 2 themes × 3 scales = 60 |
| Additional normal-size and empty-history renders | PASS, 9 |
| Actual executable startup / window close / process exit | PASS |
| Real Windows integration | PASS, 3 cases |
| GitHub Windows CI | PENDING |

The full suite completed in about 6 seconds on this machine. Performance fixtures validate bounded result sizes and operation outside the UI thread; this is not a million-record latency benchmark. Queries use literal normalized substring matching, so a large text search can scan rows. Snapshot comparison materializes two inventories and their diff on a worker; UI retains one result page.

只有截图渲染按 100/150/200% 缩放；没有改用户显示设置，也没有验证物理显示器跨 DPI 切换。自动焦点与下拉模板检查不等于完整人工键盘验收。截图只使用测试数据，源码与报告不包含真实清单、命令、路径或设备标识。

### Real read-only smoke / 实机只读摘要

All four categories succeeded, first baselines had no changes. The isolated inventory database was removed by the test. Only aggregate counts/status are retained.

| Category | Items | Skipped |
| --- | ---: | ---: |
| Software / 软件 | 211 | 152 |
| Startup / 启动来源 | 9 | 0 |
| Present PnP devices / 当前设备 | 293 | 0 |
| Network adapters / 网卡 | 60 | 0 |

Manual verification: visually inspected representative minimum/normal-size overview, populated/empty list and detail, snapshot comparison, troubleshooting and settings, across languages/themes and scaled renders. Fixed selection-box object text, dark date contrast and changed-field ordering found during visual review. Physical DPI transitions and full manual keyboard/software-install experiments remain NOT RUN.

Known limitations: physical DPI acceptance remains open; substring search is not culture-specific linguistic search; translated UI labels are not search aliases, so use the category filter; rejected ambiguous daylight-saving input needs a different unambiguous local time. Existing category coverage limits from PHASE 03 remain. Partial freezes the entire category baseline. Theme/language are session-only. No background monitoring or export is shipped.

GitHub delivery: draft PHASE 04 PR will be stacked on codex/phase-03 while earlier PRs remain unmerged. Review earlier phases first; this task does not authorize implicit merges. CI links below will identify the tested implementation commit, and latest PR checks cover later documentation updates.

Next phase proposal: PHASE 05 opt-in tray, scheduling, login start, export and retention, after the user requests continuation. 第四阶段不自动跨入第五阶段。
