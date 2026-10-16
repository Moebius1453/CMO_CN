#!/usr/bin/env python3
"""发版前回归测试（台账 docs/BUG_REGISTRY.md 的可执行部分）。

    python scripts/verify_regressions.py --static     # 无需游戏，package.sh 硬门禁
    python scripts/verify_regressions.py --installer  # 隔离临时目录验证安装/还原/回退（无需游戏）
    python scripts/verify_regressions.py --live       # 需游戏运行（发版前必跑）
    python scripts/verify_regressions.py              # 三者都跑

结果写入 build/regression_report.json；任一用例失败则退出码非零。
新增 bug 时：在 docs/BUG_REGISTRY.md 记一条，并在这里加一条用例（本文件是唯一权威）。
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "scripts"))

GAME = Path("G:/soft/steam/steamapps/common/Command - Modern Operations")
ENGINE = ROOT / "src/engine/CMOLocalizer.cs"
DICT = ROOT / "dict/CMOLocalizer.json"
REPORT = ROOT / "build/regression_report.json"
SHOTS = ROOT / "evidence/live/regression"

OBJECT_TYPE_TOKENS = ["Aircraft", "Ship", "Submarine", "Facility",
                      "Ground Unit", "Satellite", "Weapon", "Sensor"]
# 游戏真实渲染出的详情页字节数下限（Aircraft 页面含大量小节，阈值更高）
MIN_HTML_LEN = {"Aircraft": 20000}
DEFAULT_MIN_HTML_LEN = 3000   # 空壳页（未生成详情）实测 1541B，真实最小详情页 4194B
FIRST_ROW_OFFSET = (560, 78)   # 列表首行相对窗体左上角，实测可用


# ── 用例框架 ────────────────────────────────────────────────────────────────
class Runner:
    def __init__(self) -> None:
        self.results: list[dict] = []

    def record(self, cid: str, name: str, ok: bool, detail: str,
               evidence: str = "") -> None:
        self.results.append({"id": cid, "name": name, "status": "pass" if ok else "fail",
                             "detail": detail, "evidence": evidence})
        print(f"  [{'PASS' if ok else 'FAIL'}] {cid} {name} — {detail}", flush=True)


# ── static：源码/资产/词库断言（防止保护被删、资产漏发） ────────────────────
# R0 二进制标记：部署到游戏目录的引擎必须包含历次修复的关键符号。
# 不用整文件哈希：csc 每次编译都产生不同 MVID/时间戳，重新 build 后哈希必变，
# 但“旧包覆盖回不含修复的引擎”会被标记缺失抓住。
DEPLOY_MARKERS = ["IsProtectedDBViewerCombo", "FindDBViewerForm"]


def run_static(r: Runner) -> None:
    print("== static 用例 ==", flush=True)
    src = ENGINE.read_text(encoding="utf-8")

    # R0：环境一致性——游戏目录必须装的是含本轮修复的产物。
    # （历史踩坑：发版前被旧安装包覆盖成不含修复的引擎，回归“失败”实为环境错）
    drift = []
    if GAME.is_dir():
        deployed_json, deployed_dll = GAME / "CMOLocalizer.json", GAME / "CMOLocalizer.dll"
        if not deployed_json.is_file() or deployed_json.read_bytes() != DICT.read_bytes():
            drift.append("stale in game: CMOLocalizer.json")
        if not deployed_dll.is_file():
            drift.append("missing: CMOLocalizer.dll")
        else:
            blob = deployed_dll.read_bytes()
            lack = [m for m in DEPLOY_MARKERS if m.encode() not in blob]
            if lack:
                drift.append(f"deployed engine lacks fix markers: {lack}")
    r.record("R0-static-env", "R0 游戏目录引擎/词库含本轮修复", not drift,
             "; ".join(drift) or f"词库同哈希 + 引擎标记齐全{tuple(DEPLOY_MARKERS)}",
             "scripts/deploy.sh 或安装包")

    has_helper = "private static bool IsProtectedDBViewerCombo(ComboBox cb)" in src
    generic_guarded = re.search(
        r"LocalizeComboBoxSafe\(ComboBox cb\)\s*\{[^}]*?IsProtectedDBViewerCombo\(cb\)", src, re.S)
    dedicated_guarded = re.search(
        r"LocalizeDBViewerComboBox\(ComboBox cb\)\s*\{.*?IsProtectedDBViewerCombo\(cb\)", src, re.S)
    hwnd_fallback = re.search(r"FindDBViewerForm\(\)[\s\S]{0,900}?EnumWindows", src)
    r.record("R1-static-guard", "R1 CB_ObjectType 保护（通用+专用路径）",
             bool(has_helper and generic_guarded and dedicated_guarded),
             f"helper={bool(has_helper)} generic={bool(generic_guarded)} dedicated={bool(dedicated_guarded)}",
             str(ENGINE.relative_to(ROOT)))

    r.record("R1-static-hwnd", "R1 辅助线程窗体解析（EnumWindows 回退）", bool(hwnd_fallback),
             "FindDBViewerForm 使用 EnumWindows + Control.FromHandle",
             str(ENGINE.relative_to(ROOT)))

    skip = re.search(r"if \(ctrl is ComboBox \|\| ctrl is ListControl \|\| ctrl is UpDownBase\)\s*\{",
                     src)
    r.record("R8-static-text-ban", "R8 禁止改写 ComboBox/ListControl/UpDownBase 的 .Text",
             bool(skip), "TranslateControlText 跳过分支存在", str(ENGINE.relative_to(ROOT)))

    missing = []
    if GAME.is_dir():
        for set_name, rel in (("Synoptic", "WW/PlacenameSets/Synoptic/0000_0000.wwp"),
                              ("Cities", "WW/PlacenameSets/Cities/0000_0000.wwp")):
            srcf, dstf = ROOT / "assets/placenames" / f"{set_name}.wwp", GAME / rel
            if not srcf.is_file():
                missing.append(f"asset missing: {srcf.name}")
            elif dstf.is_file() and srcf.read_bytes() != dstf.read_bytes():
                missing.append(f"stale in game: {rel}")
    r.record("R2-static-placenames", "R2 汉化地名集资产存在且已分发",
             not missing, "; ".join(missing) or "Synoptic+Cities 与游戏目录逐字节一致",
             "assets/placenames")

    eng_names = set(re.findall(r'"(Btn_[A-Za-z0-9_\-]+\.png)"', src))
    have = {p.name for p in (ROOT / "assets/symbols_menu").glob("*.png")}
    lack = sorted(eng_names - have)
    r.record("R5-static-buttons", "R5 中文按钮图覆盖引擎期望文件名",
             bool(eng_names) and not lack,
             f"expect={len(eng_names)} missing={lack or 'none'}", "assets/symbols_menu")

    doc = json.loads(DICT.read_text(encoding="utf-8"))
    bad = [k for k in doc["offsets"]
           if any(t in k for t in ("Data Source", "Connection", "SELECT ", " AND ", "INSERT "))]
    bad += [k for k, v in doc["offsets"].items()
            if any(t in str(v) for t in ("Data Source", "SELECT ", " AND "))]
    r.record("R6-static-sql", "R6 offsets 不含 SQL/连接串片段",
             not bad, f"offenders={bad[:5] or 'none'}", "dict/CMOLocalizer.json")

    term = subprocess.run([sys.executable, str(ROOT / "scripts/verify_terminology.py")],
                          cwd=str(ROOT), capture_output=True, text=True)
    tail = (term.stdout or term.stderr).strip().splitlines()[-1:] or [""]
    r.record("R7-static-terms", "R7 术语/机翻门禁", term.returncode == 0, tail[0][:120],
             "scripts/verify_terminology.py")

    # R18：共享状态层单测（设置/清单/包缓存/门控），测试宿主由 build.sh 产出
    shared = ROOT / "build/CMOSharedTest.exe"
    if not shared.is_file():
        r.record("R18-static-selftest", "R18 共享状态层单测（设置/清单/缓存/门控）", False,
                 "缺少 build/CMOSharedTest.exe，请先跑 bash scripts/build.sh")
    else:
        p = subprocess.run([str(shared)], capture_output=True, text=True,
                           encoding="utf-8", errors="replace")
        out = (p.stdout or "") + (p.stderr or "")
        fails = [ln for ln in out.splitlines() if ln.startswith("FAIL ")]
        passes = [ln for ln in out.splitlines() if ln.startswith("PASS ")]
        r.record("R18-static-selftest", "R18 共享状态层单测（设置/清单/缓存/门控）",
                 p.returncode == 0 and not fails,
                 f"{len(passes)} 项 PASS" + (f"，失败 {fails[:3]}" if fails else ""),
                 "src/shared/CMOSharedTest.cs")
        # R16：降级提示门控真值表（负向：本地==远端 / 更旧但无缓存 → 不得提示回退）
        gate_ok = ("PASS gate-truth-table" in out and "PASS gate-empty-inputs" in out
                   and "PASS pkgversion-filename-wins" in out)
        r.record("R16-static-gate", "R16 降级提示门控（无缓存不得提示回退）", gate_ok,
                 "gate-truth-table + gate-empty-inputs + pkgversion-filename-wins 均通过"
                 if gate_ok else "门控断言缺失或失败", "src/shared/CMOShared.cs")

    # R17-static-pack-readme：绿色包说明（打包为 汉化说明.md）必须告知可自动登记安装状态
    readme = (ROOT / "README.md").read_text(encoding="utf-8", errors="replace")
    pack_doc_ok = ("登记安装状态" in readme) and ("--adopt" in readme)
    r.record("R17-static-pack-readme", "R17 绿色包说明含自动登记安装状态（adopt）", pack_doc_ok,
             "README.md 含 adopt 说明（随包为 汉化说明.md）" if pack_doc_ok else "README.md 缺少 adopt 说明",
             "README.md")


# ── live：需要在运行中的游戏里实测 ──────────────────────────────────────────
def _ctrl_tree(H) -> dict:
    r = H.send("DB_CTRL_TREE", timeout=20)
    return {n["name"]: n for n in r.get("tree", [])}


def _form_origin(bounds: str) -> tuple[int, int]:
    m = re.search(r"X=(-?\d+),Y=(-?\d+)", bounds or "")
    return (int(m.group(1)), int(m.group(2))) if m else (0, 0)


def _open_viewer(H, attempts: int = 10) -> bool:
    for _ in range(attempts):
        if H.send("CLICK_MENU 数据库查看器", timeout=20).get("status") == "ok":
            time.sleep(6)
            if H.send("DB_CTRL_TREE", timeout=20).get("status") == "ok":
                return True
        time.sleep(3)
    return False


def _shot(H, name: str, hwnd: int) -> Path:
    SHOTS.mkdir(parents=True, exist_ok=True)
    out = SHOTS / f"{name}.png"
    subprocess.run([str(ROOT / "build/GuiDriver.exe"), "capture", str(out), str(hwnd)],
                   capture_output=True)
    return out


def run_live(r: Runner) -> None:
    print("== live 用例（实机）==", flush=True)
    import live_harness as H  # noqa: PLC0415

    if H.game_pid() is None:
        r.record("L0-game", "L0 游戏进程在运行", False, "Command.exe 未运行，live 用例无法执行")
        return
    if not _open_viewer(H):
        r.record("L0-viewer", "L0 打开数据库查看器", False, "CLICK_MENU 数据库查看器 失败")
        return
    r.record("L0-viewer", "L0 打开数据库查看器", True, "DB_CTRL_TREE 可解析")

    tree = _ctrl_tree(H)
    form = tree.get("InternalDBViewer")
    if not form:
        r.record("L0-form", "L0 定位查看器窗体", False, "控件树缺少 InternalDBViewer")
        return
    hwnd = None
    x, y = _form_origin(form.get("bounds", ""))
    for w in H.list_windows(H.game_pid()):
        if w["rect"]["X"] == x and w["rect"]["Y"] == y and "WindowsForms" in w["cls"]:
            hwnd = w["hwnd"]
            break

    # L1：八类对象逐个切换 + 真实点击首行 → 详情页必须生成
    for t in OBJECT_TYPE_TOKENS:
        probe = t.split()[0]              # "Ground Unit" 含空格，DB_SET_FILTER 按空格切分
        H.send("DB_SET_FILTER CB_ObjectType " + probe, timeout=15)
        time.sleep(2.0)
        if hwnd:
            subprocess.run([str(ROOT / "build/GuiDriver.exe"), "focus", str(hwnd)], capture_output=True)
        H.click(x + FIRST_ROW_OFFSET[0], y + FIRST_ROW_OFFSET[1])
        time.sleep(2.5)
        ins = H.send("DB_INSPECT", timeout=15)
        got = int(ins.get("html_len") or 0)
        need = MIN_HTML_LEN.get(t, DEFAULT_MIN_HTML_LEN)
        r.record(f"R1-live-{t.replace(' ', '')}", f"R1 {t} 详情页渲染",
                 got >= need, f"html_len={got} (>= {need})")

    # L2：类型下拉必须是游戏原生 token
    tree = _ctrl_tree(H)
    token = (tree.get("CB_ObjectType") or {}).get("text", "")
    ok = token in OBJECT_TYPE_TOKENS
    r.record("R1-live-token", "R1 CB_ObjectType 保持游戏原生 token", ok, f"text={token!r}")

    # L3：无频闪（同一稳定态两帧截图一致）
    if hwnd:
        a = _shot(H, "flicker_a", hwnd)
        time.sleep(2.5)
        b = _shot(H, "flicker_b", hwnd)
        if a.is_file() and b.is_file():
            ha = hashlib.md5(a.read_bytes()).hexdigest()
            hb = hashlib.md5(b.read_bytes()).hexdigest()
            r.record("R4-live-flicker", "R4 界面无中英频闪（两帧一致）", ha == hb,
                     f"md5 {ha[:10]} vs {hb[:10]}", str(a.relative_to(ROOT)))
        else:
            r.record("R4-live-flicker", "R4 界面无中英频闪", False, "截图失败")
    else:
        r.record("R4-live-flicker", "R4 界面无中英频闪", False, "未找到查看器窗口句柄")


# ── installer：安装/还原/回退的隔离验证（临时合成游戏目录，不需要游戏） ──────
def _reg_snapshot() -> dict:
    import winreg  # noqa: PLC0415
    keys = {}
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Software\CMOChinese") as k:
            for name in ("GamePath", "Version", "AutoCheck", "CheckIntervalHours", "AutoStart",
                         "NotifyMode", "AutoInstall", "Mirror"):
                try:
                    keys[name] = winreg.QueryValueEx(k, name)
                except FileNotFoundError:
                    pass
    except FileNotFoundError:
        pass
    return keys


def _reg_restore(keys: dict) -> None:
    import winreg  # noqa: PLC0415
    with winreg.CreateKeyEx(winreg.HKEY_CURRENT_USER, r"Software\CMOChinese") as k:
        for name, (value, kind) in keys.items():
            winreg.SetValueEx(k, name, 0, kind, value)


def _decode(raw: bytes) -> str:
    """安装器输出走控制台编码（中文 Windows 为 GBK）；先试 UTF-8 再回退 GBK。"""
    for enc in ("utf-8", "gbk"):
        try:
            return raw.decode(enc)
        except UnicodeDecodeError:
            continue
    return raw.decode("utf-8", errors="replace")


def _mk_game(root: Path) -> Path:
    game = root / "game"
    for d in ("DB/Templates", "Symbols/Menu", "Scenarios"):
        (game / d).mkdir(parents=True, exist_ok=True)
    (game / "Command.exe").write_bytes(b"stub")
    (game / "Command.exe.config").write_text(
        '<?xml version="1.0"?><configuration><runtime></runtime></configuration>',
        encoding="utf-8")
    return game


def run_installer(r: Runner) -> None:
    """R13/R14/R15/R17：用安装器 CLI 在临时目录验证状态/设置/回退，不碰真实游戏目录。"""
    import shutil  # noqa: PLC0415
    import tempfile  # noqa: PLC0415

    print("== installer 用例（隔离临时目录）==", flush=True)
    version = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
    setup = ROOT / "build" / f"CMO_Chinese_Setup_v{version}.exe"
    if not setup.is_file():
        r.record("R13-installer-setup", "R13 安装器产物存在", False,
                 f"缺少 {setup.name}，请先跑 bash scripts/build.sh")
        return

    settings = Path(os.environ["APPDATA"]) / "CMOChinese" / "settings.json"
    packages = Path(os.environ["LOCALAPPDATA"]) / "CMOChinese" / "packages"
    saved_settings = settings.read_bytes() if settings.exists() else None
    saved_reg = _reg_snapshot()
    tmp = Path(tempfile.mkdtemp(prefix="cmo-reg-installer-"))
    # 安装包缓存整个搬走再搬回：用例内部会删缓存来验证“无缓存拒绝回退”，
    # 不能把用户真实缓存的安装包（回退素材）弄丢。
    pkg_backup = Path(tempfile.mkdtemp(prefix="cmo-pkg-backup-"))
    if packages.is_dir():
        for p in packages.glob("*.exe"):
            shutil.move(str(p), str(pkg_backup / p.name))

    def run(*args: str, timeout: int = 600) -> tuple[int, str]:
        p = subprocess.run([str(setup), *args], capture_output=True, timeout=timeout)
        return p.returncode, (_decode(p.stdout or b"") + _decode(p.stderr or b"")).strip()

    try:
        # R13：安装写清单 / 还原清清单（状态不再靠注册表字符串）
        game = _mk_game(tmp / "a")
        rc, out = run("--install", str(game), "--silent")
        man = game / "CMOChinese_Installed.json"
        man_data = json.loads(man.read_text(encoding="utf-8")) if man.is_file() else {}
        dll = game / "CMOLocalizer.dll"
        dll_ok = (dll.is_file()
                  and man_data.get("files", {}).get("CMOLocalizer.dll") == hashlib.sha256(dll.read_bytes()).hexdigest())
        r.record("R13-install-manifest", "R13 安装后写清单且哈希与磁盘一致",
                 rc == 0 and man_data.get("version") == version and dll_ok,
                 f"rc={rc} version={man_data.get('version')} dll_hash_match={dll_ok}", out[:80])

        rc, out = run("--list-versions", str(game))
        r.record("R13-list-versions", "R13 状态查询显示已安装版本",
                 rc == 0 and f"installed=v{version}" in out, out.replace("\n", " | ")[:120])

        rc, out = run("--uninstall", str(game), "--silent")
        r.record("R13-uninstall-manifest", "R13 还原后清单消失（状态=未安装）",
                 rc == 0 and not man.is_file() and not dll.is_file(),
                 f"rc={rc} manifest={man.is_file()} dll={dll.is_file()}", out[:80])

        # R14：还原/重装都不得丢设置
        settings.parent.mkdir(parents=True, exist_ok=True)
        seed = {"schema": 1, "check_interval_hours": 3, "notify_mode": 0,
                "tray_on_close": False, "mirror": "https://ghproxy.net/", "auto_check": True}
        settings.write_text(json.dumps(seed, ensure_ascii=False, indent=1), encoding="utf-8")
        run("--install", str(game), "--silent")
        run("--uninstall", str(game), "--silent")
        after = json.loads(settings.read_text(encoding="utf-8")) if settings.exists() else {}
        kept = (settings.is_file() and after.get("check_interval_hours") == 3
                and after.get("notify_mode") == 0 and after.get("tray_on_close") is False)
        r.record("R14-uninstall-keeps-settings", "R14 还原后设置保留（频率/提醒/托盘）", kept,
                 f"settings_exists={settings.is_file()} interval={after.get('check_interval_hours')} "
                 f"notify={after.get('notify_mode')} tray={after.get('tray_on_close')}")
        rc, out = run("--purge-settings")
        r.record("R14-purge-explicit", "R14 仅显式清除才删设置（--purge-settings）",
                 rc == 0 and not settings.exists(), f"rc={rc} settings_exists={settings.exists()}")

        # R17：存量/绿色包自愈（有文件无清单 → adopt）
        run("--install", str(game), "--silent")
        man.unlink()
        rc, out = run("--adopt", str(game))
        adopted = json.loads(man.read_text(encoding="utf-8")) if man.is_file() else {}
        r.record("R17-adopt-selfheal", "R17 无清单时自动登记安装状态（adopt）",
                 rc == 0 and adopted.get("version") == version and adopted.get("source") == "adopted",
                 f"rc={rc} version={adopted.get('version')} source={adopted.get('source')}", out[:80])

        # R15：回退一致性（清单版本 → 目标版本；部署 dll 与缓存包内嵌 dll 一致）
        packages.mkdir(parents=True, exist_ok=True)
        cached = packages / f"CMO_Chinese_Setup_v{version}.exe"
        if not cached.is_file():
            shutil.copy(setup, cached)
        data = json.loads(man.read_text(encoding="utf-8"))
        data["version"] = "9.9.9"          # 模拟“已装了比缓存包更新的版本”
        man.write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")
        rc, out = run("--rollback", str(game), "--silent")
        back = json.loads(man.read_text(encoding="utf-8")) if man.is_file() else {}
        # 缓存包内嵌的引擎就是刚 build 出来的 build/CMOLocalizer.dll，所以直接比它
        built_dll = ROOT / "build/CMOLocalizer.dll"
        same_dll = (dll.is_file() and built_dll.is_file()
                    and hashlib.sha256(dll.read_bytes()).hexdigest()
                    == hashlib.sha256(built_dll.read_bytes()).hexdigest())
        r.record("R15-rollback", "R15 回退到缓存版本且引擎与缓存包一致",
                 rc == 0 and back.get("version") == version and same_dll,
                 f"rc={rc} manifest=v{back.get('version')} dll==cached_pkg_dll:{same_dll}", out[:80])

        cached.unlink()
        data["version"] = "9.9.9"
        man.write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")
        rc, out = run("--rollback", str(game), "--silent")
        unchanged = (json.loads(man.read_text(encoding="utf-8")).get("version") == "9.9.9")
        r.record("R15-rollback-requires-cache", "R15 无缓存包时拒绝回退（清单不变+报错）",
                 rc != 0 and unchanged and "9.9.9" in out,
                 f"rc={rc} manifest_unchanged={unchanged} out={out[:80]}")
    finally:
        if saved_settings is None:
            if settings.exists():
                settings.unlink()
        else:
            settings.write_bytes(saved_settings)
        if packages.is_dir():
            for p in packages.glob("*.exe"):
                p.unlink()
        for p in pkg_backup.glob("*.exe"):
            packages.mkdir(parents=True, exist_ok=True)
            shutil.move(str(p), str(packages / p.name))
        shutil.rmtree(pkg_backup, ignore_errors=True)
        _reg_restore(saved_reg)
        shutil.rmtree(tmp, ignore_errors=True)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--static", action="store_true")
    ap.add_argument("--live", action="store_true")
    ap.add_argument("--installer", action="store_true",
                    help="安装/还原/回退的隔离验证（临时目录，不需要游戏）")
    ap.add_argument("--no-report", action="store_true",
                    help="不写 build/regression_report.json（供 package.sh 做中间门禁，"
                         "避免覆盖发版用的 live 报告）")
    args = ap.parse_args()
    any_flag = args.static or args.live or args.installer
    do_static = args.static or not any_flag
    do_live = args.live or not any_flag
    do_installer = args.installer or not any_flag

    version = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
    runner = Runner()
    if do_static:
        run_static(runner)
    if do_installer:
        run_installer(runner)
    if do_live:
        run_live(runner)

    failed = [x["id"] for x in runner.results if x["status"] != "pass"]
    report = {
        "version": version,
        "generated_at": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "modes": [m for m, on in (("static", do_static), ("installer", do_installer),
                                  ("live", do_live)) if on],
        "passed": not failed,
        "failed": failed,
        "results": runner.results,
    }
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    if not args.no_report:
        REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"\n==> 回归报告: {REPORT.relative_to(ROOT)}" + ("（--no-report，未写入）" if args.no_report else "")
          + f"  {len(runner.results) - len(failed)}/{len(runner.results)} 通过"
          + (f"，失败：{', '.join(failed)}" if failed else ""))
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
