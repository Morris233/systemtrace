# Data model / 数据模型

## PHASE 02 contract / 第二阶段契约

This implementation supplements the unavailable original design. 本规格为本次实现补充，非原稿恢复。

- `SnapshotItem`: stable key, copied read-only sorted fields; duplicate normalized names/keys rejected. `FieldValue(Value, ErrorCode)` preserves missing, null, empty and unreadable states. Field values retain exact text. Keys and field names use Unicode Form C.
- StableKeys builds identity from registry hive/view/path/value, startup scope/path, device instance ID or adapter GUID; Windows identities normalize casing/separators and use length-prefixed components. Display names are never identity. Demo keys start with `demo/`.
- `CollectionResult`: category, Succeeded/Partial/Failed/Skipped, UTC start/end, items and error. Succeeded cannot contain unreadable fields; use Partial conservatively. Failed/Skipped cannot contain items. Partial evidence is saved but never compared or promoted.
- `ScanRun`: nonempty ID, UTC interval, Demo flag, exactly four unique category outcomes with intervals contained in the run. Collections are copied read-only.
- `Snapshot`: successful category observation. `Change`: deterministic ID, Added/Removed/Modified, full before/after and field deltas, previous/new snapshot observation window. Enumeration order does not affect comparison. Null and empty differ. First success produces no changes. Cross-category or backwards-time comparisons are rejected.
- `InventoryHistory`: total runs, last attempt, current category baselines, newest 200 changes, cumulative category counts. `ScanCommit`: duplicate indicator and new event count. Reusing a scan ID with different canonical payload fails.

SQLite v1 (`PRAGMA user_version=1`) tables: store_info (enforced Demo/System origin), scan_runs (canonical input hash and complete evidence), collection_results (four outcomes), snapshots (success only), baselines (one per category), changes (unique new-snapshot/item). Foreign keys and unique constraints are enabled. Schema migration is transactional; unknown newer schema is rejected. Every run, evidence, snapshots, changes and baseline movement commit atomically. Cancellation before commit or any SQL/diff error rolls back; cancellation after a completed commit does not undo it. Writes use immediate transactions; history reads use consistent transactions. Values are parameterized.

演示操作才创建 `%LOCALAPPDATA%/SystemTrace/demo-v1.db`，仅打开程序不创建新库；已有演示历史可重载。真实数据必须使用独立库，来源不匹配会拒绝。SQLite 工作在 UI 线程外执行。没有注册表或系统清单 API。失败、不完整、跳过结果不生成误删且不推进基线；成功空集合才代表确实为空。

## PHASE 01 historical scope / 第一阶段历史范围

Initialization supplement, not the missing original design. 本次初始化补充，非缺失原稿。

PHASE 01 defines only `DemoCategory(ResourceKey, ExampleCount)` and `DemoOverview(IsDemo, Categories)`. The provider returns four fixed categories: software, startup, drivers and network. Counts are illustrative; they are never measurements. `IsDemo` is always true for the shipped provider. No timestamp claims a scan occurred, and no domain snapshot or database schema is introduced yet.

第一阶段只定义演示类别及演示概览；数量均为固定样例，不表示本机数据，也不声明发生过真实扫描。本阶段没有快照领域模型或数据库。

For PHASE 02, the PRD requires Snapshot, Change, CollectionResult and ScanRun contracts, canonical stable keys, per-field null/error distinction, UTC observation windows (`observed_from_utc`, `observed_to_utc`), per-category Succeeded/Partial/Failed/Skipped status, deduplication and transactional baseline advancement. First success establishes a baseline with no changes. Final schemas and migration tests must be designed in that phase; the unavailable original DATA-MODEL.md has not been reconstructed.

第二阶段应按原 PRD 确定稳定键、字段级空值与错误、UTC 观察窗口、四种采集状态、去重及事务基线。首次成功仅建立基线。正式 schema 留待该阶段设计与测试。
