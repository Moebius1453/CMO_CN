#!/usr/bin/env python3
"""Publish a GitHub Release and upload installer/pack assets for the current VERSION."""
import json
import os
import re
import subprocess
import sys
import time
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
BUILD_DIR = ROOT / "build"
VERSION_FILE = ROOT / "VERSION"
CHANGELOG_FILE = ROOT / "docs/CHANGELOG.md"

PROXY = "http://127.0.0.1:7890"

def get_github_token() -> str:
    # 1. Environment variable
    token = os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN")
    if token:
        return token
    # 2. Git credential helper
    try:
        p = subprocess.Popen(
            ["git", "credential", "fill"],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True
        )
        out, _ = p.communicate(input="protocol=https\nhost=github.com\n\n")
        for line in out.splitlines():
            if line.startswith("password="):
                return line[9:].strip()
    except Exception as e:
        print(f"Warning: git credential fill failed: {e}")
    return ""

def get_opener():
    handlers = []
    if PROXY:
        handlers.append(urllib.request.ProxyHandler({"http": PROXY, "https": PROXY}))
    return urllib.request.build_opener(*handlers)

def main():
    version = VERSION_FILE.read_text(encoding="utf-8").strip()
    tag_name = f"v{version}"
    print(f"==> Target Release: {tag_name}")

    token = get_github_token()
    if not token:
        print("ERROR: No GitHub token found. Please configure git credential or set GITHUB_TOKEN.")
        sys.exit(1)

    opener = get_opener()

    # Extract changelog
    changelog = CHANGELOG_FILE.read_text(encoding="utf-8")
    m = re.search(r"## v" + re.escape(version) + r"\s+[^\n]*\n(.*?)(?=\n## v|\Z)", changelog, re.S)
    notes = m.group(0).strip() if m else f"## v{version}\n\n《现代行动：指挥》汉化更新包发布。"

    title = f"v{version} — 数据库全量百科正文深度汉化与筛选下拉框彻底加固"
    body = f"""{notes}

### 安装方式

- **一键安装器（推荐）**：下载并运行 `CMO_Chinese_Setup_v{version}.exe`，自动识别 Steam 游戏目录并一键完成汉化。
- **绿色免安装包**：下载 `CMO_Chinese_Pack_{version}.zip`，解压后覆盖到游戏根目录即可。
"""

    # Check if release exists
    repo = "mj8724/CMO_CN"
    api_base = f"https://api.github.com/repos/{repo}"
    headers = {
        "Authorization": f"Bearer {token}",
        "User-Agent": "CMO-Localizer-Release-Tool",
        "Accept": "application/vnd.github.v3+json"
    }

    release_info = None
    check_req = urllib.request.Request(f"{api_base}/releases/tags/{tag_name}", headers=headers)
    try:
        with opener.open(check_req) as resp:
            release_info = json.loads(resp.read().decode("utf-8"))
            print(f"==> Release {tag_name} already exists (ID: {release_info['id']})")
    except urllib.error.HTTPError as e:
        if e.code == 404:
            print(f"==> Release {tag_name} does not exist yet. Creating...")
        else:
            print(f"ERROR checking release: {e}")
            sys.exit(1)

    if not release_info:
        create_payload = json.dumps({
            "tag_name": tag_name,
            "target_commitish": "master",
            "name": title,
            "body": body,
            "draft": False,
            "prerelease": False
        }).encode("utf-8")
        create_req = urllib.request.Request(
            f"{api_base}/releases",
            data=create_payload,
            headers={**headers, "Content-Type": "application/json"}
        )
        with opener.open(create_req) as resp:
            release_info = json.loads(resp.read().decode("utf-8"))
            print(f"==> Successfully created Release: {release_info['html_url']}")

    upload_url_template = release_info["upload_url"] # e.g. https://uploads.github.com/.../assets{?name,label}
    upload_base = upload_url_template.split("{")[0]

    # Existing assets
    existing_assets = {a["name"]: a["id"] for a in release_info.get("assets", [])}

    # Files to upload
    setup_file = BUILD_DIR / f"CMO_Chinese_Setup_v{version}.exe"
    if not setup_file.is_file():
        setup_file = BUILD_DIR / f"CMO_Chinese_Setup_{version}.exe"
    if not setup_file.is_file():
        setup_file = BUILD_DIR / "CMO_Chinese_Setup.exe"

    files_to_upload = [
        setup_file,
        BUILD_DIR / f"CMO_Chinese_Pack_{version}.zip"
    ]

    for file_path in files_to_upload:
        if not file_path.is_file():
            print(f"ERROR: Asset file missing: {file_path}")
            continue

        file_name = file_path.name
        file_size = file_path.stat().st_size
        print(f"\n==> Preparing upload for {file_name} ({file_size / 1024 / 1024:.2f} MB)...")

        # If asset already exists, delete it first to ensure update
        if file_name in existing_assets:
            asset_id = existing_assets[file_name]
            print(f"  Deleting existing asset {file_name} (ID: {asset_id})...")
            del_req = urllib.request.Request(
                f"{api_base}/releases/assets/{asset_id}",
                headers=headers,
                method="DELETE"
            )
            try:
                with opener.open(del_req) as resp:
                    print("  Deleted.")
            except Exception as e:
                print(f"  Warning deleting asset: {e}")

        # Upload
        content_type = "application/vnd.microsoft.portable-executable" if file_name.endswith(".exe") else "application/zip"
        upload_url = f"{upload_base}?name={file_name}"
        data = file_path.read_bytes()

        upload_req = urllib.request.Request(
            upload_url,
            data=data,
            headers={
                **headers,
                "Content-Type": content_type,
                "Content-Length": str(file_size)
            }
        )

        t0 = time.time()
        with opener.open(upload_req) as resp:
            asset_res = json.loads(resp.read().decode("utf-8"))
            t1 = time.time()
            print(f"  [OK] Uploaded {file_name} in {t1 - t0:.1f}s")
            print(f"       Download URL: {asset_res['browser_download_url']}")

    print(f"\n==> All release assets published successfully to {release_info['html_url']} !")

if __name__ == "__main__":
    main()
