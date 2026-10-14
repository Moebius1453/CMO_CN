#!/usr/bin/env python3
"""Extract the Database Viewer description lines that still need translation.

The Database Viewer renders `DB/Descriptions/{DB3000,CWDB}/{Type}_{ID}.txt` into
the detail pane.  Each file is a sequence of blank-line separated blocks whose
first line carries a marker (OVERVIEW:, DETAILS:, NOTE:, SOURCE: ...).  The pane
renders one text node per line, so the work unit is a *line*:

  * the marker of a block's first line is stripped and translated by the engine,
    so the dictionary key is the line body only;
  * every other line is keyed verbatim.

Outputs
-------
dict/db_desc_work.json          {"<en line>": {"occ": n, "marker": "OVERVIEW"|null}}
dict/db_desc_chunks/chunkNNN.json  [{"i": <global index>, "en": "<en line>"}, ...]

Chunks are balanced by source character count, not by line count, and are
numbered sequentially so the work can be resumed batch by batch.
"""
from __future__ import annotations

import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GAME = Path("G:/soft/steam/steamapps/common/Command - Modern Operations")
DESC = GAME / "DB/Descriptions"
DICT = ROOT / "dict/CMOLocalizer.json"
WORK = ROOT / "dict/db_desc_work.json"
CHUNKS = ROOT / "dict/db_desc_chunks"

MARKER = re.compile(
    r"^(OVERVIEW|DETAIL|DETAILS|SPECIFICATION|SPECIFICATIONS|NOTE|NOTES|SOURCE|SOURCES)\s*:\s*",
    re.I)
BLANK = re.compile(r"\r?\n\s*\r?\n")
HAN = re.compile(r"[\u4e00-\u9fff]")
# Lines that carry no translatable English (pure numbers, unit-only columns).
NO_LETTERS = re.compile(r"^[^A-Za-z]*$")
CHUNK_CHARS = 12000


def load_known() -> set[str]:
    d = json.loads(DICT.read_text(encoding="utf-8"))
    known: set[str] = set()
    for section in ("exact", "partial"):
        for k, v in (d.get(section) or {}).items():
            if HAN.search(str(v)):
                known.add(re.sub(r"\s+", " ", k.strip()))
    return known


def collect(known: set[str]) -> tuple[dict, Counter]:
    work: dict[str, dict] = {}
    occ: Counter = Counter()
    stats = defaultdict(int)
    for sub in sorted(p for p in DESC.iterdir() if p.is_dir()):
        for f in sub.glob("*.txt"):
            text = f.read_bytes().decode("utf-8", "replace")
            for block in BLANK.split(text):
                lines = [ln.rstrip() for ln in block.splitlines()]
                for idx, raw in enumerate(lines):
                    line = raw.strip()
                    if not line or HAN.search(line) or NO_LETTERS.search(line):
                        continue
                    marker = None
                    if idx == 0:
                        m = MARKER.match(line)
                        if m:
                            marker = m.group(1).upper()
                            line = line[m.end():].strip()
                    if len(line) < 4 or HAN.search(line):
                        continue
                    key = re.sub(r"\s+", " ", line)
                    if key in known:
                        stats["already"] += 1
                        continue
                    entry = work.get(key)
                    if entry is None:
                        work[key] = {"occ": 1, "marker": marker}
                    else:
                        entry["occ"] += 1
                        if entry["marker"] is None and marker is not None:
                            entry["marker"] = marker
                    occ[key] += 1
                    stats["todo"] += 1
    return work, occ


def main() -> int:
    known = load_known()
    work, _ = collect(known)
    items = sorted(work.items(), key=lambda kv: (-kv[1]["occ"], -len(kv[0])))
    chars = sum(len(k) for k in work)
    print(f"todo lines: {len(work)}  chars: {chars}  already covered: skipped")

    WORK.write_text(json.dumps(work, ensure_ascii=False, indent=1), encoding="utf-8")

    CHUNKS.mkdir(exist_ok=True)
    for old in CHUNKS.glob("chunk*.json"):
        old.unlink()

    batches: list[list] = []
    current: list = []
    size = 0
    for i, (en, meta) in enumerate(items):
        current.append({"i": i, "en": en})
        size += len(en)
        if size >= CHUNK_CHARS:
            batches.append(current)
            current = []
            size = 0
    if current:
        batches.append(current)

    for n, batch in enumerate(batches, 1):
        (CHUNKS / f"chunk{n:03d}.json").write_text(
            json.dumps(batch, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"wrote {len(batches)} chunks of <= {CHUNK_CHARS} source chars -> {CHUNKS}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
