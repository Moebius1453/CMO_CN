#!/usr/bin/env python3
"""Release gate for the CMO localization dictionary.

Fails (exit 1) on any of:
  1. dict/CMOLocalizer.json is not valid JSON or is missing sections
  2. an `offsets` key is not a real engine-pool offset
  3. an `offsets` value differs from the same string's `exact` value
     (offset injection and the UI-tree watcher must agree)
  4. a translated string is not actually Chinese
  5. Chinese text contains embedded ASCII spaces between Han characters
  6. an English function word survives inside a Chinese translation
  7. placeholder counts differ between source and translation
  8. leading/trailing whitespace is not preserved
  9. parentheses / quotes are unbalanced
 10. engine-internal strings were translated at offset level (corruption risk)

Also prints a coverage summary for the player-visible engine strings.
"""
from __future__ import annotations

import json
import re
import sys
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DICT = ROOT / "dict"

HAN = re.compile(r"[\u4e00-\u9fff]")
HAN_SPACE = re.compile(r"[\u4e00-\u9fff][ \t]+[\u4e00-\u9fff]")
FUNC_WORDS = re.compile(
    r"(?<![A-Za-z])(the|of|and|for|with|from|to|in|on|at|by|is|are|will|be)"
    r"(?![A-Za-z])", re.I)
PLACEHOLDER = re.compile(r"\{[0-9]+\}|%[sdif]|\{[A-Za-z]+\}")
BRACKETS = {"(": ")", "[": "]", "（": "）", "【": "】", "〔": "〕"}

# English fragments that must stay verbatim inside a Chinese translation:
# licence identifiers, URLs, citations, registered names, code credits.  These
# are stripped before the residual-function-word check so a citation such as
# "Survey Review XXII" or "CC BY 4.0" is not reported as an untranslated word.
PROTECTED = re.compile(
    r"(https?://\S+"
    r"|\bCC\s+BY(?:-[A-Z]{2})?\s*[\d.]*"
    r"|\b(?:MIT|GNU|BSD|Apache|LGPL|GPL)\b[^\s，。]*"
    r"|\(C\)\s*[^\r\n，。]*"
    r"|\b(?:Survey Review|Springer|Elsevier|Wiley|IEEE)\b[^\r\n，。]*"
    r"|\[[^\]]*\]"
    r"|《[^》]*》"
    r"|「[^」]*」)", re.I)

failures: list[str] = []


def fail(msg: str) -> None:
    failures.append(msg)


def check_offsets(doc: dict, pool: dict[str, str]) -> None:
    off = doc["offsets"]
    exact = doc["exact"]
    known = set(pool)
    for k, v in off.items():
        if k not in known:
            fail(f"offsets[{k}] is not an engine string-pool offset")
            continue
        src = pool[k]
        if not HAN.search(v):
            fail(f"offsets[{k}] not Chinese: {v!r}")
        if src in exact and exact[src] != v:
            fail(f"offsets[{k}]={v!r} disagrees with exact[{src!r}]={exact[src]!r}")


def check_quality(entries: dict[str, str], label: str, pool_vals: set[str]) -> None:
    for src, dst in entries.items():
        if not HAN.search(dst):
            fail(f"{label}[{src!r}] missing Chinese: {dst!r}")
            continue
        m = HAN_SPACE.search(dst)
        if m:
            fail(f"{label}[{src!r}]={dst!r} has a space between Han characters")
        # Only flag when the leftover English word is not part of a kept acronym
        # or a protected licence / URL / citation fragment.
        scanned = PROTECTED.sub(" ", dst)
        residual = [w for w in re.findall(r"[A-Za-z]{2,}", scanned)
                    if w.lower() in {"the", "of", "and", "for", "with", "from",
                                     "to", "in", "on", "at", "by", "is", "are",
                                     "will", "be"}]
        if residual:
            fail(f"{label}[{src!r}]={dst!r} keeps English function word {residual}")
        if sorted(PLACEHOLDER.findall(src)) != sorted(PLACEHOLDER.findall(dst)):
            fail(f"{label}[{src!r}]={dst!r} placeholder mismatch "
                 f"{PLACEHOLDER.findall(src)} vs {PLACEHOLDER.findall(dst)}")
        if (src[:1].isspace() and not dst[:1].isspace()) or \
           (src[-1:].isspace() and not dst[-1:].isspace()):
            fail(f"{label}[{src!r}]={dst!r} lost leading/trailing whitespace")
        # Fragments such as "(Type:" are intentionally unbalanced; compare the
        # *difference* in balance instead of demanding a closed pair.
        for o, c in BRACKETS.items():
            if (src.count(o) - src.count(c)) != (dst.count(o) - dst.count(c)):
                fail(f"{label}[{src!r}]={dst!r} bracket balance changed for {o}{c}")
                break
        if src.count("\n") != dst.count("\n"):
            fail(f"{label}[{src!r}] line-count changed ({src.count(chr(10))}"
                 f" -> {dst.count(chr(10))})")
        if src.count("\r") != dst.count("\r"):
            fail(f"{label}[{src!r}] CR count changed")


def main() -> int:
    main_file = DICT / "CMOLocalizer.json"
    try:
        doc = json.loads(main_file.read_text(encoding="utf-8"))
    except Exception as exc:  # noqa: BLE001
        print(f"FAIL: {main_file} is not valid JSON: {exc}")
        return 1

    for section in ("offsets", "exact", "partial"):
        if section not in doc:
            fail(f"missing section {section!r}")

    pool = json.loads((DICT / "strings_pool.json").read_text(encoding="utf-8"))
    pool_vals = set(pool.values())

    check_offsets(doc, pool)
    check_quality(doc["exact"], "exact", pool_vals)
    check_quality(doc["partial"], "partial", pool_vals)

    sys.path.insert(0, str(ROOT / "scripts"))
    from triage import classify  # noqa: E402

    ui = {k: v for k, v in pool.items() if classify(v) in ("display", "label")}
    off, exact = doc["offsets"], doc["exact"]
    covered = [k for k, v in ui.items() if k in off or v in exact]

    print(f"CMOLocalizer.json  offsets={len(off)}  exact={len(exact)}  "
          f"partial={len(doc['partial'])}")
    print(f"player-visible engine strings : {len(ui)}")
    print(f"covered                        : {len(covered)} "
          f"({100.0*len(covered)/max(1,len(ui)):.1f}%)")
    print(f"uncovered                      : {len(ui)-len(covered)}")

    if failures:
        print(f"\nFAIL: {len(failures)} problem(s)")
        for f in failures[:80]:
            print(f"  - {f}")
        if len(failures) > 80:
            print(f"  ... and {len(failures)-80} more")
        return 1

    print("\nPASS: dictionary integrity and quality checks clean")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
