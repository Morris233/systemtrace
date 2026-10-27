# Data model / 数据模型

## PHASE 04 queries and schema 2 / 第四阶段查询与迁移

- ChangeQuery accepts literal text (up to 512 characters), an optional category and optional inclusive UTC observation-window bounds. Pages are zero based, default 40, allowed size 1–100. Out-of-range positive pages clamp to the last page. Results sort by observed_to_utc DESC, id DESC within one consistent transaction.
- Text search covers stable identity, category/kind identifiers, field names, before/after values and error codes. Text is normalized to Unicode NFC and invariant uppercase for literal substring matching; SQL metacharacters are data, not wildcards. This is not culture-specific linguistic search, and translated UI category/status labels are not indexed. Category has a separate localized filter.
- Window matching is overlap: observed_to_utc >= requested_from AND observed_from_utc <= requested_to. A change whose observation interval crosses the issue time may match. The result is a clue, never proof of cause or an exact occurrence time. UI accepts local HH:mm or HH:mm:ss and converts to UTC; invalid or ambiguous daylight-saving times are rejected.
- ChangePreview stores a label capped at 200 characters and value previews at 256 (plus an ellipsis). The full Change and all fields remain intact for on-demand detail. Missing field, null, empty string and unreadable value remain distinct. Changed fields appear first; all fields and exact source identity remain readable/copyable. Device absence is labeled Not detected this scan.
- SnapshotHeader contains only identity, category, UTC observation time and item count. Only successful category results enter snapshots. Comparison requires two distinct successful snapshots, same category and non-reversed time. It is read-only and does not append historical events.
- DashboardSummary includes latest attempt, latest all-four-success run, retained category baseline sizes/times, nullable latest change counts, diagnostics and the newest 8 previews. A Partial/Failed/Skipped category has no current count; it does not become a zero-change success.
- Schema 1 -> 2 adds changes.search_text and changes.summary_json, snapshots.item_count, and category/time indexes. The migration checks version again under an immediate transaction, backfills in batches of 128 changes, supports cancellation checkpoints and commits version 2 only after success. Injected backfill failure rolls back all added columns/indexes and preserves version 1 and evidence. JSON counts use SQLite's [built-in JSON functions](https://www.sqlite.org/json1.html). Database filename remains system-v1.db.
- Search text and summaries duplicate existing evidence only in the same local database. They do not add collection or network requests. Old schema JSON remains compatible. No historical data is removed.

查询按观察区间相交检索，时间端点包含在内；不是按“故障发生时间”推断变化。全文为字面子串、统一大小写与 NFC 规范化，分页稳定且有上限。schema 2 原子补齐摘要、检索文本和快照数量；迁移故障不会留下半完成结构。原演示库保留在磁盘但不再由应用访问。


## PHASE 03 additions / 第三阶段补充

CollectionResult adds optional SkippedItemCount (default 0) and Warnings (default empty); old JSON remains readable without a schema change. Software records display name/version/publisher/system-component and exact registry source; no uninstall command or install path is captured. Startup records value name/command/source/hive/view/key, or folder scope/relative filename/last-write signal. Commands and source paths can be sensitive; they stay in the local system database and are excluded from Git/artifacts.

驱动字段为 display_name、class、manufacturer、driver_provider、driver_version、driver_date。设备实例 ID 只用于本地稳定键。CM_NO_SUCH_VALUE 表示该属性确实不存在（null，无错误）；读取失败保留错误并使类别 Partial。默认仅包含 present 设备；Removed 在 UI 显示“本次未检测到”，不推断卸载。异常空设备列表视为失败。

Network persists only display_name, description, interface_type and operational_status; GUID forms identity. IP/MAC/SSID/password and other identifying network data are absent from the model. Successful-empty and unsuccessful-empty remain distinct. Access errors or races preserve the last good category baseline. Startup file modification time is evidence only, never a claimed change occurrence time.

真实扫描写入 is_demo=false 的独立 system-v1.db，演示仍为 is_demo=true 的 demo-v1.db。数据库来源检查禁止混用；启动仅载入已有历史，手动扫描后才创建新库。

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
