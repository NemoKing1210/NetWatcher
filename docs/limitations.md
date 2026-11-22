# Limitations

NetWatcher is a passive, local observer. These limits are deliberate for v1.

| Limitation | Cause | What you see |
|---|---|---|
| DoH/DoT and IPs hardcoded in the app | DNS bypasses the Windows DNS Client | IP only; domain shows "—" |
| Shared CDN addresses | one IP serves many domains | domain prefixed with `~` (shared/CDN) |
| Proxy/VPN | the proxy process owns the sockets | connection attributed to the proxy process |
| hosts file / DNS cache | no network query is made | the domain may not appear |
| Short-lived connections | they can fall between 2 s table polls | ETW catches most; polling is only a fallback |
| Inbound connections | hidden by default in v1 | not shown in the table or CSV |
| Anti-cheat-protected games | anti-cheats may react to tracing | v1 uses passive ETW only — no drivers, no injection, no packet capture |
| No administrator rights | the manifest requests elevation, so the process cannot start unelevated | the app is unavailable; run it elevated |
| ETW blocked by policy | the ETW session cannot start even when elevated | TCP-table polling with a warning banner |
| Session-only history | v1 keeps data in memory | clearing the app or exiting forgets history |
| PTR for private addresses | reverse DNS is meaningless there | no PTR host; the IP is shown |

## Not in v1

- Blocking or throttling traffic.
- HTTPS decryption or request content inspection.
- Persistent history (SQLite).
- Npcap/driver packet capture.
- TLS SNI domains for traffic that bypasses system DNS.

These are candidates for phase 2.

## Privacy

NetWatcher collects no telemetry and sends nothing anywhere except the PTR
reverse-DNS lookup for public IP addresses, which uses the Windows resolver.
All data stays in memory; logs, settings, and export files stay on the local
machine.
