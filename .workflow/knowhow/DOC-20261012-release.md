---
title: 禁止擅自执行Release打包与发布
type: document
category: review
explicitId: doc-20261012-release
created: 2026-10-12T12:09:28.809Z
keywords:
  - 打包
  - package
  - 发布
  - release
  - 权限
  - 禁令
lifecycleStatus: active
---

## 规则约束：未经用户明确要求严禁自行打包与发布

### 核心铁律
1. 未经用户明确下达指令（如明确说“打包”、“打个包”、“发布Release”），**绝对严禁擅自执行 ==> version 4.0.0
==> building CMOLocalizer.dll
==> building CMO_Chinese_Setup.exe
total 9897
drwxr-xr-x 1 admin 197609       0 Oct 12 20:09 .
drwxr-xr-x 1 admin 197609       0 Oct 11 23:46 ..
-rwxr-xr-x 1 admin 197609   83968 Oct 12 20:09 CMOLocalizer.dll
-rw-r--r-- 1 admin 197609 1284740 Oct 12 14:30 CMO_Chinese_Pack_4.0.0.zip
-rwxr-xr-x 1 admin 197609 3730432 Oct 12 20:09 CMO_Chinese_Setup.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 12 19:55 Disasm.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 12 18:58 FindCallers.exe
-rwxr-xr-x 1 admin 197609   11264 Oct 11 21:32 GuiDriver.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 16:19 ProbeDarkTreeView.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 23:00 ProbeLoadScenarioMethods.exe
-rwxr-xr-x 1 admin 197609    6144 Oct 11 12:57 WinProbe.exe
-rw-r--r-- 1 admin 197609   11082 Oct 12 01:12 all_menu_item_fields.txt
-rw-r--r-- 1 admin 197609  381810 Oct 11 22:35 all_tutorials_dump.json
-rw-r--r-- 1 admin 197609 1558691 Oct 11 13:11 bisect_dict.json
-rwxr-xr-x 1 admin 197609   12288 Oct 12 19:17 build_chinese_tiles.exe
-rw-r--r-- 1 admin 197609       3 Oct 11 17:07 command_core_offsets.tsv
-rw-r--r-- 1 admin 197609    3404 Oct 11 18:41 db_viewer_dump.json
-rw-r--r-- 1 admin 197609   98831 Oct 11 22:46 done_air_warfare.json
-rw-r--r-- 1 admin 197609   51948 Oct 11 23:42 done_encrypted_briefings.json
-rw-r--r-- 1 admin 197609   54597 Oct 11 22:46 done_other_tutorials.json
-rw-r--r-- 1 admin 197609  119960 Oct 11 22:55 done_submarines.json
-rw-r--r-- 1 admin 197609  108220 Oct 11 22:54 done_uncle_mark.json
-rwxr-xr-x 1 admin 197609    8704 Oct 11 22:35 dump_all_tutorials.exe
-rwxr-xr-x 1 admin 197609    8192 Oct 11 23:34 dump_encrypted_briefings.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 17:24 dump_gaw.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 22:30 dump_scen_xml.exe
-rw-r--r-- 1 admin 197609  189279 Oct 11 23:34 encrypted_briefings_dump.tsv
-rwxr-xr-x 1 admin 197609    5120 Oct 11 23:26 find_crypto.exe
-rwxr-xr-x 1 admin 197609    6656 Oct 11 17:07 find_ew_strings.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 23:30 find_offset_method.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 22:28 find_scen_decomp.exe
-rwxr-xr-x 1 admin 197609    6144 Oct 11 23:27 find_sharedkey.exe
-rw-r--r-- 1 admin 197609  692794 Oct 11 22:30 flight1.xml
-rw-r--r-- 1 admin 197609   33950 Oct 12 09:30 garbage_audit_report.json
-rwxr-xr-x 1 admin 197609    6144 Oct 12 01:36 inspect_airops_lua.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 12 09:41 inspect_boatops_buttons.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 11 23:28 inspect_crypto.exe
-rwxr-xr-x 1 admin 197609    4608 Oct 12 00:57 inspect_darkcb.exe
-rwxr-xr-x 1 admin 197609    4608 Oct 12 00:57 inspect_darkcb2.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 12 00:56 inspect_dbviewer_cb.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 11 23:25 inspect_fromxml.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 11 23:25 inspect_fromxml2.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 11 23:25 inspect_fromxml3.exe
-rwxr-xr-x 1 admin 197609    6144 Oct 11 17:23 inspect_getallweapons.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 23:23 inspect_getbriefing.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 18:53 inspect_internal_dbviewer.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 22:59 inspect_loadfrompath.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 12 01:52 inspect_readyac_click.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 23:30 inspect_side_il.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 23:31 inspect_side_il2.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 12 01:06 inspect_unitstatus_wpf.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 15:55 inspect_webviews.exe
-rwxr-xr-x 1 admin 197609    6144 Oct 12 01:33 list_scen_units.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 11 23:24 list_side_methods.exe
-rw-r--r-- 1 admin 197609   64935 Oct 11 15:28 live_menus.json
-rw-r--r-- 1 admin 197609   88987 Oct 11 20:43 missing_db_enums.json
-rw-r--r-- 1 admin 197609    3566 Oct 11 15:40 open_forms.json
-rw-r--r-- 1 admin 197609    3625 Oct 11 16:09 open_forms2.json
-rw-r--r-- 1 admin 197609    2841 Oct 11 16:43 open_forms_loaded.json
drwxr-xr-x 1 admin 197609       0 Oct 12 14:30 pack
-rwxr-xr-x 1 admin 197609    4608 Oct 11 22:27 probe_comp.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 11 22:59 probe_load_scen_method.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 11 22:28 probe_lz4.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 11 22:26 probe_scen.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 11 22:29 probe_scen_core.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 11 23:17 probe_side_briefing.exe
-rwxr-xr-x 1 admin 197609    6656 Oct 11 23:24 probe_side_decrypted.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 12 01:12 scan_all_context_menus.exe
-rwxr-xr-x 1 admin 197609    6144 Oct 11 17:03 scan_dal.exe
-rw-r--r-- 1 admin 197609   87432 Oct 11 22:27 scen_b64.txt
-rw-r--r-- 1 admin 197609   65357 Oct 11 22:31 sub1_1.xml
-rwxr-xr-x 1 admin 197609    5632 Oct 11 23:28 test_decrypt.exe
-rwxr-xr-x 1 admin 197609    6144 Oct 11 23:29 test_decrypt2.exe
-rwxr-xr-x 1 admin 197609    6144 Oct 11 23:27 test_decrypt_briefing.exe
-rwxr-xr-x 1 admin 197609    6656 Oct 11 23:32 test_side_fromxml.exe
-rw-r--r-- 1 admin 197609   26747 Oct 11 22:36 tutorial_instructions.json
-rw-r--r-- 1 admin 197609  247785 Oct 11 23:15 uncle1.xml
-rw-r--r-- 1 admin 197609   46864 Oct 11 23:34 untranslated_encrypted_briefings.json
-rw-r--r-- 1 admin 197609  294746 Oct 11 22:35 untranslated_tutorial_segments.json
-rw-r--r-- 1 admin 197609   72617 Oct 11 22:38 work_air_warfare.json
-rw-r--r-- 1 admin 197609   39497 Oct 11 22:38 work_other_tutorials.json
-rw-r--r-- 1 admin 197609   80518 Oct 11 22:38 work_submarines.json
-rw-r--r-- 1 admin 197609  102123 Oct 11 22:38 work_uncle_mark.json
==> build ok
==> assembling green pack
wrote Command.exe.config.patch
wrote G:\code\cmo-CN\build\CMO_Chinese_Pack_4.0.0.zip (1288886 bytes)
==> /g/code/cmo-CN/build/CMO_Chinese_Pack_4.0.0.zip
-rwxr-xr-x 1 admin 197609   83968 Oct 12 20:09 /g/code/cmo-CN/build/CMOLocalizer.dll
-rw-r--r-- 1 admin 197609 1288886 Oct 12 20:09 /g/code/cmo-CN/build/CMO_Chinese_Pack_4.0.0.zip
-rwxr-xr-x 1 admin 197609 3730432 Oct 12 20:09 /g/code/cmo-CN/build/CMO_Chinese_Setup.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 12 19:55 /g/code/cmo-CN/build/Disasm.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 12 18:58 /g/code/cmo-CN/build/FindCallers.exe
-rwxr-xr-x 1 admin 197609   11264 Oct 11 21:32 /g/code/cmo-CN/build/GuiDriver.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 16:19 /g/code/cmo-CN/build/ProbeDarkTreeView.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 23:00 /g/code/cmo-CN/build/ProbeLoadScenarioMethods.exe
-rwxr-xr-x 1 admin 197609    6144 Oct 11 12:57 /g/code/cmo-CN/build/WinProbe.exe
-rwxr-xr-x 1 admin 197609   12288 Oct 12 19:17 /g/code/cmo-CN/build/build_chinese_tiles.exe
-rwxr-xr-x 1 admin 197609    8704 Oct 11 22:35 /g/code/cmo-CN/build/dump_all_tutorials.exe
-rwxr-xr-x 1 admin 197609    8192 Oct 11 23:34 /g/code/cmo-CN/build/dump_encrypted_briefings.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 17:24 /g/code/cmo-CN/build/dump_gaw.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 22:30 /g/code/cmo-CN/build/dump_scen_xml.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 23:26 /g/code/cmo-CN/build/find_crypto.exe
-rwxr-xr-x 1 admin 197609    6656 Oct 11 17:07 /g/code/cmo-CN/build/find_ew_strings.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 23:30 /g/code/cmo-CN/build/find_offset_method.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 22:28 /g/code/cmo-CN/build/find_scen_decomp.exe
-rwxr-xr-x 1 admin 197609    6144 Oct 11 23:27 /g/code/cmo-CN/build/find_sharedkey.exe
-rwxr-xr-x 1 admin 197609    6144 Oct 12 01:36 /g/code/cmo-CN/build/inspect_airops_lua.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 12 09:41 /g/code/cmo-CN/build/inspect_boatops_buttons.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 11 23:28 /g/code/cmo-CN/build/inspect_crypto.exe
-rwxr-xr-x 1 admin 197609    4608 Oct 12 00:57 /g/code/cmo-CN/build/inspect_darkcb.exe
-rwxr-xr-x 1 admin 197609    4608 Oct 12 00:57 /g/code/cmo-CN/build/inspect_darkcb2.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 12 00:56 /g/code/cmo-CN/build/inspect_dbviewer_cb.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 11 23:25 /g/code/cmo-CN/build/inspect_fromxml.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 11 23:25 /g/code/cmo-CN/build/inspect_fromxml2.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 11 23:25 /g/code/cmo-CN/build/inspect_fromxml3.exe
-rwxr-xr-x 1 admin 197609    6144 Oct 11 17:23 /g/code/cmo-CN/build/inspect_getallweapons.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 23:23 /g/code/cmo-CN/build/inspect_getbriefing.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 18:53 /g/code/cmo-CN/build/inspect_internal_dbviewer.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 22:59 /g/code/cmo-CN/build/inspect_loadfrompath.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 12 01:52 /g/code/cmo-CN/build/inspect_readyac_click.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 23:30 /g/code/cmo-CN/build/inspect_side_il.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 23:31 /g/code/cmo-CN/build/inspect_side_il2.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 12 01:06 /g/code/cmo-CN/build/inspect_unitstatus_wpf.exe
-rwxr-xr-x 1 admin 197609    5120 Oct 11 15:55 /g/code/cmo-CN/build/inspect_webviews.exe
-rwxr-xr-x 1 admin 197609    6144 Oct 12 01:33 /g/code/cmo-CN/build/list_scen_units.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 11 23:24 /g/code/cmo-CN/build/list_side_methods.exe
-rwxr-xr-x 1 admin 197609    4608 Oct 11 22:27 /g/code/cmo-CN/build/probe_comp.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 11 22:59 /g/code/cmo-CN/build/probe_load_scen_method.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 11 22:28 /g/code/cmo-CN/build/probe_lz4.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 11 22:26 /g/code/cmo-CN/build/probe_scen.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 11 22:29 /g/code/cmo-CN/build/probe_scen_core.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 11 23:17 /g/code/cmo-CN/build/probe_side_briefing.exe
-rwxr-xr-x 1 admin 197609    6656 Oct 11 23:24 /g/code/cmo-CN/build/probe_side_decrypted.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 12 01:12 /g/code/cmo-CN/build/scan_all_context_menus.exe
-rwxr-xr-x 1 admin 197609    6144 Oct 11 17:03 /g/code/cmo-CN/build/scan_dal.exe
-rwxr-xr-x 1 admin 197609    5632 Oct 11 23:28 /g/code/cmo-CN/build/test_decrypt.exe
-rwxr-xr-x 1 admin 197609    6144 Oct 11 23:29 /g/code/cmo-CN/build/test_decrypt2.exe
-rwxr-xr-x 1 admin 197609    6144 Oct 11 23:27 /g/code/cmo-CN/build/test_decrypt_briefing.exe
-rwxr-xr-x 1 admin 197609    6656 Oct 11 23:32 /g/code/cmo-CN/build/test_side_fromxml.exe
==> package ok、生成最终分发压缩包或向 GitHub Releases 推送**。
2. 开发、汉化与调试验证过程中，只需在本地编译引擎 DLL、更新主词库并通过  / 实机截图验证功能正确性即可。
3. 打包是最终交付层行为，消耗较大算力且生成庞大产物，必须由用户完全自主掌控交付节奏与时机。
