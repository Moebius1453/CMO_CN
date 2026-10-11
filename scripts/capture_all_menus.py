#!/usr/bin/env python3
"""Capture live on-screen screenshots for all 10 CMO top-level menus and key submenus.

Drives the in-process CMOLocalizer test command channel (SHOW_MENU, SHOW_SUBMENU,
HIDE_MENUS) and uses GuiDriver.exe to capture the physical screen rect.
"""
from __future__ import annotations

import json
import os
import subprocess
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GAME = Path("G:/soft/steam/steamapps/common/Command - Modern Operations")
DRIVER = ROOT / "build/GuiDriver.exe"
OUT_DIR = ROOT / "evidence/live/menus"
OUT_DIR.mkdir(parents=True, exist_ok=True)

MENU_NAMES = [
    (0, "00_file_文件"),
    (1, "01_view_视图"),
    (2, "02_game_推演"),
    (3, "03_mapsettings_地图设置与工具"),
    (4, "04_quickjump_快速跳转"),
    (5, "05_unitorders_单位命令"),
    (6, "06_unitorders_old_单位命令旧版"),
    (7, "07_missions_任务与参考点"),
    (8, "08_contacts_目标接触"),
    (9, "09_editor_想定编辑"),
    (10, "10_help_帮助"),
]

KEY_SUBMENUS = [
    (3, 13, "sub_03_13_rangesymbols_显示范围符号"),
    (3, 21, "sub_03_21_datablocks_数据块"),
    (5, 0, "sub_05_00_attack_攻击选项"),
    (5, 1, "sub_05_01_asw_反潜专有行动"),
    (9, 20, "sub_09_20_unitops_单位操作"),
    (9, 18, "sub_09_18_events_事件编辑器"),
]


def send_cmd(cmd_text: str) -> dict:
    res = subprocess.run([str(DRIVER), "cmd", str(GAME), cmd_text],
                         capture_output=True, text=True, encoding="utf-8",
                         errors="replace")
    try:
        return json.loads(res.stdout)
    except Exception:
        return {"raw": res.stdout}


def capture(out_png: Path) -> None:
    subprocess.run([str(DRIVER), "capture", str(out_png)], check=True)


def main() -> int:
    print("Capturing live screenshots of all top-level menus...")
    # Hide any existing dropdowns first
    send_cmd("HIDE_MENUS")
    time.sleep(0.3)

    for idx, label in MENU_NAMES:
        print(f"  [{idx}] {label}...")
        res = send_cmd(f"SHOW_MENU {idx}")
        time.sleep(0.5)
        out_path = OUT_DIR / f"{label}.png"
        capture(out_path)
        print(f"      saved: {out_path.name}")
        time.sleep(0.2)

    print("\nCapturing live screenshots of key submenus...")
    for p_idx, c_idx, label in KEY_SUBMENUS:
        print(f"  [{p_idx}:{c_idx}] {label}...")
        res = send_cmd(f"SHOW_SUBMENU {p_idx}:{c_idx}")
        time.sleep(0.5)
        out_path = OUT_DIR / f"{label}.png"
        capture(out_path)
        print(f"      saved: {out_path.name}")
        time.sleep(0.2)

    send_cmd("HIDE_MENUS")
    print(f"\nAll menu screenshots saved to {OUT_DIR}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
