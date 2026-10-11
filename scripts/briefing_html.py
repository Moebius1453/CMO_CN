#!/usr/bin/env python3
"""Structure-preserving HTML briefing translation harness.

The scenario briefings are hand-written HTML with narrative prose inside <p>,
<li>, <h2> ... tags.  Rewriting them with a whole-file substitution would break
markup, so this tool treats the document as an alternating sequence of "markup"
and "text" runs, translates only the text runs, and rebuilds the file byte-for-
byte from the original markup.

Commands:
    extract <scenario-root> -o brief_work.json [--only-untranslated]
        Emit {text: [file-relative keys]} for every translatable text run.

    apply <scenario-root> <translations.json>
        Rewrite every briefing, substituting translated text runs.  Files whose
        every run is already translated are skipped.

Original English is preserved as `<file>.orig` on first apply.
"""
from __future__ import annotations

import argparse
import html
import json
import re
from pathlib import Path

# A run of markup (tag, comment, doctype) or a run of text between them.
TOKEN = re.compile(r"<!--.*?-->|<[^>]*>", re.S)
SKIP_BLOCKS = re.compile(r"<(script|style)\b.*?</\1>", re.S | re.I)
# Entities that must survive verbatim.
ENTITY = re.compile(r"&(#\d+|[A-Za-z]+);")
# Leading / trailing padding that carries layout intent (indentation, &nbsp;).
PADDING = (re.compile(r"^(?:\s|&nbsp;|&#160;)+"),
           re.compile(r"(?:\s|&nbsp;|&#160;)+$"))
HAS_WORD = re.compile(r"[A-Za-z]{2}")
HAN = re.compile(r"[\u4e00-\u9fff]")


def split_padding(text: str) -> tuple[str, str]:
    """Return (leading, trailing) layout padding of a raw text run."""
    lead = PADDING[0].match(text)
    trail = PADDING[1].search(text)
    l = lead.group(0) if lead else ""
    t = trail.group(0) if trail else ""
    if len(l) + len(t) > len(text):
        return "", ""
    return l, t


def segments(doc: str) -> list[tuple[int, str]]:
    """Return (position, text) for every translatable text run."""
    out: list[tuple[int, str]] = []
    skip: list[tuple[int, int]] = [(m.start(), m.end()) for m in SKIP_BLOCKS.finditer(doc)]

    def in_skip(pos: int) -> bool:
        return any(a <= pos < b for a, b in skip)

    for m in TOKEN.finditer(doc):
        pass
    pos = 0
    for m in TOKEN.finditer(doc):
        text = doc[pos:m.start()]
        if text and not in_skip(pos) and HAS_WORD.search(text) and not HAN.search(text):
            out.append((pos, text))
        pos = m.end()
    tail = doc[pos:]
    if tail and HAS_WORD.search(tail) and not HAN.search(tail):
        out.append((pos, tail))
    return out


def key_of(text: str) -> str:
    """Normalised lookup key: collapse whitespace and decode entities."""
    return re.sub(r"\s+", " ", html.unescape(text)).strip()


def rebuild(doc: str, table: dict[str, str]) -> tuple[str, int, int]:
    """Replace translatable runs using `table`; returns (new_doc, hit, miss)."""
    parts: list[str] = []
    pos = 0
    hit = miss = 0
    for m in TOKEN.finditer(doc):
        text = doc[pos:m.start()]
        new = table.get(key_of(text))
        if new is not None and HAS_WORD.search(text) and not HAN.search(text):
            lead, trail = split_padding(text)
            parts.append(lead + new.strip() + trail)
            hit += 1
        else:
            if text and HAS_WORD.search(text) and not HAN.search(text):
                miss += 1
            parts.append(text)
        parts.append(m.group(0))
        pos = m.end()
    parts.append(doc[pos:])
    return "".join(parts), hit, miss


def extract(root: Path, only_untranslated: bool) -> dict[str, list[str]]:
    work: dict[str, list[str]] = {}
    for f in sorted(root.rglob("*.html")):
        if f.name.endswith(".orig"):
            continue
        doc = f.read_text(encoding="utf-8", errors="replace")
        if only_untranslated and HAN.search(doc):
            continue
        rel = str(f.relative_to(root))
        for _pos, text in segments(doc):
            k = key_of(text)
            if not k or not HAS_WORD.search(k):
                continue
            work.setdefault(k, [])
            if rel not in work[k]:
                work[k].append(rel)
    return work


def apply(root: Path, table: dict[str, str]) -> None:
    total_hit = total_miss = files = 0
    for f in sorted(root.rglob("*.html")):
        if f.name.endswith(".orig"):
            continue
        doc = f.read_text(encoding="utf-8", errors="replace")
        new, hit, miss = rebuild(doc, table)
        if hit == 0 or new == doc:
            continue
        bak = Path(str(f) + ".orig")
        if not bak.exists():
            bak.write_text(doc, encoding="utf-8")
        f.write_text(new, encoding="utf-8")
        files += 1
        total_hit += hit
        total_miss += miss
        print(f"  {f.relative_to(root)}: {hit} translated, {miss} left")


def main() -> int:
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    e = sub.add_parser("extract")
    e.add_argument("root")
    e.add_argument("-o", "--out", default="dict/brief_work.json")
    e.add_argument("--only-untranslated", action="store_true")
    a = sub.add_parser("apply")
    a.add_argument("root")
    a.add_argument("translations")
    args = ap.parse_args()

    root = Path(args.root)
    if args.cmd == "extract":
        work = extract(root, args.only_untranslated)
        Path(args.out).write_text(
            json.dumps(work, ensure_ascii=False, indent=0, sort_keys=True),
            encoding="utf-8")
        chars = sum(len(k) for k in work)
        print(f"wrote {args.out}: {len(work)} unique strings, {chars} chars")
    else:
        table = json.loads(Path(args.translations).read_text(encoding="utf-8"))
        table = {k: v for k, v in table.items()
                 if isinstance(v, str) and not k.startswith("_")}
        apply(root, table)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
