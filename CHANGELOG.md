# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Fluent (WPF-UI) shell: title bar, a permanent navigation pane with the
  process list, and Connections/DNS events pages that keep their state.
- DNS events page: newest answers first, capped at the 2000 most recent, with
  its own filter and an empty-state hint.
- Persisted light/dark theme with a Mica backdrop (solid window on systems
  without Mica support) and live tracking while the choice is "system".
- Live TCP/UDP connection monitoring with process attribution (ETW
  Kernel-Network plus a 2 s TCP table fallback).
- DNS correlation through the Windows DNS Client provider, with shared/CDN
  markers and asynchronous PTR fallback.
- Session history with first/last seen timestamps, bounded in-memory store.
- Process list, text filters, pause, clear, and CSV export.
- Window geometry persistence under `%LOCALAPPDATA%\NetWatcher\`.
- Calibration mode (`--probe`) for verifying ETW field mappings.
- Open-source repository files: MIT license, README, contributing guide, code
  of conduct, security policy, issue/PR templates, and CI.

### Changed

- Pause is session-wide: freezing either page freezes both.
- Controls now use the Fluent WPF-UI set: buttons, filter boxes, the theme
  switch, the process list, the tables, and their scroll bars.
- Tables size their columns to the window and no longer clip or open a
  horizontal scroll bar at the minimum width.
- Status bar wording is explicit (`seen` / `shown`), and empty tables explain
  whether there is no traffic or no match for the active filter.
- The new-row cue is a short success-coloured time cell instead of an opaque
  green row fill, and it clears when the row ages.
- The connection context menu and the details window are Fluent surfaces; a
  right click selects the row it acts on.
- The window carries the project icon.

### Fixed

- DNS answers are dispatched by payload shape instead of a hard-coded event-ID
  list, so they appear on Windows builds whose DNS-Client provider uses newer
  event IDs.
