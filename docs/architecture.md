# Architecture

NetWatcher is a passive observer. It reads network metadata from ETW and from
the Windows connection tables, correlates IP addresses with DNS answers, and
never touches the traffic itself.

## Components

| Component | Responsibility |
|---|---|
| `EtwSessionService` | One real-time ETW session for Kernel-Network and DNS-Client; raises events on the ETW thread |
| `DnsEventDispatch` | Classifies a DNS-Client event by its payload fields: completed answer, query-only, or unknown |
| `TcpUdpEventParser` | Raw ETW fields → normalized `ConnectionEvent`; dedup key |
| `DnsEventParser` | QueryResults → `DnsRecord` (addresses only, garbage skipped) |
| `ConnectionTableReader` | `GetExtendedTcpTable` snapshot (IPv4/IPv6), ports converted from network order |
| `ProcessResolver` | `(PID, StartTime)` → name/path; guards against PID reuse |
| `NetworkStore` | Thread-safe store, dedup, per-IP DNS index, size caps, windowed lookups |
| `DomainCorrelator` | PID-scoped DNS (−15 min … +2 s), then global ±2 min as shared/CDN |
| `ReverseDnsResolver` | Public-address PTR, 24 h cache, 4 concurrent, 2 s timeout |
| `Aggregator` | Drains bounded ETW queues every 250 ms into the store; evicts beyond caps |
| `MainViewModel` | 1 Hz snapshot, filters, pause, status line, exports, PTR enrichment |
| `DnsPageViewModel` | DNS page: newest-first rows capped at 2000, filter, in-place row reuse |
| `PauseState` | Session-wide pause flag shared by both pages |
| `ThemeController` | Resolves system/light/dark, persists the choice, applies it through `IThemeApplier` |
| `LanguageController` | Resolves the Windows or explicit UI language, persists it, applies the process cultures |
| `ProcessIconProvider` | Shell icon per executable path, cached and extracted off the UI thread |
| `CsvExporter`, `MarkdownExporter`, `JsonExporter` | The visible rows as CSV, a Markdown table, or a JSON array |

## Data flow

```
Kernel-Network ETW ──┐
DNS-Client ETW ──────┴─► Aggregator (250 ms) ─► NetworkStore ─► DomainCorrelator ─► MainViewModel ─► UI (1 Hz)
Connection table (2 s) ─────────────────────────►      ▲
PTR (async, public IPs only) ─────────────────────────┘
```

The aggregator resolves a PID to its process instance once per short TTL and
stores the identity with the record; the table reader shares the same resolver.

## Shell and pages

`MainWindow` is a WPF-UI `FluentWindow`: a Fluent title bar, a `NavigationView`
whose pane stays open, and cached `UserControl` pages (`ConnectionsPage`,
`DnsPage`, `SettingsPage`) swapped through the control's content host, so a
table's sort, selection, and scroll position survive navigation. A process
select in the Connections command bar, next to the search box, shows each
executable's shell icon and sets `MainViewModel.SelectedProcess`; its first
option, "All processes", clears the focus and is restored automatically when
the selected process disappears. The Connections Process column shows the same
icons while "All processes" is selected, and the whole column is hidden when
one process is focused. Both data pages share one `PauseState`, so pausing
either page freezes the session.
`ThemeController` resolves the saved preference (system by default) to light
or dark, applies it through `IThemeApplier` (WPF-UI's `ApplicationThemeManager`),
and re-resolves it when Windows reports a theme change while the preference is
"system". Window geometry, the theme, and the language live in
`%LOCALAPPDATA%\NetWatcher\settings.json`.

The Settings page owns both preferences. UI strings live in
`Localization/Strings*.resx` (English neutral plus Russian, Spanish, German,
French, Brazilian Portuguese, Simplified Chinese, and Japanese) and are read
through the `Strings` facade; XAML references them with `x:Static`, so a
language change swaps the shell window after applying the new process culture.
The collectors and view models stay alive across that swap. With no explicit
choice, `LanguageController` maps the Windows UI language captured at startup
to a supported catalog, falling back to English.

DNS-Client events are classified by payload shape rather than by event ID: a
query name plus a result string is a completed answer, a name alone is a
query-only event and is ignored, and an event with neither is reported once in
the log as unknown. Windows builds disagree about the event IDs (current builds
use 3009–3020 where older ones used 3006/3008), but the payload of a completed
answer is stable.

## Threads

- **ETW thread** — `TraceEventSession.Source.Process()`; events are enqueued
  with an unresolved PID and never open process handles or block on I/O. A
  crash restarts the session once, then surfaces a banner.
- **Table poll** — 2 s timer reads the TCP table as a fallback and for state,
  and writes directly to the store (bypassing the aggregator).
- **Aggregator** — 250 ms flush into the store; resolves each PID at most once
  per 2.5 s; bounded queues drop the oldest event instead of stalling the
  producer.
- **UI** — `DispatcherTimer` at 1 Hz takes an immutable snapshot of the visible
  page only.
- **PTR** — task pool, at most 4 queries in flight, 2 s per query.

## Timing constants

| Constant | Value |
|---|---|
| DNS dedup window | 30 s |
| PID resolution cache | 2.5 s |
| PID correlation window | −15 min … +2 s |
| Global IP window | ±2 min |
| Aggregation interval | 250 ms |
| UI refresh | 1 s |
| Table poll | 2 s |

## Limits

- 100,000 connections and 100,000 DNS records in memory, evicted by last seen.
- PTR cache: 24 h, 20,000 entries.
- History is session-only; nothing is written except logs, settings, and exports.

## Calibration

ETW field names can differ between Windows builds. `NetWatcher.exe --probe`
logs provider, EventID, EventName, and every field for 60 seconds. Run it
elevated, generate known traffic (`curl https://example.com`,
`nslookup example.com`), and use the log to confirm or correct the parser
mappings in `Services/Etw`.
