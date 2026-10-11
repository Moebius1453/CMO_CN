#!/usr/bin/env python3
"""Purge engine identifiers that must never be translated.

Discovery (bisect over the offset table): translating the INI section header
`[Game Preferences]` makes the game throw a NullReferenceException during
`Application_Startup`.  The previous session had also produced a corrupted
`配置/Command.ini` in which section headers and key names were translated
(`[General]` -> `[常规作战条令]`, `SonobuoyVisibility` -> `声呐浮标可见度`),
and a Chinese-named directory to hold it.

So the identifier surface is:
  * INI section names and key names from Config/*.ini
  * directory and file names that exist on disk under the game root
  * log / registry / database identifiers found in the pool

This script builds that deny-list from live evidence and writes it to
dict/src/identifiers.json, which build_dict.py merges into the blacklist.
"""
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DICT = ROOT / "dict"
GAME = Path("G:/soft/steam/steamapps/common/Command - Modern Operations")

SECTION = re.compile(r"^\s*\[(.+?)\]\s*$")
KEYVAL = re.compile(r"^\s*([A-Za-z_][\w .\-]*?)\s*=")


def ini_identifiers() -> set[str]:
    out: set[str] = set()
    cfg = GAME / "Config"
    if not cfg.is_dir():
        return out
    for f in sorted(cfg.glob("*.ini")):
        for line in f.read_text(encoding="utf-8", errors="replace").splitlines():
            m = SECTION.match(line)
            if m:
                out.add(m.group(1).strip())
                continue
            m = KEYVAL.match(line)
            if m:
                out.add(m.group(1).strip())
    return out


def corrupt_config_identifiers() -> set[str]:
    """Section/key names the previous session corrupted into Chinese.

    The English originals are recovered from the intact Config/Command.ini, so
    this mostly guards against re-introducing the same keys; the Chinese forms
    are also collected so they can be reported.
    """
    out: set[str] = set()
    for d in GAME.glob("配置"):
        if not d.is_dir():
            continue
        for f in d.glob("*.ini"):
            for line in f.read_text(encoding="utf-8", errors="replace").splitlines():
                m = SECTION.match(line)
                if m and re.search(r"[\u4e00-\u9fff]", m.group(1)):
                    out.add(m.group(1).strip())
    return out


def filesystem_names() -> set[str]:
    """Path components that must stay verbatim.

    Translating a string the engine uses as a path component breaks the lookup:
    the previous session produced a Chinese-named `配置/` directory because
    `Config` was translated.  Only directories and configuration/database file
    names are collected -- arbitrary data-file stems produced false positives
    (a scenario file called `Air Base.scen` would have blocked the legitimate
    `Air Base` facility label).
    """
    out: set[str] = set()
    for p in GAME.rglob("*"):
        if p.is_dir():
            out.add(p.name)
    for p in GAME.glob("*"):
        if p.is_file():
            out.add(p.name)
            if p.suffix.lower() in (".exe", ".ini", ".config", ".dat"):
                out.add(p.stem)
    return out


def main() -> int:
    ini = ini_identifiers()
    corrupted = corrupt_config_identifiers()
    fs = filesystem_names()

    pool = json.loads((DICT / "strings_pool.json").read_text(encoding="utf-8"))
    pool_values = set(pool.values())
    doc = json.loads((DICT / "CMOLocalizer.json").read_text(encoding="utf-8"))

    # Only identifiers that actually appear in the engine pool matter.
    hits = sorted((ini | fs) & pool_values)
    corrupted_hits = sorted(corrupted & pool_values)

    # Report which are currently translated -- these are live defects.
    live = [v for v in hits if v in doc["exact"] or
            any(pool[k] == v for k in doc["offsets"])]

    print(f"ini identifiers      : {len(ini)}")
    print(f"corrupted (Chinese)  : {len(corrupted)}")
    print(f"filesystem names     : {len(fs)}")
    print(f"  present in pool    : {len(hits)}")
    print(f"  currently translated: {len(live)}")
    for v in live[:40]:
        zh = doc["exact"].get(v)
        print(f"    TRANSLATED: {v!r} -> {zh!r}")
    if corrupted_hits:
        print("  corrupted CN identifiers still in pool:", corrupted_hits)

    out = {
        "_note": "引擎标识符禁译表：INI 段名/键名、磁盘目录与文件名。"
                 "翻译它们会导致配置读取失败（启动 NRE）或路径解析失败。",
        "identifiers": hits,
    }
    dest = DICT / "src/identifiers.json"
    dest.write_text(json.dumps(out, ensure_ascii=False, indent=1),
                    encoding="utf-8")
    print(f"\nwrote {dest} ({len(hits)} identifiers)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
