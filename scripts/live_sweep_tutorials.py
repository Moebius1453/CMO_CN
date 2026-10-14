#!/usr/bin/env python3
"""Live regression sweep over every tutorial scenario.

For each tutorial: relaunch the game, load the scenario through the in-process
command channel, capture the briefing (LoadScenario preview) and the first
special-message popup, then record the window snapshot.

Usage:  python scripts/live_sweep_tutorials.py [--limit N] [--only SUBSTR]
"""
from __future__ import annotations

import json
import re
import subprocess
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import live_harness as H  # noqa: E402
import live_load_tutorial as L  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
GAME = H.GAME
OUT = ROOT / "evidence/live/v413/tutorials"
REPORT = ROOT / "build/tutorial_sweep_v413.json"
HAN = re.compile(r"[\u4e00-\u9fff]")
# Tutorial titles as they appear in the scenario browser (Chinese, from the dict).
QUERY = {
    "Electronic Warfare Tutorial": "Electronic Warfare",
    "Flight Tutorial 1 - Basic Manual Flight": "Flight Tutorial 1",
    "Flight Tutorial 2 - Unattended Flight": "Flight Tutorial 2",
    "Flight Tutorial 3 - AAW 1 - Simple Air Intercept": "Flight Tutorial 3",
    "Flight Tutorial 7 - AAW 5 - Modern Russian and Chinese Weapons": "Flight Tutorial 7",
    "Strike Tutorial 1 - Bombing Range": "Strike Tutorial 1",
    "Submarine Tutorial 1.1": "Submarine Tutorial 1.1",
    "Surface Tutorial 1": "Surface Tutorial 1",
    "1 - NATO Surface Group": "NATO Surface Group",
}


def tutorial_list(limit: int, only: str) -> list[str]:
    base = GAME / "Scenarios/Tutorials"
    files = sorted(p.name[:-5] for p in base.rglob("*.scen"))
    if only:
        files = [f for f in files if only.lower() in f.lower()]
    if limit:
        files = files[:limit]
    return files


def main() -> int:
    limit = 0
    only = ""
    for i, a in enumerate(sys.argv):
        if a == "--limit" and i + 1 < len(sys.argv):
            limit = int(sys.argv[i + 1])
        if a == "--only" and i + 1 < len(sys.argv):
            only = sys.argv[i + 1]
    OUT.mkdir(parents=True, exist_ok=True)
    tuts = tutorial_list(limit, only)
    print(f"tutorials: {len(tuts)}", flush=True)

    results = []
    for idx, name in enumerate(tuts):
        slug = re.sub(r"[^A-Za-z0-9]+", "_", name)[:48]
        entry = {"index": idx + 1, "title": name, "status": "pending"}
        try:
            pid = H.restart_game(None, wait=120)
            time.sleep(3)
            L.dismiss_launcher(pid)
            L.open_load_scenario(pid)
            query = QUERY.get(name, name.split(" - ")[0])
            H.send("SCEN_EXPAND_ALL", timeout=30)
            time.sleep(1.0)
            sel = H.send("SCEN_SELECT " + query, timeout=30)
            entry["selected"] = sel.get("status")
            time.sleep(1.5)
            wins = [w for w in H.big_visible_windows(pid) if w["rect"]["Width"] >= 1000]
            if wins:
                H.shot(f"tutorials/{idx + 1:02d}_{slug}_briefing", hwnd=wins[0]["hwnd"], out_dir=OUT)
            load = H.send("SCEN_LOAD", timeout=25)
            entry["load"] = load.get("status")
            time.sleep(10)
            entered = L._retry(lambda: H.send("CLICK_BUTTON 进入想定", timeout=15), 12, 3.0, "enter")
            entry["enter"] = entered.get("status") if isinstance(entered, dict) else "?"
            time.sleep(6)
            for _ in range(10):
                if H.send("CLICK_WPF 开始推演", timeout=25).get("clicked", 0) > 0:
                    break
                time.sleep(2.0)
            time.sleep(4)
            msgs = []
            for label in ("关闭并继续", "关闭"):
                r = H.send("CLICK_BUTTON " + label, timeout=12)
                if r.get("status") == "ok":
                    msgs.append(label)
                    break
            entry["closed_msg"] = msgs
            time.sleep(2)
            wins = [w for w in H.big_visible_windows(pid) if w["rect"]["Width"] >= 900]
            if len(wins) > 1:
                H.shot(f"tutorials/{idx + 1:02d}_{slug}_message", hwnd=wins[1]["hwnd"], out_dir=OUT)
            snap = H.send("DUMP_OPEN_FORMS", timeout=20)
            text = json.dumps(snap, ensure_ascii=False)
            entry["forms"] = [f.get("type") for f in snap.get("forms", [])]
            entry["has_chinese"] = bool(HAN.search(text))
            entry["status"] = "ok"
        except Exception as ex:
            entry["status"] = "error"
            entry["error"] = str(ex)[:200]
        results.append(entry)
        print(f"[{idx + 1}/{len(tuts)}] {name}: {entry['status']}", flush=True)
        REPORT.write_text(json.dumps(results, ensure_ascii=False, indent=1), encoding="utf-8")

    ok = sum(1 for r in results if r["status"] == "ok")
    print(f"done: {ok}/{len(results)} ok")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
