# Changelog

All notable changes to this package are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2026-07-23

### Added
- Per-page background rendering: each screen carries its own background sprite
  (menu opaque, gameplay transparent), replacing the single global background
  layer.
- Code-driven tween animations (`UiTween`) and cascade reveal (`UiCascade`) for
  page and widget transitions, alongside the existing USS-preset animations.
- Button press presets (`scale` / `sink` / `pop` / `none`) selectable in the
  config.
- Fake-loading flow on the start page: progress bar fills over 1–3 s, then
  routes to `mainmenu`.
- Win/lose flow wiring through `UiKit.Flow`.
- `Samples~/GeneratedExample`: a full generated-interface reference snapshot
  (generated views, `UiIds`, config, Neoxider adapter, override USS).
- Editor window rebuilt in UI Toolkit: tabs, colored buttons, a default-config
  button.

### Changed
- Pages hide via `display:none` and stay bound between shows, so re-showing a
  page re-binds cleanly.
- `BarView` fill width is clamped to the actual track width.

### Fixed
- White flash during page transitions.
- Blank page on re-show.
- Button click rebinding surviving a re-show.
- Editor window layout on narrow widths (removed horizontal scrolling).

## [0.1.0] - 2026-07-16

### Added
- Initial release: page router with Push/Pop history and Back/Escape, popups,
  typed generated views, global counters, USS button/press animations, click
  sound, and pluggable game adapters (`UiKit.Flow`).
