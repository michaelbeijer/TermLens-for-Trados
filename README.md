# TermLens

A Trados Studio plugin that displays terminology matches inline within the source segment text — the same approach used in [Supervertaler](https://supervertaler.com).

Instead of showing matched terms in a separate list, TermLens renders the full source segment word-by-word with translations displayed directly underneath each matched term. This gives translators immediate, in-context terminology awareness without breaking their reading flow.

## Features

- **Inline terminology display** — source words flow left to right with translations directly underneath matched terms
- **Color-coded by termbase** — project termbases (pink) vs. regular termbases (blue) at a glance
- **Multi-word term support** — correctly handles phrases like "hard acoustic panels" as single units
- **Click to insert** — click any translation to insert it into the target segment
- **Supervertaler-compatible** — reads Supervertaler's SQLite termbase format directly, so you can share termbases between both tools

## Requirements

- Trados Studio 2024 or later
- .NET Framework 4.8

## Building

1. Install the [Trados Studio SDK](https://developers.rws.com/) and Visual Studio 2022
2. Open `TermLens.sln`
3. Restore NuGet packages
4. Build the solution

The output `.sdlplugin` package will be in `src/TermLens/bin/Release/`.

## Installation

Copy the `.sdlplugin` file to:
```
%AppData%\Trados\Trados Studio\18\Plugins\Packages\
```

Or submit to the [RWS AppStore](https://appstore.rws.com/) for distribution.

## License

MIT License — see [LICENSE](LICENSE) for details.

## Author

Michael Beijer — [supervertaler.com](https://supervertaler.com)
