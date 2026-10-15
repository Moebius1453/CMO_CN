# v4.1.4 未完成项（交接给下一轮）

## 1. 任务编辑器「可用」列表：点击前后载荷文字互换（待复现）

用户提供两张截图：同一个列表，点击前 `#1 (Air Superiority BVR)` / `#2 (A/A: AIM-120C-7 AMRAAM, Light 202)`（#2 选中）；
点击后 `#1 (A/A: AIM-120C-7 AMRAAM, Light 202)`（#1 选中）/ `#2 (Air Superiority BVR)`——**被选中那行的载荷文字换了位置**。

已确认的事实：
- 该列表节点文本确实会被引擎改写：`TranslateMilitaryUnitName` 做子串替换（`Sufa → 苏法/风暴`、`Falcon → 「战隼」`），
  截图里的 `苏法/风暴 #1 …` 就是它的产物（source: `src/engine/CMOLocalizer.cs`，见 `TranslateMilitaryUnitName`）。
- 但**载荷名当时的复合串不会被我们改写**（旧版 2.2 分支要求「不含中文」，载荷串含中文被跳过），
  因此「载荷互换」在旧版本上更可能是游戏自身在指派后重排列/重排载荷。
- 无法复现的原因：找不到用户当时那个想定。Shifting Sands 11「Downtown in Damascus」是 1967 年海军想定，不含 F-16I；
  `.scen`/`.ini` 按 DBID 存储，grep `F-16I Sufa` 无命中。用户场景特征：拉马特戴维基地 + 2x B707 加油机 + 4x F-15A「鹰」Baz +
  6x F-16I「战隼」[苏法/风暴] + 2x G550 AEW [纳雄-埃塔姆, CAEW] + 2x Hermes 900 UAV [科哈夫] + AN/FPS-89/100/117 雷达。

**下一步**：请用户提供想定名/存档名 → 用 `scripts/live_load_tutorial.py` 加载 → 打开任务编辑器 →
`H.send('CTRL_TREE')` + `H.send('WPF_TEXTLOG')` + 逐行点击截图对比，判定是否引擎行为；
若确属引擎改写导致，改为「不改写列表项文本、仅在绘制层显示中文」。

## 2. 本轮已完成的相邻修复（同一批反馈）

- **地图仍是英文**：安装器/绿色包从未分发 `WW/PlacenameSets/*.wwp`，已内嵌分发并保证先部署地名集再重绘标签瓦片
  （`DeployPlacenames` / `RestorePlacenames`）。
- **括号内载荷名不翻译**：引擎 2.3 分支 + 词库 `[UNAVAILABLE]`/`A/A:`/`Light 202`/`Radar` 等。
- **WPF 中英频闪**：见 `docs/CHANGELOG.md` v4.1.4；每帧绘制前重译实时 WPF 文本。

## 3. 发布状态

`VERSION=4.1.4`；`build/CMO_Chinese_Setup_v4.1.4.exe`、`build/CMO_Updater.exe` 已构建；
未打包发布（打包/tag/Release 需用户明确指令）。
