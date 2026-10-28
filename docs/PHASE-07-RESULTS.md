# PHASE 07 — v0.1.1 history usability / 历史检索优化

Date / 日期: 2026-10-28

## Implemented / 实现

- Optional Added/Removed/Modified filter intersects text, category and observation windows. Existing schema 2 and privacy defaults remain compatible.
- Reset restores the first unfiltered page and clears detail; recent-record navigation clears the type filter as well.
- Empty filtered results use the existing localized no-match message. Language switches retain selected type.
- Paged history and JSON/CSV share one SQL predicate. Export still covers all matches. Failed search/page reads do not replace applied conditions.
- Bilingual changelog, README, release notes, query/UI documentation and updated synthetic screenshot.

类型筛选、重置与空结果提示保持简洁。导出与列表条件一致，搜索失败不会偷偷改变导出范围；不增加采集、依赖或迁移。

## Local validation / 本地验证

| Check | Actual result |
| --- | --- |
| Locked restore | PASS; existing verified package cache/feed, unchanged locks |
| Release build | PASS; 0 warnings, 0 errors; SDK 10.0.403 |
| Full Windows tests | PASS; 83 passed, 0 failed, 0 skipped |
| New regression cases | PASS; 6 cases covering every kind, intersections, paging, both export formats, invalid kind, reset and failed-search consistency |
| Formatting | PASS; `dotnet format --verify-no-changes --no-restore` |
| WPF matrix | PASS; 5 pages × 2 languages × 2 themes × 3 raster scales, plus other existing cases; selected type survives language changes |
| Visual inspection | English light and Chinese dark timeline at 960×620 DIP: search, category, type, Search and Reset fit; public synthetic screenshot refreshed |
| Portable payload | PASS; bundled runtime, clean exit, second instance exits, no inventory DB created on open |
| ZIP round trip | PASS; extracted package repeats runtime isolation/start/exit checks |

Commands: locked `dotnet restore`, `dotnet build -c Release --no-restore`, `dotnet test -c Release --no-restore --logger trx`, `dotnet format --verify-no-changes --no-restore`, `scripts/Build-Portable.ps1`. Local wrappers set only test-process environment and isolated output paths; no actual monitoring/sign-in setting is enabled.

An initial complete test run caught a duplicate localized resource key and the associated startup failure. The duplicate was removed, then the complete suite passed. Results above describe the corrected build.

## GitHub verification / 远端验证

Remote CI and release publication are checked separately after this commit. Local success alone does not establish publication. The [Actions page](https://github.com/Morris233/systemtrace/actions) and [v0.1.1 release](https://github.com/Morris233/systemtrace/releases/tag/v0.1.1) expose their actual state.

## Limits / 边界

The package is unsigned. Physical mixed-monitor transitions, full manual screen-reader use, actual sign-out/sign-in, third-party installation experiments and clean-VM certification remain NOT RUN. Raster renders and isolated process tests do not replace those experiments. No performance claim is made from this usability update.

Next proposed scope: evaluate additional startup coverage independently; no scheduled-task or Event Log collector is included in this maintenance stage.
