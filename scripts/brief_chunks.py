#!/usr/bin/env python3
"""Split the briefing work list into balanced translation chunks.

Chunks are balanced by character count (not entry count) so a chunk full of long
paragraphs is the same effort as one full of list items.  Entries are ordered
short-first, so early chunks cover headings, location lines and objective bullets
before the long narrative blocks.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "dict/brief_work.json"
OUT = ROOT / "dict/brief_chunks"


def main(n: int = 10) -> int:
    work = json.loads(SRC.read_text(encoding="utf-8"))
    items = sorted(work.items(), key=lambda kv: (len(kv[0]), kv[0]))
    total = sum(len(k) for k, _ in items)
    target = total / n
    OUT.mkdir(exist_ok=True)
    chunks: list[list] = [[] for _ in range(n)]
    sizes = [0] * n
    idx = 0
    for k, files in items:
        # Sequential fill: keeps long blocks together and avoids interleaving.
        if idx < n - 1 and sizes[idx] >= target:
            idx += 1
        chunks[idx].append((k, files))
        sizes[idx] += len(k)

    for i, chunk in enumerate(chunks, 1):
        if not chunk:
            continue
        doc = {k: files for k, files in chunk}
        p = OUT / f"brief{i:02d}.json"
        p.write_text(json.dumps(doc, ensure_ascii=False, indent=0,
                                sort_keys=False), encoding="utf-8")
        chars = sum(len(k) for k in doc)
        print(f"  {p.name}: {len(doc):>4} strings  {chars:>7} chars")
    print(f"total {len(items)} strings / {total} chars in {n} chunks")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(int(sys.argv[1]) if len(sys.argv) > 1 else 10))
