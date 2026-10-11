#!/usr/bin/env python3
"""Deterministically build dict/CMOLocalizer.json from all translation sources.

Sources, merged in order (later wins):

    dict/CMOLocalizer.json          base — previously shipped entries
    dict/glossary.json              authoritative terminology seed
    dict/src/*.json                 curated translation batches ({"en": "zh"})
    dict/src/blacklist.json         English keys to purge from every section

Placement
---------
An English string triaged as `display` *and* present in the engine string pool is
written to `offsets`, so the engine's own string table returns Chinese everywhere
it is used.  It is also written to `exact` as a UI-tree safety net.  Everything
else lands in `exact` only.

Consistency
-----------
The engine injects `offsets` into the game's string pool at startup, so for pool
strings the offset value is what the player actually reads.  Any disagreement
between `offsets[off]` and `exact[pool[off]]` would surface as the same English
text rendering two different Chinese strings.  After merging, the two sections are
therefore reconciled:

    curated  entry -> the curated translation wins in both sections
    base     entry -> the offset value is propagated into `exact`

`words` is intentionally empty: word-by-word composition produced garbled output
and is no longer used by the engine.

Output is byte-stable: two runs over identical sources produce identical files.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DICT = ROOT / "dict"
SRC = DICT / "src"
MAIN = DICT / "CMOLocalizer.json"

sys.path.insert(0, str(ROOT / "scripts"))
from triage import classify, is_script_or_schema  # noqa: E402

ELLIPSIS = re.compile(r"…+")
DOT_RUN = re.compile(r"(?:\.\s*){3,}")
# Literal two-character escape text that must become real control characters.
LITERAL_ESC = re.compile(r"\\r\\n|\\n|\\r|\\t")
ESC_MAP = {"\\r\\n": "\r\n", "\\n": "\n", "\\r": "\r", "\\t": "\t"}
HAN = re.compile(r"[\u4e00-\u9fff]")
# Chinese text does not space-separate Han characters; translators sometimes leave
# an ASCII space where the English source had one ("小牛 同级武器").
HAN_GAP = re.compile(r"(?<=[\u4e00-\u9fff])[ \t]+(?=[\u4e00-\u9fff])")
BRACKET_PAIRS = ("()", "[]", "（）：", "【】", "〔〕")
BRACKET_PAIRS = (("(", ")"), ("[", "]"), ("（", "）"), ("【", "】"), ("〔", "〕"))


def load(path: Path):
    with path.open(encoding="utf-8") as fh:
        return json.load(fh)


def iter_source_maps():
    """Yield curated `dict/src/*.json` translation maps."""
    if not SRC.is_dir():
        return
    for p in sorted(SRC.glob("*.json")):
        if p.name in ("blacklist.json", "labels.json"):
            continue
        data = load(p)
        if isinstance(data, dict):
            yield p.name, {k: v for k, v in data.items()
                           if isinstance(v, str) and not k.startswith("_")}


def normalize_ellipsis(en: str, zh: str) -> str:
    """Keep the ellipsis style the engine expects: ASCII '...' when the source
    uses dots, so string comparisons elsewhere in the game stay intact."""
    if "…" in zh and DOT_RUN.search(en):
        return ELLIPSIS.sub("...", zh)
    return zh


def sanitize(en: str, zh: str, allow_sensitive: bool = False) -> str | None:
    """Repair mechanical defects, or return None when the entry must be dropped.

    Drops rather than guesses: a missing entry falls back to the offset-injection
    translation, which is always better than showing broken text to a player.

    `allow_sensitive` is set for `labels.json` entries.  Those are translated in
    the UI layer only and never injected into the engine string pool, so a token
    such as `.scen` inside a menu label is safe there.
    """
    if not zh:
        return None
    # Code / schema / path strings must never carry a translation.
    if not allow_sensitive and is_script_or_schema(en):
        return None
    # Literal "\\r\\n" written as text while the source holds real CR/LF.
    if any(c in en for c in "\r\n\t"):
        zh = LITERAL_ESC.sub(lambda m: ESC_MAP[m.group(0)], zh)
    zh = HAN_GAP.sub("", zh)
    # Restore whitespace the engine relies on for concatenated fragments.
    lead_src = en[:len(en) - len(en.lstrip())]
    lead_dst = zh[:len(zh) - len(zh.lstrip())]
    if lead_src and not lead_dst:
        zh = lead_src + zh
    trail_src = en[len(en.rstrip()):]
    trail_dst = zh[len(zh.rstrip()):]
    if trail_src and not trail_dst:
        zh = zh + trail_src
    # A translation must actually be a translation.
    if not HAN.search(zh):
        return None
    # Bracket balance must match the source's balance.
    for o, c in BRACKET_PAIRS:
        if (en.count(o) - en.count(c)) != (zh.count(o) - zh.count(c)):
            return None
    # Line structure is part of the string contract.
    if en.count("\n") != zh.count("\n"):
        return None
    return zh


def build() -> dict:
    base = load(MAIN)
    offsets: dict[int, str] = {}
    for k, v in base.get("offsets", {}).items():
        try:
            offsets[int(k)] = v
        except (TypeError, ValueError):
            continue
    exact: dict[str, str] = dict(base.get("exact", {}))
    # `partial` is retained for file-format compatibility but is intentionally
    # empty: the engine no longer performs word-level composition, and the
    # inherited entries contained split-word artifacts ("海里 到达返航油量").
    partial: dict[str, str] = {}

    blacklist: set[str] = set()
    blfile = SRC / "blacklist.json"
    if blfile.is_file():
        blacklist = set(load(blfile))

    pool = load(DICT / "strings_pool.json")
    text_to_offset = {v: int(k) for k, v in pool.items()}
    bucket_of = {int(k): classify(v) for k, v in pool.items()}

    curated: set[str] = set()

    # --- glossary -------------------------------------------------------
    glossary = load(DICT / "glossary.json")
    for section, terms in glossary.items():
        if section.startswith("_") or not isinstance(terms, dict):
            continue
        for en, zh in terms.items():
            exact.setdefault(en, zh)
            curated.add(en)

    # --- curated batches -------------------------------------------------
    for _name, mapping in iter_source_maps():
        for en, zh in mapping.items():
            if not en or not zh or en in blacklist:
                continue
            zh = normalize_ellipsis(en, zh)
            exact[en] = zh
            curated.add(en)
            off = text_to_offset.get(en)
            if off is not None and bucket_of.get(off) == "display":
                offsets[off] = zh

    # --- labels-only batch (never injected at offset level) --------------
    labfile = SRC / "labels.json"
    labels_only: set[str] = set()
    if labfile.is_file():
        for en, zh in load(labfile).items():
            if en and zh and en not in blacklist and not en.startswith("_"):
                exact[en] = zh
                curated.add(en)
                labels_only.add(en)

    # --- reconcile offsets <-> exact -------------------------------------
    for off, zh in list(offsets.items()):
        en = pool.get(str(off))
        if en is None:
            continue
        if en in curated:
            offsets[off] = exact[en]
        else:
            exact[en] = zh

    # --- drop unreliable legacy briefing fragments ----------------------
    # Scenario briefing prose is delivered by editing the .scen/.html files, not
    # by the UI dictionary.  The fragments previously merged into `exact` came
    # from sentence-splitting machine translation ("Zulu" -> "祖鲁语") and only
    # 42 of 4885 ever matched an engine string, so they are purged unless a
    # curated source claims the same English key.
    brief = DICT / "briefing_dict.json"
    if brief.is_file():
        for en in load(brief):
            if en not in curated:
                exact.pop(en, None)

    # --- sanitize: repair or drop every surviving entry ------------------
    for en in list(exact):
        fixed = sanitize(en, exact[en], allow_sensitive=en in labels_only)
        if fixed is None:
            del exact[en]
        else:
            exact[en] = fixed
    for off in list(offsets):
        en = pool.get(str(off))
        if en in exact:
            offsets[off] = exact[en]
        else:
            del offsets[off]

    # --- purge -----------------------------------------------------------
    for bad in blacklist:
        exact.pop(bad, None)
        partial.pop(bad, None)
    # --- safety invariant: offsets may only hold display prose -----------
    # Injecting a translation over a schema identifier, script statement or
    # control name corrupts the engine (this is exactly what broke the DAL and
    # the Lua sandbox before).  Only strings triage is confident about reach
    # `offsets`; every other string is still served through `exact`.
    offsets = {k: v for k, v in offsets.items()
               if bucket_of.get(k) == "display"}
    known = set(text_to_offset.values())
    offsets = {k: v for k, v in offsets.items() if k in known}

    ordered_offsets = {str(k): offsets[k] for k in sorted(offsets)}
    ordered_exact = {k: exact[k] for k in sorted(exact)}
    ordered_partial = {k: partial[k] for k in sorted(partial)}

    return {
        "_meta": {
            "engine": "CMOLocalizer",
            "version": (ROOT / "VERSION").read_text(encoding="utf-8").strip(),
            "counts": {
                "offsets": len(ordered_offsets),
                "exact": len(ordered_exact),
                "partial": len(ordered_partial),
                "words": 0,
            },
        },
        "offsets": ordered_offsets,
        "exact": ordered_exact,
        "partial": ordered_partial,
        "words": {},
    }


def write(doc: dict, path: Path) -> None:
    text = json.dumps(doc, ensure_ascii=False, indent=1, sort_keys=False)
    path.write_text(text + "\n", encoding="utf-8")


def main() -> int:
    doc = build()
    write(doc, MAIN)
    c = doc["_meta"]["counts"]
    print(f"built {MAIN}")
    print(f"  offsets={c['offsets']}  exact={c['exact']}  partial={c['partial']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
