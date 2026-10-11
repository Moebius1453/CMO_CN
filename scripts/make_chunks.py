#!/usr/bin/env python3
"""Split dict/worklist.json into ordered translation chunks for parallel work.

Ordering is by English length ascending: short strings are the labels, column
headers and buttons a player meets first, so they get translated (and reviewed)
before the long editor diagnostics.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DICT = ROOT / "dict"
OUT = DICT / "chunks"


def main(n: int = 8) -> int:
    work = json.loads((DICT / "worklist.json").read_text(encoding="utf-8"))
    items = sorted(work.items(), key=lambda kv: (len(kv[1]), int(kv[0])))
    OUT.mkdir(exist_ok=True)
    size = (len(items) + n - 1) // n
    total = 0
    for i in range(n):
        chunk = items[i * size:(i + 1) * size]
        if not chunk:
            continue
        # Offset -> English, so the builder can place entries in `offsets`.
        doc = {off: en for off, en in chunk}
        p = OUT / f"chunk{i+1:02d}.json"
        p.write_text(json.dumps(doc, ensure_ascii=False, indent=0),
                     encoding="utf-8")
        lo = min(len(e) for _, e in chunk)
        hi = max(len(e) for _, e in chunk)
        total += len(chunk)
        print(f"  {p.name}: {len(chunk):>4} strings  len {lo}-{hi}")
    print(f"total {total} strings across {n} chunks")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(int(sys.argv[1]) if len(sys.argv) > 1 else 8))
