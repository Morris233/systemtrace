# UI guidelines / 界面规范

The user's rule is authoritative: few words, direct functional labels, clear reading order. No slogans, decorative theme headlines, promotional prose or duplicate explanations. Style comes from spacing, typography, color and consistent controls.

用户准则：简洁少字、易扫读、操作有逻辑。页面用功能名；不用口号、主题类文案、营销句或重复说明。仅保留功能标签、状态、操作提示。排版、留白、配色与一致控件体现风格。

## PHASE 04

- Five persistent pages: Overview / Changes / Snapshots / Troubleshoot / Settings. The global header has a short factual local/read-only/manual notice and language, theme and scan actions. No Demo entry or synthetic production data.
- Overview distinguishes Last complete scan and Last attempt, four category cards, latest scan counts and status, retained baseline counts and recent 8 changes. Unsuccessful counts are “—”, not zero. Coverage/permission details remain in tooltips.
- Changes: literal search, category, paged list and full detail. Search or Enter applies the filters. Search resets to page one. Detail clears when the query/page changes.
- Detail: direct title/category/kind, observation interval, changed fields first, all before/after fields, and collapsible source identity. Read-only text fields allow copy and wrapping; long values scroll within a bounded field. Missing/null/empty/unreadable are explicit; error codes are tooltips. Raw collected values and unknown field identifiers remain exact evidence rather than being translated.
- Snapshots: category, paged successful-snapshot catalogue, earlier/later selections, Compare and paged results. Selected headers stay available across catalogue pages; the list stays bounded (40 plus two pinned items). Invalid reversed/same/category-mismatched pairs disable Compare.
- Troubleshoot: local issue date/time, 1/6/24-hour or 7-day lookback, category/text and Search. Concise caution explains observation-window clues without asserting cause. It uses the same paged list/detail.
- Settings: session-only language/theme; background monitoring remains unavailable until PHASE 05.
- Navy and ivory themes with violet accent; 960×620 DIP minimum, 1180×780 default. Bounded lists recycle containers; only overview/settings and the detail pane scroll. Text values use system fonts Segoe UI / Microsoft YaHei UI.
- All interface labels/accessibility names use matching zh-CN/en-US dictionaries. Changing language preserves results, query choices, selected snapshot pair and detail. Native date presentation follows WPF culture behavior; numeric dates carry no root-cause meaning.
- Keyboard: F5 scans, Enter searches, Tab/Shift+Tab follows controls, lists support selection and controls have visible focus. Loading disables query controls and shows a thin progress indicator. Empty and error states stay concise and distinct.

Validate all five pages × both languages × both themes at 960×620, render at 100/150/200% raster scale, inspect representative layouts and binding logs, exercise keyboard focus and app close. Raster-scale render checks supplement physical DPI validation; they do not simulate Windows per-monitor DPI transitions. Record physical monitor switching and full manual keyboard traversal as NOT RUN unless actually performed.

验收区分自动渲染缩放和实机 DPI 切换。不能用截图渲染代替显示器切换测试；未执行项明确记录 NOT RUN。历史阶段的 Demo 界面约束已由第四阶段移除演示入口的要求取代。
