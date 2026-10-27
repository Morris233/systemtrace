# PHASE 06 verification / 第六阶段验证

Phase: P6 / Release hardening for v0.1.0

Implemented: complete replacement T001–T026 acceptance matrix; corruption/clock/instance/large-history checks; accessible control names and five-page focus traversal; conservative metadata redaction and CSV control-character guards; application/tray icon and contrast fixes; bilingual README with synthetic screenshots; contribution/issue templates; separate publish locks; self-contained win-x64 packaging, license collection, SHA256 and build provenance; controlled GitHub release workflow.

Changed files: HardeningTests/PublicPreview and expanded WPF/integration tests; App data-directory test isolation, icon/control resources, version/baseline action and appearance error preservation; export validation; normal/publish lock handling; scripts/Build-Portable.ps1 and Test-Portable.ps1; Windows CI and release workflow; bilingual README, acceptance/release/privacy/dependency/contribution notes and screenshots.

## Actual local results / 实际本地结果

| Check | Result |
| --- | --- |
| Normal locked restore and separate win-x64 locked restore | PASS |
| Release build | PASS, 0 warnings / 0 errors |
| Full final test suite | PASS, 77 passed / 0 failed / 0 skipped |
| WPF raster validation | PASS, 60 page/language/theme/scale combinations plus 11 normal/empty/documentation renders |
| Five-page keyboard focus traversal / accessible names | PASS, automated; recent-record buttons and field/value names corrected |
| Process startup/early close and opted-in tray Hide/Show/Pause/Exit | PASS |
| Damaged DB and future clock | PASS, evidence preserved, no implicit reset or baseline advance |
| Production dependency/runtime advisory lookup | PASS, 28 resolved packages, no matching NuGet advisory ranges |
| Source privacy check | PASS, no actual inventory, credentials, user paths, logs or test results in tracked payload |
| Self-contained portable build and runtime-isolated smoke | PASS, loaded bundled coreclr, no shared .NET discovery, empty isolated profile, normal exit and secondary-instance handoff |
| ZIP extraction round-trip smoke | PASS, bundled runtime, second-instance exit 0, main exit 0, no automatic inventory DB |
| Runtime/dependency licenses and original attribution | Included; missing unsupported licenses fail packaging |
| Public GitHub delivery | Verify final Actions/tag/assets/visibility at delivery; it is not inferred from source upload |

Windows x64, SDK 10.0.403, bundled .NET/WindowsDesktop 10.0.14. Integration ran with Elevated=false and created only randomized test-owned registry keys/directories. A restricted desktop context could not complete a process-close smoke; rerunning in the ordinary user desktop context passed. No administrator token or real sign-in registration was required.

Measured sample on this machine: real read-only scan about 0.9 seconds, all four baselines and zero initial changes. A 12,000-change fixture retained a 40-row UI page; first plus filtered page queries took about 48 ms, and streaming 3,000 software changes to a null JSON stream about 122 ms. These are sample timings, not universal guarantees; fixture setup, cold disk caches and other hardware change them. The final local payload startup measured 587 ms and its extracted-ZIP startup 600 ms to a responsive window. No OS cache purge or true cold-machine benchmark is claimed.

## Acceptance and evidence / 验收证据

T001–T025 passed for their explicitly stated automated scope. T026 adds exact remote CI, tag/assets/checksum and public-visibility verification at delivery. See [the matrix](TEST-PLAN.md), [Windows checks](https://github.com/Morris233/systemtrace/actions/workflows/ci.yml), [release workflow](https://github.com/Morris233/systemtrace/actions/workflows/release.yml) and [v0.1.0](https://github.com/Morris233/systemtrace/releases/tag/v0.1.0). A private repository or merely triggered workflow must never be reported as public publication.

The release workflow is restricted to main and an intentional manual dispatch or [release] commit marker. Its packaging job has read permission, repeats build/tests/formatting, includes licenses, starts the payload without shared runtime discovery, checks the extracted ZIP and uploads immutable assets. Only the dependent publish job has contents-write permission; it rechecks SHA256 and creates the tag/Release once. Existing versions fail instead of being overwritten. The user explicitly authorized this release and public source.

Current local package files are ZIP, SHA256SUMS.txt and BUILD-INFO.json. The GitHub-built Release assets are canonical and identify their exact source commit/SDK/runtime; local and remote ZIP hashes may differ due to provenance/timestamps. Source original PRODUCT.md / PHASES.md remain unchanged. Public screenshots use only fictional test fixtures, with no demo generator linked into production.

## Known limitations / 限制

Physical mixed-monitor DPI transitions, full manual screen-reader use, actual sign-out/sign-in and third-party install/uninstall remain NOT RUN. Automated 96/144/192-DPI raster output and focus/name traversal do not pretend to be those physical checks. Runtime-isolated fresh-profile smoke does not claim clean-VM certification or forced power-loss testing.

Collector coverage remains as documented. Partial freezes the category baseline. Substring search may scan rows on very large databases; full snapshots are retained per scan within the retention policy. Nonstandard/free-text metadata is redacted conservatively in default reports; unfiltered export requires confirmation. The portable build is unsigned, contains no updater/telemetry and does not modify Windows configuration except its explicitly enabled owned login value.

Next: no remaining MVP development phase. Future v0.2+ work stays outside this release. Final publication requires verified GitHub access, Release success and public repository visibility; any missing external authorization/session must be reported rather than claimed complete.
