# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Fluent (WPF-UI) shell: title bar, a permanent navigation pane, and
  Connections/DNS events pages that keep their state.
- DNS events page: newest answers first, capped at the 2000 most recent, with
  its own filter and an empty-state hint.
- Persisted light/dark theme with a Mica backdrop (solid window on systems
  without Mica support) and live tracking while the choice is "system".
- Settings page: auto/light/dark theme and the interface language, in two
  aligned setting cards with matching selects.
- Localization for English, Russian, Spanish, German, French, Brazilian
  Portuguese, Simplified Chinese, and Japanese. The Windows UI language is
  detected automatically, with English as the fallback; the choice is saved.
- Live TCP/UDP connection monitoring with process attribution (ETW
  Kernel-Network plus a 2 s TCP table fallback).
- DNS correlation through the Windows DNS Client provider, with shared/CDN
  markers and asynchronous PTR fallback.
- Session history with first/last seen timestamps, bounded in-memory store.
- Process filter, text filters, pause, clear, and export as CSV, Markdown,
  or JSON from one menu.
- Unique-IP toggle on the Connections page: one row per remote address,
  picking the most recently active connection.
- Application icons from the executable's shell icon, cached per path and
  extracted off the UI thread: in the process select and in the Connections
  Process column. That column is hidden while a single process is focused,
  since every row would repeat the same name and icon.
- Window geometry persistence under `%LOCALAPPDATA%\NetWatcher\`.
- Calibration mode (`--probe`) for verifying ETW field mappings.
- Open-source repository files: MIT license, README, contributing guide, code
  of conduct, security policy, issue/PR templates, and CI.
- Release pipeline: a `v*` tag builds the self-contained win-x64 app, the Inno
  Setup installer, and the portable zip, and publishes them with
  `SHA256SUMS.txt` to GitHub Releases. Code signing is optional and turns on
  with repository secrets.

### Changed

- The process filter is a select next to the Connections search box, with
  "All processes" as its first option, instead of a process list in the
  navigation pane. Its selection falls back to "All processes" when the
  process disappears.
- Pause is session-wide: freezing either page freezes both.
- Controls now use the Fluent WPF-UI set: buttons, filter boxes, the theme
  and language selects, the process select, the tables, and their scroll bars.
- Tables size their columns to the window; when the content is narrower than
  the column minimums, a horizontal scroll bar appears instead of clipping the
  last columns.
- The window adapts to the monitor: it opens and restores inside the visible
  work area, supports windows down to 900 × 460, and the navigation pane
  collapses to its icon rail below 1220 px so the tables keep their width.
- The Connections toolbar moves its buttons to a second row when the filter
  row runs out of space, and long page counters trim behind a tooltip.
- The app is per-monitor DPI aware (PerMonitorV2), so it stays sharp at 125%,
  150%, and mixed-monitor scaling instead of being bitmap-scaled.
- Settings cards shrink their select column on narrow windows.
- Status bar wording is explicit (`seen` / `shown`), and empty tables explain
  whether there is no traffic or no match for the active filter.
- The new-row cue is a short success-coloured time cell instead of an opaque
  green row fill, and it clears when the row ages.
- The connection context menu and the details window are Fluent surfaces; a
  right click selects the row it acts on.
- The window carries the project icon.

### Fixed

- Toolbar button labels stay readable when pressed in the dark theme. WPF-UI
  4.3.0 sets the pressed colour on the button but its template binding leaves
  the content black (lepoco/wpfui#1722); the toolbar buttons now bind their
  content foreground to the button directly.
- DNS answers are dispatched by payload shape instead of a hard-coded event-ID
  list, so they appear on Windows builds whose DNS-Client provider uses newer
  event IDs.
- ETW timestamps are converted to UTC before they reach the store, so domains
  correlate and records evict correctly outside UTC time zones.
- The ETW callback no longer resolves processes or writes log files; PID
  resolution moved to the aggregator with a 2.5 s cache shared with the table
  reader.
- Restarting ETW after a failure can no longer start a session that shutdown
  does not stop.
- IPv4-mapped IPv6 addresses are normalized to IPv4, and unique-local IPv6
  addresses no longer trigger public PTR lookups.
- Filters survive a language change; the filter box is bound to the view model
  instead of copied through a change handler.
- The process filter distinguishes two processes that reused the same PID.
- CSV exports start with a UTF-8 BOM and neutralize leading formula characters;
  all export timestamps are ISO-8601 UTC.
- Settings are written through a temporary file, and logging failures can no
  longer break a collector.
- Failed DNS lookups are kept with their error status instead of being dropped.
- The release pipeline signs `NetWatcher.exe` before building the installer, and
  signing secrets are scoped to the signing steps.

### Performance

- DNS deduplication and eviction use secondary indexes and lazy priority
  queues instead of scanning or sorting the whole store under the lock.
- The DNS page selects the newest 2,000 records without copying and sorting
  100,000, and only refreshes while it is visible.
- Connection rows skip formatting and property-change notifications when a
  record, its binding, and its process name are unchanged.
- `MainViewModel` aggregates processes during the snapshot pass instead of a
  second `GroupBy`/`Max`/sort, and reads the DNS count without copying the DNS
  snapshot.
- The table reader retries `ERROR_INSUFFICIENT_BUFFER` instead of silently
  dropping half a snapshot.
