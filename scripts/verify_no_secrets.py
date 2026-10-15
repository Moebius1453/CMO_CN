#!/usr/bin/env python3
"""密钥门禁：禁止任何明文凭据进入仓库（工作区、历史、发布产物）。

用法:
    python scripts/verify_no_secrets.py            # 扫描已跟踪文件（快，作为提交/发布门禁）
    python scripts/verify_no_secrets.py --history   # 追加扫描全部提交历史（git pickaxe）
退出码 0 = 干净，1 = 发现疑似凭据。
"""
from __future__ import annotations

import re
import subprocess
import sys
from pathlib import Path

PATTERNS = [
    ("LLM API Key", re.compile(r"\bsk-[A-Za-z0-9]{20,}\b")),
    ("GitHub Token", re.compile(r"\bgh[pousr]_[A-Za-z0-9]{20,}\b")),
    ("AWS Access Key", re.compile(r"\bAKIA[0-9A-Z]{16}\b")),
    ("Slack Token", re.compile(r"\bxox[baprs]-[A-Za-z0-9\-]{10,}")),
    ("Private Key", re.compile(r"-----BEGIN [A-Z ]*PRIVATE KEY-----")),
    ("JWT", re.compile(r"\beyJ[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,}")),
    ("Hardcoded key", re.compile(r"""(?i)(api[_-]?key|apikey|secret|token|passwd|password)\s*[:=]\s*["'][A-Za-z0-9_\-/+]{16,}["']""")),
]

# 允许出现的占位符/环境变量形式
ALLOW = re.compile(r"(os\.environ|getenv|Environment\.GetEnvironmentVariable|\*\*\*|REMOVED|MASKED|你的|xxxx|<[A-Za-z_]+>)")


def tracked_files() -> list[Path]:
    out = subprocess.run(["git", "ls-files"], capture_output=True, text=True, encoding="utf-8").stdout
    return [Path(p) for p in out.splitlines() if p.strip()]


def scan_worktree() -> list[str]:
    hits: list[str] = []
    for f in tracked_files():
        try:
            if f.stat().st_size > 5_000_000:
                continue
            text = f.read_text(encoding="utf-8", errors="ignore")
        except Exception:
            continue
        for i, line in enumerate(text.splitlines(), 1):
            if ALLOW.search(line):
                continue
            for label, rx in PATTERNS:
                if rx.search(line):
                    hits.append(f"{f}:{i}  [{label}]")
                    break
    return hits


def scan_history() -> list[str]:
    hits: list[str] = []
    for label, rx in PATTERNS:
        out = subprocess.run(["git", "log", "--all", "--oneline", "-G" + rx.pattern],
                             capture_output=True, text=True, encoding="utf-8", errors="ignore").stdout
        if out.strip():
            hits.append(f"[{label}] 历史命中 {len(out.strip().splitlines())} 个提交:\n    " +
                        "\n    ".join(out.strip().splitlines()[:5]))
    return hits


def main() -> int:
    deep = "--history" in sys.argv
    print("密钥门禁（verify_no_secrets）")
    print("-" * 62)
    w = scan_worktree()
    print(f"  已跟踪文件: {len(tracked_files())} 个 → 命中 {len(w)}")
    for h in w[:20]:
        print("   ✗ " + h)
    h = scan_history() if deep else []
    if deep:
        print(f"  提交历史: 命中 {len(h)} 组")
        for x in h:
            print("   ✗ " + x)
    if w or h:
        print("-" * 62)
        print("结果: [FAIL] 发现疑似明文凭据，禁止提交/发布")
        return 1
    print("-" * 62)
    print("结果: [PASS] 未发现明文凭据")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
