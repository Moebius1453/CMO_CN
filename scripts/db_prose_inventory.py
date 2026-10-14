#!/usr/bin/env python3
"""Measure the Database Viewer prose backlog under DB/Descriptions.

Prints per-directory totals: files, bytes, characters, and the character split
between the long narrative blocks (OVERVIEW/DETAIL/NOTE prose) and the
structured label:value specification lines.
"""
from __future__ import annotations

import re
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path("G:/soft/steam/steamapps/common/Command - Modern Operations/DB/Descriptions")

LABEL = re.compile(r"^(OVERVIEW|DETAILS?|SPECIFICATIONS?|NOTES?|SOURCES?|Fact|IOC|Operators|Platforms?|"
                   r"Conflicts|Length|Diameter|Wingspan|Weight|Speed|Range|Guidance|Warhead|"
                   r"Maneuverability|Ceiling|Crew|Cargo|Displacement|Beam|Draught|Draft|"
                   r"Complement|Propulsion|Sensors|Armament|Aircraft|Helicopters)\s*:", re.I)


def main() -> int:
    per = defaultdict(lambda: [0, 0, 0, 0, 0])  # files, bytes, chars, prose, spec
    for sub in sorted(p for p in ROOT.iterdir() if p.is_dir()):
        for f in sub.glob("*.txt"):
            raw = f.read_bytes()
            try:
                text = raw.decode("utf-8")
            except UnicodeDecodeError:
                text = raw.decode("utf-8", "replace")
            stat = per[sub.name]
            stat[0] += 1
            stat[1] += len(raw)
            stat[2] += len(text)
            for block in text.split("\n\n"):
                s = block.strip()
                if not s:
                    continue
                if LABEL.match(s):
                    stat[4] += len(s)
                else:
                    stat[3] += len(s)
    tot = [0, 0, 0, 0, 0]
    print(f"{'dir':<12}{'files':>7}{'MB':>8}{'chars':>12}{'prose':>12}{'spec':>12}")
    for name, s in per.items():
        for i, v in enumerate(s):
            tot[i] += v
        print(f"{name:<12}{s[0]:>7}{s[1]/1048576:>8.1f}{s[2]:>12}{s[3]:>12}{s[4]:>12}")
    print(f"{'TOTAL':<12}{tot[0]:>7}{tot[1]/1048576:>8.1f}{tot[2]:>12}{tot[3]:>12}{tot[4]:>12}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
