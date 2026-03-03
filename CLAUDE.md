# TermLens – Claude Context

## What this project is
TermLens is a Trados Studio 2024 (v18) editor ViewPart plugin that displays terminology matches inline in the source segment — essentially a live glossary lookup panel docked below the editor. It reads Supervertaler's own SQLite termbase format (`supervertaler.db`) so both tools share the same glossaries.

- **Language**: C# / .NET Framework 4.8, SDK-style .csproj
- **Build**: `bash build.sh` from repo root (dotnet build → package_plugin.py → deploy)
- **Deploy target**: `%LOCALAPPDATA%\Trados\Trados Studio\18\Plugins\Packages\TermLens.sdlplugin`
- **Strong-name key**: `src/TermLens/TermLens.snk` — PublicKeyToken: `6afde1272ae2306a`
  (Trados's `DefaultPluginTypeLoader` refuses unsigned assemblies — this is non-negotiable)

---

## SQLite library: Microsoft.Data.Sqlite (not System.Data.SQLite)

We use **`Microsoft.Data.Sqlite`** + SQLitePCLRaw (native DLL: `e_sqlite3.dll`).

**Do NOT switch to `System.Data.SQLite`** — it uses `SQLite.Interop.dll` with a version-fingerprint
hash scheme (`SI04b638e115f7beb4` etc.) that causes `EntryPointNotFoundException` inside Trados
Studio's plugin environment. The root cause: other apps (memoQ, Glossary Converter) ship their
own `SQLite.Interop.dll` with different hashes, and Windows's DLL loader picks the wrong one.
Microsoft.Data.Sqlite uses standard SQLite3 C entry points — no version-hash conflicts.

`AppInitializer.cs` pre-loads `e_sqlite3.dll` by full path and handles `AssemblyResolve` for all
managed DLLs we ship (Microsoft.Data.Sqlite, SQLitePCLRaw, System.Memory, etc.) because Trados
ships older versions of several .NET Standard polyfills.

---

## Key files

| File | Purpose |
|------|---------|
| `src/TermLens/TermLensEditorViewPart.cs` | Main ViewPart controller — Initialize(), segment events, settings wiring |
| `src/TermLens/AppInitializer.cs` | Runs at Trados startup; pre-loads `e_sqlite3.dll`, registers `AssemblyResolve` for our DLLs |
| `src/TermLens/Core/TermbaseReader.cs` | SQLite reader — Open(), SearchTerm(), LoadAllTerms(), GetTargetSynonyms() |
| `src/TermLens/Core/TermMatcher.cs` | In-memory term matching against source segment tokens |
| `src/TermLens/Controls/TermLensControl.cs` | WinForms panel — term blocks, gear button, SettingsRequested event |
| `src/TermLens/Controls/TermBlock.cs` | Individual term chip rendered in the panel |
| `src/TermLens/Settings/TermLensSettings.cs` | JSON settings persisted to `%LocalAppData%\TermLens\settings.json` |
| `src/TermLens/Settings/TermLensSettingsForm.cs` | Settings dialog — file picker, termbase info label (shows LastError) |
| `src/TermLens/TermLens.plugin.xml` | Extension manifest (UTF-16 LE — write via Python to preserve encoding) |
| `build.sh` | Build → package → deploy script; aborts if Trados is running |
| `package_plugin.py` | Creates OPC-format `.sdlplugin` (NOT plain ZIP — needs `[Content_Types].xml`, `_rels/`) |

---

## Build / deploy rules

- **Trados must be fully closed** before running `bash build.sh` — it locks plugin files and skips re-extraction if `Unpacked/TermLens/` is non-empty. `build.sh` detects this via `tasklist.exe` and aborts.
- `build.sh` wipes `%LOCALAPPDATA%\Trados\...\Plugins\Unpacked\TermLens\` before deploying so Trados re-extracts cleanly on next start.
- `.sdlplugin` is OPC (Open Packaging Convention), like `.docx`. Requires `[Content_Types].xml` and `_rels/` entries — plain ZIP will silently fail to load.

---

## SQLite / WAL notes

- `supervertaler.db` uses WAL mode (Write-Ahead Log). Leftover `.db-wal` / `.db-shm` files after non-clean Supervertaler shutdown are harmless — SQLite replays the WAL on next open.
- Connection string uses `SqliteConnectionStringBuilder` with `Mode = SqliteOpenMode.ReadOnly` — safe for concurrent access while Supervertaler has the DB open.

---

## Planned features (not yet started)

- **Right-click to add term** — context menu in Trados editor grid to add selected source + target text as a new term; needs `[Action]` class, `AddTermDialog`, and write methods in `TermbaseReader`
- **Import from TSV** — must match Supervertaler's exact TSV format (tab-separated, pipe-delimited synonyms, `[!forbidden]` syntax, UUID tracking)
- **Export to TSV** — same format, so files are interchangeable between Supervertaler and TermLens
- **TBX support** — to be added simultaneously in both Supervertaler and TermLens when the time comes
