"""Repair reversed-direction entries in the Supervertaler termbase DB.

Legacy write-path bugs (pre-v4.19.13) caused some term entries to be saved with
their `source_lang` / `target_lang` metadata flipped relative to the termbase's
declared direction. In some cases the TEXT itself (source_term / target_term
columns) was also swapped — the entry was saved "upside down" relative to the
termbase.

The v4.19.21 plugin fix makes matching resilient to flipped lang tags by using
the termbase's declared direction instead of per-entry tags. But entries where
the TEXT is reversed still produce wrong index keys. This script identifies and
fixes those.

Categories (for each entry in a termbase, relative to the termbase's declared
source_lang / target_lang):

  A. Tags wrong, text correct — text is in termbase direction, lang tags lie.
     Fix: rewrite tags to match termbase declaration.

  B. Tags wrong AND text reversed — source_term is in the termbase's TARGET
     language, target_term is in the termbase's SOURCE language.
     Fix: swap source_term ↔ target_term and rewrite tags.

  C. Ambiguous — text too short or language-neutral (chemical names, numbers,
     proper nouns) to detect reliably. Skipped, reported for manual review.

The detection uses a stopword-based heuristic keyed to Dutch ↔ English, which is
the only language pair in the user's termbases. Pass `--apply` to write changes;
by default runs dry-run and prints a summary.
"""

from __future__ import annotations

import argparse
import os
import re
import shutil
import sqlite3
import sys
from collections import Counter
from dataclasses import dataclass
from typing import Iterable, Optional

# ---------------------------------------------------------------------------
# Language detection heuristic
# ---------------------------------------------------------------------------

# Whitespace-bounded markers that strongly indicate Dutch. Each marker is
# searched as a " word " token to avoid matching substrings inside other words.
# "Strong" Dutch markers — words that practically never appear in English text.
# A single occurrence, with no English markers present, is enough to classify.
DUTCH_STRONG = {
    "het", "een", "der", "den", "van", "zijn",
    "waarbij", "waarvan", "welke",
    "gekend", "gekende", "volgens", "huidige",
    "voorkeursvorm", "voorkeursuitvoering", "uitvinding", "uitvoeringsvorm",
    "werkwijze", "inrichting", "onderhavige",
}

# "Moderate" Dutch markers — need a confirming signal (another marker or n-gram).
DUTCH_MARKERS = DUTCH_STRONG | {
    "de", "bij", "met", "aan", "uit", "naar", "door", "onder", "over",
    "worden", "werd", "wordt", "hebben", "heeft",
    "niet", "geen", "maar", "ook", "nog", "zoals",
    "die", "dat", "deze", "dit",
    "hun", "haar", "ons", "onze",
    "beschreven",
}

# "Strong" English markers — virtually never in Dutch text.
ENGLISH_STRONG = {
    "the", "and",
    "which", "whose",
    "would", "should", "could",
    "prior", "known",
}

ENGLISH_MARKERS = ENGLISH_STRONG | {
    "or", "with", "from", "as", "are", "was", "were",
    "been", "being", "have", "has", "had", "does", "did",
    "will",
    "this", "that", "these", "those", "who", "whom",
    "its", "they",
    "but", "also", "only", "such",
    "described", "current", "according",
}

# Distinctive Dutch character bigrams/trigrams. Removed "ee" / "oo" because
# both appear freely in English ("see", "committee", "book"). "ij" stays because
# it's a hallmark Dutch digraph. "sch", "lijk", "heid" are reliably Dutch.
DUTCH_NGRAMS = ("ij", "aa", "uu", "eu ", "oe ", "sch", "lijk", "heid", "tje", "tjes")
ENGLISH_NGRAMS = ("tion", "ough", "th ", "wh ", " ing ", "'s ", "ght")

WORD_RE = re.compile(r"\b[a-zà-ÿ]+\b", re.IGNORECASE)


def detect_language(text: Optional[str]) -> Optional[str]:
    """Return 'nl', 'en', or None if ambiguous."""
    if not text:
        return None
    lowered = text.lower()
    words = [w.lower() for w in WORD_RE.findall(lowered)]
    if not words:
        return None

    nl_strong = sum(1 for w in words if w in DUTCH_STRONG)
    en_strong = sum(1 for w in words if w in ENGLISH_STRONG)
    nl_words = sum(1 for w in words if w in DUTCH_MARKERS)
    en_words = sum(1 for w in words if w in ENGLISH_MARKERS)

    nl_ngrams = sum(lowered.count(ng) for ng in DUTCH_NGRAMS)
    en_ngrams = sum(lowered.count(ng) for ng in ENGLISH_NGRAMS)

    # A single "strong" marker is sufficient when the other language has none
    # of ANY marker (weak or strong) and no n-grams for it either.
    if nl_strong >= 1 and en_words == 0 and en_ngrams == 0:
        return "nl"
    if en_strong >= 1 and nl_words == 0 and nl_ngrams == 0:
        return "en"

    # Two or more words with a clear margin
    if nl_words >= 2 and nl_words > en_words + 1:
        return "nl"
    if en_words >= 2 and en_words > nl_words + 1:
        return "en"

    # Single weaker stopword + at least one confirming n-gram, and no counter-signal
    if nl_words >= 1 and en_words == 0 and nl_ngrams >= 1:
        return "nl"
    if en_words >= 1 and nl_words == 0 and en_ngrams >= 1:
        return "en"

    # N-gram-only fallback: need ≥3 to offset noise, and no counter-ngrams
    if nl_ngrams >= 3 and en_ngrams == 0:
        return "nl"
    if en_ngrams >= 3 and nl_ngrams == 0:
        return "en"

    return None


# ---------------------------------------------------------------------------
# Repair logic
# ---------------------------------------------------------------------------


def normalize_lang(lang: Optional[str]) -> Optional[str]:
    """Collapse variants ('nl-BE', 'Dutch (Belgium)', 'Dutch') → 'nl' / 'en'."""
    if not lang:
        return None
    lower = lang.strip().lower()
    if lower.startswith(("nl", "dutch", "flemish")):
        return "nl"
    if lower.startswith(("en", "english")):
        return "en"
    return lower


@dataclass
class RepairAction:
    entry_id: int
    termbase_id: int
    current_source: str
    current_target: str
    current_src_lang: Optional[str]
    current_tgt_lang: Optional[str]
    category: str  # 'A' (tags only) | 'B' (swap text+tags) | 'C' (ambiguous)
    detected_src_lang: Optional[str]
    detected_tgt_lang: Optional[str]
    new_source: str
    new_target: str
    new_src_lang: str
    new_tgt_lang: str


def plan_repairs(db_path: str) -> tuple[list[RepairAction], dict]:
    """Scan the DB and return a list of proposed repair actions plus stats."""
    conn = sqlite3.connect(db_path)
    conn.row_factory = sqlite3.Row
    cur = conn.cursor()

    termbases = {
        r["id"]: (r["name"], normalize_lang(r["source_lang"]), normalize_lang(r["target_lang"]))
        for r in cur.execute("SELECT id, name, source_lang, target_lang FROM termbases")
    }

    actions: list[RepairAction] = []
    stats = Counter()

    for row in cur.execute(
        "SELECT id, source_term, target_term, termbase_id, source_lang, target_lang FROM termbase_terms"
    ):
        tb_id_raw = row["termbase_id"]
        try:
            tb_id = int(tb_id_raw)
        except (TypeError, ValueError):
            stats["no_termbase_id"] += 1
            continue
        tb_info = termbases.get(tb_id)
        if not tb_info:
            stats["orphan_termbase"] += 1
            continue

        tb_name, tb_src, tb_tgt = tb_info
        if not tb_src or not tb_tgt:
            stats["termbase_no_direction"] += 1
            continue

        entry_src = normalize_lang(row["source_lang"])
        entry_tgt = normalize_lang(row["target_lang"])

        tags_match_termbase = entry_src == tb_src and entry_tgt == tb_tgt
        tags_reversed = entry_src == tb_tgt and entry_tgt == tb_src

        if tags_match_termbase:
            stats["already_correct"] += 1
            continue

        # Not tags_match_termbase — candidate for repair. Detect text language.
        source_text = row["source_term"] or ""
        target_text = row["target_term"] or ""
        detected_src = detect_language(source_text)
        detected_tgt = detect_language(target_text)

        # Text swap (Category B) is destructive, so require BOTH sides to agree:
        # source detected as termbase.target_lang AND target detected as
        # termbase.source_lang. Either side alone is too easy to fool with Latin
        # phrases ("in vitro", "in situ") or technical terms.
        #
        # Category A (tag-only fix) is safe, so one-sided confirmation is enough.
        src_looks_like_target = detected_src is not None and detected_src == tb_tgt
        tgt_looks_like_source = detected_tgt is not None and detected_tgt == tb_src
        src_looks_correct = detected_src is not None and detected_src == tb_src
        tgt_looks_correct = detected_tgt is not None and detected_tgt == tb_tgt

        if src_looks_like_target and tgt_looks_like_source:
            # Category B: text is reversed. Swap text AND fix tags.
            category = "B"
            new_source, new_target = target_text, source_text
            stats["category_B_swap_text"] += 1
        elif src_looks_correct or tgt_looks_correct:
            # Category A: at least one side confirms termbase direction. Fix tags only.
            category = "A"
            new_source, new_target = source_text, target_text
            stats["category_A_tag_only"] += 1
        else:
            # Category C: can't determine with confidence. Leave alone.
            category = "C"
            new_source, new_target = source_text, target_text
            stats["category_C_ambiguous"] += 1

        actions.append(
            RepairAction(
                entry_id=int(row["id"]),
                termbase_id=tb_id,
                current_source=source_text,
                current_target=target_text,
                current_src_lang=row["source_lang"],
                current_tgt_lang=row["target_lang"],
                category=category,
                detected_src_lang=detected_src,
                detected_tgt_lang=detected_tgt,
                new_source=new_source,
                new_target=new_target,
                new_src_lang=tb_src,
                new_tgt_lang=tb_tgt,
            )
        )

    conn.close()
    stats["total_candidates"] = len(actions)
    return actions, stats


def apply_repairs(db_path: str, actions: Iterable[RepairAction]) -> int:
    """Apply repair actions for categories A and B. Returns rows written."""
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
            written += cur.rowcount
        conn.commit()
    except Exception:
        conn.rollback()
        raise
    finally:
        conn.close()
    return written


def truncate(text: str, limit: int = 60) -> str:
    text = text or ""
    return text if len(text) <= limit else text[: limit - 1] + "…"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument(
        "db_path",
        help="Path to the Supervertaler .db file (e.g. C:\\Users\\mbeijer\\Supervertaler\\resources\\supervertaler.db)",
    )
    parser.add_argument("--apply", action="store_true", help="Write changes (default is dry-run).")
    parser.add_argument(
        "--show-ambiguous",
        type=int,
        default=30,
        help="Max Category C rows to list in the report (default 30).",
    )
    parser.add_argument(
        "--show-swaps",
        type=int,
        default=30,
        help="Max Category B rows to list in the report (default 30).",
    )
    args = parser.parse_args()

    if not os.path.exists(args.db_path):
        print(f"DB not found: {args.db_path}", file=sys.stderr)
        return 1

    actions, stats = plan_repairs(args.db_path)

    print(f"DB: {args.db_path}")
    print()
    print("=== Summary ===")
    for key in (
        "already_correct",
        "category_A_tag_only",
        "category_B_swap_text",
        "category_C_ambiguous",
        "total_candidates",
        "orphan_termbase",
        "termbase_no_direction",
        "no_termbase_id",
    ):
        if stats.get(key):
            print(f"  {key:30s} {stats[key]:,}")
    print()

    # Sample Category B (text swaps) — these are the ones that matter for matching
    b_actions = [a for a in actions if a.category == "B"]
    if b_actions:
        print(f"=== Sample Category B (text would be swapped) — up to {args.show_swaps} shown ===")
        for a in b_actions[: args.show_swaps]:
            print(
                f"  id={a.entry_id:>7} tb={a.termbase_id:>3} "
                f"src({a.current_src_lang}->{a.new_src_lang}): "
                f"{truncate(a.current_source, 45)!r} -> {truncate(a.new_source, 45)!r}"
            )
        if len(b_actions) > args.show_swaps:
            print(f"  ...and {len(b_actions) - args.show_swaps} more")
        print()

    # Sample Category C (ambiguous — skipped)
    c_actions = [a for a in actions if a.category == "C"]
    if c_actions:
        print(f"=== Sample Category C (ambiguous, skipped) — up to {args.show_ambiguous} shown ===")
        for a in c_actions[: args.show_ambiguous]:
            print(
                f"  id={a.entry_id:>7} tb={a.termbase_id:>3} "
                f"src: {truncate(a.current_source, 45)!r} | tgt: {truncate(a.current_target, 45)!r}"
            )
        if len(c_actions) > args.show_ambiguous:
            print(f"  ...and {len(c_actions) - args.show_ambiguous} more")
        print()

    if not args.apply:
        print("=== Dry run — no changes written. Rerun with --apply to commit. ===")
        return 0

    # Backup before writing
    backup_path = args.db_path + ".pre_tag_repair.bak"
    if os.path.exists(backup_path):
        print(f"Backup already exists at {backup_path} — aborting to avoid overwriting.", file=sys.stderr)
        return 2
    print(f"Backing up DB to {backup_path}")
    shutil.copy2(args.db_path, backup_path)

    written = apply_repairs(args.db_path, actions)
    print(f"Wrote {written:,} rows.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
