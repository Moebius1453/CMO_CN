#!/usr/bin/env python3
"""Audit translation coverage of Database Viewer prose under DB/Descriptions."""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GAME = Path("G:/soft/steam/steamapps/common/Command - Modern Operations")
DESC = GAME / "DB/Descriptions"
DICT = ROOT / "dict/CMOLocalizer.json"

BLANK = re.compile(r"\r?\n\s*\r?\n")
MARKER = re.compile(
    r"^(OVERVIEW|DETAIL|DETAILS|SPECIFICATION|SPECIFICATIONS|NOTE|NOTES|SOURCE|SOURCES)\s*:\s*",
    re.I)
HAN = re.compile(r"[\u4e00-\u9fff]")
NO_LETTERS = re.compile(r"^[^A-Za-z]*$")


def main() -> int:
    d = json.loads(DICT.read_text(encoding="utf-8"))
    exact = d.get("exact") or {}

    total_files = 0
    total_lines = 0
    hit_lines = 0
    total_chars = 0
    hit_chars = 0
    by_category: dict[str, list[int]] = {}

    for sub in sorted(p for p in DESC.iterdir() if p.is_dir()):
        for f in sub.glob("*.txt"):
            total_files += 1
            kind = re.match(r"([A-Za-z]+)_", f.name)
            cat = kind.group(1).capitalize() if kind else sub.name
            if cat not in by_category:
                by_category[cat] = [0, 0, 0, 0]  # total_l, hit_l, total_c, hit_c

            text = f.read_bytes().decode("utf-8", "replace")
            for block in BLANK.split(text):
                lines = [ln.rstrip() for ln in block.splitlines()]
                for idx, raw in enumerate(lines):
                    line = raw.strip()
                    if not line or HAN.search(line) or NO_LETTERS.search(line) or len(line) < 4:
                        continue
                    if idx == 0:
                        m = MARKER.match(line)
                        if m:
                            line = line[m.end():].strip()
                    if len(line) < 4:
                        continue
                    key = re.sub(r"\s+", " ", line)
                    total_lines += 1
                    total_chars += len(key)
                    by_category[cat][0] += 1
                    by_category[cat][2] += len(key)
                    if key in exact:
                        hit_lines += 1
                        hit_chars += len(key)
                        by_category[cat][1] += 1
                        by_category[cat][3] += len(key)

    print("=" * 70)
    print("CMO Database Descriptions Translation Audit Report")
    print("=" * 70)
    print(f"Total description files scanned: {total_files}")
    print(f"Total translatable text lines:  {total_lines:,}")
    print(f"Translated lines in dictionary: {hit_lines:,} ({hit_lines * 100.0 / total_lines:.2f}%)")
    print(f"Translated text volume (chars): {hit_chars:,} / {total_chars:,} ({hit_chars * 100.0 / total_chars:.2f}%)")
    print("-" * 70)
    print(f"{'Category':<15}{'Total Lines':>14}{'Hit Lines':>14}{'Coverage %':>15}{'Chars Cov %':>12}")
    print("-" * 70)
    for cat in sorted(by_category, key=lambda c: -by_category[c][0]):
        stats = by_category[cat]
        line_pct = stats[1] * 100.0 / stats[0] if stats[0] else 0.0
        char_pct = stats[3] * 100.0 / stats[2] if stats[2] else 0.0
        print(f"{cat:<15}{stats[0]:>14,}{stats[1]:>14,}{line_pct:>14.2f}%{char_pct:>11.2f}%")
    print("=" * 70)
    return 0


if __name__ == "__main__":
    sys.exit(main())
