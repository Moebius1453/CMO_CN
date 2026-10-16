#!/usr/bin/env bash
# One-shot migration: legacy flat 汉化/ workspace -> structured cmo-CN repo.
set -euo pipefail

SRC="C:/Users/admin/Desktop/ga-admin-portable-windows-amd64-v0.2.29/GenericAgent/temp/projects/汉化"
DST="G:/code/cmo-CN"

cd "$SRC"

mkdir -p "$DST"/{docs,dict,scripts,assets/db_templates,assets/scenario_briefings,build,evidence/screenshots,evidence/logs}
mkdir -p "$DST"/src/{engine,installer,tools,tests}

# --- Core engine + installer ---
cp -f src/CMOLocalizer.cs      "$DST/src/engine/"
cp -f src/CMO_Chinese_Setup.cs "$DST/src/installer/"

# --- Probe / inspection utilities (IL + reflection analysis) ---
for f in src/Disasm.cs src/ILScanner.cs src/DumpIL.cs src/DumpTokens.cs src/FindCallers.cs \
         src/FindLoadTypes.cs src/FindStringMethod.cs src/FindStringMethod2.cs src/FindTVScensUsage.cs \
         src/Inspect.cs src/InspectDetail.cs src/InspectFormIL.cs src/InspectG7.cs src/InspectGameDLL.cs \
         src/InspectLoadForm.cs src/InspectLoadScenario.cs src/InspectScenLoad.cs src/InspectTV.cs \
         src/InspectTree.cs src/InspectWrapper.cs src/InspectWrapper2.cs src/ProbeCodersLab.cs \
         src/ProbeDarkTreeView.cs src/ProbeLoadScenarioMethods.cs src/ProbeLoadScenarioNested.cs \
         src/ProbeNodeDetails.cs src/ProbeNodeFullPath.cs src/ProbeScenarioForm.cs \
         check_core.cs dump_all_sandbox.cs dump_baml.cs dump_dal.cs dump_refresh.cs dump_refresh2.cs \
         dump_refresh3.cs dump_sf_ui.cs dump_target.cs find_caller.cs find_decompress.cs find_refresh.cs \
         inspect_all_webviews.cs inspect_brief_methods.cs inspect_dbfuncs.cs inspect_decompress.cs \
         inspect_ew_il.cs inspect_fields.cs inspect_fields2.cs inspect_forms.cs inspect_forms2.cs \
         inspect_loadscen.cs inspect_loadscen_ctrls.cs inspect_loadscen_il.cs inspect_loadscen_methods.cs \
         inspect_render_methods.cs inspect_sandbox.cs inspect_sandbox_x64.cs inspect_sf.cs \
         inspect_sf_fields.cs inspect_ui_targets.cs inspect_wv.cs inspect_wv_events.cs \
         inspect_wv_methods.cs inspect_wv_msg.cs scan_core.cs scan_dal.cs scan_ui_dialogs.cs ; do
  [ -f "$f" ] && cp -f "$f" "$DST/src/tools/"
done

# --- Test harnesses ---
for f in src/CMOLocalizerTest.cs src/CMOLocalizerTestV2.cs src/DummyHost.cs src/TestDarkReflection.cs \
         src/TestHijack.cs src/TestV3.cs test_db_load.cs search_methods.cs ; do
  [ -f "$f" ] && cp -f "$f" "$DST/src/tests/"
done

# --- Dictionaries ---
cp -f CMOLocalizer.json "$DST/dict/"
cp -f translations/ui_terms.json translations/cmo_master_dictionary.json "$DST/dict/"
cp -f scen_tree_dict.json all_scen_items.json extracted_campaigns_scens.json "$DST/dict/" 2>/dev/null || true

# --- Pipeline scripts ---
# 注：shards/translate_shard2.py 结构上不可解析（ast.parse → SyntaxError: unmatched '}' @580）
# 且从不写出任何结果，已归档为 $DST/scripts/archive/translate_shard2.py（只读历史，
# 不参与任何流程），因此不列入拷贝清单——本脚本不再引用它。
for f in deep_translator_engine.py deep_translator_engine_v2.py find_nav_calls.py \
         generate_deep_localization.py shards/run_translate.py shards/translate_shard1_a.py ; do
  [ -f "$f" ] && cp -f "$f" "$DST/scripts/"
done

# --- Assets ---
cp -f DB_Templates_Chinese/*.html "$DST/assets/db_templates/" 2>/dev/null || true

# --- Docs ---
for f in *.md README_汉化说明.txt ; do
  [ -f "$f" ] && cp -f "$f" "$DST/docs/"
done

echo "migration copy complete"
