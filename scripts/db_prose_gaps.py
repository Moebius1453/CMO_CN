#!/usr/bin/env python3
"""Count the distinct Database Viewer prose blocks that still need translation."""
from __future__ import annotations

import json
import re
import sys
from collections import Counter
from pathlib import Path

ROOT = Path("G:/soft/steam/steamapps/common/Command - Modern Operations/DB/Descriptions")
DICT = Path(__file__).resolve().parent.parent / "dict/CMOLocalizer.json"

HEAD = re.compile(r"^(OVERVIEW|DETAIL|DETAILS|SPECIFICATION|SPECIFICATIONS|NOTE|NOTES|SOURCE|SOURCES)\s*:\s*", re.I)
LABEL_LINE = re.compile(r"^[A-Z][A-Za-z /()\-]{1,28}:")


def has_cjk(s: str) -> bool:
    return any("\u4e00" <= ch <= "\u9fa5" for ch in s)


def main() -> int:
    known = set()
    d = json.loads(DICT.read_text(encoding="utf-8"))
    for section in ("exact", "partial"):
        for k, v in (d.get(section) or {}).items():
            if has_cjk(str(v)):
                known.add(re.sub(r"\s+", " ", k.strip()))
    print(f"dict entries with CJK: {len(known)}")

    blocks: Counter[str] = Counter()
    for sub in sorted(p for p in ROOT.iterdir() if p.is_dir()):
        for f in sub.glob("*.txt"):
            text = f.read_bytes().decode("utf-8", "replace")
            for block in text.split("\n\n"):
                s = block.strip()
                if not s or has_cjk(s):
                    continue
                m = HEAD.match(s)
                if not m:
                    continue
                body = s[m.end():].strip()
                if len(body) < 30:
                    continue
                # A head-block whose body is only label: lines is spec data, not prose.
                lines = [ln for ln in body.split("\n") if ln.strip()]
                if lines and all(LABEL_LINE.match(ln.strip()) for ln in lines):
                    continue
                blocks[re.sub(r"\s+", " ", body)] += 1

    total = sum(len(k) for k in blocks)
    missing = [k for k in blocks if k not in known]
    print(f"distinct prose blocks: {len(blocks)}  chars: {total}")
    print(f"already in dict : {len(blocks) - len(missing)}")
    print(f"still missing   : {len(missing)}  chars: {sum(len(k) for k in missing)}")
    sizes = sorted(len(k) for k in missing)
    if sizes:
        print(f"char size: min={sizes[0]} p50={sizes[len(sizes)//2]} p90={sizes[int(len(sizes)*0.9)]} max={sizes[-1]}")
    out = Path(__file__).resolve().parent.parent / "build/db_prose_missing.json"
    out.write_text(json.dumps({k: v for k, v in blocks.items() if k in set(missing)},
                              ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"wrote {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
