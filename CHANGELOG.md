# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

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
