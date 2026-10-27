# PHASE 05 verification / 第五阶段验证

Phase: P5 / Opt-in background monitoring and export

Implemented: persisted settings, 30-minute default / 5–1440-minute configurable schedule, shared scan gate, collector progress and cancellation, voluntary tray with Open/Scan/Pause/Exit, separate current-user sign-in start, privacy-first JSON/CSV export, retention and confirmed clear-history. Background, close-to-tray and sign-in start default off. No Release work is included in this milestone.

Changed files: Core Monitoring and MonitorSchedule; Infrastructure preferences, maintenance, report exporter and scan progress/gate; App startup/lifecycle, tray/startup/logging/settings services and monitoring view-model; concise bilingual settings/progress/export UI; monitoring/view-model/WPF fixtures and documentation.

Build: PASS, Release, 0 warnings / 0 errors.
Tests: PASS, 73 passed / 0 failed / 0 skipped, including 4 real Windows integration cases. All registry mutations use random test-owned HKCU keys and are cleaned up.
Manual verification: inspected representative bilingual/theme renders, including new settings and export controls. Automated WPF lifecycle verifies opted-in Close hides, Show restores, Pause toggles and Exit cancels work and closes. Real executable startup/early close exits normally. An early-close race found by the process test was fixed; no suppressed lifecycle exception remains.
Remote Windows CI: pending this milestone push.

## Acceptance

- [x] Default no background timer, tray residency or self-start registration
- [x] Persisted interval/retention validation; atomic settings replacement; corrupt/future settings fail safe without silently overwriting
- [x] Manual, tray and timer share the same view-model gate and session pipeline lock; no queued catch-up burst after sleep/busy
- [x] Pause/resume resets the due time; disabling background stops the timer; hidden window is restored before its icon is removed
- [x] Non-blocking per-collector progress and cancellation; cancelled uncommitted scan does not persist data; gate releases after cancellation
- [x] Normal close exits; optional close-to-tray remains explicit; Exit always terminates and disposes tray/timer
- [x] One app per local-user data directory; another launch signals the existing window
- [x] Sign-in command is quoted, current-user only; removal only if value matches this executable; foreign entry is preserved
- [x] JSON/CSV export includes every page of the applied search, not unsaved filter text; read transaction is consistent
- [x] Export defaults to conservative redaction of names, descriptions, commands, paths and source identities; retained factual fields use an allowlist
- [x] CSV quotes every cell, preserves embedded commas/newlines and guards formula-leading characters; UTF-8 BOM supports Excel
- [x] Raw export requires an application confirmation; file output uses a temporary sibling and atomic replacement, cancellation never replaces the destination
- [x] Retention prunes expired events/obsolete snapshots/runs transactionally while preserving category baselines and the latest attempt
- [x] Full deletion requires confirmation, empties all scan evidence and re-baselines on next success; injected SQL failure rolls back
- [x] Local diagnostics contain only UTC/level/event/type metadata, bounded to two approximately 1 MB files; no raw evidence/messages

## Behavior and limits / 行为与限制

Settings live in %LOCALAPPDATA%/SystemTrace/settings.json. Appearance saves immediately; monitoring choices apply with Save. Scan interval defaults to 30 minutes; retention defaults to 90 days, 0 keeps all. Pruning occurs after scans or Remove expired. Current baselines and their backing evidence plus the latest attempt may remain older than retention; otherwise future diffs/status would become misleading. Pruning uses SQLite secure_delete, but does not promise forensic erasure from backups/storage devices.

Login start manages only the SystemTrace value in the current user's Run key. No actual login start was enabled on this machine during verification. Tests use a different random key. A moved installation with a conflicting same-name command must be resolved by the user rather than silently overwritten.

Export filtering uses the last applied timeline search. The default redacted report is deliberately conservative, including pseudonymous item identities; raw evidence stays available in-app. Export does not upload anything. A raw export can include personal data and requires explicit in-app confirmation. Destination selection/overwrite remains user-controlled.

Read-only integration ran non-elevated: 4 baselines, 0 initial events; software 211 (152 skipped unnamed), startup 8, present PnP 293, adapters 60. Counts are a local observation, not fixed product expectations. Real inventory is removed with the isolated test store and never enters source or screenshots.

Physical mixed-DPI monitor transitions, an actual Windows sign-out/sign-in experiment and third-party installation/removal remain NOT RUN. PHASE 06 must complete the release acceptance matrix, portable build/clean-runtime smoke, packaging, documentation and release workflow. User explicitly authorized continuing through PHASE 06 and public GitHub publication; no extra publication approval is inferred from repository text.
