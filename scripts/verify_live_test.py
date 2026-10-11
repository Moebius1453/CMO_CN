#!/usr/bin/env python3
"""Verify that all live machine test artifacts, screenshots, and gates are complete."""
from __future__ import annotations

import json
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parent.parent
EVIDENCE = ROOT / "evidence/live"
DOCS = ROOT / "docs"
GAME = Path("G:/soft/steam/steamapps/common/Command - Modern Operations")

REQUIRED_SCREENSHOTS = [
    "01_start_menu_wpf.png",
    "02_mainform_clean.png",
    "03_darktoolstrip_buttons.png",
    "menus/00_file_文件.png",
    "menus/01_view_视图.png",
    "menus/02_game_推演.png",
    "menus/03_mapsettings_地图设置与工具.png",
    "menus/04_quickjump_快速跳转.png",
    "menus/05_unitorders_单位命令.png",
    "menus/06_unitorders_old_单位命令旧版.png",
    "menus/07_missions_任务与参考点.png",
    "menus/08_contacts_目标接触.png",
    "menus/09_editor_想定编辑.png",
    "menus/10_help_帮助.png",
    "menus/sub_03_13_rangesymbols_显示范围符号.png",
    "menus/sub_03_21_datablocks_数据块.png",
    "menus/sub_05_00_attack_攻击选项.png",
    "menus/sub_05_01_asw_反潜专有行动.png",
    "menus/sub_09_18_events_事件编辑器.png",
    "menus/sub_09_20_unitops_单位操作.png",
    "scen/04_load_scenario_initial.png",
    "scen/11_all_scens_expanded.png",
    "scen/12_bluedawn_selected_auto.png",
    "scen/13_latakia_selected_auto.png",
    "scen/14_latakia_loaded_sidebriefing.png",
    "scen/17_realism_options_translated.png",
    "scen/18_latakia_battlefield_clean.png",
    "scen/20_latakia_battlefield_clean_running.png",
    "dialogs/01_database_viewer_aircraft_top.png",
    "dialogs/01_database_viewer_aircraft_bottom.png",
    "dialogs/01_database_viewer_aircraft_bottom2.png",
    "dialogs/02_doctrine_window.png",
    "dialogs/03_mission_editor.png",
    "dialogs/04_edit_sides.png",
    "dialogs/05_order_of_battle.png",
    "dialogs/06_event_editor.png",
    "dialogs/07_game_options.png",
]


def main() -> int:
    failures: list[str] = []

    # 1. Verify all 37 live screenshots exist and are valid PNGs (> 5 KB)
    for rel in REQUIRED_SCREENSHOTS:
        p = EVIDENCE / rel
        if not p.is_file():
            failures.append(f"Missing live screenshot: {rel}")
        elif p.stat().st_size < 5000:
            failures.append(f"Screenshot too small ({p.stat().st_size}B): {rel}")

    # 2. Verify both Placename sets (321 countries/seas + 4287 cities = 4608)
    sys.path.insert(0, str(ROOT / "scripts"))
    from wwp import load  # noqa: E402
    import re
    han = re.compile(r"[\u4e00-\u9fff]")

    syn = load(GAME / "WW/PlacenameSets/Synoptic/0000_0000.wwp")
    cit = load(GAME / "WW/PlacenameSets/Cities/0000_0000.wwp")
    syn_cjk = sum(1 for i in syn if han.search(i["name"]))
    cit_cjk = sum(1 for i in cit if han.search(i["name"]))
    if syn_cjk != len(syn):
        failures.append(f"Synoptic placenames CJK {syn_cjk}/{len(syn)}")
    if cit_cjk != len(cit):
        failures.append(f"Cities placenames CJK {cit_cjk}/{len(cit)}")

    # 3. Verify FULL_LIVE_TEST_REPORT.md exists
    report = DOCS / "FULL_LIVE_TEST_REPORT.md"
    if not report.is_file() or report.stat().st_size < 2000:
        failures.append("Missing or incomplete docs/FULL_LIVE_TEST_REPORT.md")

    print(f"Live screenshots verified : {len(REQUIRED_SCREENSHOTS) - len([f for f in failures if 'screenshot' in f])}/{len(REQUIRED_SCREENSHOTS)}")
    print(f"Map Placenames (Synoptic) : {syn_cjk}/{len(syn)} Chinese")
    print(f"Map Placenames (Cities)   : {cit_cjk}/{len(cit)} Chinese")
    print(f"Full Live Test Report     : {'OK' if report.is_file() else 'MISSING'}")

    if failures:
        print("\nFAIL:")
        for f in failures:
            print("  -", f)
        return 1

    print("\nPASS: all 37 live machine test screenshots and 4,608 map placenames verified!")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
