"""LLM-powered repair of reversed-direction term entries.

The stopword-based repair (tools/repair_termbase_directions.py) is too
conservative for short single-word entries — "uitdampen", "uitvinding",
"uitlaattemperatuur" all evaded detection because they don't contain common
stopwords. This script uses an LLM to classify each pair's languages as
ground truth, then proposes swaps where the text is opposite to the
termbase's declared direction.

Runs only against ONE termbase at a time (by name or id). Defaults to PATENTS.

Usage (dry-run):
  python ai_repair_termbase_directions.py <db_path> [--termbase PATENTS]

Apply:
  python ai_repair_termbase_directions.py <db_path> --apply

Safety:
  - Creates `.pre_ai_repair.bak` before writing.
  - Never touches entries where tags + text are both in termbase direction
    (classified as "already correct" by the LLM).
  - Writes in a single SQL transaction.
  - LLM responses are cached so reruns don't cost money.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import sqlite3
import sys
import time
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Optional

# ---------------------------------------------------------------------------
# Settings plumbing — read the Claude API key from the plugin's settings.json
# ---------------------------------------------------------------------------

SETTINGS_PATH = Path(os.environ["USERPROFILE"]) / "Supervertaler" / "trados" / "settings" / "settings.json"
CACHE_DIR = Path(os.environ["LOCALAPPDATA"]) / "Supervertaler.Trados" / "ai_repair_cache"


def load_claude_key() -> str:
    with open(SETTINGS_PATH, encoding="utf-8-sig") as f:
        s = json.load(f)
    ai = s.get("aiSettings") or s.get("AiSettings") or {}
    keys = ai.get("apiKeys") or {}
    key = keys.get("claude") or keys.get("anthropic")
    if not key:
        raise RuntimeError(
            f"No Claude API key found in {SETTINGS_PATH}. Check aiSettings.apiKeys.claude."
        )
    return key


# ---------------------------------------------------------------------------
# Claude client — bare-bones HTTP to avoid needing the anthropic SDK installed
# ---------------------------------------------------------------------------

CLAUDE_MODEL = "claude-sonnet-4-6"  # best quality, cost is trivial at this volume
CLAUDE_ENDPOINT = "https://api.anthropic.com/v1/messages"

SYSTEM_PROMPT = """You classify the language of short bilingual term pairs from a Dutch/English translation memory.

For each pair you receive, identify the language of side `a` and side `b` independently. Possible values:
- "nl" — Dutch
- "en" — English
- "ambiguous" — truly language-neutral (pure numbers, chemical formulas like "2-methyl-4-isothiazolin-3-one", a proper noun like "Python", or a single word that is spelled identically in both languages like "code", "tekst", or a loanword with no morphological marker).

Reply ONLY with a JSON array, one object per pair, same order as input:
[{"id": <id>, "a": "nl"|"en"|"ambiguous", "b": "nl"|"en"|"ambiguous"}, ...]

Do NOT add commentary, explanation, or markdown fences. Just the raw JSON array."""


def call_claude(api_key: str, user_content: str, max_tokens: int = 4000) -> str:
    import urllib.request
    import urllib.error

    payload = {
        "model": CLAUDE_MODEL,
        "max_tokens": max_tokens,
        "system": SYSTEM_PROMPT,
        "messages": [{"role": "user", "content": user_content}],
    }
    req = urllib.request.Request(
        CLAUDE_ENDPOINT,
        data=json.dumps(payload).encode("utf-8"),
        headers={
            "Content-Type": "application/json",
            "x-api-key": api_key,
            "anthropic-version": "2023-06-01",
        },
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            body = json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        detail = e.read().decode("utf-8", errors="replace")[:500]
        raise RuntimeError(f"Claude API HTTP {e.code}: {detail}")
    # Messages API returns content as a list of blocks; take the first text block.
    for block in body.get("content", []):
        if block.get("type") == "text":
            return block.get("text", "")
    raise RuntimeError(f"No text block in Claude response: {body}")


# ---------------------------------------------------------------------------
# Batch prompt + cache
# ---------------------------------------------------------------------------


def batch_cache_key(pairs: list[tuple[int, str, str]]) -> str:
    """Stable hash of (id, a, b) tuples — identical input produces identical key."""
    canonical = json.dumps([[i, a, b] for i, a, b in pairs], ensure_ascii=False, sort_keys=False)
    return hashlib.sha256(canonical.encode("utf-8")).hexdigest()


def load_cached_batch(key: str) -> Optional[list[dict]]:
    path = CACHE_DIR / f"{key}.json"
    if not path.exists():
        return None
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return None


def save_cached_batch(key: str, parsed: list[dict]) -> None:
    CACHE_DIR.mkdir(parents=True, exist_ok=True)
    (CACHE_DIR / f"{key}.json").write_text(json.dumps(parsed), encoding="utf-8")


def classify_batch(api_key: str, pairs: list[tuple[int, str, str]]) -> list[dict]:
    """Classify a batch of pairs. Returns list of {id, a, b} dicts."""
    if not pairs:
        return []

    cache_key = batch_cache_key(pairs)
    cached = load_cached_batch(cache_key)
    if cached is not None:
        return cached

    # Build the user content. Keep each pair compact; truncate absurdly long
    # source text to 300 chars since we only need to identify the language.
    def clip(s: str) -> str:
        s = s or ""
        return s if len(s) <= 300 else s[:297] + "..."

    body = json.dumps(
        [{"id": i, "a": clip(a), "b": clip(b)} for i, a, b in pairs],
        ensure_ascii=False,
    )
    user_content = f"Classify each pair:\n{body}"

    raw = call_claude(api_key, user_content)
    raw = raw.strip()
    # Be forgiving about markdown fences the model sometimes emits despite the
    # system prompt asking for bare JSON.
    if raw.startswith("```"):
        raw = raw.split("\n", 1)[1] if "\n" in raw else raw
        if raw.endswith("```"):
            raw = raw[: raw.rfind("```")]
        raw = raw.strip()

    try:
        parsed = json.loads(raw)
    except json.JSONDecodeError as e:
        raise RuntimeError(f"Could not parse LLM response as JSON: {e}\nRaw: {raw[:500]}")

    if not isinstance(parsed, list):
        raise RuntimeError(f"Expected JSON array, got {type(parsed).__name__}: {raw[:300]}")

    save_cached_batch(cache_key, parsed)
    return parsed


# ---------------------------------------------------------------------------
# Planning
# ---------------------------------------------------------------------------


def normalize_lang(lang: Optional[str]) -> Optional[str]:
    if not lang:
        return None
    lower = lang.strip().lower()
    if lower.startswith(("nl", "dutch", "flemish")):
        return "nl"
    if lower.startswith(("en", "english")):
        return "en"
    return lower


@dataclass
class Action:
    entry_id: int
    current_source: str
    current_target: str
    current_src_lang: Optional[str]
    current_tgt_lang: Optional[str]
    category: str  # "A" tag-only | "B" swap text+tags | "C" ambiguous | "S" already correct (skip)
    llm_src: str
    llm_tgt: str
    new_source: str
    new_target: str
    new_src_lang: str
    new_tgt_lang: str


def plan(
    db_path: str,
    termbase_name: str,
    api_key: str,
    batch_size: int,
    verbose: bool,
) -> tuple[list[Action], dict]:
    conn = sqlite3.connect(db_path)
    conn.row_factory = sqlite3.Row
    cur = conn.cursor()

    tb_row = cur.execute(
        "SELECT id, name, source_lang, target_lang FROM termbases WHERE name = ?",
        (termbase_name,),
    ).fetchone()
    if not tb_row:
        raise RuntimeError(f"Termbase '{termbase_name}' not found.")
    tb_id = tb_row["id"]
    tb_src = normalize_lang(tb_row["source_lang"])
    tb_tgt = normalize_lang(tb_row["target_lang"])
    if not tb_src or not tb_tgt:
        raise RuntimeError(f"Termbase '{termbase_name}' has no declared direction.")

    entries = cur.execute(
        "SELECT id, source_term, target_term, source_lang, target_lang "
        "FROM termbase_terms WHERE termbase_id = ? ORDER BY id",
        (str(tb_id),),
    ).fetchall()
    conn.close()

    if verbose:
        print(f"Termbase '{termbase_name}' (id={tb_id}, declared {tb_src}->{tb_tgt})")
        print(f"Total entries to classify: {len(entries):,}")
        print(f"Batch size: {batch_size}  -> ~{(len(entries) + batch_size - 1) // batch_size} LLM requests")
        print()

    actions: list[Action] = []
    stats = Counter()
    results_by_id: dict[int, dict] = {}

    # Classify in batches with caching
    t_start = time.time()
    total_batches = (len(entries) + batch_size - 1) // batch_size
    for i in range(0, len(entries), batch_size):
        batch = entries[i : i + batch_size]
        pairs = [(row["id"], row["source_term"] or "", row["target_term"] or "") for row in batch]
        batch_num = i // batch_size + 1

        classifications = classify_batch(api_key, pairs)
        for c in classifications:
            if isinstance(c, dict) and "id" in c:
                results_by_id[int(c["id"])] = c

        if verbose:
            elapsed = time.time() - t_start
            done = min(i + batch_size, len(entries))
            print(
                f"  batch {batch_num}/{total_batches}  "
                f"({done:,}/{len(entries):,} entries, {elapsed:.1f}s)",
                flush=True,
            )

    # Build actions from classifications
    for row in entries:
        eid = int(row["id"])
        c = results_by_id.get(eid)
        src_text = row["source_term"] or ""
        tgt_text = row["target_term"] or ""
        cur_src_lang = row["source_lang"]
        cur_tgt_lang = row["target_lang"]

        llm_a = (c or {}).get("a", "ambiguous")
        llm_b = (c or {}).get("b", "ambiguous")

        if llm_a == tb_src and llm_b == tb_tgt:
            # Text in termbase direction. Tags might still be wrong.
            tags_correct = normalize_lang(cur_src_lang) == tb_src and normalize_lang(cur_tgt_lang) == tb_tgt
            if tags_correct:
                stats["already_correct"] += 1
                continue
            category = "A"
            new_src, new_tgt = src_text, tgt_text
            stats["category_A_tag_only"] += 1
        elif llm_a == tb_tgt and llm_b == tb_src:
            # Text reversed. Swap text AND fix tags.
            category = "B"
            new_src, new_tgt = tgt_text, src_text
            stats["category_B_swap_text"] += 1
        else:
            category = "C"
            new_src, new_tgt = src_text, tgt_text
            stats["category_C_ambiguous"] += 1

        actions.append(
            Action(
                entry_id=eid,
                current_source=src_text,
                current_target=tgt_text,
                current_src_lang=cur_src_lang,
                current_tgt_lang=cur_tgt_lang,
                category=category,
                llm_src=llm_a,
                llm_tgt=llm_b,
                new_source=new_src,
                new_target=new_tgt,
                new_src_lang=tb_src,
                new_tgt_lang=tb_tgt,
            )
        )

    stats["total_classified"] = len(results_by_id)
    stats["total_entries"] = len(entries)
    return actions, stats


# ---------------------------------------------------------------------------
# Apply
# ---------------------------------------------------------------------------


def apply_actions(db_path: str, actions: Iterable[Action]) -> int:
    """Apply Category A (tag fix) and Category B (text swap + tags).

    For Category B we also flip synonym language tags, mirroring the in-plugin
    ReverseTermDirection logic.
    """
    conn = sqlite3.connect(db_path)
    cur = conn.cursor()
    written = 0
    try:
        cur.execute("BEGIN")
        for a in actions:
            if a.category not in ("A", "B"):
                continue
            cur.execute(
                """
                UPDATE termbase_terms
                   SET source_term = ?,
                       target_term = ?,
                       source_lang = ?,
                       target_lang = ?
                 WHERE id = ?
                """,
                (a.new_source, a.new_target, a.new_src_lang, a.new_tgt_lang, a.entry_id),
            )
            if a.category == "B":
                # Flip synonym language tags
                cur.execute(
                    """
                    UPDATE termbase_synonyms
                       SET language = CASE language
                                        WHEN 'source' THEN 'target'
                                        WHEN 'target' THEN 'source'
                                        ELSE language
                                      END
                     WHERE term_id = ?
                    """,
                    (a.entry_id,),
                )
            written += 1
        conn.commit()
    except Exception:
        conn.rollback()
        raise
    finally:
        conn.close()
    return written


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------


def truncate(text: str, limit: int = 55) -> str:
    text = text or ""
    return text if len(text) <= limit else text[: limit - 1] + "..."


def main() -> int:
    # Windows default console is cp1252 — force UTF-8 with `replace` so fancy
    # quotes, arrows, etc. in termbase content never crash the print loop.
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("db_path", help="Path to the Supervertaler .db file.")
    p.add_argument("--termbase", default="PATENTS", help="Termbase name to process (default: PATENTS).")
    p.add_argument("--apply", action="store_true", help="Write changes (default: dry-run).")
    p.add_argument("--batch-size", type=int, default=50, help="Pairs per LLM request (default: 50).")
    p.add_argument("--show-swaps", type=int, default=30, help="Max Category B rows to list.")
    p.add_argument("--show-ambiguous", type=int, default=15, help="Max Category C rows to list.")
    p.add_argument("--quiet", action="store_true", help="Suppress batch progress.")
    args = p.parse_args()

    if not os.path.exists(args.db_path):
        print(f"DB not found: {args.db_path}", file=sys.stderr)
        return 1

    api_key = load_claude_key()
    actions, stats = plan(
        args.db_path,
        args.termbase,
        api_key,
        args.batch_size,
        verbose=not args.quiet,
    )

    print()
    print("=== Summary ===")
    for key in (
        "already_correct",
        "category_A_tag_only",
        "category_B_swap_text",
        "category_C_ambiguous",
        "total_classified",
        "total_entries",
    ):
        if stats.get(key) is not None:
            print(f"  {key:30s} {stats[key]:,}")
    print()

    b_actions = [a for a in actions if a.category == "B"]
    if b_actions:
        print(f"=== Sample Category B (text would be swapped) — up to {args.show_swaps} shown ===")
        for a in b_actions[: args.show_swaps]:
            print(
                f"  id={a.entry_id:>7}  "
                f"{truncate(a.current_source, 45)!r} | {truncate(a.current_target, 45)!r}"
            )
        if len(b_actions) > args.show_swaps:
            print(f"  ...and {len(b_actions) - args.show_swaps} more")
        print()

    c_actions = [a for a in actions if a.category == "C"]
    if c_actions:
        print(f"=== Sample Category C (LLM said ambiguous, skipped) — up to {args.show_ambiguous} shown ===")
        for a in c_actions[: args.show_ambiguous]:
            print(
                f"  id={a.entry_id:>7}  "
                f"{truncate(a.current_source, 45)!r} | {truncate(a.current_target, 45)!r}"
            )
        if len(c_actions) > args.show_ambiguous:
            print(f"  ...and {len(c_actions) - args.show_ambiguous} more")
        print()

    if not args.apply:
        print("=== Dry run — no changes written. Rerun with --apply to commit. ===")
        return 0

    backup_path = args.db_path + ".pre_ai_repair.bak"
    if os.path.exists(backup_path):
        print(f"Backup already exists at {backup_path} — aborting to avoid overwriting.", file=sys.stderr)
        return 2
    print(f"Backing up DB to {backup_path}")
    shutil.copy2(args.db_path, backup_path)

    written = apply_actions(args.db_path, actions)
    print(f"Wrote {written:,} rows.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
