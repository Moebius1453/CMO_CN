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

## R13 还原后仍显示「已安装 v4.1.5」，无法回退旧版本（v4.1.6）

- **现象**：用户点「一键还原原版英文」后（或想回退旧版时），安装器/助手仍显示「已安装 v4.1.5」；
  想装回旧版本也回退不了。
- **根因**：
  1. 「已安装什么版本」只存在 `HKCU\Software\CMOChinese\Version` 这一个字符串里，且**任何安装包**
     安装后都会把它写成**该安装包自己的** `CurrentVersion`（`CMO_Chinese_Setup.cs` `WriteRegistrySettings`），
     与真实部署的资产无关；
  2. 更新助手只查 `releases/latest` 且比较用 `>`，永远不会提示「有更旧的版本」；
  3. 安装器只有「一键安装 / 一键还原英文」，没有回退入口。
- **修复位置**：游戏目录内新增 `CMOChinese_Installed.json` 安装清单（版本 + 时间 + 来源 + 引擎文件 sha256）
  作为唯一状态真相；`CmoSettings.RegistryInstalledVersion()` 与清单 `Adopt()` 自愈只作迁移期回退；
  `VersionStore` 缓存安装包 + `Rollback.Run()` 提供回退；安装器「回退到上一版本 vX」按钮与
  助手托盘「回退到上一版本…」共用同一实现。
- **用户可见验收**：还原后立刻显示「原版英文环境（未安装汉化补丁）」；装任何版本包后显示**该包**的版本与时间；
  有本地缓存时可直接回退并显示回退后的版本。
- **回归测试点**：`installer` 模式 `R13-install-manifest`（清单版本与磁盘哈希一致）、
  `R13-uninstall-manifest`（还原后清单消失）、`R13-list-versions`；`R15-rollback`（回退后清单=目标版本
  且部署 dll == 缓存包内嵌 dll）、`R15-rollback-requires-cache`（无缓存必须拒绝且清单不变）。

## R14 还原后更新设置全部丢失（v4.1.6）

- **现象**：用户反馈「之前的最小化托盘设置啥的全都没有了，更新频率也没有了」——还原一次后，检查频率、
  提醒方式、镜像、开机自启偏好全部回到默认。
- **根因**：设置与安装态共用同一个注册表键，而 `CleanupUpdater()`（还原路径）执行的是
  `Registry.CurrentUser.DeleteSubKeyTree(@"Software\CMOChinese")`，一次删掉整个键。
- **修复位置**：设置正文迁到 `%APPDATA%\CMOChinese\settings.json`（`CmoSettings`，含旧注册表一次性迁移、
  原子写、损坏文件备份为 `.bad`）；还原只清安装态（清单 + Run 键 + 助手 exe + 注册表 `Version`），
  设置一律保留；要清除必须显式勾选「还原时同时清除我的全部设置」或 `--purge-settings`。
- **用户可见验收**：设 3 小时检查 + 弹窗提醒 → 还原 → 重装 → 仍然是 3 小时 + 弹窗提醒。
- **回归测试点**：`installer` 模式 `R14-uninstall-keeps-settings`、`R14-purge-explicit`；
  `static` `R18-static-selftest`（设置原子写往返/损坏回退/注册表迁移）。
  另：`scripts/test_installer_ui.py` 会实机改频率/提醒方式并断言 `settings.json` 变化。

## R15 回退会得到「旧引擎 + 新资产」混合态（v4.1.6，设计期拦截）

- **现象（设计评审发现）**：若按"快照 dll+json"实现回退，回退后 Scenarios(323 文件/218MB)、Symbols/Menu
  (158 文件)、地名集、DB 模板仍是新版本的 → 得到一个版本号对不上、资产互不匹配的混合安装。
- **根因**：安装足迹远大于引擎资产；且旧版安装器不写新清单。
- **修复位置**：回退单位改为**整个安装包**（`%LOCALAPPDATA%\CMOChinese\packages\`，保留最近 `snapshot_keep`
  个），由新版代码执行 `缓存包 --install --silent` 后**补写清单**，并校验部署的 `CMOLocalizer.dll`
  sha256 == 该包内嵌的 dll sha256，不一致则拒绝登记清单（避免混合态被记成"正常"）。
- **用户可见验收**：回退后安装器显示回退后的版本；文件与缓存包一致；再升级可正常回到新版本。
- **回归测试点**：`installer` 模式 `R15-rollback`（含 dll 一致性）、`R15-rollback-requires-cache`。

## R16 「可回退」提示会骚扰装了新版本的用户（v4.1.6，评审 P0 拦截）

- **现象（评审发现）**：把比较从 `>` 改成"不相等就提示"后，如果本地跑的是**比线上更新**的版本
  （未发布/内测），每次检查更新都会提示用户"回退到线上那个更旧的版本"，属于反向骚扰。
- **根因**：缺少"本地是否真的有那个版本的安装包"这一前置条件。
- **修复位置**：`ReleaseGate.ShouldOfferRollback(local, remote, hasCachedRemote)`：仅当
  `remote < local` **且** 本地已缓存该版本安装包时才提示回退；否则静默（只写日志）。
  助手 `--check` 在"本地更高但无缓存"时给出明确说明而不是升级/回退流程。
- **用户可见验收**：本地等于线上 → 静默；本地更高且无缓存 → 不提示回退（说明原因）；
  本地更高且已缓存 → 提示「发现可回退版本 vX」。
- **回归测试点**：`static` `R16-static-gate`（共享层门控真值表 + 空输入）、
  实机 `scripts/test_updater_live.py` 的四条 `--check` 路径（有更新/已最新/可回退/门控）。

## R17 存量 4.1.x 用户与绿色包用户被误报「未安装」（v4.1.6）

- **现象（评审发现）**：状态改成只读清单后，用旧版本装过的用户（没有清单文件）会看到
  「原版英文环境（未安装汉化补丁）」；只解压绿色包的用户同样没有清单。
- **根因**：清单是新增文件，历史安装不会凭空产生它。
- **修复位置**：`InstallManifest.Adopt(game, fallbackVersion)` 自愈：引擎文件存在但清单缺失时，
  按当前文件重新生成清单（`source="adopted"`），版本回退链 = 调用方给的注册表 `Version`
  → 已部署资产的 `_meta.version` → `unknown`；安装器打开、助手启动/检查时自动执行，
  并可用 `--adopt "<game>"` 手动触发。绿色包说明（README → 包内 `汉化说明.md`）已写明。
- **用户可见验收**：旧版用户打开安装器显示「已安装完整中文汉化补丁（已登记当前状态，v4.1.5）」
  而不是「未安装」；执行一次 `--adopt` 后 `--list-versions` 显示该版本且 `source=adopted`。
- **回归测试点**：`installer` 模式 `R17-adopt-selfheal`；`static` `R17-static-pack-readme`。

## R18 状态机缺少可执行单测（v4.1.6）

- **现象（评审发现）**：设置/清单/包缓存/回退这些新逻辑如果没有单测，后续改动很容易悄悄破坏，
  而实机用例（需游戏）太重，发版前不一定跑得起来。
- **修复位置**：`src/shared/CMOSharedTest.cs`（测试宿主，`build.sh` 编译为 `build/CMOSharedTest.exe`，
  不进入安装包）：设置首次写/覆盖写/损坏备份/注册表迁移、清单往返 + Modified + Clear、Adopt 需要完整引擎文件、
  包缓存 Prune 保留最近 N + Previous、安装包版本戳解析（文件名优先 + 无版本号时回退内容戳）、
  门控真值表；全部在系统临时目录内运行（不碰用户设置与真实游戏目录）。
- **回归测试点**：`static` `R18-static-selftest`（要求 ALL PASS，退出码 0）。

## R19 创建任务/搜索剧本时输入一个字符即闪退（v4.1.7）

- **现象**：用户实测——创建任务时在任务名框输入 `m` 就闪退；「搜索剧本」（加载想定）搜索框输入 `m` 也闪退。
  表现为进程瞬间消失（无报错弹窗）。
- **根因（有实证）**：
  1) 事件日志（Application 1000/1001）：`Command.exe` 于 2026-10-14 20:16/20:18 以 `System.StackOverflowException`
     崩在 `System.Windows.Forms.ni.dll`；于 2026-10-15 12:30/12:38/12:54/12:55 以
     `Environment.FailFast` 终止（12:54 那条 faulting module = `PresentationFramework.ni.dll`）——两者都是
     「异常/递归从注入进程的托管回调里逃逸」的签名。
  2) 本机复现 + 引擎日志铁证：在「加载想定」搜索框输入 `m`，`CMOLocalizer.log` 出现
     `UI Controls Translated count: 3700 (Sample: m -> 米)`，每 2.5 秒 +50，一路涨到 5900，
     紧接着 15:11:12 进程以 `0xc00000fd`（栈溢出）消失。
  3) 代码实证：`TranslateControlTextInner` 只对 `TextBoxBase` 做「聚焦即跳过」；游戏 DarkUI 的自定义输入框
     （搜索想定框、任务名框）**不继承 TextBoxBase**，落进通用分支被当普通标签翻译；词典里有 `m → 米`
     （单位换算）这类短词条，于是引擎把用户正在输入的 `m` 改写成「米」，游戏再写回 `m`，形成无终止拉锯。
- **修复位置**：`src/engine/CMOLocalizer.cs`
  - `IsTextInputControl()` + 在 `TranslateControlTextInner` 中提前返回：输入控件正文一律不改写（只翻水印）；
  - 组合框聚焦或下拉展开时不改 `Items`；聚焦中的 WPF `RichTextBox` 不动其文档；
  - `AllowWrite()` 回写拉锯熔断：同一控件被反复写同一个目标值超过 6 次即永久放弃该控件并打印堆栈；
  - `EnterMutation()` 深度闸门（上限 8）+ 堆栈留证，只挂在游戏事件回写路径；
  - `InstallCrashGuard()`：`AppDomain.UnhandledException` / `Application.ThreadException` / WPF 钩子，
    完整堆栈写入游戏目录 `CMOLocalizer_Crash.log`，仅当堆栈含本引擎帧时才吞掉异常。
- **用户可见验收**：在任务名框/搜索想定框输入 `m`、中文都不再闪退；输入内容保持原样；若仍有异常，
  游戏目录出现 `CMOLocalizer_Crash.log` 供定位。
- **回归测试点**：`static` 的 `R19-static-input-guard`（断言输入控件早退、崩溃取证、熔断、深度闸门均在位）；
  `static` 的 `R0-static-env` 把 `IsTextInputControl`/`InstallCrashGuard` 纳入部署标记，防止旧引擎假绿；
  `live` 的 `L0/R1-*` 保证加固没有让翻译回退。

## R20 战役里部分剧本名与「系列战役」窗口标签仍是英文（v4.1.7）

- **现象**：用户实测——新版里战役内有些剧本名又变回英文（「沉默的部队」整场如此）；
  「系列战役」窗口有 `Select one of the campaigns…`、`Resume from save`、`Delete selected save` 三个英文标签，
  工坊页签还叠译成「创意工坊 (创意工坊 (Steam Workshop))」。
- **根因**：
  1) 战役内剧本名来自 `Scenarios/**/*.campaign` 的 `<Name>`，共 154 条去重值中 **25 条从未入词典**
     （`The Silent Service` 16 条全缺、`Desert Storm` 的 SCUD Hunting、`Northern Inferno` 的
     The Grey Ghost from the East Coast，另 6 条是 `Local Video: *.wmv/mp4` 文件名，**不得翻译**）。
  2) `Resume from save` 属游戏字符串池（offset 752572），引擎启动时把池内英文注入，只补 `exact` 不够，
     必须同时补 `offsets`。
  3) `Steam Workshop` 词条非幂等（`创意工坊 (Steam Workshop)`），被二次翻译即叠成
     「创意工坊 (创意工坊 (Steam Workshop))」。
  4) 想定浏览器标题是复合串 `[NOT LICENSED] <标题>, <年份>`，整串查表必然落空。
- **修复位置**：`dict/CMOLocalizer.json`（19 条剧本名 + 3 条窗口标签 + 工坊幂等与清理项 + 2 条 offsets 注入）、
  新增 `dict/src/campaign_ui.json`（精选批次，保证重建不丢）、`src/engine/CMOLocalizer.cs`（复合标题拆分规则）、
  `scripts/verify_all.py`（scenario meta 门禁纳入 `<Name>`）。
- **用户可见验收**：系列战役窗口三个标签为中文；战役内剧本名全中文；工坊页签显示「创意工坊」；
  想定浏览器标题显示「[未授权] 航母杀手, 1985」。
- **回归测试点**：`static` 的 `R20-static-campaign-names`（战役剧本名全覆盖断言 + 工坊叠译自愈断言）；
  `verify_all.py` 的 `scenario meta` 门禁（`campaign names N/N`）；实机抽查「加载想定 → 沉默的部队 → 航母杀手」。

## R21 缺少「发版打包清单」与统一入库检查（v4.1.7）

- **现象**：要发版时没有一份权威清单说明「要发什么、必须先入库什么、禁止带上什么」；
  门禁散落在 `package.sh`/`publish_release.py`/`verify_*` 里，靠记忆执行，容易漏项。
- **根因**：发版知识与判定逻辑没有单一入口，且打包门禁曾覆盖 live 回归报告导致发布校验误报（见 R18 相关的
  `--no-report` 修复）。
- **修复位置**：`docs/RELEASE_MANIFEST.md`（清单）+ `scripts/release_check.py`（统一入库检查）+
  `scripts/publish_release.py`（复用 `release_check.check_regression_report`）+ `docs/RELEASE_TEST_SOP.md`（接入顺序）。
- **用户可见验收**：发布前一条命令 `python scripts/release_check.py` 即可看到逐项 PASS/FAIL。
- **回归测试点**：`static` 的 `R21-static-manifest`（清单与脚本存在、互相引用、SOP 引用二者）。
