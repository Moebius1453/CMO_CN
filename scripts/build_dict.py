#!/usr/bin/env python3
"""Deterministically build dict/CMOLocalizer.json from all translation sources.

Sources, merged in order (later wins):

    dict/CMOLocalizer.json          base — previously shipped entries
    dict/glossary.json              authoritative terminology seed
    dict/src/*.json                 curated translation batches ({"en": "zh"})
    dict/src/blacklist.json         English keys to purge from every section

Placement rule
--------------
An English string that is triaged as `display` *and* present in the engine string
pool is written to `offsets`, so the engine's own string table returns Chinese
everywhere it is used.  It is also written to `exact` as a UI-tree safety net.
Everything else lands in `exact` only.

`partial` holds long multi-word phrases used for conservative in-string
substitution.  `words` is intentionally empty (word-by-word composition produced
garbled output and is no longer used by the engine).

Output is byte-stable: two runs over identical sources produce identical files.
"""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DICT = ROOT / "dict"
SRC = DICT / "src"
MAIN = DICT / "CMOLocalizer.json"

import sys
sys.path.insert(0, str(ROOT / "scripts"))
from triage import classify  # noqa: E402


def load(path: Path):
    with path.open(encoding="utf-8") as fh:
        return json.load(fh)


def iter_source_maps():
    """Yield (name, mapping) for every curated translation file."""
    if not SRC.is_dir():
        return
    for p in sorted(SRC.glob("*.json")):
        if p.name in ("blacklist.json", "labels.json"):
            continue
        data = load(p)
        if isinstance(data, dict):
            yield p.name, {k: v for k, v in data.items()
                           if isinstance(v, str) and not k.startswith("_")}


def build() -> dict:
    base = load(MAIN)
    offsets: dict[int, str] = {}
    for k, v in base.get("offsets", {}).items():
        try:
            offsets[int(k)] = v
        except (TypeError, ValueError):
            continue
    exact: dict[str, str] = dict(base.get("exact", {}))
    partial: dict[str, str] = dict(base.get("partial", {}))
    _dbg = __import__("os").environ.get("BUILD_DEBUG")
    if _dbg:
        print(f"[dbg] base offsets={len(offsets)} exact={len(exact)}")

    blacklist: set[str] = set()
    blfile = SRC / "blacklist.json"
    if blfile.is_file():
        blacklist = set(load(blfile))

    pool = load(DICT / "strings_pool.json")
    # English text -> offset, for offset-level injection.
    text_to_offset = {v: int(k) for k, v in pool.items()}
    bucket_of = {int(k): classify(v) for k, v in pool.items()}

    # --- glossary -------------------------------------------------------
    glossary = load(DICT / "glossary.json")
    for section, terms in glossary.items():
        if section.startswith("_") or not isinstance(terms, dict):
            continue
        for en, zh in terms.items():
            exact.setdefault(en, zh)

    # --- curated batches -------------------------------------------------
    for _name, mapping in iter_source_maps():
        for en, zh in mapping.items():
            if not en or not zh or en in blacklist:
                continue
            exact[en] = zh
            off = text_to_offset.get(en)
            if off is not None and bucket_of.get(off) == "display":
                offsets[off] = zh

    # --- labels-only batch (never injected at offset level) --------------
    labfile = SRC / "labels.json"
    if labfile.is_file():
        for en, zh in load(labfile).items():
            if en and zh and en not in blacklist:
                exact[en] = zh

    # --- purge -----------------------------------------------------------
    for bad in blacklist:
        exact.pop(bad, None)
        partial.pop(bad, None)
    known = set(text_to_offset.values())
    stale = len(offsets) - sum(1 for k in offsets if k in known)
    offsets = {k: v for k, v in offsets.items() if k in known}
    if _dbg:
        print(f"[dbg] offsets kept={len(offsets)} stale-dropped={stale}")

    # --- deterministic ordering -----------------------------------------
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
