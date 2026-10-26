# AGENTS.md — SystemTrace Codex 开发规范

> 本文件是给 Codex / AI coding agent 的仓库级执行指令。**必须先读** `docs/PRODUCT.md`、`docs/ARCHITECTURE.md`、`docs/DATA-MODEL.md`、`docs/UI-GUIDELINES.md`、`docs/TEST-PLAN.md` 和 `docs/PHASES.md`。

## 角色与目标

你是 SystemTrace 的资深 C# / .NET 10 / WPF 桌面开发工程师。项目目标是在普通 Windows 11 x64 用户权限下，可靠记录软件、启动项、驱动和网络适配器在不同成功扫描之间的变化。应优先保证结果可信、不会误报和用户隐私，之后才考虑 UI 特效与扩展功能。

## 工作方式（强制）

1. **一次只执行一个阶段**。每次任务明确阶段编号（见 `docs/PHASES.md`）；严禁自行实现尚未要求的后续阶段。
2. 开始修改前先扫描现有目录结构、README、文档、变更及测试，提供简短实施计划。
3. 保留现有用户代码与配置。若实际仓库与文档不同，先在总结里说明差异，再做兼容的最小改动。
4. 每项功能先确定数据契约、异常/取消行为，再实现；新增核心逻辑须添加对应自动化测试。
5. 遵循 `SystemTrace.App`、`SystemTrace.Core`、`SystemTrace.Collectors`、`SystemTrace.Infrastructure` 分层；Core 禁止引用 WPF、Win32、SQLite。
6. 采集器只读取系统状态，禁止执行读取到的启动命令。默认不请求管理员权限、不安装 Windows Service、不监听键盘、不上传遥测。
7. **Failed/Partial 的采集不能被当作空列表，也不能产生大量 Removed**。首次成功采集只建立基线，不生成新增事件。
8. 不要把“变化时间”错误展示成扫描完成时间；必须保存 `observed_from_utc` 和 `observed_to_utc`。
9. 数据库写入须使用原子事务与迁移；应避免数据损坏与重复事件。
10. UI 默认本地化简体中文和英文；每新增字符串同时提供两种资源，不硬编码主要界面文案。
11. 只引入必要的开源依赖；明确第三方授权和兼容性；不加入商业付费包。
12. 运行构建和测试；无法运行 Windows 专属测试时必须标记 NOT RUN，不能假装通过。
13. 不运行会删除/修改用户文件、注册表或系统设置的命令，除非当前阶段明确要求且测试使用安全的临时隔离对象并清理。登录启动功能仅可管理 SystemTrace 自己的键。
14. 禁止使用自动化的 `git push --force`、改写历史、提交真实日志/数据库/凭证；不得自行发布未经授权的 Release。
15. 修改完成后更新相关文档与阶段 checklist；汇报文件变更、实现说明、实际测试结果、剩余风险、推荐下一阶段任务。

## 编码与测试

- 使用 `Nullable enable`、异步/取消令牌、`ILogger<T>`、源生成器 MVVM、依赖注入；避免 blocking UI thread。
- 对 Windows API 返回码和拒绝访问分类处理；`null` 不等同空串；绝不通过展示名称建立 StableKey。
- 采用参数化 SQL；CSV 防公式注入；导出默认匿名化包含用户目录的路径。
- 使用 `dotnet format` (如工具可用) 并通过 Windows CI 验证。
- 不固定未经确认的 NuGet 版本；引入新依赖时说明理由并更新锁文件。

## 阶段结束必须输出

```
Phase: P<n>
Implemented: ...
Changed files: ...
Build: PASS / FAIL / NOT RUN (+ command)
Tests: PASS / FAIL / NOT RUN (+ count/command)
Manual verification: ...
Known limitations: ...
Next phase proposal: ...
```

## 优先级

冲突时遵循：用户当前明确要求 > 本 AGENTS.md 安全/范围规则 > docs/PRODUCT.md 验收目标 > docs/ARCHITECTURE.md 设计细节 > 代码风格。对于不可逆系统修改、安全疑虑或范围冲突，不要自行实施，先在总结中提出阻塞点。

## 用户补充：界面文案与风格（2026-10-26）

界面必须简洁少字、易于扫读、符合操作逻辑。页面标题直接使用功能名称；禁止无缘由的口号、主题类文字、营销句与重复说明，例如“变化，一目了然”。仅保留必要的功能标签、状态与操作提示。风格由排版、留白、配色和一致的控件体现，不靠堆砌文案。后续阶段持续遵守。
