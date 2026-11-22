# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 1.x     | Yes       |
| < 1.0   | Pre-release, no support |

## Reporting a Vulnerability

Please do **not** open a public issue for security problems.

Report vulnerabilities privately through
[GitHub Security Advisories](https://github.com/NemoKing1210/NetWatcher/security/advisories/new)
for this repository. Include:

- the affected version and Windows build;
- steps to reproduce;
- what an attacker could achieve;
- any suggested fix.

You can expect an acknowledgement within 7 days and a status update within
30 days. Please give the maintainer a reasonable window to release a fix
before any public disclosure.

## Scope

NetWatcher is a local, passive monitoring tool. It runs with administrator
rights and reads network metadata; it makes no network requests of its own and
collects no telemetry. Reports about privilege handling, log or CSV content
leakage, and unsafe parsing of untrusted network metadata are in scope.

The maintainer's contact: [NemoKing1210](https://github.com/NemoKing1210).
