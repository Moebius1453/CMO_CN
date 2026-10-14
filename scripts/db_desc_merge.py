#!/usr/bin/env python3
"""Merge translated Database Viewer description lines into dict/src.

Reads every `dict/db_desc_out/*.json` batch and writes
`dict/src/db_descriptions.json` as a plain `{ "<en line>": "<zh>" }` map, which
`scripts/build_dict.py` already merges into the shipped dictionary.

Accepted batch formats (both may be mixed across files):

    [{"i": 12, "zh": "..."}, ...]      index form (preferred: robust to typos)
    {"12": "..."}                      index-keyed object

Indices refer to the ordering produced by `scripts/db_desc_work.py`, which is
recomputed here so a batch never has to echo a long English key.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
WORK = ROOT / "dict/db_desc_work.json"
OUT_DIR = ROOT / "dict/db_desc_out"
TARGET = ROOT / "dict/src/db_descriptions.json"
HAN = re.compile(r"[\u4e00-\u9fff]")


def ordered_keys() -> list[str]:
    work = json.loads(WORK.read_text(encoding="utf-8"))
    items = sorted(work.items(), key=lambda kv: (-kv[1]["occ"], -len(kv[0])))
    return [k for k, _ in items]


def collect() -> tuple[dict[int, str], int]:
    got: dict[int, str] = {}
    batches = 0
    for p in sorted(OUT_DIR.glob("*.json")):
        if p.name.startswith("_"):
            continue
        try:
            data = json.loads(p.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            print(f"  !! {p.name}: {exc}")
            continue
        batches += 1
        if isinstance(data, dict):
            for k, v in data.items():
                if k.startswith("_"):
                    continue
                try:
                    got[int(k)] = str(v)
                except (TypeError, ValueError):
                    print(f"  !! {p.name}: bad index {k!r}")
        elif isinstance(data, list):
            for row in data:
                try:
                    got[int(row["i"])] = str(row["zh"])
                except (KeyError, TypeError, ValueError):
                    print(f"  !! {p.name}: bad row {row!r}")
    return got, batches


def main() -> int:
    keys = ordered_keys()
    got, batches = collect()
    merged: dict[str, str] = {}
    bad_han = 0
    bad_index = 0
    for i, zh in got.items():
        if i < 0 or i >= len(keys):
            bad_index += 1
            continue
        if not zh or not HAN.search(zh):
            bad_han += 1
            continue
        merged[keys[i]] = zh

    if merged:
        existing = {}
        if TARGET.is_file():
            try:
                existing = json.loads(TARGET.read_text(encoding="utf-8"))
            except json.JSONDecodeError:
                existing = {}
        existing.update(merged)
        TARGET.write_text(json.dumps(existing, ensure_ascii=False, indent=1),
                          encoding="utf-8")

    total = sum(len(k) for k in keys)
    print(f"batches read      : {batches}")
    print(f"translations found: {len(got)}")
    print(f"merged this run   : {len(merged)}  (skipped: no-Han {bad_han}, bad index {bad_index})")
    if TARGET.is_file():
        allmerged = json.loads(TARGET.read_text(encoding="utf-8"))
        done = sum(len(k) for k in allmerged)
        print(f"cumulative lines  : {len(allmerged)} / {len(keys)}  ({done} / {total} chars = {done * 100.0 / total:.1f}%)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
