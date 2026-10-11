#!/usr/bin/env python3
"""Coverage audit for the CMO localization dictionary.

Answers, with evidence:
  * how many extracted engine strings are human-readable UI text
  * how many of those the dictionary actually covers
  * which of the covered entries are low quality (stiff / machine-translated)
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DICT = ROOT / "dict"

# --- classification ------------------------------------------------------

# Identifiers, resource keys, .NET types, control names -> never translate.
RE_IDENTIFIER = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")
RE_DOTTED_IDENT = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)+$")
RE_CONTROL_NAME = re.compile(r"^(TSMI_|ToolStrip|MenuStrip|Timer\d|Timer|ComboBox|Label|Button|TextBox|Panel|GroupBox|CheckBox|TabPage|DataGridView|ListView|TreeView|PictureBox|SplitContainer|ContextMenuStrip|NotifyIcon)")
RE_SQL = re.compile(r"\b(SELECT|INSERT|UPDATE|DELETE|CREATE|DROP|ALTER|FROM|WHERE|JOIN|ORDER BY|GROUP BY|PRAGMA|VACUUM)\b", re.I)
RE_PATHISH = re.compile(r"(^[A-Za-z]:\\|\\\\|\.dll$|\.exe$|\.db3$|\.scen$|\.ini$|\.xml$|\.png$|\.csv$|\.log$|\.config$)")
RE_EXT_FILTER = re.compile(r"\*\.\w+")
RE_FORMAT_ONLY = re.compile(r"^[^A-Za-z]*(\{\d+\}|%\w+|[^A-Za-z])*$")
RE_URL = re.compile(r"^(https?|ftp)://|^www\.", re.I)
RE_GUID = re.compile(r"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-")
RE_HEXISH = re.compile(r"^[0-9A-Fa-f]{16,}$")
RE_SYMBOLISH = re.compile(r"^[^\w]+$")
RE_XMLISH = re.compile(r"^<[^>]*>$")
RE_REGKEY = re.compile(r"^SOFTWARE\\\\|^HKEY_|^[A-Z_]{6,}$")

# Pure ASCII single word that is a known engine/internal concept.
INTERNAL_WORDS = {
    "Culture", "Neutral", "publicKeyToken", "Version", "retargetable",
    "True", "False", "None", "Null", "null", "true", "false",
}


def is_ui_text(s: str) -> bool:
    """Heuristic: is this string something a player should read on screen?"""
    if not s or len(s.strip()) == 0:
        return False
    s = s.strip()
    if len(s) < 2:
        return False
    if not re.search(r"[A-Za-z]", s):
        # Pure CJK / symbols / digits: nothing for us to do.
        return False
    if RE_URL.match(s) or RE_GUID.match(s) or RE_HEXISH.match(s):
        return False
    if RE_PATHISH.search(s):
        return False
    if RE_EXT_FILTER.search(s) and not re.search(r"[a-z]{4}", s):
        return False
    if RE_SYMBOLISH.match(s) or RE_XMLISH.match(s):
        return False
    if RE_REGKEY.match(s):
        return False
    if RE_CONTROL_NAME.match(s):
        return False
    if RE_DOTTED_IDENT.match(s):
        return False
    if RE_IDENTIFIER.match(s):
        # CamelCase / snake_case identifiers, or single lowercase internal words.
        if s in INTERNAL_WORDS:
            return False
        if "_" in s:
            return False
        # "Aircraft" style single words are real UI text; "AircraftID" is not.
        if re.search(r"[a-z][A-Z]", s):
            return False
        return True
    if RE_SQL.search(s):
        return False
    if RE_FORMAT_ONLY.match(s):
        return False
    # Needs at least one run of >=2 Latin letters to be translatable prose.
    if not re.search(r"[A-Za-z]{2}", s):
        return False
    return True


# --- quality -------------------------------------------------------------

SUSPICIOUS_PATTERNS = [
    (re.compile(r"[\u4e00-\u9fff]\s+[\u4e00-\u9fff]"), "汉字间空格"),
    (re.compile(r"[A-Za-z]{3,}"), "残留英文词"),
    (re.compile(r"[，。、；：]{2,}"), "重复标点"),
    (re.compile(r"^[\u4e00-\u9fff]$"), "单字译文"),
    (re.compile(r"[\u4e00-\u9fff][/／][\u4e00-\u9fff]"), "斜杠并列"),
]

# English function words that must never survive inside a Chinese label.
RESIDUAL_STOPWORDS = re.compile(
    r"\b(the|of|and|for|with|from|to|in|on|at|by|is|are|will|be|"
    r"All|No|Yes|On|Off|None|Other|New|Add|Edit|Delete|Show|Hide)\b"
)


def flag_quality(src: str, dst: str) -> list[str]:
    """Return a list of quality concerns for one translation."""
    out = []
    if not dst or not re.search(r"[\u4e00-\u9fff]", dst):
        out.append("未汉化")
        return out
    for rx, label in SUSPICIOUS_PATTERNS:
        if rx.search(dst):
            # Residual English is only a problem when it dominates.
            if label == "残留英文词":
                lat = len(re.findall(r"[A-Za-z]", dst))
                cjk = len(re.findall(r"[\u4e00-\u9fff]", dst))
                if lat <= cjk * 0.6:
                    continue
                if RESIDUAL_STOPWORDS.search(dst):
                    out.append(label + "(虚词)")
                continue
            out.append(label)
    if dst.count("（") != dst.count("）"):
        out.append("括号不配对")
    return out


def main() -> int:
    pool = json.loads((DICT / "strings_pool.json").read_text(encoding="utf-8"))
    d = json.loads((DICT / "CMOLocalizer.json").read_text(encoding="utf-8"))
    off, exact, partial, words = d["offsets"], d["exact"], d["partial"], d["words"]

    ui = {k: v for k, v in pool.items() if is_ui_text(v)}
    print(f"string pool        : {len(pool)}")
    print(f"  UI-translatable  : {len(ui)}")
    print(f"  internal/ignored : {len(pool) - len(ui)}")
    print(f"offsets entries    : {len(off)}")
    print(f"exact entries      : {len(exact)}")
    print(f"partial entries    : {len(partial)}")
    print(f"words entries      : {len(words)}")

    covered = [k for k, v in ui.items() if k in off]
    missing = [k for k, v in ui.items() if k not in off]
    print(f"\nengine-string coverage (offsets): {len(covered)}/{len(ui)} "
          f"= {100.0*len(covered)/max(1,len(ui)):.1f}%")
    print(f"uncovered engine strings        : {len(missing)}")

    # Coverage through the exact map (UI tree catches these at runtime).
    exact_vals = set(exact.keys())
    strand = [k for k in missing if ui[k] in exact_vals]
    print(f"  of which caught by exact map  : {len(strand)}")
    print(f"  truly unhandled               : {len(missing)-len(strand)}")

    # Quality of the exact dictionary.
    flagged: dict[str, list[tuple[str, str, list[str]]]] = {}
    for src, dst in exact.items():
        issues = flag_quality(src, dst)
        for i in issues:
            flagged.setdefault(i, []).append((src, dst, issues))
    print("\nexact-map quality flags:")
    for k in sorted(flagged, key=lambda x: -len(flagged[x])):
        print(f"  {k:<14}: {len(flagged[k])}")

    if "--dump" in sys.argv:
        want = sys.argv[sys.argv.index("--dump") + 1]
        rows = flagged.get(want, [])
        for src, dst, _ in rows[:80]:
            print(f"    {src!r} -> {dst!r}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
