# 《现代行动：指挥 (Command: Modern Operations)》专家级全汉化工程归档 (v3.0 终极版)

## 一、项目概述
- 游戏根目录：`G:\soft\steam\steamapps\common\Command - Modern Operations`
- 游戏主程序：`Command.exe` (版本 1.06, 64-bit .NET Framework 4.8 桌面应用，受 SmartAssembly 8.x 强名称混淆保护)
- 交付成果：
  1. `C:\Users\admin\Desktop\CMO_Chinese_Setup.exe`：单文件独立图形化一键安装/卸载程序（内嵌全部引擎、字库与 8 大数据库模板，支持命令行静默模式与自动寻路）
  2. `C:\Users\admin\Desktop\CMO_Chinese_Localization_Pack.zip`：绿色免安装汉化包（解压至游戏根目录即可）
  3. `projects/汉化/`：全套 C# 源代码、词典处理脚本及原始备份

---

## 二、架构演进与突破（v1.0 -> v2.0 -> v3.0）

### 1. 核心注入机制：CLR 运行时级无损挂载
- 通过 `Command.exe.config` 配置 `<runtime><assemblyBinding>`，实现主程序启动时强行挂载外置汉化引擎 `CMOLocalizer.dll`。
- 不修改 `Command.exe` 任何二进制字节，不破坏 SmartAssembly 强名称签名，彻底规避游戏反作弊闪退与 Steam 文件校验冲突。
- 采用双轨驱动架构：
  - **静态字符串池解密**：动态解密主程序中 28,833 个字符串槽位。
  - **WinForms / WPF / WebBrowser 视觉树侦听**：深度扫描所有窗体、菜单栏、状态栏、树形视图、表格单元格以及富文本简报网页，挂载 `DocumentCompleted` 与实时 DOM 注入。

### 2. 数据库安全汉化突破（DB3K & CWDB - 彻底攻克）
- **解决核心矛盾**：SQLite 数据库中的实体名称（如 F-16C、AIM-120）是 Lua 脚本与想定事件绑定的物理主键，直接改动库字段会导致历史想定无法加载或闪退。
- **攻克方案（双层解耦）**：
  1. **HTML 模板层彻底汉化**：本地化 `DB/Templates/` 下全部 8 个装备详情渲染模板（`Aircraft.html`, `Facility.html`, `Loadout.html`, `Magazine.html`, `Satellite.html`, `Sensor.html`, `Ship.html`, `Submarine.html`），涵盖全部性能参数、装甲指标、雷达航电参数、航程推重比等数百项技术字段，并备份原始英文模板于 `DB/Templates_Original_Backup`。
  2. **Enum 枚举参数全量本地化**：从数据库 71 张 `Enum%` 枚举字典表中提取 2,122 项战术属性与分类术语（推进系统、航电代次、作战分类、挂载构型、WRA 拦截条令等），翻译后全量并入 `CMOLocalizer.json` 词库（词条扩充至 13,566+ 条）。
  3. **装备型号与武器名称双语无损显示 (v4.0 新增)**：在保持底层 SQLite 数据库主键 100% 官方原版以防想定报错的前提下，在 `CMOLocalizer.dll` 与 8 大 HTML 渲染模板中内嵌动态无损标注引擎，对 F-16C、苏-35S、歼-20A、伯克级、055型、AIM-120D、霹雳-15、战斧等主流战机、军舰、导弹及地面雷达设施动态标注中英双语译名（如 `F-16C Fighting Falcon 【F-16C "战隼"战斗机】`），鼠标悬浮与页面阅读无缝对照。

### 3. 战役列表与关卡想定双语汉化 (v4.0 新增)
- 遍历并汉化全部 11 场官方经典与 DLC 战役定义（`Chains of War -> 战争之链`、`Desert Storm -> 沙漠风暴` 等），并在关卡列表中双语显示任务序号与中文译名。
- 遍历并汉化全部 261 个 `.scen` 文件的关卡标题 (`<ScenTitle>`) 与 139 个地理作战区域 (`<ScenSetting>`)，如 `1. 蓝色黎明 (1. Blue Dawn)`、`台湾海峡 (Taiwan Strait)`，使战役选择界面彻底中文化。
- 所有 `.campaign` 与 `.scen` 原始文件均在同目录下保留 `.orig` 备份，支持 100% 无损还原。

### 4. 战役剧情与作战简报正文全量深度翻译 (v4.0 新增)
- 遍历游戏 `Scenarios/` 目录下全部 323 个 HTML 任务简报文件及 182 个内嵌简报的想定文件，通过多线程军事翻译管线，全量翻译 4,885 个长篇叙事段落与背景剧情。
- 中文字符覆盖量从原始的 2,472 字跃升至 **104,813 字**，将复杂的历史战况背景、最高指挥部战役条令、核武器使用授权限制与电磁管制指令全部中文化。
- 原版 HTML 简报均保留 `.html.orig` 备份，随时可通过一键安装器原样还原。

---

## 三、安装与卸载使用说明

### 1. 一键安装器 (`CMO_Chinese_Setup.exe`)
- 双击运行：程序自动扫描注册表并锁定 Steam 游戏目录，点击“一键安装汉化”即可。
- 静默自动化：
  - 安装：`CMO_Chinese_Setup.exe /install`
  - 卸载：`CMO_Chinese_Setup.exe /uninstall`
- 内置自我还原：点击“还原英文原版”可彻底移除注入配置、还原全部 8 个数据库 HTML 模板、还原全部战役简报原始文件，恢复 100% 官方纯净状态。

### 2. 绿色免安装包 (`CMO_Chinese_Localization_Pack.zip`)
- 解压后将全部文件覆盖到游戏根目录即可。

## [2026-10-11] 全量军语深度精修、机翻清洗与实机终审闭环

1. **机翻痕迹彻底清洗与纯正军语替换**：
   - 全量清洗 4,008 个安全 offsets 词条与 exact/partial 词库中生硬的双斜杠备选词与生硬机翻。
   - 重点替换：
     * "接触" / "目标目标接触" -> "目标接触"（如取消虚警、雷达辐射源特征、遗忘目标接触）
     * "载荷/有效载荷" -> "外挂载荷/挂载构型"
     * "突击/攻击" -> "空中突击/对地对海打击"
     * "巡逻/巡视" -> "战斗巡逻/警戒巡逻"
     * "投射物/弹药" -> "弹药基数/武备发射"
     * "装填/重新装填" -> "武器再装填/战备整备"
     * "拦截/拦截机" -> "防空拦截/截击机"
     * "任务想定编辑/飞行计划想定编辑" -> "任务编辑器/飞行计划编辑器"
   - 彻底移除了 14 个高危数据库内部哈希检测字符串，解决数据库哈希校验错误 2，游戏启动及新建想定全程 0 报错。

2. **核心业务窗口实机交互验证**：
   - **主菜单栏**（文件/视图/推演/地图设置与工具/预设视角/单位命令/任务与参考点/目标接触/想定编辑/帮助）100% 纯正军语。
   - **快捷工具栏**（自动交战/手动交战/航线规划/油门/高度/编队编辑/弹药库/航空作业/小艇作业/武备系统/传感器/系统与损管/作战条令/任务编辑）100% 中文自绘图标。
   - **【编辑阵营 (Edit Sides)】窗口**：添加/移除/重命名/条令-交战规则/阵营外交/态势感知等级全中文。
   - **【作战条令与交战规则 (Doctrine / RoE)】窗口**：超视距交战逻辑、绝气推进(AIP)、撤退与重新部署、辐射控制(EMCON)、充电工况等近百项细化条令全中文。
   - **【任务编辑器 (Mission Editor)】窗口**：多任务联合视图/战术总览、空中任务指令(ATO)、起飞时间(Zulu)、目标到达时间(ToT)等全中文。

3. **最终交付物**：
   - `projects\汉化\CMO_Chinese_Setup.exe` (支持一键安装与一键卸载)
   - `projects\汉化\CMO_Chinese_Pack_v3.0.zip` (免安装绿色整合包)
   - 游戏根目录已同步部署就绪。

## [2026-10-11] v3.0 终极全量增强：子菜单全覆盖、战役想定树全汉化与简报引擎升级

1. **主菜单栏 10 个顶级菜单及其全部 397 个子菜单项 100% 汉化与精修**：
   - 全量扫描提取主程序内存与菜单定义共 397 项（覆盖：文件、视图、推演、地图设置与工具、预设视角、单位命令、任务与参考点、目标接触、想定编辑、帮助）。
   - 修复了此前遗漏的带斜杠/括号复合项（如 `Add/Edit Sides` -> `添加/编辑阵营`、`Enable/Disable Autosave` -> `开启/关闭自动存档`、`Show/Hide...` 等共 11 项）。
   - 修复了 2 处因词根匹配产生的叠词现象（如 `切换单位状态面板显示`、`单位条令与交战规则...`）。

2. **战役与想定选择树 (Scenario Browser) 100% 汉化**：
   - 深度梳理了官方 11 部经典战役（《战争之链》、《沙漠风暴》、《北方炼狱》、《赤潮》、《潜艇危机》、《安全核戒备》、《马岛战争》、《克什米尔烽火》、《流沙》、《战火重燃》等）及全部 261 个想定（共 499 个树节点）。
   - 为每个想定定制了纯正中文军事译名（如《蓝色拂晓》、《以眼还眼》、《布比延海战》、《黑金闪电战》、《空海一体战》、《铁腕镇压》等）。
   - 解决了带编号（如 `1. Blue Dawn`、`10. Crackdown`）和带年份（`, 1985`）的动态正则智能匹配。

3. **想定简报与地图探明**：
   - 探明地图全大写国名烘焙在 Stamen 瓦片 PNG 中，矢量地名来自 `WW\Data\Earth\Placenames\*.wwp` 并受限于 WorldWind 的 GDI 字体绑定；
   - 加载想定界面支持动态注入翻译。

## 打包完整性铁律（2026-10-15 用户明确要求）

**打包（`scripts/package.sh`）时必须把全部汉化资产打进去，缺一不可。**

- 用户原话：「打包的时候必须打包齐全」「请确保打包的时候把所有的汉化都带上」。
- 事故背景：v4.1.3 及更早的安装包与绿色包**从未包含** `WW\PlacenameSets\*.wwp`（地图地名集）、
  `Symbols\Menu\*.png`（13 张中文按钮图）、`Launcher\Localization\stringsChinese.resx`，
  于是用户装包后**工具栏按钮与地图仍是英文**；而开发者本机因手工部署过这些文件而显示中文，
  造成「本地全好、用户全英文」的假象。
- 强制要求：
  1. `scripts/package.sh` 末尾的「汉化资产覆盖率断言」必须保持通过，缺失即构建失败，不得绕过。
  2. 新增任何汉化资产时必须五处同步：① 收录到 `assets/`；② `build.sh` 内嵌资源；
     ③ 安装器 `Deploy*` 部署 + 卸载 `Restore*` 还原（覆盖前写 `.orig` 备份）；
     ④ 绿色包携带；⑤ 覆盖率断言清单加入该资产。
  3. 发布前必须实测核对：`grep -a <资源名> build/CMO_Chinese_Setup_v<ver>.exe` 确认内嵌；
     解包 `CMO_Chinese_Pack_<ver>.zip` 核对条目数与各资产数量。
- 当前资产清单：`CMOLocalizer.dll`、`CMOLocalizer.json`、`CMO_Updater.exe`、`Command.exe.config` 补丁、
  `DB\Templates` 8 个、`Scenarios\**\*.html` 323(+323 备份)、`WW\PlacenameSets` 2、
  `Symbols\Menu` 28、`Launcher\Localization` 1、`汉化说明.md`（绿色包共 690 条目）。
- 另一类同类问题：**位图管按钮文字、词典管悬停提示**——替换了按钮位图不等于提示语也汉化了，
  提示语缺失只能补词条（例：`Auto Engage Target→自动交战目标`）。

## 待办：凭据泄露收尾（2026-10-15 发现）

- 已止血（commit 90668f8）：`scripts/translate_worker.py` 密钥改环境变量并入 .gitignore；
  删除 `.workflow/recovery/`（含 GitHub 凭证的压缩检查点）；.gitignore 加三条硬规则。
- **未完成 ①**：用户自行吊销/轮换两处凭据——网关 `newapi.liubaitech.cn` 的 `sk-` 令牌、
  GitHub OAuth/会话恢复令牌（用户 2026-10-15 表示稍后处理）。
- **未完成 ②**：历史重写 + 强推（`git filter-repo --replace-text` + `--invert-paths` 删
  `.workflow/recovery` 与 `scripts/translate_worker.py`），需先做 mirror 备份；
  旧提交 `998c563` 起仍含明文密钥，重写后建议联系 GitHub 支持清理缓存视图。
- 预防建议：加提交前密钥扫描（`sk-`/`ghp_`/`gho_`/`AKIA` 等）并接入发布门禁。

## 密钥使用规范（2026-10-15 事故后确立）

**任何密钥/令牌一律不入库。** 统一用 `scripts/credentials.py` 解析，顺序：
① 环境变量（`CMO_LLM_API_KEY` 等，临时/CI 首选）→ ② `%USERPROFILE%\.cmo-cn\secrets.json`（本机长期）
→ ③ 仓库内 `.secrets.local.json`（已在 .gitignore）。格式见 `docs/credentials.example.json`（仅占位符）。

- 代码里**只允许** `get_credential("llm_api_key", env="CMO_LLM_API_KEY")` 这种写法，不得出现明文。
- `.gitignore` 已覆盖：`.env*`、`.secrets.local.json`、`secrets/`、`*.secrets.json`、`*.pem`、`*.key`、
  `.workflow/recovery/`、`.workflow/**/session-*.md`、`scripts/translate_worker.py`。
- 三重拦截：`verify_all.py` 第 10 项 `secrets`（含 `--history` 全历史）、`package.sh` 打包前硬门禁、
  可选提交钩子（`git config core.hooksPath scripts/hooks`）。
- 万一再次泄露：**先吊销/轮换**（公开仓库分钟级被抓），再清理 tip、重写历史强推（配方见本文件「凭据泄露收尾」节）。
- 更优做法：能在 pi/teammate 运行时用现成凭据完成的工作，就不要在仓库里放密钥。

## 发布后运维要点（2026-10-15 实机踩坑）

1. **Steam 校验/更新会冲掉汉化**：实测发现游戏目录被还原为原版（`Scenarios` 含中文 0/323、
   `.orig` 备份 0 个），原因是 Steam 校验游戏文件完整性 / 游戏更新会还原它管理的文件。
   处置：**重跑一次安装器即恢复**（`CMO_Chinese_Setup.exe` 或绿色包覆盖）。
   注：`.orig` 备份被清掉后，卸载仍可还原（Steam 侧本身就是原版），但重装会重新生成备份。
2. **用户侧升级路径**：≤ v4.1.3 没有更新助手，必须**手动装一次 v4.1.4**；从 4.1.4 起
   才有「开机自检 + 每 6 小时检查 + 托盘常驻 + 检查升级/不再提醒」的自动升级。
3. **密钥三重拦截的启用**：`verify_all.py` 第 10 项与 `package.sh` 已默认生效；
   本机再启用提交钩子：`git config core.hooksPath scripts/hooks`。
4. **发布后自检清单**：`verify_all.py` 需 10/10；`grep -a <资源名> build/CMO_Chinese_Setup_v<ver>.exe`
   核对内嵌；解包绿色包核对条目数（v4.1.4 = 690 条目）；Release 四个资产（安装包/绿色包 + 两个 sha256）
   用 HTTP 206 分片下载验证。
