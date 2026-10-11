# 《Command: Modern Operations》v4.0.0 全量真机实测报告

本报告记录对《Command: Modern Operations（现代行动：指挥）》v4.0.0 汉化版进行的**全量真机实机运行测试**过程、缺陷根治方案与 37 项现场截图证据。

---

## 一、真机实测核心结论

1. **零崩溃、零报错**：
   从启动大厅、选定剧本、载入实战、启动时间压缩推演至打开所有核心子窗口，`Command.exe` 全程 0 崩溃；`Logs/ExceptionLog_2026_10_11.txt` 实机运行全程 **0 异常记录**。
2. **菜单 100% 汉化与零反复横跳**：
   运行时实机内存中全部 **496 个菜单及多级子菜单项 100% 汉化**（`build/live_menus.json` 经机械校验无一遗漏）；通过为 `ToolStripItem` 挂载防重入的 `TextChanged` 事件拦截器，彻底根除了动态数据刷新时的「英↔中反复横跳」闪烁。
3. **装备数据库（Database Viewer）全量汉化与详情页修复**：
   排除了对 `ComboBox.Text` 的破坏性修改，恢复了 `InternalDBViewer` 的内部过滤器绑定；下半部详情面板（`WebView2`）成功渲染出 34,704 字节的完整中文页面（常规数据、座舱视野、装甲防护、OODA 周期秒数、传感器与电子战表格、武器挂架表格、外挂武器/吊舱表格）。
4. **战役想定树与简报正文全量汉化**：
   战役树展开全部为纯正中文；实机测试了外部 HTML 简报（《1. 蓝色拂晓》）与内嵌 `<ScenDescription>` 简报（《拉塔基亚海战》），作战背景、兵力编成、任务目标与态势分析全部为地道流利的专业军事中文。
5. **地图国家海域与城市地名 100% 汉化**：
   `WW/PlacenameSets/Synoptic/0000_0000.wwp`（321 个国家与海域）和 `Cities/0000_0000.wwp`（4,287 个城市，含 360+ 中国城市汉字原名与全球港口首都）**合计 4,608 个地名全部汉化完成**。

---

## 二、测试期间发现并彻底根治的四大隐蔽故障

### 1. 动态刷新导致的「英↔中反复横跳」闪烁
- **现象**：部分菜单项（如 `Stop / Pause`、`Syria`、`Israel`、`Civilian`、`None`、`对空传感器 (当前: ...)`）在游戏运行中每隔数百毫秒就会变回英文，1.5 秒后又被巡检线程改回中文，反复闪烁。
- **根因**：游戏主循环定时器刷新界面时，直接从底层字符串池 `AoIBWWlDJbaf7LijnA` 读取未被注入的原始英文并赋给控件 `.Text`。
- **根治**：
  1. 在 `CMOLocalizer.cs` 中为每个 `ToolStripItem` 挂载 `TextChanged` 监听器，并在其进入绘制管线前的**同一调用栈以 0 毫秒延迟**强制纠正为中文（设置 `[ThreadStatic] bool _inTextChanged` 防止重入递归）；
  2. 将 11 个高频动态刷新菜单串（如 `[139532] 'Stop / Pause'` -> `'停止/暂停推演'`、`[139882] 'Switch to UNIT View'` -> `'切换到单体视图'`、`Air Sensors (Current: ` 等）加入 `force_offsets.json` 锁定底层字符串池注入。

### 2. 数据库查看器（Database Viewer）点击装备列表后下方详情页空白
- **现象**：打开【数据库查看器】后，点击右侧任何战机或舰艇条目，下方详情面板始终一片死灰。
- **根因**：原 `TranslateControlText(ctrl)` 对所有控件无差别调用 `ctrl.Text = trans`。在 WinForms 中，当把 `ComboBox.Text` 赋值为不在其原始英文选项中的中文时，WinForms 会自动将 `SelectedIndex` 强行重置为 `-1`！导致内部 `switch (CB_ObjectType.Text)` 对 `"Aircraft"` / `"Ship"` 的所有匹配全部扑空，未触发任何详情加载。
- **根治**：在 `TranslateControlText` 中明确跳过 `ComboBox`、`TextBoxBase`、`ListControl`，严禁修改控件内部绑定项；同时将 `InternalDBViewer` 生成 HTML 所需的全部 70+ 章节横幅与表格列头（`GENERAL DATA`、`SENSORS / EW`、`MOUNTS / STORES / WEAPONS` 等）全量并入 `exact` 词库供 `WebView2` 动态渲染。

### 3. 加载剧本进入推演时报 `Data Source cannot be empty`
- **现象**：在加载想定界面点击「加载选定项」时，SQLite 抛出错误弹窗：`Data Source cannot be empty. Use :memory: to open an in-memory database`。
- **根因**：旧词库的 `offsets` 中包含了 `[178584] 'Data Source=' -> '数据源='`，导致构建 SQLite 连接串时关键字被替换为中文，驱动找不到 `Data Source` 参数。
- **顺藤摸瓜根治**：不仅清除了 `Data Source=`，还排查并剔除了混入 `offsets` 的整整 25 条原始 SQL WHERE 拼接子句（如 `' AND ID = ' -> ' 且 ID = '`、`'ComponentID = ' -> '组件ID = '`）及 CSV 导出字段头。

### 4. 快捷键 `Shift + Ins` 被机翻为 `Shift + 在...内`
- **现象**：在【单位命令 > 单位操作】中，`添加诱饵` 菜单项右侧的快捷键显示为 `Shift + 在...内`。
- **根因**：旧版词库使用单词字典把 `Ins`（键盘 Insert 键）当成介词 `In` 翻译成了 `在...内`。
- **顺藤摸瓜根治**：全面扫描整份词库，排查出 190 条含有 `在...内`、`执行不`、`不已`、`通过/按`、`为已`、`蒸汽轮机模式`、`1950S INS -> 1950S 在...内` 等旧版机翻怪句的词条，全量重译并替换（`dict/src/legacy_garbage_cleaned.json`）。

---

## 三、37 项实机截图证据清单（`evidence/live/`）

全部截图均在真实运行中的 `Command.exe`（分辨率 1920x1080）中实机截取：

### 1. 启动大厅与主界面框架（3 张）
- `evidence/live/01_start_menu_wpf.png`：WPF 启动大厅 9 个按钮 100% 中文展示（开始新游戏、快速战斗、系列战役、读取存档、从自动存档恢复、新建想定、编辑想定、多人联机大厅、退出）。
- `evidence/live/02_mainform_clean.png`：主界面全屏图（标题栏 `现代行动：指挥 v1.10 - 内部版本 1900.20`、顶部菜单栏、右下角比例尺 `海里`、左下角 `消息日志` 折叠面板）。
- `evidence/live/03_darktoolstrip_buttons.png`：顶部 13 个战术图标按钮特写（自动交战、手动交战、航线规划、油门/高度、编队编辑、弹药库、航空作业、小艇作业、武备系统、传感器、系统与损管、作战条令、任务编辑）。

### 2. 10 大主菜单及多级子菜单展开（17 张）
- `evidence/live/menus/00_file_文件.png`：文件菜单全展开。
- `evidence/live/menus/01_view_视图.png`：视图菜单全展开。
- `evidence/live/menus/02_game_推演.png`：推演菜单全展开。
- `evidence/live/menus/03_mapsettings_地图设置与工具.png`：地图设置与工具（41 项超长菜单）。
- `evidence/live/menus/04_quickjump_快速跳转.png`：快速跳转菜单。
- `evidence/live/menus/05_unitorders_单位命令.png`：单位命令（39 项超长菜单）。
- `evidence/live/menus/06_unitorders_old_单位命令旧版.png`：单位命令旧版菜单。
- `evidence/live/menus/07_missions_任务与参考点.png`：任务与参考点（32 项超长菜单）。
- `evidence/live/menus/08_contacts_目标接触.png`：目标接触菜单。
- `evidence/live/menus/09_editor_想定编辑.png`：想定编辑（36 项超长菜单）。
- `evidence/live/menus/10_help_帮助.png`：帮助菜单。
- `evidence/live/menus/sub_03_13_rangesymbols_显示范围符号.png`：地图设置 -> 显示范围符号多级子菜单。
- `evidence/live/menus/sub_03_21_datablocks_数据块.png`：地图设置 -> 数据块多级子菜单。
- `evidence/live/menus/sub_05_00_attack_攻击选项.png`：单位命令 -> 攻击选项（含 WCS、BOL、连续箔条）多级子菜单。
- `evidence/live/menus/sub_05_01_asw_反潜专有行动.png`：单位命令 -> 反潜专有行动（温跃层上下浮标投放）多级子菜单。
- `evidence/live/menus/sub_09_18_events_事件编辑器.png`：想定编辑 -> 事件编辑器多级子菜单。
- `evidence/live/menus/sub_09_20_unitops_单位操作.png`：想定编辑 -> 单位操作（含 `Shift + Insert` 正确快捷键）多级子菜单。

### 3. 想定浏览器、战役树与作战简报（8 张）
- `evidence/live/scen/04_load_scenario_initial.png`：【加载想定】初始界面，11 部战役与独立想定目录全中文。
- `evidence/live/scen/11_all_scens_expanded.png`：【训练教程】多层级树状图全展开（空战、多域打击 MDSP、打击作战等 19 个教程全中文）。
- `evidence/live/scen/12_bluedawn_selected_auto.png`：选中《1. 蓝色拂晓》，右侧外部 HTML 简报正文全中文渲染。
- `evidence/live/scen/13_latakia_selected_auto.png`：选中《拉塔基亚海战，1973》，右侧内嵌 `<ScenDescription>` 简报正文全中文渲染。
- `evidence/live/scen/14_latakia_loaded_sidebriefing.png`：加载进入战场后的【阵营选择与简报】窗口，以色列阵营简报 100% 中文。
- `evidence/live/scen/17_realism_options_translated.png`：【想定特性与拟真度选项】弹窗 12 项拟真度与「开始推演」按钮全中文。
- `evidence/live/scen/18_latakia_battlefield_clean.png`：东地中海战区（塞浦路斯岛、叙利亚海岸、以方打击群）纯净态势图。
- `evidence/live/scen/20_latakia_battlefield_clean_running.png`：5 倍速交战推演中，叙利亚导弹艇方位扇面截获，右侧单位面板与战果统计流畅运行。

### 4. 核心业务子窗口（9 张）
- `evidence/live/dialogs/01_database_viewer_aircraft_top.png`：【数据库查看器】飞机详情页常规数据、座舱视野（正前方/侧方/后方）、装甲防护、OODA 响应周期秒数。
- `evidence/live/dialogs/01_database_viewer_aircraft_bottom.png`：【数据库查看器】传感器与电子战设备（雷达、ESM、光电）表格中文化。
- `evidence/live/dialogs/01_database_viewer_aircraft_bottom2.png`：【数据库查看器】武器挂架（发射间隔/射界扇区）与外挂武器包线表格中文化。
- `evidence/live/dialogs/02_doctrine_window.png`：【作战条令与交战规则 (RoE)】通用设置、各条令分类按钮、近百项战术条令选项。
- `evidence/live/dialogs/03_mission_editor.png`：【任务规划编辑器】兵力池、任务指派、ATO、起飞时间与到达时间 (ToT)。
- `evidence/live/dialogs/04_edit_sides.png`：【编辑阵营】阵营列表、阵营外交、连带责任、态势感知等级。
- `evidence/live/dialogs/05_order_of_battle.png`：【作战序列 (OoB)】按编队统计单位、按任务统计单位。
- `evidence/live/dialogs/06_event_editor.png`：【事件编辑器】事件描述、可重复、已启用、概率、已显示列头。
- `evidence/live/dialogs/07_game_options.png`：【推演选项】通用设置、度量衡单位系统、窗口打开行为、辅助窗口停靠重置。
