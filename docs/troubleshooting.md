# Troubleshooting

## The app does not start

NetWatcher requires administrator rights (ETW). Launch it from an elevated
terminal or accept the UAC prompt. If the process starts and exits
immediately, read the log in `%LOCALAPPDATA%\NetWatcher\logs\`.

## "ETW is unavailable" banner

The ETW session could not start: usually the provider is blocked by policy, or
the process is not elevated (the manifest requests elevation). The app keeps
working in table-polling mode: TCP connections are still listed, but
short-lived connections and UDP flows may be missed. Run as administrator for
full capture.

## No domains, only IPs

- The traffic may bypass the system resolver (DoH/DoT, hardcoded IPs). This is
  expected; see [limitations](limitations.md).
- DNS events may use a different schema on your Windows build. Run the
  calibration probe as administrator and check the log:

  ```powershell
  .\NetWatcher.exe --probe
  ```

  Generate test traffic in another window (`curl https://example.com`,
  `nslookup example.com`). The log records provider, EventID, EventName, and
  every field for 60 seconds.

## Empty table

- Wait a couple of seconds: the table polls every 2 s and the UI refreshes at 1 Hz.
- Check that the process has network activity.
- Verify the filter box and the selected process; press "Clear" only if you
  want to discard history.

## Windows SmartScreen warning

The release executable is unsigned. SmartScreen may warn on first launch;
choose "More info" → "Run anyway" if you trust the source. You can also build
from source.

## High CPU or memory

Expected targets are CPU below 5% idle / 15% under load and memory below
200 MB. A large number of long-lived connections can raise memory up to the
100,000-record cap. If numbers stay high, collect a log and open an issue.

## Where are the logs?

`%LOCALAPPDATA%\NetWatcher\logs\netwatcher-yyyyMMdd.log`

## Where are the settings?

`%LOCALAPPDATA%\NetWatcher\settings.json` — window geometry, theme, and
interface language. Delete the file to return to system theme/language and the
default window size.
