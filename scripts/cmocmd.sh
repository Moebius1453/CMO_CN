#!/usr/bin/env bash
# Send a UTF-8 command to the in-process CMOLocalizer command channel and print the JSON result.
# Usage: scripts/cmocmd.sh "CLICK_MENU 数据库查看器"
set -euo pipefail
GAME="G:/soft/steam/steamapps/common/Command - Modern Operations"
CMD="$1"
TIMEOUT="${2:-12}"

printf '%s' "$CMD" > "$GAME/CMOLocalizer_Cmd.txt"
rm -f "$GAME/CMOLocalizer_CmdResult.json"

for _ in $(seq 1 "$((TIMEOUT * 10))"); do
  sleep 0.1
  if [ -f "$GAME/CMOLocalizer_CmdResult.json" ]; then
    cat "$GAME/CMOLocalizer_CmdResult.json"
    exit 0
  fi
done
echo '{"status":"timeout"}'
exit 1
