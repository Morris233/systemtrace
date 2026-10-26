# UI guidelines / 界面规范

Initialization supplement, not the missing original design. 本次初始化补充，非缺失原稿。

- Five top-level pages: Overview, Timeline, Snapshots, Troubleshoot, Settings. Change details will be a secondary surface in PHASE 04.
- Persistent sidebar, direct functional page titles, one concise DEMO notice, quiet category cards and clear placeholder states. No slogans, decorative prose, promotional headings or repeated explanations. Style comes from spacing, typography, color and consistent controls. Keep only text that helps a user identify a function, understand its state or act.
- Dark navy and light ivory themes, violet accent, 8-pixel spacing rhythm, 960×620 DIP minimum, scrollable content, wrapping copy. Use system fonts Segoe UI / Microsoft YaHei UI fallback.
- Text, accessible names and placeholders use matching English / Simplified Chinese dictionary keys. Default follows OS UI language: any `zh` maps to Simplified Chinese, everything else to English. User can switch live.
- Keyboard navigation follows logical reading order. Buttons retain visible focus. Meaning never depends only on color.
- Language/theme actions are functional. Scan/export/startup/tray actions are unavailable, with explanatory text; no disabled control implies a completed scan.
- Synthetic entries remain visibly Demo. No real machine identifier or scan time appears.

五页面统一侧栏、功能标题与必要状态。用户要求简洁少字：不使用口号、装饰性主题标题、营销句或重复解释；风格由留白、排版、配色与一致控件体现。文字只用于识别功能、说明状态、支持操作。变化详情留待第四阶段；演示标识始终可见。中英文与深浅主题实时切换，内容可滚动，长文案换行。所有文案与无障碍名称来自双语资源。未开放的操作须明确状态。

Verify 100%, 150% and 200% DPI, keyboard focus, 960×620 minimum, both languages and themes. Record unperformed manual checks as NOT RUN.

手工验收包含三种 DPI、键盘焦点、最小尺寸、双语与双主题；未完成项必须记为 NOT RUN。
