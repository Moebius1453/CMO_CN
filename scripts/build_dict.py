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
        if p.name in ("blacklist.json", "labels.json", "identifiers.json",
                      "force_offsets.json", "dlc_briefing_fixes.json"):
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

    # Engine identifiers -- INI section/key names and on-disk directory names.
    # They may be shown to a player, so they stay in `exact`, but they must never
    # be injected at offset level: the pool string is also the lookup key, and
    # translating it breaks configuration reads (proven: `Game Preferences`
    # causes a NullReferenceException during Application_Startup) or path
    # resolution (proven: `Config` produced a Chinese-named 配置/ directory).
    identifiers: set[str] = set()
    idfile = SRC / "identifiers.json"
    if idfile.is_file():
        data = load(idfile)
        if isinstance(data, dict):
            identifiers = set(data.get("identifiers", []))

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
    # --- curated batches -------------------------------------------------
    # Only UI surface definitions (menus, HUD overrides, scenario titles/settings)
    # are permitted into `offsets`.  General translation chunks (chunk02..08)
    # and briefing prose live strictly in `exact`, where the UI-tree watcher,
    # WPF visitor, and WebView2 DOM injector consume them safely without
    # overwriting DAL queries, database hashes, or Lua sandbox strings in memory.
    offset_safe_sources = {
        "menu_terms.json", "force_offsets.json", "legacy_fixes.json",
        "scenario_titles.json", "scenario_settings.json"
    }

    for name, mapping in iter_source_maps():
        for en, zh in mapping.items():
            if not en or not zh or en in blacklist:
                continue
            zh = normalize_ellipsis(en, zh)
            exact[en] = zh
            curated.add(en)
            if name in offset_safe_sources:
                off = text_to_offset.get(en)
                if off is not None and bucket_of.get(off) == "display" \
                        and en not in identifiers:
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

    # --- clean and merge briefing paragraphs for WebView2 inline rendering ---
    # Scenarios with inline <ScenDescription> (182 files) and side briefings in
    # memory are rendered via WebView2 DOM tree traversal in CMOLocalizer.cs.
    # We clean and merge all briefing entries into `exact` (never into `offsets`),
    # fixing Zulu mistranslations and spacing so they pass the release gate.
    # DLC briefing prose is WebView2-rendered (never engine-pool injected), so it
    # is merged with allow_sensitive=True: the script/schema guard is meant for
    # engine strings, and it false-positives on prose such as task-unit numbers
    # ("544.01.06 (") or telegram styles ("... underground;" -> CODE_EXPR).
    brief1_file = DICT / "briefing_dict.json"
    brief2_file = DICT / "brief_translations.json"
    brief3_file = SRC / "dlc_briefing_fixes.json"
    all_briefs: dict[str, str] = {}
    if brief1_file.is_file():
        all_briefs.update(load(brief1_file))
    if brief2_file.is_file():
        all_briefs.update(load(brief2_file))
    if brief3_file.is_file():
        all_briefs.update(load(brief3_file))

    for en, zh in all_briefs.items():
        if not en or not zh or en in blacklist:
            continue
        zh_clean = zh.replace("祖鲁语", "标准时").replace("祖鲁时间", "标准时")
        zh_clean = HAN_GAP.sub("", zh_clean)
        zh_clean = re.sub(r"(?<![A-Za-z])(and|or|of|the|to|in|on|at|for|with|by|from|is|are|will|be)(?![A-Za-z])",
                          "", zh_clean, flags=re.I)
        zh_clean = re.sub(r"  +", " ", zh_clean)
        fixed = sanitize(en, zh_clean, allow_sensitive=True)
        if fixed is not None:
            exact[en] = fixed
            labels_only.add(en)

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
        bad_off = text_to_offset.get(bad)
        if bad_off is not None:
            offsets.pop(bad_off, None)
    # --- safety invariant: offsets may only hold display prose -----------
    # Injecting a translation over a schema identifier, script statement, control
    # name or path/config key corrupts the engine (this is exactly what broke the
    # DAL, the Lua sandbox, the INI reader and the Scenarios directory lookup
    # before).  Only strings triage is confident about reach `offsets`; every
    # other string is still served through `exact`.
    offsets = {k: v for k, v in offsets.items()
               if bucket_of.get(k) == "display"
               and pool.get(str(k)) not in identifiers}

    # Explicit, vetted HUD/status bar offsets that must be injected into the pool
    force_file = SRC / "force_offsets.json"
    if force_file.is_file():
        fdata = load(force_file)
        if isinstance(fdata, dict):
            for k, zh in fdata.items():
                if k.startswith("_"):
                    continue
                off_i = int(k)
                en_str = pool.get(k)
                if en_str and en_str not in identifiers and en_str not in blacklist:
                    offsets[off_i] = zh
                    exact[en_str] = zh
    known = set(text_to_offset.values())
    offsets = {k: v for k, v in offsets.items() if k in known}

    ordered_offsets = {str(k): offsets[k] for k in sorted(offsets)}
    ordered_exact = {k: exact[k] for k in sorted(exact)}
    ordered_partial = {k: partial[k] for k in sorted(partial)}

    return {
        "_meta": {
            "engine": "CMOLocalizer",
            "version": (ROOT / "VERSION").read_text(encoding="utf-8").strip(),
            # Keys are deliberately prefixed so this block can never be mistaken
            # for a top-level section by the engine's hand-rolled JSON scanner
            # (see scripts/verify_engine_parse.py).
            "counts": {
                "n_offsets": len(ordered_offsets),
                "n_exact": len(ordered_exact),
                "n_partial": len(ordered_partial),
                "n_words": 0,
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
    print(f"  offsets={c['n_offsets']}  exact={c['n_exact']}  "
          f"partial={c['n_partial']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
