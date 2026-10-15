#!/usr/bin/env python3
"""凭据读取规范（唯一入口）——密钥绝不入库。

解析顺序（先命中先用）:
  1. 环境变量        CMO_LLM_API_KEY / CMO_LLM_API_URL ...
  2. 用户级私有文件  %USERPROFILE%\.cmo-cn\secrets.json      （永不入库）
  3. 仓库内本地文件  <repo>/.secrets.local.json              （已在 .gitignore）
用法:
    from credentials import get_credential
    key = get_credential("llm_api_key", env="CMO_LLM_API_KEY")
缺失时抛出清晰错误，提示三种放置方式，避免有人为了方便又把密钥写回代码。
"""
from __future__ import annotations

import json
import os
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
USER_FILE = Path(os.path.expanduser("~")) / ".cmo-cn" / "secrets.json"
LOCAL_FILE = ROOT / ".secrets.local.json"


def _load(path: Path) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return {}


def get_credential(name: str, env: str | None = None, required: bool = True) -> str | None:
    if env and os.environ.get(env):
        return os.environ[env]
    for f in (USER_FILE, LOCAL_FILE):
        v = _load(f).get(name)
        if v:
            return v
    if not required:
        return None
    raise SystemExit(
        f"缺少凭据 [{name}]（环境变量 {env or name.upper()}）。请三选一：\n"
        f"  1) set {env or name.upper()}=<值>            （临时/CI 推荐）\n"
        f"  2) 写入 {USER_FILE}      （本机长期，永不入库）\n"
        f"  3) 写入 {LOCAL_FILE}      （仓库内但已被 .gitignore 忽略）\n"
        f"切勿把密钥写进任何被 git 跟踪的文件——verify_no_secrets.py 会把提交挡下来。"
    )


if __name__ == "__main__":
    n = sys.argv[1] if len(sys.argv) > 1 else "llm_api_key"
    v = get_credential(n, env="CMO_" + n.upper(), required=False)
    print(f"{n}: " + ("已配置（%d 字符）" % len(v) if v else "未配置"))
