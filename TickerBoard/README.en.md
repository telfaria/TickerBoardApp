# TickerBoard (English)

TickerBoard is a stock ticker bar built with WPF (.NET 10). It stays at the top of the screen and scrolls quote items horizontally.

## Features

- Yahoo Finance-based quote retrieval (no API key required)
- Add/Edit/Remove symbols from the settings window
- Symbol name resolution on save (JP-first lookup)
- Configurable scroll speed, font, bar height, opacity, and always-on-top
- Multi-display / high-DPI aware window placement

## Run

1. Open `TickerBoard.slnx` in Visual Studio
2. Set `TickerBoard` as the startup project
3. Run (F5)

## Settings file

Created on first launch:

- `%LOCALAPPDATA%\\TickerBoard\\settings.json`

Main settings fields include:

- `symbols`
- `refreshIntervalSeconds`
- `height`
- `opacityPercent`
- `alwaysOnTop`
- `fontFamily`
- `fontSize`
- `scrollSpeed`
- `initialOffsetPercent`

## Quick operations

- Gear icon: open settings menu
- Double-click the bar: open settings window

## Notes

- You can tune display behavior and initial positioning via `settings.json`.
- Rendering behavior may vary depending on monitor layout and DPI settings.
