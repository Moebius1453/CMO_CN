# 汉化问题台账（BUG REGISTRY）

> 用途：**每一个用户反馈过的问题都要在这里留案**——现象、根因、改动位置、用户可见验收、回归测试点。
> 发版前必须按 `docs/RELEASE_TEST_SOP.md` 对台账里所有 `回归` 项逐条测试；
> 能自动化的用例由 `scripts/verify_regressions.py` 执行（`static` 子集进 `package.sh` 硬门禁，`--live` 子集发版前必跑）。
>
> 编号规则：`R<序号>`，**只增不改**；修复后若用例失效或需调整，追加新条目并在旧条目注明「已被 Rx 取代」。
> 新增问题必须同时：① 写台账条目；② 落一条可执行的回归用例（优先写进 verify_regressions.py）；
> ③ 在 `docs/CHANGELOG.md` 对应版本段落说明。

| 编号 | 版本 | 用户可感知现象 | 回归用例（自动化） | 状态 |
|---|---|---|---|---|
| R1 | 4.1.5 | 数据库查看器详情面板空白 + 中文闪成英文 | `static` CB_ObjectType 保护 + `live` 八类详情渲染/token/无频闪 | 已修 |
| R2 | 4.1.4 | 装了汉化地图仍然是英文 | `static` 地名集资产 + `live` 中文标签 | 已修 |
| R3 | 4.1.4 | 任务编辑器载荷名括号内英文 | `static` 词库/术语断言（verify_terminology） | 已修 |
| R4 | 4.1.4 | 右侧单位状态面板中英频闪 | `live` 两帧截图 md5 一致 | 已修 |
| R5 | 4.1.3 | 工具栏按钮文字仍是英文 | `static` 中文按钮图覆盖引擎期望文件名 | 已修 |
| R6 | 4.0.0 | 加载想定报 `Data Source cannot be empty` | `static` offsets 无 SQL 片段（verify_release） | 已修 |
| R7 | 4.0.0 | 快捷键显示 `Shift + 在...内` | `static` 词库机翻残留（verify_terminology） | 已修 |
| R8 | 4.0.0 | 数据库查看器点击条目详情空白（首例） | `static` 禁止改写 ComboBox/ListControl/TextBoxBase 的 .Text | 已修（R1 为其复发） |
| R9 | 4.1.3 | 教程/简报正文仍英文、WebView2 注入崩溃 | `static` 简报/教程覆盖门禁 | 已修 |
| R10 | 4.1.4 | 消息日志多行教程消息漏译 | 手动抽样（日志窗口） | 已修 |
| R11 | 4.1.2 | 数据库筛选下拉框汉化后筛选失效 | `live` 国家/子类型真实交互联动 | 已修 |
| R12 | 4.1.4 | 工具栏「自动交战/手动交战」提示词未汉化 | `static` 词条存在性 | 已修 |

---

## R1 数据库查看器详情面板空白 + 点击后中文闪成英文（v4.1.5）

- **现象**：打开【数据库查看器】→ 列表有数据、筛选可用，但点击任何装备条目，下方详情面板（WebView2）一片死灰；
  点击时中文会闪一下变回英文。
- **根因**：v4.1.3「下拉框全量汉化」把汉化铺到**通用路径**
  （`TranslateControlTree` → `LocalizeComboBoxSafe` → `TranslateComboItems` → `TranslateDBComboItem`），
  而 `InternalDBViewer.CB_ObjectType` 是**游戏按控件文本分派**的控件：
  异常栈 `DarkListView.OnMouseDown → DisplaySelection → DisplayInfoInDetailsPanel(_SelectedObjectID, _SelectedObjectType)`。
  选项被改写成「飞机 / 航空器 (Aircraft)」后所有分支落空 → 不生成详情 HTML（`RenderedHTMLString` = 0）；
  巡检每轮重译同一控件造成中英闪烁。专用路径原有的保护被通用路径绕过（通用路径先执行）。
- **修复位置**：`src/engine/CMOLocalizer.cs`
  - `IsProtectedDBViewerCombo()`：通用路径 `LocalizeComboBoxSafe` 与专用路径 `LocalizeDBViewerComboBox` 统一生效；
  - `FindDBViewerForm()` / `IsDBViewerForm()`：`EnumWindows` + `Control.FromHandle` 回退（该窗体在辅助 UI 线程创建，
    `Application.OpenForms` 查不到），替换 6 处重复查找，恢复 `DB_*` 诊断命令。
- **用户可见验收**：数据库查看器点击条目 → 详情页显示中文「概述说明 / 常规数据 / 装备大类 / 机体尺寸 …」并带配图。
- **回归测试点**（`verify_regressions.py`）：
  - `static`：源码中 `IsProtectedDBViewerCombo` 必须在两条路径都被调用（防止有人删掉保护）；
  - `live`：八类对象（Aircraft/Ship/Submarine/Weapon/Facility/Satellite/Ground Unit/Sensor）逐个切换后点击条目，
    `DB_INSPECT.html_len` 必须 > 阈值（Aircraft ≥ 20000，其余 ≥ 5000）；
  - `live`：`CB_ObjectType` 文本必须是游戏原生 token（`Aircraft`/`Ship`/…），出现中文即回归失败；
  - `live`：同一稳定态两帧截图 md5 必须一致（无中英频闪）。
- **证据**：`evidence/live/dbviewer/99_live_repro.png`（故障）vs `103_pane_rendered.png`、`140_final_state.png`（修复后）；
  发布后实机：`evidence/live/dbviewer/200_release_v415_installer_live.png`（html_len 34 874）。

## R2 装了汉化地图仍然英文（v4.1.4）

- **现象**：用户装包后地图上国名/海域/城市名仍为英文。
- **根因**：安装器/绿色包从未分发 `WW/PlacenameSets/{Synoptic,Cities}/0000_0000.wwp`
  （World Wind 地名集是地图标签数据源），引擎再汉化也无效。
- **修复位置**：`src/installer/CMO_Chinese_Setup.cs`（`DeployPlacenames` / `RestorePlacenames`，带 `.orig` 备份）、
  `scripts/package.sh`（绿色包携带 + 资产覆盖断言）、`assets/placenames/*.wwp`。
- **用户可见验收**：装包后地图国名/海名/城市名为中文；卸载可还原。
- **回归测试点**：`static` 两个 `.wwp` 存在且含汉字、安装器内嵌引用存在；`live` 地图标签为中文（人工/截图）。

## R3 任务编辑器「可用」列表括号内载荷名英文（v4.1.4）

- **现象**：行文本「苏法/风暴 #1 (Air Superiority BVR) (拉马特戴维)」整串查表落空、含中文被跳过 → 括号里永远英文。
- **根因**：复合串无整串词条；`ContainsChinese` 早退逻辑把混排串整体放过。
- **修复位置**：`src/engine/CMOLocalizer.cs`（`TranslateString` 早退前按 `()`/`[]` 拆出纯英文片段单独翻译再拼回）、
  词库补 `[UNAVAILABLE]`/`A/A:`/`Light 202`/`Radar`。
- **回归测试点**：`static` `verify_terminology.py`；手动抽样任务编辑器窗口。

## R4 单位状态面板中英频闪（v4.1.4）

- **现象**：右侧面板 航速/深度/阵营 等文本在中文与英文之间反复跳。
- **根因**：仅靠 UI 轮询补翻译（600ms/1500ms）+ WPF 数据绑定持续回推英文 → 中间窗口期被画到屏幕上。
- **修复位置**：`src/engine/CMOLocalizer.cs`（记录实时 WPF 元素 + `CompositionTarget.Rendering` 每帧绘制前重译，
  `SetCurrentValue` 保留绑定；每 8 帧全量补扫）。
- **回归测试点**：`live` 同一稳定态两帧截图 md5 一致（历史上做过 30 帧/60s 采样 0 跳变）。

## R5 工具栏按钮文字仍是英文（v4.1.3）

- **现象**：顶部 13 个战术按钮仍是英文（文字画在位图里）。
- **根因**：`Symbols/Menu/*.png` 中文按钮图未随包分发。
- **修复位置**：`assets/symbols_menu/`、安装器 `DeploySymbolsAndLauncher`、`scripts/package.sh` 资产断言。
- **回归测试点**：`static` 引擎期望的按钮图文件名（当前 13 张）在 `assets/symbols_menu/` 与绿色包中**零缺失**；
  `live` 引擎日志出现「Successfully pre-loaded N localized button images」。

## R6 加载想定报 `Data Source cannot be empty`（v4.0.0）

- **现象**：点「加载选定项」弹 SQLite 错误。
- **根因**：词库 `offsets`（引擎字符串池注入）里混入 `Data Source=` 等 SQL 拼接片段，被翻译后连接串失效。
- **修复位置**：`dict/CMOLocalizer.json` 清洗 + 硬性不变式「只有 display 类字符串可进 offsets」；
  `scripts/verify_release.py` 增加断言。
- **回归测试点**：`static` `verify_release.py`（offsets 不得含 SQL/连接串片段）。

## R7 快捷键显示 `Shift + 在...内`（v4.0.0）

- **现象**：`Shift + Insert` 被单词字典把 `Ins` 当 `In` 翻译。
- **根因**：单词级机翻误伤键名。
- **修复位置**：`dict/src/legacy_garbage_cleaned.json`、`scripts/verify_terminology.py` 门禁。
- **回归测试点**：`static` 术语门禁扫描「在...内」等机翻怪句。

## R8 数据库查看器点击条目详情空白（首例，v4.0.0）

- **现象**：与 R1 相同（第一代）。
- **根因**：`TranslateControlText` 无差别 `ctrl.Text = 中文`，把 `ComboBox.Text` 改成不在选项里的中文 →
  WinForms 把 `SelectedIndex` 重置为 -1 → 游戏 `switch (CB_ObjectType.Text)` 全部落空。
- **修复位置**：`TranslateControlText` 明确跳过 `ComboBox` / `ListControl` / `UpDownBase`。
- **回归测试点**：`static` 源码断言（三类型跳过分支必须存在）；`live` 同 R1。

## R9 教程/简报正文仍英文、WebView2 单引号注入崩溃（v4.1.3）

- **现象**：教程消息与想定简报部分段落英文；修复过程中曾因 JS 注入用单引号导致 WebView2 渲染崩溃。
- **根因**：内嵌 `<ScenDescription>`/教程正文未进词库；注入脚本未做引号转义。
- **修复位置**：`scripts/localize_html_files.py`、词库补录、`TranslateWebView2` 注入脚本转义。
- **回归测试点**：`static` `verify_all.py` 的 briefings 门禁（中文文件数/CJK 字数不下降）+ 教程覆盖门禁。

## R10 消息日志多行教程消息漏译（v4.1.4）

- **现象**：日志里多行教程消息只有首行中文。
- **根因**：整段查表落空；撇号形态（`'` vs `’`）不一致。
- **修复位置**：`src/engine/CMOLocalizer.cs`（逐行翻译 + 撇号容错）。
- **回归测试点**：手动抽样消息日志窗口（历史报告 `docs/TUTORIAL_REGRESSION_V413.md`）。

## R11 数据库筛选下拉框汉化后筛选失效（v4.1.2）

- **现象**：国家/阵营、子类型下拉框汉化后，选值不再过滤列表。
- **根因**：直接改写绑定下拉框的显示值/`Text` 破坏游戏过滤键。
- **修复位置**：`TranslateComboDisplayColumn`（只译显示列，保留 ID 键）。
- **回归测试点**：`live` 真实交互（打开下拉框 → 提交）后列表内容必须变化
  （例：国家=中国 → 出现 CH-4B/CH-5/CH-6 Rainbow 等中国平台）。
  注意：**程序化设置 `SelectedIndex` 不触发游戏的用户事件处理器**，用它验证会误判「筛选失效」。

## R12 工具栏提示词未汉化（v4.1.4）

- **现象**：`自动交战`/`手动交战` 按钮悬停提示仍是英文。
- **根因**：ToolTip 词条未入库。
- **回归测试点**：`static` 词条存在性（`dict/CMOLocalizer.json` exact 含对应英文源串）。
