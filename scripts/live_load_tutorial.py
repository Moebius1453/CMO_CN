#!/usr/bin/env python3
"""Load a CMO scenario/tutorial in the live game and drive the entry dialogs.

Usage:  python scripts/live_load_tutorial.py "Electronic Warfare" [--no-restart]
"""
from __future__ import annotations

import json
import subprocess
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import live_harness as H  # noqa: E402


def _retry(fn, tries: int = 20, delay: float = 1.5, label: str = ""):
    last = None
    for _ in range(tries):
        last = fn()
        if isinstance(last, dict) and last.get("status") == "ok":
            return last
        time.sleep(delay)
    print(f"[warn] step failed: {label} -> {json.dumps(last, ensure_ascii=False)[:160]}", flush=True)
    return last


def dismiss_launcher(pid: int) -> None:
    for _ in range(15):
        for w in H.list_windows(pid):
            if w["visible"] and "HwndWrapper" in w["cls"] and w["rect"]["Width"] > 500:
                r = w["rect"]
                H.click(r["X"] + 1005, r["Y"] + 18)
        time.sleep(1.0)
        if not any(w["visible"] and "HwndWrapper" in w["cls"] and w["rect"]["Width"] > 500
                   for w in H.list_windows(pid)):
            return


def open_load_scenario(pid: int) -> None:
    _retry(lambda: H.send("CLICK_MENU TSMI_LoadScenario", timeout=15), 12, 2.0, "open LoadScenario")
    time.sleep(2.0)


def load_scenario(pid: int, query: str) -> None:
    _retry(lambda: H.send("SCEN_EXPAND_ALL", timeout=30), 3, 2.0, "expand tree")
    time.sleep(1.0)
    _retry(lambda: H.send("SCEN_SELECT " + query, timeout=30), 3, 2.0, "select " + query)
    time.sleep(1.0)
    _retry(lambda: H.send("SCEN_LOAD", timeout=25), 8, 3.0, "load")
    # side briefing -> enter battle
    _retry(lambda: H.send("CLICK_BUTTON 进入想定", timeout=15), 20, 3.0, "enter scenario")
    time.sleep(6.0)
    # realism options -> start
    for _ in range(12):
        if H.send("CLICK_WPF 开始推演", timeout=25).get("clicked", 0) > 0:
            break
        time.sleep(2.0)
    time.sleep(4.0)
    # optional realism window rendered with a WinForms dark button
    for _ in range(6):
        if not any(w["visible"] and w["rect"]["Width"] == 850 for w in H.list_windows(pid)):
            break
        for w in H.list_windows(pid):
            if w["visible"] and w["rect"]["Width"] == 850:
                r = w["rect"]
                H.click(r["X"] + int(426 * r["Width"] / 850), r["Y"] + int(522 * r["Height"] / 600))
        time.sleep(2.5)
    time.sleep(2.0)
    # tutorial special message popup
    for label in ("关闭并继续", "关闭"):
        for _ in range(6):
            r = H.send("CLICK_BUTTON " + label, timeout=12)
            if r.get("status") == "ok":
                time.sleep(2.0)
                break
            break
    return


def main() -> int:
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    no_restart = "--no-restart" in sys.argv
    query = args[0] if args else "Electronic Warfare"
    pid = H.game_pid()
    if not no_restart:
        pid = H.restart_game(None, wait=120)
        time.sleep(3.0)
    if not pid:
        print("Command.exe not running")
        return 1
    dismiss_launcher(pid)
    open_load_scenario(pid)
    load_scenario(pid, query)
    wins = [(w["hwnd"], w["rect"]["Width"], w["rect"]["Height"], w["title"])
            for w in H.big_visible_windows(pid)[:10]]
    print(json.dumps({"pid": pid, "wins": wins}, ensure_ascii=False, indent=1))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
