#!/usr/bin/env bash
# Build the CMO localization engine DLL and the one-click installer.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VER="$(tr -d '[:space:]' < "$ROOT/VERSION")"
CSC="/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe"
OUT="$ROOT/build"
SETUP_EXE="CMO_Chinese_Setup_v${VER}.exe"
mkdir -p "$OUT"

# csc.exe is a native Windows binary: hand it backslash paths.
W() { cygpath -w "$1"; }

echo "==> building CMOLocalizer.dll"
WPF="/c/Windows/Microsoft.NET/Framework64/v4.0.30319/WPF"
"$CSC" -nologo -target:library -platform:anycpu -optimize+ \
  -out:"$(W "$OUT/CMOLocalizer.dll")" \
  -reference:System.dll -reference:System.Core.dll \
  -reference:System.Data.dll \
  -reference:System.Windows.Forms.dll -reference:System.Drawing.dll \
  -reference:System.Xml.dll -reference:System.Xml.Linq.dll \
  -reference:"$(W "$WPF/WindowsBase.dll")" \
  -reference:"$(W "$WPF/PresentationCore.dll")" \
  -reference:"$(W "$WPF/PresentationFramework.dll")" \
  -reference:"$(W "/c/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Xaml.dll")" \
  "$(W "$ROOT/src/engine/CMOLocalizer.cs")"

echo "==> building $SETUP_EXE"
RES=()
RES+=(-resource:"$(W "$OUT/CMOLocalizer.dll")",CMOLocalizer.dll)
RES+=(-resource:"$(W "$ROOT/dict/CMOLocalizer.json")",CMOLocalizer.json)
RES+=(-resource:"$(W "$ROOT/assets/donate/wechat_pay.png")",QR_WeChat)
RES+=(-resource:"$(W "$ROOT/assets/donate/alipay.jpg")",QR_Alipay)
for t in "$ROOT"/assets/db_templates/*.html; do
  RES+=(-resource:"$(W "$t")","TPL_$(basename "$t")")
done
# Fully localized Scenarios HTML briefing/tutorial bundle (produced by
# scripts/localize_html_files.py and zipped by scripts/package.sh).
if [ -f "$OUT/Scenarios_HTML.zip" ]; then
  RES+=(-resource:"$(W "$OUT/Scenarios_HTML.zip")",SCEN_HTML_ZIP)
else
  echo "==> warning: build/Scenarios_HTML.zip missing; installer will fall back to runtime injection"
fi
"$CSC" -nologo -target:winexe -platform:anycpu -optimize+ \
  -out:"$(W "$OUT/$SETUP_EXE")" \
  -reference:System.dll -reference:System.Core.dll \
  -reference:System.Windows.Forms.dll -reference:System.Drawing.dll \
  -reference:System.IO.Compression.dll -reference:System.IO.Compression.FileSystem.dll \
  "${RES[@]}" \
  "$(W "$ROOT/src/installer/CMO_Chinese_Setup.cs")"

# Keep standard CMO_Chinese_Setup.exe as compatibility copy
cp -f "$OUT/$SETUP_EXE" "$OUT/CMO_Chinese_Setup.exe"

ls -la "$OUT"
echo "==> build ok"
