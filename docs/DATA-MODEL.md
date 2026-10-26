# Data model / 数据模型

Initialization supplement, not the missing original design. 本次初始化补充，非缺失原稿。

PHASE 01 defines only `DemoCategory(ResourceKey, ExampleCount)` and `DemoOverview(IsDemo, Categories)`. The provider returns four fixed categories: software, startup, drivers and network. Counts are illustrative; they are never measurements. `IsDemo` is always true for the shipped provider. No timestamp claims a scan occurred, and no domain snapshot or database schema is introduced yet.

第一阶段只定义演示类别及演示概览；数量均为固定样例，不表示本机数据，也不声明发生过真实扫描。本阶段没有快照领域模型或数据库。

For PHASE 02, the PRD requires Snapshot, Change, CollectionResult and ScanRun contracts, canonical stable keys, per-field null/error distinction, UTC observation windows (`observed_from_utc`, `observed_to_utc`), per-category Succeeded/Partial/Failed/Skipped status, deduplication and transactional baseline advancement. First success establishes a baseline with no changes. Final schemas and migration tests must be designed in that phase; the unavailable original DATA-MODEL.md has not been reconstructed.

第二阶段应按原 PRD 确定稳定键、字段级空值与错误、UTC 观察窗口、四种采集状态、去重及事务基线。首次成功仅建立基线。正式 schema 留待该阶段设计与测试。
