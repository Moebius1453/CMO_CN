#!/usr/bin/env python3
"""Bisect the offset table to find entries that break game start-up.

Symptom under investigation: with the localization dictionary deployed the game
shows a modal "beta-1 application startup" error dialog (NullReferenceException)
before the main menu; with the localizer disabled it does not.

Each probe deploys a dictionary containing a candidate subset of offsets,
launches Command.exe, waits for the start window, and looks for the error dialog
by enumerating the game's own top-level windows.

Usage:
    python bisect_offsets.py --once            # deploy the current dict and report
    python bisect_offsets.py --empty           # deploy with offsets emptied
    python bisect_offsets.py --bisect          # binary-search the first bad offset
"""
from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GAME = Path("G:/soft/steam/steamapps/common/Command - Modern Operations")
DICT = ROOT / "dict/CMOLocalizer.json"
PROBE = ROOT / "build/WinProbe.exe"
BACKUP = Path("/tmp/cmo_bisect_backup.json")


def deploy(offsets: dict[str, str], exact: dict[str, str]) -> None:
    doc = json.loads(DICT.read_text(encoding="utf-8"))
    doc["offsets"] = offsets
    doc["exact"] = exact
    tmp = ROOT / "build/bisect_dict.json"
    tmp.write_text(json.dumps(doc, ensure_ascii=False, indent=1) + "\n",
                   encoding="utf-8")
    shutil.copy(tmp, GAME / "CMOLocalizer.json")


def launch_and_probe(wait: float = 42.0) -> bool:
    """Return True when the startup error dialog is present."""
    subprocess.run(["taskkill", "/F", "/IM", "Command.exe"],
                   capture_output=True)
    time.sleep(2)
    subprocess.Popen(["cmd", "/c", "start", "", str(GAME / "Command.exe")],
                     cwd=str(GAME), shell=False)
    time.sleep(wait)
    out = subprocess.run([str(PROBE), "probe"], capture_output=True, text=True,
                         errors="replace").stdout
    bad = "Application startup" in out or "应用程序启动" in out
    titles = [l.split("title=", 1)[1] for l in out.splitlines() if "vis=True" in l]
    print(f"    visible: {titles}")
    return bad


def kill() -> None:
    subprocess.run(["taskkill", "/F", "/IM", "Command.exe"], capture_output=True)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--once", action="store_true")
    ap.add_argument("--empty", action="store_true")
    ap.add_argument("--bisect", action="store_true")
    ap.add_argument("--wait", type=float, default=42.0)
    args = ap.parse_args()

    doc = json.loads(DICT.read_text(encoding="utf-8"))
    offsets = doc["offsets"]
    exact = doc["exact"]
    BACKUP.write_text(json.dumps({"offsets": offsets, "exact": exact},
                                 ensure_ascii=False), encoding="utf-8")

    if args.empty:
        print("probe: empty offsets, full exact")
        deploy({}, exact)
        bad = launch_and_probe(args.wait)
        kill()
        print("RESULT:", "ERROR DIALOG PRESENT" if bad else "clean start")
        print("=> offsets injection is",
              "not the cause" if bad else "the cause")
        deploy(offsets, exact)
        return 0

    if args.once:
        print("probe: current dictionary")
        bad = launch_and_probe(args.wait)
        kill()
        print("RESULT:", "ERROR DIALOG PRESENT" if bad else "clean start")
        return 0

    # Binary search: find the smallest prefix of offsets that triggers the error.
    keys = sorted(offsets, key=lambda k: int(k))
    print(f"bisecting {len(keys)} offsets (each probe ~{args.wait:.0f}s)")

    lo, hi = 0, len(keys)          # lo is known good (empty), hi assumed bad
    print("probe: empty offsets (expect clean)")
    deploy({}, exact)
    if not launch_and_probe(args.wait):
        print("  empty offsets is CLEAN -> injection causes the error")
    else:
        print("  empty offsets ALSO fails -> the cause is not the offsets table")
        kill()
        deploy(offsets, exact)
        return 0
    kill()

    while lo + 1 < hi:
        mid = (lo + hi) // 2
        subset = {k: offsets[k] for k in keys[:mid]}
        print(f"probe: first {mid} offsets")
        deploy(subset, exact)
        if launch_and_probe(args.wait):
            hi = mid
        else:
            lo = mid
        kill()
        if hi - lo <= 1:
            break

    if hi < len(keys):
        bad_key = keys[lo]
        print(f"\nFIRST BAD OFFSET: {bad_key}")
        print(f"  source : {json.loads((ROOT / 'dict/strings_pool.json').read_text(encoding='utf-8')).get(bad_key)!r}")
        print(f"  target : {offsets[bad_key]!r}")
    else:
        print("\nno single prefix boundary found")

    deploy(offsets, exact)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
