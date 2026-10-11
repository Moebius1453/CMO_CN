#!/usr/bin/env python3
"""Extract scenario metadata (titles, settings, dates) from every .scen/.campaign.

CMO stores scenario metadata as XML in the first lines of each .scen file; the
rest of the file is a base64 blob.  <ScenTitle> and <ScenSetting> are what the
scenario browser displays, so they are the translation surface for T6.
"""
from __future__ import annotations

import json
import re
import sys
from collections import Counter
from pathlib import Path

GAME = Path("G:/soft/steam/steamapps/common/Command - Modern Operations")
SCEN = GAME / "Scenarios"
TAG = re.compile(r"<(ScenTitle|ScenSetting|ScenDate|Complexity|Difficulty)>"
                 r"(.*?)</\1>", re.S)
CAMPAIGN_NAME = re.compile(r"<Name>(.*?)</Name>", re.S)


def main() -> int:
    root = Path(sys.argv[1]) if len(sys.argv) > 1 else SCEN
    titles: dict[str, list[str]] = {}
    settings: dict[str, list[str]] = {}
    campaigns: list[dict] = []

    for p in sorted(root.rglob("*.scen")):
        head = p.read_text(encoding="utf-8", errors="replace")[:4000]
        found = dict(TAG.findall(head))
        t = found.get("ScenTitle")
        s = found.get("ScenSetting")
        rel = str(p.relative_to(root))
        if t:
            titles.setdefault(t, []).append(rel)
        if s:
            settings.setdefault(s, []).append(rel)

    for p in sorted(root.rglob("*.campaign")):
        head = p.read_text(encoding="utf-8", errors="replace")[:20000]
        campaigns.append({
            "file": str(p.relative_to(root)),
            "names": CAMPAIGN_NAME.findall(head),
        })

    unique_t = {k: v[0] for k, v in titles.items()}
    unique_s = {k: v[0] for k, v in settings.items()}
    print(f"scen files      : {sum(len(v) for v in titles.values())}")
    print(f"unique titles   : {len(unique_t)}")
    print(f"unique settings : {len(unique_s)}")
    print(f"campaigns       : {len(campaigns)}")
    print("\n--- unique settings ---")
    for k in sorted(unique_s):
        print(f"  {k!r}  ({len(settings[k])} scenarios)")

    out = {
        "titles": unique_t,
        "settings": unique_s,
        "campaigns": campaigns,
    }
    dest = Path("dict/scenario_meta.json")
    dest.write_text(json.dumps(out, ensure_ascii=False, indent=1),
                    encoding="utf-8")
    print(f"\nwrote {dest}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
