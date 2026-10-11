#!/usr/bin/env bash
# Deploy the built engine and dictionary into the game directory.
#
#   scripts/deploy.sh            deploy build/CMOLocalizer.dll + dict/CMOLocalizer.json
#   scripts/deploy.sh --verify   deploy, then launch and report the window set
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GAME="G:/soft/steam/steamapps/common/Command - Modern Operations"
PY="${PY:-C:/Users/admin/Desktop/ga-admin-portable-windows-amd64-v0.2.29/python/python.exe}"

[ -f "$ROOT/build/CMOLocalizer.dll" ] || { echo "run scripts/build.sh first"; exit 1; }

cp "$ROOT/build/CMOLocalizer.dll" "$GAME/"
cp "$ROOT/dict/CMOLocalizer.json" "$GAME/"
cp "$ROOT/assets/db_templates/"*.html "$GAME/DB/Templates/"

"$PY" - "$GAME" <<'PYEOF'
import json, pathlib, sys
game = pathlib.Path(sys.argv[1])
d = json.loads((game / "CMOLocalizer.json").read_text(encoding="utf-8"))
print(f"deployed: offsets={len(d['offsets'])} exact={len(d['exact'])} "
      f"version={d['_meta']['version']}")
PYEOF

if [ "${1:-}" = "--verify" ]; then
  "${BASH_SOURCE[0]%/*}/verify_launch.sh"
fi

echo "==> deploy ok"
