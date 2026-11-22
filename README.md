# NetWatcher

<p align="center">
  <img src="assets/logo.svg" width="96" height="96" alt="NetWatcher logo" />
</p>

<p align="center">
  <a href="https://github.com/NemoKing1210/NetWatcher/actions/workflows/ci.yml"><img src="https://github.com/NemoKing1210/NetWatcher/actions/workflows/ci.yml/badge.svg" alt="CI" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License: MIT" /></a>
  <img src="https://img.shields.io/badge/.NET-10-512BD4" alt=".NET 10" />
  <img src="https://img.shields.io/badge/platform-Windows%20x64-0078D6" alt="Platform: Windows x64" />
</p>

**See, in real time, which domains and IP addresses a specific application or
game on your Windows machine connects to.**

NetWatcher watches the Windows network stack passively, attributes every
connection to a process, and maps remote IPs to the domains that process
resolved. It never blocks traffic, never decrypts TLS, and sends nothing
anywhere.

> **Status:** v1 in development. Screenshots are added with the first release.

## Features

- **Live connections** — TCP and UDP, with process attribution.
- **Process focus** — pick a process (or a game) from the process select on the
  Connections page, with its application icon, and see only its traffic. While
  no single process is focused, the Process column shows the same icons; once
  one is focused, the redundant column is hidden.
- **Domains** — from the Windows DNS Client; shared/CDN IPs are marked `~`,
  PTR fallbacks are shown in italics.
- **DNS events page** — every answer the system resolver returned, newest
  first, filterable by domain, address, or process.
- **Session history** — first and last seen per connection.
- **Filters, pause, clear** — narrow both pages, optionally to one row per
  remote IP; freeze them without stopping collection.
- **Light and dark theme** — follows Windows until you pick one, then stays.
- **Settings page** — theme and interface language in one place.
- **Localization** — English, Russian, Spanish, German, French, Brazilian
  Portuguese, Simplified Chinese, and Japanese; the Windows language is used
  until you pick another.
- **Export** — the exact filtered view as CSV, a Markdown table, or JSON.
- **Honest about limits** — DoH, VPNs, and CDNs are documented, not hidden.

## Requirements

- Windows 10 22H2 or Windows 11, x64.
- Administrator rights. ETW and the full connection table need elevation; the
  app still runs without them in table-polling mode.
- No .NET installation needed for the release build (self-contained).

## Installation

1. Download the latest `NetWatcher-v*-win-x64-setup.exe` from
   [Releases](https://github.com/NemoKing1210/NetWatcher/releases) and run it.
2. Accept the UAC prompt: the installer is per-machine and puts the app in
   `Program Files`, with a Start Menu shortcut and an uninstaller.
3. Launch NetWatcher from the Start Menu — ETW requires administrator rights.

The binaries are unsigned, so Windows SmartScreen may warn on first launch
("More info" → "Run anyway"). Prefer building from source if you do not want
to run an unsigned binary.

Nothing to install? Download `NetWatcher-v*-win-x64.zip` instead, extract it
anywhere, and run `NetWatcher.exe`. Uninstalling keeps your settings in
`%LOCALAPPDATA%\NetWatcher`.

## Usage

1. Launch NetWatcher as administrator.
2. Pick a process in the select next to the Connections filter to focus on its
   traffic; **All processes** clears the focus.
3. Read the table: time, process, domain, IP, port, protocol, state, source,
   last activity.
4. Filter by domain, IP, or process name; pause to freeze the view; clear to
   discard session history.
5. Open **DNS events** for the answers themselves: newest first, capped at the
   2000 most recent, with its own filter.
6. Use **Export** to save the current view as CSV, Markdown, or JSON.
7. Open **Settings** to choose the theme (follow Windows, light, or dark) or
   the interface language. The theme applies immediately; a language change
   reloads the shell with the same live data.

The status chip shows whether you are elevated, whether ETW is running, and the
current connection/DNS counts.

## How it works

NetWatcher combines three passive sources:

- **ETW `Microsoft-Windows-Kernel-Network`** — TCP connect/accept and UDP
  send/receive events, with PID, addresses, and ports.
- **ETW `Microsoft-Windows-DNS-Client`** — query names and answer addresses,
  attributed to the requesting PID.
- **Windows TCP table** — a 2-second `GetExtendedTcpTable` snapshot that
  supplies connection state and acts as a fallback when ETW is unavailable.

A correlator binds a remote IP to a domain from the same process within
−15 min … +2 s of the connection, then falls back to a global ±2 min window
(marked `~`), then to reverse DNS. Details: [docs/architecture.md](docs/architecture.md).

## Limitations

DoH/DoT and hardcoded IPs produce an IP without a domain. Shared CDN addresses
are marked `~`. Proxy and VPN traffic is attributed to the proxy process.
Inbound connections are hidden in v1. See [docs/limitations.md](docs/limitations.md).

## Building from source

Requires the .NET 10 SDK and Windows.

```powershell
dotnet restore NetWatcher.slnx
dotnet build NetWatcher.slnx -c Release
dotnet test NetWatcher.slnx -c Release --filter "Category!=RequiresAdmin"
```

Run (elevated terminal):

```powershell
dotnet run --project src/NetWatcher
```

Self-contained publish:

```powershell
dotnet publish src/NetWatcher -c Release -r win-x64 --self-contained true
```

Calibration mode (elevated), when ETW field mappings need checking on your
Windows build:

```powershell
.\NetWatcher.exe --probe
```

## Roadmap

- Blocking/throttling rules (opt-in).
- TLS SNI domains for traffic that bypasses system DNS.
- Persistent history (SQLite).
- Showing inbound connections (toggle).
- Npcap-based capture for exotic protocols.

## Contributing

Contributions are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md) and the
[Code of Conduct](CODE_OF_CONDUCT.md) first. Security issues: see
[SECURITY.md](SECURITY.md) — please do not open a public issue.

## Privacy

No telemetry, no network requests of its own, no data leaves the machine.
Settings and logs live under `%LOCALAPPDATA%\NetWatcher\`.

## License

[MIT](LICENSE) © 2026 NemoKing1210
