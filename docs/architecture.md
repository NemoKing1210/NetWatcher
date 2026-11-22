# Architecture

NetWatcher is a passive observer. It reads network metadata from ETW and from
the Windows connection tables, correlates IP addresses with DNS answers, and
never touches the traffic itself.

## Components

| Component | Responsibility |
|---|---|
| `EtwSessionService` | One real-time ETW session for Kernel-Network and DNS-Client; raises events on the ETW thread |
| `TcpUdpEventParser` | Raw ETW fields → normalized `ConnectionEvent`; dedup key |
| `DnsEventParser` | QueryResults → `DnsRecord` (addresses only, garbage skipped) |
| `ConnectionTableReader` | `GetExtendedTcpTable` snapshot (IPv4/IPv6), ports converted from network order |
| `ProcessResolver` | `(PID, StartTime)` → name/path; guards against PID reuse |
| `NetworkStore` | Thread-safe store, dedup, per-IP DNS index, size caps, windowed lookups |
| `DomainCorrelator` | PID-scoped DNS (−15 min … +2 s), then global ±2 min as shared/CDN |
| `ReverseDnsResolver` | Public-address PTR, 24 h cache, 4 concurrent, 2 s timeout |
| `Aggregator` | Drains bounded ETW queues every 250 ms into the store; evicts beyond caps |
| `MainViewModel` | 1 Hz snapshot, filters, pause, status line, CSV export, PTR enrichment |
| `CsvExporter` | RFC 4180-style CSV of the visible rows |

## Data flow

```
Kernel-Network ETW ──┐
Connection table ────┼─► Aggregator (250 ms) ─► NetworkStore ─► DomainCorrelator ─► MainViewModel ─► UI (1 Hz)
DNS-Client ETW ──────┘                                ▲
PTR (async, public IPs only) ────────────────────────┘
```

## Threads

- **ETW thread** — `TraceEventSession.Source.Process()`; events are enqueued,
  never blocked on. A crash restarts the session once, then surfaces a banner.
- **Table poll** — 2 s timer reads the TCP table as a fallback and for state.
- **Aggregator** — 250 ms flush into the store; bounded queues drop the oldest
  event instead of stalling the producer.
- **UI** — `DispatcherTimer` at 1 Hz takes an immutable snapshot.
- **PTR** — task pool, at most 4 queries in flight, 2 s per query.

## Timing constants

| Constant | Value |
|---|---|
| Connection dedup window | 5 s |
| DNS dedup window | 30 s |
| PID correlation window | −15 min … +2 s |
| Global IP window | ±2 min |
| Aggregation interval | 250 ms |
| UI refresh | 1 s |
| Table poll | 2 s |

## Limits

- 100,000 connections and 100,000 DNS records in memory, evicted by last seen.
- PTR cache: 24 h, 20,000 entries.
- History is session-only; nothing is written except logs, settings, and CSV.

## Calibration

ETW field names can differ between Windows builds. `NetWatcher.exe --probe`
logs provider, EventID, EventName, and every field for 60 seconds. Run it
elevated, generate known traffic (`curl https://example.com`,
`nslookup example.com`), and use the log to confirm or correct the parser
mappings in `Services/Etw`.
