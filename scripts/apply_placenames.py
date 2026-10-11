#!/usr/bin/env python3
"""Apply translated placenames to the game's World Wind .wwp files.

Backs up each file as `<name>.wwp.orig` on first apply (so the whole map
localization is reversible with one command) and asserts the entry count and
coordinates are unchanged afterwards -- only the names may differ.

    python apply_placenames.py            apply country + city names
    python apply_placenames.py --verify   report only, change nothing
    python apply_placenames.py --revert   restore every .wwp from .orig
"""
from __future__ import annotations

import argparse
import json
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GAME = Path("G:/soft/steam/steamapps/common/Command - Modern Operations")
SETS = GAME / "WW/PlacenameSets"

sys.path.insert(0, str(ROOT / "scripts"))
from wwp import load, save  # noqa: E402

HAN = __import__("re").compile(r"[\u4e00-\u9fff]")


def gather() -> dict[str, str]:
    table: dict[str, str] = {}
    country = json.loads((ROOT / "dict/placenames_countries.json").read_text(
        encoding="utf-8"))
    table.update({k: v for k, v in country.items() if not k.startswith("_")})
    city_dir = ROOT / "dict/city_src"
    if city_dir.is_dir():
        for p in sorted(city_dir.glob("*.json")):
            data = json.loads(p.read_text(encoding="utf-8"))
            table.update({k: v for k, v in data.items()
                          if isinstance(v, str) and not k.startswith("_")})
    return table


def process(rel: str, table: dict[str, str], verify: bool) -> None:
    path = SETS / rel
    if not path.is_file():
        print(f"  {rel}: MISSING")
        return
    items = load(path)
    before = [(i["lon"], i["lat"], i["flags"]) for i in items]
    renamed = 0
    for it in items:
        new = table.get(it["name"])
        if new and new != it["name"]:
            it["name"] = new
            renamed += 1
    after = [(i["lon"], i["lat"], i["flags"]) for i in items]
    assert before == after, f"{rel}: coordinates changed"
    already = sum(1 for i in items if HAN.search(i["name"]))
    print(f"  {rel}: {len(items)} entries, {renamed} to rename, "
          f"{already} already Chinese, coords verified")
    if verify or renamed == 0:
        return
    bak = Path(str(path) + ".orig")
    if not bak.exists():
        shutil.copy(path, bak)
    save(path, items)
    check = load(path)
    assert len(check) == len(items), f"{rel}: entry count changed"
    assert [(i["lon"], i["lat"], i["flags"]) for i in check] == after, \
        f"{rel}: coordinates changed on write"
    not_chinese = [i["name"] for i in check if not HAN.search(i["name"])]
    print(f"    wrote {rel} (backup at {bak.name}); "
          f"{len(check) - len(not_chinese)}/{len(check)} names Chinese")


def revert() -> None:
    n = 0
    for bak in sorted(SETS.rglob("*.wwp.orig")):
        target = Path(str(bak)[:-5])
        shutil.copy(bak, target)
        n += 1
        print(f"  restored {target.relative_to(SETS)}")
    print(f"reverted {n} files")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--verify", action="store_true")
    ap.add_argument("--revert", action="store_true")
    args = ap.parse_args()

    if args.revert:
        revert()
        return 0

    table = gather()
    print(f"translation table: {len(table)} names")
    for rel in ("Synoptic/0000_0000.wwp", "Cities/0000_0000.wwp"):
        process(rel, table, args.verify)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
