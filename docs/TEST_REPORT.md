# CMO 汉化 v4.0.0 测试与验收报告

本报告记录本轮对《Command: Modern Operations》汉化包的全部实机测试、静态门禁与
回归验证证据。

---

## 一、实机测试（进程级 + 窗口级）

### 1. 启动与词库加载验证

- **方法**：清除 `CMOLocalizer.log` → 启动 `Command.exe` → 用
  `build/WinProbe.exe`（基于 Win32 `EnumWindows` / `GetClassName` /
  `GetWindowText`）枚举进程顶层窗口 → 读取引擎日志。
- **修复前（上一轮遗留状态）**：
  ```
  [2026-10-11 12:48:51] === CMOLocalizer v4.0 Engine Starting ===
  [2026-10-11 12:48:51] Loaded dictionaries: 0 offsets, 0 exact texts, 0 partial terms, 21908 word roots.
  ```
  —— **词库一条都未加载**。原因：词库头部的 `_meta.counts.offsets` 含字面量
  `"offsets"`，引擎的 `text.IndexOf("\"offsets\"")` 先命中了 `_meta` 里的那一个，
  四个分节边界全部错位。
- **修复后**：
  ```
  [2026-10-11 14:19:19] === CMOLocalizer v4.0 Engine Starting ===
  [2026-10-11 14:19:19] Loaded dictionaries: 9550 offsets, 12615 exact texts, 0 partial terms, 0 word roots.
  [2026-10-11 12:54:08] Successfully pre-populated 9512 translated strings into AoIBWWlDJbaf7LijnA string pool!
  [2026-10-11 12:37:32] DarkToolStrip2 localized: updated 13 button images with Chinese bitmaps!
  [2026-10-11 12:37:32] UI Menu/ToolStrip Translated count: 150 (Sample: Events -> 事件)
  ```

### 2. 启动期 `NullReferenceException` 定位与消除

- **现象**：部署词库后启动弹出模态对话框：
  `v1.10 - Build 1900.20 - 应用程序启动时出错！` /
  `beta-1 应用程序启动时发现错误。错误详情: 未将对象引用设置到对象的实例。`
- **对照实验**（`scripts/bisect_offsets.py`）：
  1. 禁用 `Command.exe.config` 中的 `appDomainManager` → 启动干净，无对话框。
     **结论：异常确系汉化引擎引入**，非原版固有问题。
  2. 保持引擎启用，将 `offsets` 置空、`exact` 保留全量 → 启动干净。
     **结论：异常来自偏移池注入**，非 UI 树巡检。
  3. 对 9,550 条 `offsets` 做二分（14 轮实机启动探测）：
     - 前 8,668 条：干净
     - 前 8,669 条：干净
     - 前 8,670 条：**复现**
     - 定位到唯一触发项：**offset `1749248`，原文 `'Game Preferences'` →
       译文 `'游戏偏好设置'`**。
- **根因**：`'Game Preferences'` 同时是 `Config/Command.ini` 的 **INI 节名**
  （`[Game Preferences]`）。字符串池里的同一个槽位既被用于显示标题，也被用于
  读取配置文件；在偏移层把它改成中文后，配置读取返回空引用。
  同类证据：游戏根目录下存在上一轮残留的中文名目录 `配置/Command.ini`，其中
  `[General]` 被写成 `[常规作战条令]`、`SonobuoyVisibility` 被写成
  `声呐浮标可见度`——正是 `'Config'` 与键名被注入翻译后的产物。
- **修复**：`scripts/identifiers.py` 抽取 `Config/*.ini` 全部节名/键名与磁盘
  目录名（178 项），`build_dict.py` 禁止它们进入 `offsets`，但保留在 `exact`
  供 UI 层显示使用；清理残留的 `配置/` 目录。
- **复测结果**（PID 2512）：
  ```
  1380382  pid=2512  vis=True  class=HwndWrapper[Command.exe;StartGameWindowThread;...]  title=Command
  2101656  pid=2512  vis=True  class=WindowsForms10.Window.8.app.0.282af8c_r8_ad1        title=现代行动：指挥 v1.10 - 内部版本 1900.20
  ```
  仅剩启动菜单与主窗口两个可见顶层窗口，**错误对话框彻底消失**。

### 3. 启动日志核对（`Logs/ExceptionLog_2026_10_11.txt`）

```
Successfully completed Set Current Scenario.
Successfully initialized Lua.
Successfully cached map symbols.
Successfully created new scenario.
Initialization completed.
Done loading MainForm.
Prepared Wold Window.
Showed Start Game window.
Finished MainForm_Shown.
DXNTRACE-INIT: Initialized DX11 renderer on NVIDIA GeForce GTX 1080.
```

主窗口、WorldWind 地球控件、DX11 渲染器、Lua 沙箱、地图符号缓存全部正常完成初始化。

---

## 二、机械验收门禁结果（`scripts/verify_all.py`）

| 门禁 | 结果 | 指标 |
|---|---|---|
| `dictionary` | PASS | `offsets=9,550`、`exact=12,615`、`partial=0`；0 条非中文、0 条汉字间空格、0 条残留英文虚词、占位符与括号配平 100% 一致 |
| `menu` | PASS | 328 项菜单项：326 项已译、2 项按键名按惯例保留英文、**0 项未译、0 项生硬/残留** |
| `engine parse` | PASS | Python 忠实复刻引擎手写扫描器，加载出的键值与 `json.loads` 逐项一致（9,550 / 12,615） |
| `scenario meta` | PASS | 261 个 `.scen`：标题 **259/259**、地理区域 **123/123** |
| `briefings` | PASS | 323 个 HTML 简报：**304/304** 个含正文文件已汉化（另 19 个为纯 `<img>` 包装页），累计 **131,796** 个汉字；HTML 标签计数与 `.orig` 100% 一致 |
| `placenames` | PASS | `Synoptic/0000_0000.wwp` 国家与海域 **321/321** 已汉化，坐标与标志位零改动 |
| `db templates` | PASS | 8 个模板中 7 个带字段标签的模板全部中文化并改用 `Microsoft YaHei`（`Platform.html` 为纯外框） |
| `version` | PASS | `VERSION` / 引擎日志头 / 安装器标题统一为 `4.0.0` |

---

## 三、分项质量抽检

### 1. 菜单与子菜单（`docs/MENU_TERMINOLOGY.md`）

修正的典型生硬译法：

| 原文 | 旧译（问题） | 新译 |
|---|---|---|
| `Database Viewer` | 现代化武器装备数据库查看器（冗长） | 数据库查看器 |
| `Save` / `Load` | 保存想定 / 加载想定（过度特化） | 保存 / 加载 |
| `Do not show` | 隐藏所有包络圈（**词义错误**） | 不显示 |
| `Minimaps` | 态势鹰眼小地图（赘词） | 小地图 |
| `Flightplans` | 空中编队飞行计划线（赘词） | 飞行计划 |
| `Datablocks` | 单位战术信息浮动数据牌（冗长） | 数据块 |
| `Datalinks` | Link-16 / 战术数据链协同网络（过度特化） | 数据链 |
| `Quick Jump` | 预设视角（偏离原意） | 快速跳转 |
| `Ignore plotted course when attacking - All Units` | 攻击时忽略已规划航线 - 所有单元（与单选版术语不一） | 攻击时忽略标绘航线（全部单位） |

### 2. 想定简报正文（`Scenarios/Chains Of War/BlueDawnIntro.html`）

- 原文：*Kim Jong-un inherited the leadership of North Korea in December 2011. This young leader was quick to purge and restructure the North Korean military and government to shore up his power base...*
- 译文：*金正恩于 2011 年 12 月继承朝鲜领导权。这位年轻的领导人迅速在朝鲜军队和政府中展开清洗与重组以巩固权力基础……*

### 3. 地图国家与海域注记（`WW/PlacenameSets/Synoptic/0000_0000.wwp`）

321 条全部写入，含源数据中的乱码条目修正（`Bosnia ne-Herzegovina` → `波黑`、
`Sao Tome kanye ne-Principe` → `圣多美和普林西比`、`Trinidad ne-Tobago` →
`特立尼达和多巴哥`）。原文件备份为 `0000_0000.wwp.orig`，可随时用
`python scripts/apply_placenames.py --revert` 一键还原。
