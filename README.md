# NetWatcher

[![CI](https://github.com/NemoKing1210/NetWatcher/actions/workflows/ci.yml/badge.svg)](https://github.com/NemoKing1210/NetWatcher/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078D6)](#requirements)

NetWatcher shows, in real time, which domains and IP addresses a specific
application or game on your Windows machine connects to.

> **Status:** in development. The v1 implementation plan lives in
> `docs/superpowers/plans/` (local working notes, not published).

## Features

- Live TCP and UDP connections with process attribution.
- Domains from the Windows DNS Client, with `~` markers for shared/CDN IPs.
- Session history with first/last seen timestamps.
- Filters, pause, process list, and CSV export.

## Requirements

- Windows 10 22H2 or Windows 11, x64.
- Administrator rights (ETW and the full connection table).
- .NET 10 runtime for framework-dependent builds; the release build is
  self-contained.

## Limitations

NetWatcher is a passive observer: it never blocks or modifies traffic and
never decrypts TLS. Traffic that bypasses the Windows DNS resolver (DoH/DoT,
hardcoded IPs) shows an IP without a domain. See `docs/limitations.md` for the
full list.

## Building from source

```powershell
dotnet build NetWatcher.slnx -c Release
dotnet test NetWatcher.slnx -c Release --filter "Category!=RequiresAdmin"
```

Run the app **as administrator**:

```powershell
dotnet run --project src/NetWatcher
```

## Usage

1. Launch NetWatcher as administrator.
2. Pick a process in the top bar (or keep "All processes").
3. Watch connections appear in the table; filter by process or text.
4. Export the current view to CSV.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) and the
[Code of Conduct](CODE_OF_CONDUCT.md). Security issues: [SECURITY.md](SECURITY.md).

## License

[MIT](LICENSE) © 2026 NemoKing1210
