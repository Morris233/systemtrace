# Changelog / 更新记录

## 0.1.1 — 2026-10-28

- Filter history by Added, Removed or Modified, together with text and category.
- Reset returns to the first page, clears filters and closes stale details.
- Filtered empty results show “No matching records”; unfiltered empty history keeps “No changes”.
- JSON and CSV use the same filter as the visible results and export every matching page.
- Failed searches retain the last successful results and export conditions.
- Language changes retain the selected type. No new dependencies or database migration.

中文：时间线支持新增／移除／修改筛选及重置；区分无匹配记录与暂无变化；导出与列表条件一致，搜索失败时保留上次成功条件；切换语言保留类型选择。无新增依赖，无需数据库迁移。

## 0.1.0 — 2026-10-27

Initial public release: four read-only Windows collectors, successful-category baselines, local SQLite history, snapshot comparison, troubleshooting, opt-in monitoring and privacy-filtered export.

首次公开版：四类只读采集、成功类别基线、本地历史、快照对比、故障回溯、可选后台监测与脱敏导出。
