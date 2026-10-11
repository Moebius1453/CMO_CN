#!/usr/bin/env bash
# Launch the game, wait for the start window, and report what is on screen.
#
# Exit code 0 = clean start (no start-up error dialog)
# Exit code 1 = the start-up error dialog is present
#
#   scripts/verify_launch.sh [wait-seconds]
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GAME="G:/soft/steam/steamapps/common/Command - Modern Operations"
WAIT="${1:-42}"

taskkill //F //IM Command.exe >/dev/null 2>&1 || true
sleep 2
rm -f "$GAME/CMOLocalizer.log"
(cd "$GAME" && cmd //c start "" "$(cygpath -w "$GAME/Command.exe")")
sleep "$WAIT"

PID="$(tasklist 2>/dev/null | grep -i '^Command.exe' | awk '{print $2}' | head -1 || true)"
if [ -z "$PID" ]; then
  echo "FAIL: Command.exe is not running"
  exit 1
fi

OUT="$("$ROOT/build/WinProbe.exe" probe "$PID")"
echo "--- visible top-level windows ---"
echo "$OUT" | grep "vis=True" | sed 's/.*class=/  class=/' || true

echo "--- dictionary load ---"
head -2 "$GAME/CMOLocalizer.log" 2>/dev/null || echo "(no log)"

if echo "$OUT" | grep -q "Application startup\|应用程序启动"; then
  echo "RESULT: FAIL - start-up error dialog present"
  exit 1
fi
echo "RESULT: PASS - clean start"
