# Tools

Diagnostic and maintenance scripts used during plugin development. These are
**not** part of the plugin itself — they are standalone Python utilities that
operate directly on a Supervertaler `.db` file or a built `.sdlplugin`.

> **Caveat — read before running.** Every script in here that writes to the
> DB is a one-shot maintenance tool, not a user feature. They make destructive
> changes. Always close Trados and Supervertaler Workbench first (so the DB is
> not write-locked), and know that the DB is several hundred MB, so backups are
> large. Each DB-writing script creates a `.bak` file alongside the original
> before committing — that is your rollback path.

## Termbase direction repair

Legacy write-path bugs (pre-v4.19.13) produced a mix of term entries where the
per-entry `source_lang` / `target_lang` metadata and/or the actual `source_term`
/ `target_term` text were flipped relative to the termbase's declared
direction. The v4.19.21 plugin fix makes matching resilient to wrong per-entry
lang tags (it uses termbase-declared direction instead), but entries where the
**text** is reversed still need swapping. These scripts do that cleanup.

### `repair_termbase_directions.py` — stopword heuristic

Scans all entries and classifies each into:

- **A** (tag-only fix) — text is in termbase direction, only lang tags lie.
- **B** (text swap + tags) — text is reversed, swap both.
- **C** (ambiguous) — skipped, leave alone.

Detection uses Dutch/English stopword lists and distinctive character n-grams.
Conservative by design — when in doubt, skip. Fast, free, offline.

**Good for:** a first automated pass on a freshly-broken DB with lots of
long-phrase entries.

**Weakness:** single-word technical terms (`"uitdampen"`, `"uitvinding"`,
chemical names) have no stopwords, so they end up in Category C unfixed.

```
python tools/repair_termbase_directions.py <db_path>           # dry-run
python tools/repair_termbase_directions.py <db_path> --apply   # commit
```

### `ai_repair_termbase_directions.py` — LLM classifier

For everything the heuristic can't handle. Uses Claude Sonnet 4.6 (via the
Anthropic API) to classify each pair's languages as ground truth, then proposes
swaps where the text opposes termbase-declared direction.

- Reads your Claude API key from the plugin's `settings.json` automatically.
- Batches 50 pairs per LLM call.
- Caches every batch response in `%LocalAppData%\Supervertaler.Trados\ai_repair_cache\`
  so reruns and retries don't cost money.
- Runs against **one termbase at a time** (`--termbase NAME`, default `PATENTS`).
- Cost: roughly $0.25–$1 per termbase at this scale — negligible.

```
python tools/ai_repair_termbase_directions.py <db_path>                          # dry-run
python tools/ai_repair_termbase_directions.py <db_path> --termbase PATENTS       # dry-run
python tools/ai_repair_termbase_directions.py <db_path> --apply                  # commit
```

**Good for:** the Category C leftovers from the heuristic pass — short single-
word entries where morphology is the only signal.

**Caveat:** still requires an API key and a working internet connection, and
trusts the model's language calls. Always dry-run first and eyeball a sample of
proposed Category B swaps before applying.

### `fix_reversed_entries.py`

Older, simpler reversal utility kept for reference. Superseded by the two
scripts above.

## Other scripts

- `appstore_release.py` — pipeline helper for submitting a signed build to the
  RWS App Store.
- `read_plugin_version.py` — pulls version info from the built DLL for sanity
  checks.
