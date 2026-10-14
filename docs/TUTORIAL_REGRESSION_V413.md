# v4.1.3 全量教程回归报告

测试对象：《Command: Modern Operations》汉化 v4.1.3（引擎 `CMOLocalizer.dll` + 词典 + 落盘 HTML）
测试时间：2026-10-15
真机环境：`G:\soft\steam\steamapps\common\Command - Modern Operations`，1920x1080

---

## 一、结论

| 维度 | 结果 |
|---|---|
| 45 个教程全部实机加载 | **45/45 通过**（84 张实机截图存证） |
| 教程说明文本翻译率 | 65.0% → **97.93%**（983 → 917 有效片段，898 已译） |
| 教程内嵌 XML 段落 | **1625/1625 已译**（`verify_all_tutorials.py` PASS） |
| 教程外部 HTML | **183/183 含中文**（磁盘落盘汉化，不再依赖运行时注入） |
| 教程中的按键与游戏绑定一致性 | **0 处错位**（30 个按键引用全部匹配） |
| 教程中的装备名称汉化 | **0 处未译装备名** |
| 下拉菜单（含展开态） | 63 个下拉框，**0 个含英文项** |
| 界面中英文频闪 | 30 帧 / 60 秒连续采样，**0 次状态跳变** |

---

## 二、真机扫描方法

1. `scripts/live_sweep_tutorials.py`：对每个教程执行「重启 `Command.exe` → 关闭启动大厅 →
   `CLICK_MENU TSMI_LoadScenario` → `SCEN_EXPAND_ALL` → `SCEN_SELECT <教程>` → `SCEN_LOAD`
   → `CLICK_BUTTON 进入想定` → `CLICK_WPF 开始推演` → 关闭战情消息」，并抓取
   `_briefing`（想定预览与说明）与 `_message`（首条专用战情消息）两张实机截图。
   结果明细见 `build/tutorial_sweep_v413.json`，截图见 `evidence/live/v413/tutorials/`。
2. `scripts/verify_tutorials_v413.py`：静态回归门禁，读取 `build/all_tutorials_dump.json`
   （45 个教程的全部简报、描述与内嵌消息），校验覆盖率、装备名、按键、引号标签。
   结果见 `build/tutorial_static_v413.json`。
3. `scripts/verify_all_tutorials.py`：工程原有教程门禁（含 183 个 HTML 与 1625 条内嵌段落）。

---

## 三、本轮修复的教程问题

1. **教程与简报 HTML 仍是英文（根因）**：`Scenarios/**/*.html` 中 272/323 为纯英文，
   只能靠运行时 DOM 注入，导致加载竞态频闪、漏译与排版错乱。新增
   `scripts/localize_html_files.py` 落盘汉化，含中文文件 51 → **304**（其余 19 个为
   Silent Service 纯图片页），教程 HTML 残余英文段 149 → **0**。
2. **内嵌 .scen 正文缺失译文**：新增 `dict/src/tutorial_prose_fixes.json`
   补译 138 条（Tomcat84s 空中加油、潜艇教程 1.1~1.3、水雷教程、空空作战史等）。
3. **消息日志多行教程消息不翻译**：引擎新增「多行消息逐行查表」路径
   （整段查表必然落空、按标点切分的占比阈值又被长行拖低），并在撇号 `’`/`'`
   形态不一致时按两种形态重试。修复后消息日志中的教程消息整段中文，
   且与界面按钮名称一致（如「航线规划 / 绘制航线 (Plot Course)」）。
4. **装备名称不统一**：Rafael Python 系列全库统一为「怪蛇」N，教程中不再出现裸英文机型。

---

## 四、遗留与已知项（如实记录）

1. **战役简报长文残余**：`Northern Inferno`、`LIVE` 等 69 个战役 HTML 仍有 344 段长文
   未命中词典（属词典覆盖缺口，非教程范围）。教程范围已归零。
2. **引号标签措辞差异 49 处**：教程散文使用双语写法（如「航线规划 / 绘制航线 (Plot Course)」），
   与菜单标准词「规划航线」措辞不同但同义；已作为提示项记录，未计入失败。
3. **3 个教程的首轮加载竞态**：扫描过程中出现 1 次 `MenuStrip not found`（启动未完成即发命令），
   重试后该教程仍为 ok；属测试脚本时序问题，与汉化无关。
4. **19 个纯图片 HTML**：Silent Service 资产页仅含一行 `<img>`，无文本可译。

---

## 五、项目整体检查要点

- `scripts/verify_all.py`：9/9 通过（词库完整性、菜单、引擎解析、想定元数据、简报、
  地名、DB 模板、版本、仓库整洁）。
- `scripts/verify_release.py`：PASS（修复了此前把单位 `in`、型号 `AT-1M`、参考书目
  误判为漏译的假阳性规则）。
- `scripts/verify_terminology.py`：PASS（术语与命名一致性 0 违规）。
- 安装器与绿色包均携带落盘汉化的 323 个 Scenarios HTML（内嵌 `SCEN_HTML_ZIP` / 包内目录），
  保证全新安装与免安装包结果一致，且 `.orig` 备份支持 100% 还原。
