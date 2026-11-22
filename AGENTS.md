# Repository Guidelines

## Project Overview

NetWatcher is a Windows-only WPF (.NET 10) desktop app that passively shows which
domains and IPs a process connects to. It combines ETW `Microsoft-Windows-Kernel-Network`
(TCP/UDP), ETW `Microsoft-Windows-DNS-Client` (answers), and a 2 s
`GetExtendedTcpTable` snapshot. It never blocks, injects into, or decrypts traffic.
Session data is in memory only; the sole outbound activity is PTR lookup for
public IPs. The UI is WPF-UI Fluent, localized into eight cultures.

## Architecture & Data Flow

```
Kernel-Network ETW ─┐
DNS-Client ETW ─────┼─► Aggregator (250 ms) ─► NetworkStore ─► DomainCorrelator ─► MainViewModel ─► UI (1 Hz)
TCP table (2 s) ────┘                              ▲
PTR (public IPs, async) ───────────────────────────┘
```

- **Composition root is manual**: `App.OnStartup` constructs every service and
  holds singletons in fields. No DI container. `NetworkStore`, `ProcessResolver`,
  and `PauseState` are shared by both pages.
- **ETW ingress**: `EtwSessionService` raises `Action<T>` events on the
  `NetWatcher.ETW` thread. Handlers may only enqueue; `Aggregator` owns two
  bounded `Channel<T>`s (50,000, `DropOldest`) drained every 250 ms into
  `NetworkStore`. Never block that thread.
- **Table fallback**: a `System.Threading.Timer` polls the TCP table every 2 s and
  writes straight to `NetworkStore`, bypassing the aggregator. It runs even when
  ETW works and supplies connection state; there is no UDP table fallback.
- **UI refresh is polling**: `MainWindow` runs a 1 s `DispatcherTimer` calling
  `MainViewModel.Tick()` and `DnsPageViewModel.Tick()`. View models snapshot the
  store, correlate, filter, and reconcile existing `ObservableCollection` instances
  in place so sort/selection/scroll survive. There is no store-to-UI event stream.
- **Domain resolution order**: `DomainCorrelator` uses PID-scoped DNS from
  `FirstSeenUtc −15 min … +2 s` (`DomainOrigin.Dns`), then global ±2 min marked
  shared/CDN (`~`), then `MainViewModel` asks PTR (public IPs), else `—`.
- **Inbound connections** are collected but skipped by `MainViewModel.Tick` and
  all exporters.
- **Language switch rebuilds the window**: `App.RebuildWindowForLanguage` creates a
  new `MainWindow` with the same live view models/services after cultures change.
  Collectors keep running.

Invariants to preserve:

- Connection identity = PID + process start + protocol + remote IP/port + local
  port + inbound. Upsert keeps first-seen and updates last-seen/state. Despite
  `docs/architecture.md`, there is **no 5 s connection dedup**.
- Caps: 100,000 connections and 100,000 DNS records, evicted by last seen in each
  flush; DNS page shows the newest 2,000; DNS merge window is 30 s; PTR is 4
  concurrent, 2 s timeout, 24 h resolver cache.
- All collection mutation and view-model reads happen on the dispatcher. Use
  `Dispatcher.InvokeAsync` when crossing from timers/events, as `App` does.

## Key Directories

| Path | Purpose |
|---|---|
| `src/NetWatcher/Services/Etw/` | ETW session, event dispatch and payload parsing; `--probe` diagnostics |
| `src/NetWatcher/Services/Connections/` | `GetExtendedTcpTable` reader and PID→process resolution |
| `src/NetWatcher/Services/Storage/` | `Aggregator`: bounded queues and periodic flush |
| `src/NetWatcher/Storage/` | `NetworkStore`: locked, bounded, deduplicating session store |
| `src/NetWatcher/Services/Correlation/` | IP↔domain matching windows |
| `src/NetWatcher/Services/ReverseDns/` | PTR resolver with time provider, cache, and concurrency cap |
| `src/NetWatcher/ViewModels/` | UI state, filtering, row/process caches, exports |
| `src/NetWatcher/Views/`, `MainWindow.xaml*` | Cached pages and Fluent shell navigation |
| `src/NetWatcher/Localization/` | `Strings` facade plus neutral and seven `.resx` catalogs |
| `src/NetWatcher/Services/Theming/`, `Models/` | Theme preference and immutable DTOs/enums |
| `src/NetWatcher/Utils/` | Settings JSON, IP helpers, P/Invoke declarations |
| `tests/NetWatcher.Tests/` | xUnit suite; one test class per production area |

## Development Commands

Run from the repository root on Windows with the .NET 10 SDK:

```powershell
dotnet restore NetWatcher.slnx
dotnet build NetWatcher.slnx -c Release
dotnet test NetWatcher.slnx -c Release --filter "Category!=RequiresAdmin"
dotnet format NetWatcher.slnx --verify-no-changes
```

CI order is restore → build (`--no-restore`) → test (`--no-build`) → format check,
so a bare `dotnet test --no-build` requires a prior Release build.

```powershell
# Elevated terminal; includes the live TCP-table test
dotnet test NetWatcher.slnx -c Release

# One class / one test
dotnet test tests/NetWatcher.Tests/NetWatcher.Tests.csproj -c Release --filter "FullyQualifiedName~NetWatcher.Tests.MainViewModelTests"
dotnet test tests/NetWatcher.Tests/NetWatcher.Tests.csproj -c Release --filter "FullyQualifiedName=NetWatcher.Tests.MainViewModelTests.Tick_PopulatesRowsFromStoreSnapshot"

# Run (manifest forces UAC) and self-contained publish
dotnet run --project src/NetWatcher
dotnet publish src/NetWatcher -c Release -r win-x64 --self-contained true

# ETW calibration: 60 s of provider/event/field dumps in the daily log
.\NetWatcher.exe --probe

# Coverage (coverlet collector is referenced; no report tooling configured)
dotnet test NetWatcher.slnx -c Release --collect:"XPlat Code Coverage"
```

There are no build scripts or publish profiles. `.slnx` is the
only solution format; add projects by editing its XML. Packages are pinned per
project (no central management or lock files).

## Code Conventions & Common Patterns

- File-scoped namespaces, `sealed` concrete types, `_camelCase` fields, PascalCase
  members. Nullable is enabled and `TreatWarningsAsErrors=true` in the app project,
  so new warnings fail the Release build. Tests do not inherit warnings-as-errors.
- Immutable `record`s for DTOs (`ConnectionEvent`, `ConnectionRecord`, `DnsRecord`,
  `ProcessKey`, `DomainBinding`); mutable classes only for view models. Small
  services use primary constructors (`DomainCorrelator`, `ConnectionTableReader`).
- **No MVVM framework and no commands**: view models implement
  `INotifyPropertyChanged` manually; XAML uses `{Binding}` and `{x:Static}`; UI
  actions are code-behind handlers calling view-model methods/properties. Do not
  introduce CommunityToolkit/ReactiveUI or `ICommand` patterns.
- Filtering and sorting live in view models; WPF's `DataGrid` performs column
  sorting. Do not replace `Rows` wholesale or wrap it in a new `ICollectionView`;
  use the existing in-place reconciliation (`SyncRows`, row `Update`).
- Row/process objects are cached by stable string keys (`KeyOf`) and mutated only
  when a displayed value changes. Cache keys include process start time to survive
  PID reuse.
- Async/threading: ETW handlers enqueue only; services use `ConfigureAwait(false)`;
  PTR work is fire-and-forget `Task.Run` with `ConcurrentDictionary` dedup; UI
  updates marshal to the dispatcher.
- Error handling is boundary-oriented and deliberate: ETW failures fall back to
  table polling and surface `EtwAvailable`/banner; parse/poll failures increment
  `ErrorCount`; export returns `false`; `ProcessResolver` falls back to `pid:<n>`;
  `SettingsStore.Load` returns defaults. Preserve the non-fatal behavior unless a
  task explicitly asks to change it.
- Parser logic must stay testable without ETW: keep pure `internal static` helpers
  (e.g. `KernelPortToHost`, `ParseTcpRow`, `DnsEventDispatch.Classify`) and extend
  those rather than starting sessions in tests.
- **Localization**: add one property to `Localization/Strings.cs`, then the same
  key to `Strings.resx` **and all seven** satellite files (`de`, `es`, `fr`, `ja`,
  `pt-BR`, `ru`, `zh-Hans`). Reference it with `{x:Static loc:Strings.Name}`.
  Invariant tests enforce key/property parity.
- **Theming**: use `{DynamicResource ...Brush}` from WPF-UI and the styles in
  `Styles/FluentControls.xaml`. Do not add `<SolidColorBrush>` resources; a test
  rejects them and requires the documented theme brush keys.
- Naming: test methods are `Method_Behavior`; commits follow Conventional Commits
  (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`, `ci:`). All code,
  comments, docs, and commit text are English.
- Write tests first for behavior changes. Update `docs/architecture.md` when
  timing, caps, or data flow changes, and `CHANGELOG.md` under `[Unreleased]` for
  user-visible behavior.
- Versioning is SemVer. `<Version>` in `src/NetWatcher/NetWatcher.csproj` is the
  development baseline used by local builds; a release tag overrides it. Pre-1.0
  releases use `0.y.z`; move `[Unreleased]` in `CHANGELOG.md` to
  `[X.Y.Z] - YYYY-MM-DD` and push tag `vX.Y.Z` at release time. The Release
  workflow stamps that version into the executable, the installer, and the
  release name; do not bump `<Version>` for releases.

## Important Files

- `src/NetWatcher/App.xaml.cs` — composition root, ETW/table fallback, language rebuild, shutdown.
- `src/NetWatcher/MainWindow.xaml.cs` — shell, cached pages, 1 s UI timer, window persistence.
- `src/NetWatcher/ViewModels/MainViewModel.cs` — 1 Hz snapshot, correlation/PTR, filters, exports.
- `src/NetWatcher/ViewModels/DnsPageViewModel.cs` — newest-first 2,000-row DNS view.
- `src/NetWatcher/Storage/NetworkStore.cs` — thread-safe session store and DNS windows.
- `src/NetWatcher/Services/Etw/EtwSessionService.cs` — session lifecycle, restart-once policy, parsers.
- `src/NetWatcher/Services/Storage/Aggregator.cs` — channels, caps, flush loop.
- `src/NetWatcher/Services/Connections/ConnectionTableReader.cs` — native TCP table, IPv4/IPv6.
- `src/NetWatcher/Services/Correlation/DomainCorrelator.cs` — the domain-binding algorithm.
- `src/NetWatcher/Utils/SettingsStore.cs` — `%LOCALAPPDATA%\NetWatcher\settings.json` schema and legacy migration.
- `src/NetWatcher/Localization/Strings.cs` / `Strings*.resx` — UI text.
- `src/NetWatcher/NetWatcher.csproj`, `tests/NetWatcher.Tests/NetWatcher.Tests.csproj` — TFM, packages, warnings, `InternalsVisibleTo`.
- `src/NetWatcher/app.manifest` — `requireAdministrator`, Windows 10/11 only.
- `.github/workflows/ci.yml` — the authoritative build/test/format gate.
- `.github/workflows/release.yml`, `installer/NetWatcher.iss` — tag-driven release:
  publish, installer, portable zip, checksums, optional signing.

## Runtime/Tooling Preferences

- Required: Windows 10 22H2+ / Windows 11 x64, .NET 10 SDK, WPF. The project targets
  `net10.0-windows` as AnyCPU with no RID; Linux/macOS builds fail, and CI uses
  `windows-latest` with .NET `10.0.x`.
- The manifest requests administrator, so a normal launch elevates before managed
  code. Documented "runs without admin in table-polling mode" is therefore only
  reachable when an elevated process cannot start ETW (e.g. policy); do not assume
  an unelevated runtime path exists.
- Persistent state is confined to `%LOCALAPPDATA%\NetWatcher\` (`settings.json`,
  daily `logs\netwatcher-yyyyMMdd.log`, no log rotation). Session history is never
  persisted. Keep the no-telemetry/privacy constraints; PTR through
  `ReverseDnsResolver` is the only sanctioned network I/O.
- `PLAN.md`, `docs/design/`, `docs/superpowers/`, `.superpowers/`, and `artifacts/`
  are gitignored local working material. Never treat them as committed truth or
  requirements; write durable docs under `docs/` and `README.md`.
- Build artifacts stay out of git (`bin/`, `obj/`, `artifacts/`). Release
  artifacts (installer, zip, checksums) are produced by the Release workflow
  from a `v*` tag, never committed.

## Testing & QA

- xUnit 2.9.3 only: no mocking or assertion libraries. Use real in-memory stores,
  delegate fakes (`Func<...>`), and nested fakes such as `FakeTimeProvider`.
- Structure: one `public sealed class <Area>Tests` per file in namespace
  `NetWatcher.Tests`, static `Create`/factory helpers, fixed UTC timestamps.
  `[Fact]` for single cases, `[Theory]`/`[InlineData]` for tables.
- Isolation: pass GUID temp directories into `SettingsStore`/`FileLogger` and clean
  them in `finally`; never write to the real `%LOCALAPPDATA%`. Tests needing a live
  TCP table are tagged `[Trait("Category", "RequiresAdmin")]` (currently only
  `ConnectionTableReaderTests.ReadTcpSnapshot_FindsOwnConnection`) and CI excludes
  them. Do not add that tag to pure parser tests.
- UI "tests" are headless file checks: `FluentStyleTests` scans
  `src/NetWatcher/**/*.xaml`; `LocalizationTests` parses `.resx` files and loads
  satellite assemblies. Both locate the repo by walking up to `NetWatcher.slnx`, so
  run them from a normal repo checkout/build tree. Do not build `Application`/
  `Dispatcher`/page instances in tests.
- Not covered: live ETW sessions, `--probe`, WPF navigation, `ProcessIconProvider`,
  `App` composition, real reverse DNS. Prefer extracting a pure helper and testing
  that.
- Watch two latent issues: `LanguageController` mutates process-wide culture
  defaults without restoration (classes run in parallel, so avoid ambient-culture
  assertions), and `MainViewModel.Tick` launches untracked PTR tasks (do not assert
  on PTR results without synchronization).
- CI must be green: build, non-admin tests, and `dotnet format --verify-no-changes`.
  No coverage threshold is enforced.
