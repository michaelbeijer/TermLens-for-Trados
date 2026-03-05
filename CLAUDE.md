# Supervertaler for Trados – Claude Context

## What this project is
Supervertaler for Trados is a Trados Studio 2024 (v18) plugin that brings key Supervertaler features into the Trados ecosystem. Currently it provides the **TermLens** glossary panel (live inline terminology display); AI features (batch translation, prompt library, chat assistant) are planned.

- **Language**: C# / .NET Framework 4.8, SDK-style .csproj
- **Namespace**: `Supervertaler.Trados` (sub-namespaces: `.Controls`, `.Core`, `.Models`, `.Settings`)
- **Build**: `bash build.sh` from repo root (dotnet build → package_plugin.py → deploy)
- **Deploy target**: `%LOCALAPPDATA%\Trados\Trados Studio\18\Plugins\Packages\Supervertaler.Trados.sdlplugin`
- **Strong-name key**: `src/Supervertaler.Trados/Supervertaler.Trados.snk` — PublicKeyToken: `6afde1272ae2306a`
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
| `src/Supervertaler.Trados/TermLensEditorViewPart.cs` | Main ViewPart controller — Initialize(), segment events, settings wiring |
| `src/Supervertaler.Trados/AppInitializer.cs` | Runs at Trados startup; pre-loads `e_sqlite3.dll`, registers `AssemblyResolve` for our DLLs |
| `src/Supervertaler.Trados/Core/TermbaseReader.cs` | SQLite reader — Open(), SearchTerm(), LoadAllTerms(), GetTargetSynonyms() |
| `src/Supervertaler.Trados/Core/TermMatcher.cs` | In-memory term matching against source segment tokens |
| `src/Supervertaler.Trados/Controls/TermLensControl.cs` | WinForms panel — term blocks, gear button, SettingsRequested event |
| `src/Supervertaler.Trados/Controls/TermBlock.cs` | Individual term chip rendered in the panel |
| `src/Supervertaler.Trados/Settings/TermLensSettings.cs` | JSON settings persisted to `%LocalAppData%\Supervertaler.Trados\settings.json` |
| `src/Supervertaler.Trados/Settings/TermLensSettingsForm.cs` | Settings dialog — file picker, termbase info label (shows LastError) |
| `src/Supervertaler.Trados/Supervertaler.Trados.plugin.xml` | Extension manifest (UTF-16 LE — write via Python to preserve encoding) |
| `build.sh` | Build → package → deploy script; aborts if Trados is running |
| `package_plugin.py` | Creates OPC-format `.sdlplugin` (NOT plain ZIP — needs `[Content_Types].xml`, `_rels/`) |

---

## Build / deploy rules

- **Trados must be fully closed** before running `bash build.sh` — it locks plugin files and skips re-extraction if `Unpacked/Supervertaler.Trados/` is non-empty. `build.sh` detects this via `tasklist.exe` and aborts.
- `build.sh` wipes `%LOCALAPPDATA%\Trados\...\Plugins\Unpacked\Supervertaler.Trados\` before deploying so Trados re-extracts cleanly on next start.
- `.sdlplugin` is OPC (Open Packaging Convention), like `.docx`. Requires `[Content_Types].xml` and `_rels/` entries — plain ZIP will silently fail to load.

---

## Naming conventions

- **Plugin name**: "Supervertaler for Trados" (visible in Trados plugin manager)
- **Glossary panel name**: "TermLens" (visible in Trados View menu — kept as the feature name)
- **Action IDs**: Prefixed with `TermLens_` for glossary-related actions (e.g. `TermLens_AddTerm`)
- **Class names**: TermLens-prefixed classes (`TermLensEditorViewPart`, `TermLensControl`, etc.) are the glossary feature; future AI classes will use different naming
- **Settings auto-migrate** from old `%LocalAppData%\TermLens\` to `%LocalAppData%\Supervertaler.Trados\` on first run

---

## SQLite / WAL notes

- `supervertaler.db` uses WAL mode (Write-Ahead Log). Leftover `.db-wal` / `.db-shm` files after non-clean Supervertaler shutdown are harmless — SQLite replays the WAL on next open.
- Connection string uses `SqliteConnectionStringBuilder` with `Mode = SqliteOpenMode.ReadOnly` — safe for concurrent access while Supervertaler has the DB open.

---

## Planned features

- **AI batch translation** — translate segments using LLM providers (OpenAI, Anthropic, Google)
- **Prompt manager / library** — manage system and custom prompts for AI translation
- **AI chat assistant** — project-aware chat interface docked in Trados
- **TBX support** — to be added simultaneously in both Supervertaler and this plugin
