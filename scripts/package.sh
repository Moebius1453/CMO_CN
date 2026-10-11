#!/usr/bin/env bash
# Build the release artifacts for the current VERSION.
#
#   build/CMOLocalizer.dll          runtime engine
#   build/CMO_Chinese_Setup.exe     one-click installer (embeds dll + dict + templates)
#   build/CMO_Chinese_Pack_<ver>.zip   green/portable pack (unzip over the game root)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VER="$(tr -d '[:space:]' < "$ROOT/VERSION")"
OUT="$ROOT/build"
PACK="$OUT/pack"
PY="${PY:-C:/Users/admin/Desktop/ga-admin-portable-windows-amd64-v0.2.29/python/python.exe}"

echo "==> version $VER"
bash "$ROOT/scripts/build.sh"

echo "==> assembling green pack"
rm -rf "$PACK"
mkdir -p "$PACK/DB/Templates"
cp "$OUT/CMOLocalizer.dll" "$PACK/"
cp "$ROOT/dict/CMOLocalizer.json" "$PACK/"
cp "$ROOT/assets/db_templates/"*.html "$PACK/DB/Templates/"
cp "$ROOT/README.md" "$PACK/汉化说明.md"

# Command.exe.config patch that wires the AppDomainManager, without clobbering
# any binding redirects already present in the user's config.
"$PY" - "$PACK" <<'PYEOF'
import pathlib, re, sys
pack = pathlib.Path(sys.argv[1])
src = pathlib.Path("G:/soft/steam/steamapps/common/Command - Modern Operations/Command.exe.config")
text = src.read_text(encoding="utf-8-sig")
snippet = """    <appDomainManagerType value="CMOLocalizer.LocalizerDomainManager" />
    <appDomainManagerAssembly value="CMOLocalizer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null" />
"""
inject = text.replace("</runtime>", snippet + "  </runtime>")
(pack / "Command.exe.config.patch").write_text(inject, encoding="utf-8")
print("wrote Command.exe.config.patch")
PYEOF

ZIP="$OUT/CMO_Chinese_Pack_${VER}.zip"
rm -f "$ZIP"
"$PY" - "$PACK" "$ZIP" <<'PYEOF'
import pathlib, sys, zipfile
pack, dest = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
with zipfile.ZipFile(dest, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for f in sorted(pack.rglob("*")):
        if f.is_file():
            z.write(f, f.relative_to(pack))
print(f"wrote {dest} ({dest.stat().st_size} bytes)")
PYEOF
echo "==> $ZIP"

ls -la "$OUT"/*.dll "$OUT"/*.exe "$OUT"/*.zip
echo "==> package ok"
