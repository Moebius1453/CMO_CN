"""更新助手实机验证：--check 两条路径（已是最新 / 发现新版本）+ 托盘常驻 + 不再提醒。

用法：
    python scripts/test_updater_live.py            # 全部用例
    python scripts/test_updater_live.py check      # 只跑 --check
"""
import json
import subprocess
import sys
import time
from pathlib import Path

import winreg

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "scripts"))
import live_harness as H  # noqa: E402

GAME = Path(r"G:\soft\steam\steamapps\common\Command - Modern Operations")
UPD = GAME / "CMO_Updater.exe"
BASE = ROOT / "evidence" / "design" / "v414_updater"
REG = r"Software\CMOChinese"


def set_ver(v: str) -> None:
    with winreg.CreateKey(winreg.HKEY_CURRENT_USER, REG) as k:
        winreg.SetValueEx(k, "Version", 0, winreg.REG_SZ, v)


def get_ver() -> str:
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER, REG) as k:
        return winreg.QueryValueEx(k, "Version")[0]


def run_check(tag: str, ver: str, wait: float = 8.0) -> dict:
    set_ver(ver)
    p = subprocess.Popen([str(UPD), "--check"])
    time.sleep(wait)
    wins = H.list_windows()
    # 按进程号挑选对话框（GuiDriver 输出的中文标题受控制台编码影响，不能用于比对）
    dlg = [w for w in wins if w["pid"] == p.pid and w["visible"] and w["rect"]["Width"] > 200]
    dlg = [w for w in dlg if w["cls"] not in ("GDI+ Window",)]
    out = {"tag": tag, "installed": ver, "dialog": None}
    if dlg:
        w = dlg[0]
        out["dialog"] = {"title": w["title"], "cls": w["cls"], "rect": w["rect"]}
        f = H.shot(f"live/{tag}", hwnd=w["hwnd"], out_dir=BASE)
        out["shot"] = str(f)
        print(f"[{tag}] 对话框 cls={w['cls']} rect={w['rect']} -> {f}")
    else:
        out["windows"] = [w["title"] for w in wins if w["title"]][:12]
        print(f"[{tag}] 未找到对话框，窗口列表: {out['windows']}")
    H.keys("ENTER")
    time.sleep(1.0)
    try:
        p.wait(timeout=5)
        out["exit"] = p.returncode
    except Exception:
        p.kill()
        out["exit"] = None
    return out


def check_tray(tag: str = "tray") -> dict:
    """启动托盘常驻并截图托盘区域，然后结束进程。"""
    p = subprocess.Popen([str(UPD), "--restarted"])
    time.sleep(6)
    wins = H.list_windows()
    procs = subprocess.run(["tasklist", "/FI", "IMAGENAME eq CMO_Updater.exe"],
                           capture_output=True, text=True).stdout
    f = H.shot(f"live/{tag}_tray", out_dir=BASE)
    print(f"[{tag}] 进程: {'运行中' if 'CMO_Updater.exe' in procs else '未运行'} -> {f}")
    subprocess.run(["taskkill", "/IM", "CMO_Updater.exe", "/F"], capture_output=True, text=True)
    time.sleep(1)
    return {"tag": tag, "running": "CMO_Updater.exe" in procs, "shot": str(f),
            "windows": [w["title"] for w in wins if w["title"]][:10]}


def main() -> int:
    what = sys.argv[1] if len(sys.argv) > 1 else "all"
    result = []
    if what in ("all", "check"):
        result.append(run_check("check_uptodate", "4.1.4"))
        result.append(run_check("check_newver", "4.1.2"))
        result.append(run_check("check_newver2", "4.1.3"))
    if what in ("all", "tray"):
        result.append(check_tray())
    (ROOT / "build" / "updater_live_v414.json").write_text(
        json.dumps(result, ensure_ascii=False, indent=1), encoding="utf-8")
    print("saved build/updater_live_v414.json")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
