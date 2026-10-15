"""安装器新版界面实机验证：版本更新分组渲染 + 检查升级 / 不再提醒 / 开机启动 三个控件。

用法：python scripts/test_installer_ui.py
产出：evidence/design/v414_updater/live/installer_*.png 与 build/installer_ui_v414.json
"""
import json
import subprocess
import sys
import time
from pathlib import Path

import ctypes
import winreg
from ctypes import wintypes

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "scripts"))
import live_harness as H  # noqa: E402

user32 = ctypes.windll.user32


class _RECT(ctypes.Structure):
    _fields_ = [("left", ctypes.c_long), ("top", ctypes.c_long), ("right", ctypes.c_long), ("bottom", ctypes.c_long)]


class _POINT(ctypes.Structure):
    _fields_ = [("x", ctypes.c_long), ("y", ctypes.c_long)]


def client_origin(hwnd: int):
    """返回客户区原点（屏幕坐标，物理像素）与客户区尺寸。"""
    p = _POINT(0, 0)
    user32.ClientToScreen(wintypes.HWND(hwnd), ctypes.byref(p))
    r = _RECT()
    user32.GetClientRect(wintypes.HWND(hwnd), ctypes.byref(r))
    return (p.x, p.y, r.right, r.bottom)


SETUP = ROOT / "build" / "CMO_Chinese_Setup_v4.1.4.exe"
BASE = ROOT / "evidence" / "design" / "v414_updater"
FORM_W, FORM_H = 700, 655          # InstallerForm 的 ClientSize（见 CMO_Chinese_Setup.cs）
REG = r"Software\CMOChinese"
RUNKEY = r"Software\Microsoft\Windows\CurrentVersion\Run"

# 控件在客户区内的坐标（与 CMO_Chinese_Setup.cs 中的 Location 对应）
BTN_CHECK = (22 + 224 + 55, 290 + 102 + 15)     # 检查升级
BTN_NEVER = (22 + 342 + 55, 290 + 102 + 15)     # 不再提醒
CHK_START = (22 + 14 + 8, 290 + 76 + 8)         # 随 Windows 启动自动检查更新


def reg_get(name, hive=REG):
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, hive) as k:
            return winreg.QueryValueEx(k, name)[0]
    except FileNotFoundError:
        return None


def run_key() -> str | None:
    return reg_get("CMOChineseUpdater", RUNKEY)


def modal_dialogs(pid: int):
    return [w for w in H.list_windows() if w["pid"] == pid and w["cls"] == "#32770" and w["visible"]]


def dismiss_all(pid: int, tries: int = 6, wait: float = 1.2) -> bool:
    """反复按回车直到本进程的模态框全部关闭。"""
    for _ in range(tries):
        d = modal_dialogs(pid)
        if not d:
            return True
        H.keys("ENTER")
        time.sleep(wait)
    return not modal_dialogs(pid)


def main() -> int:
    result = {"steps": []}
    for p in subprocess.run(["tasklist", "/FI", "IMAGENAME eq CMO_Chinese_Setup_v4.1.4.exe"],
                            capture_output=True, text=True).stdout.splitlines():
        if "CMO_Chinese_Setup" in p:
            subprocess.run(["taskkill", "/IM", "CMO_Chinese_Setup_v4.1.4.exe", "/F"], capture_output=True)

    proc = subprocess.Popen([str(SETUP)])
    time.sleep(5)
    wins = [w for w in H.list_windows() if w["pid"] == proc.pid and w["visible"] and w["rect"]["Width"] > 500]
    if not wins:
        print("未找到安装器窗口")
        return 1
    win = wins[0]
    r = win["rect"]
    ox, oy, cw, ch = client_origin(win["hwnd"])
    print(f"窗口 rect={r} 客户区原点=({ox},{oy}) 客户区={cw}x{ch}")
    shot = H.shot("live/installer_ui", hwnd=win["hwnd"], out_dir=BASE)
    result["window"] = {"rect": r, "shot": str(shot)}

    def to_screen(p):
        return (ox + p[0], oy + p[1])

    # ① 检查升级
    H.click(*to_screen(BTN_CHECK))
    time.sleep(9)
    up = [w for w in H.list_windows() if w["cls"] != "GDI+ Window" and "CMO_Updater" in str(w.get("cls", "")) or
          (w["visible"] and w["rect"]["Width"] == 560)]
    dlgs = [w for w in H.list_windows() if w["visible"] and not w["title"].startswith("GDI+")
            and w["rect"]["Width"] in (560, 253) and w["pid"] != proc.pid]
    result["steps"].append({"step": "检查升级", "dialogs": [{"cls": d["cls"], "rect": d["rect"], "pid": d["pid"]} for d in dlgs]})
    if dlgs:
        result["steps"][-1]["shot"] = str(H.shot("live/installer_checkupdate", hwnd=dlgs[0]["hwnd"], out_dir=BASE))
    dismiss_all(proc.pid)
    subprocess.run(["taskkill", "/IM", "CMO_Updater.exe", "/F"], capture_output=True)
    time.sleep(0.8)

    # ② 不再提醒（关）→ 检查注册表
    before = reg_get("AutoCheck")
    H.click(*to_screen(BTN_NEVER))
    time.sleep(2.0)
    dismiss_all(proc.pid)
    time.sleep(1.2)
    after = reg_get("AutoCheck")
    result["steps"].append({"step": "不再提醒", "AutoCheck_before": before, "AutoCheck_after": after,
                            "SkippedVersion": reg_get("SkippedVersion")})
    result["steps"][-1]["shot"] = str(H.shot("live/installer_neverremind", hwnd=win["hwnd"], out_dir=BASE))
    print(f"不再提醒: AutoCheck {before} -> {after}, skipped={reg_get('SkippedVersion')}")

    # ③ 再点一次恢复
    H.click(*to_screen(BTN_NEVER))
    time.sleep(2.0)
    dismiss_all(proc.pid)
    time.sleep(1.2)
    result["steps"].append({"step": "恢复提醒", "AutoCheck": reg_get("AutoCheck")})
    print(f"恢复提醒: AutoCheck={reg_get('AutoCheck')}")

    # ④ 开机启动开关
    run_before = run_key()
    H.click(*to_screen(CHK_START))
    time.sleep(2.0)
    dismiss_all(proc.pid)
    run_off = run_key()
    H.click(*to_screen(CHK_START))
    time.sleep(2.0)
    dismiss_all(proc.pid)
    run_on = run_key()
    result["steps"].append({"step": "开机启动开关", "before": run_before, "after_off": run_off, "after_on": run_on})
    print(f"开机启动: 关={run_off} 开={run_on}")

    shot2 = H.shot("live/installer_ui_final", hwnd=win["hwnd"], out_dir=BASE)
    result["final_shot"] = str(shot2)
    subprocess.run(["taskkill", "/IM", "CMO_Chinese_Setup_v4.1.4.exe", "/F"], capture_output=True)
    (ROOT / "build" / "installer_ui_v414.json").write_text(json.dumps(result, ensure_ascii=False, indent=1), encoding="utf-8")
    print("saved build/installer_ui_v414.json")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
