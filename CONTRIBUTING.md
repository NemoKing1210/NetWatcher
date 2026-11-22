# Contributing to NetWatcher

Thanks for your interest. This project is open source under the MIT license,
and contributions of all sizes are welcome.

## Ground rules

- Everything in this repository is written in **English**: code, identifiers,
  comments, commit messages, issues, pull requests, and documentation.
- Be respectful. By participating you agree to the
  [Code of Conduct](CODE_OF_CONDUCT.md).

## Before you start

- For anything larger than a small fix, open an issue first so the change can
  be discussed.
- Security problems must not be reported in public issues — see
  [SECURITY.md](SECURITY.md).

## Development setup

Requirements: Windows 10 22H2+ / Windows 11 x64 and the .NET 10 SDK.

```powershell
dotnet restore NetWatcher.slnx
dotnet build NetWatcher.slnx -c Release
dotnet test NetWatcher.slnx -c Release --filter "Category!=RequiresAdmin"
```

Some tests need administrator rights and are tagged
`[Trait("Category", "RequiresAdmin")]`. Run them from an elevated terminal:

```powershell
dotnet test NetWatcher.slnx -c Release
```

The app itself must run as administrator (ETW). `dotnet run --project src/NetWatcher`
from an elevated terminal.

## Coding style

- Follow `.editorconfig`; `dotnet format` must report no changes.
- Nullable reference types are enabled; the build treats warnings as errors.
- Write tests first (red-green) for behavior changes. Tests use xUnit.
- Public APIs get XML documentation.

## Commits and pull requests

- Use [Conventional Commits](https://www.conventionalcommits.org/):
  `feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`, `ci:`.
- Keep pull requests focused; one logical change per PR.
- Describe what changed and why, and reference the related issue.
- CI must be green: build, tests, and `dotnet format --verify-no-changes`.
