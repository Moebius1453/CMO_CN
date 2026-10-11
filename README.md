# CMO 全汉化工程 (cmo-CN)

《Command: Modern Operations（现代行动：指挥）》完整中文本地化工程。

- **当前版本**：[`VERSION`](VERSION) → `4.0.0`
- **游戏根目录**：`G:\soft\steam\steamapps\common\Command - Modern Operations`
- **引擎版本**：`CMOLocalizer` v4.0（CLR `AppDomainManager` 注入，不修改 `Command.exe` 任何字节）

## 工程结构

```
src/engine/       CMOLocalizer.cs        运行时本地化引擎（编译为 CMOLocalizer.dll）
src/installer/    CMO_Chinese_Setup.cs   单文件一键安装/还原程序
src/tools/        IL 反射与窗口探测诊断工具
src/tests/        引擎加载与词典解析测试
dict/             CMOLocalizer.json      主词库（offsets / exact）
dict/glossary.json                       权威军语术语表（唯一真源）
dict/src/                                各批次人工译文源文件
assets/db_templates/                     数据库详情页渲染模板（已汉化）
scripts/          词库构建、校验、抽取与诊断脚本
docs/             工程档案、术语表、翻译规范、验收报告
build/            构建产物（不纳入版本库）
```

## 汉化架构

1. **底层字符串池注入**：解密游戏主程序 `AoIBWWlDJbaf7LijnA` 字符串池，
   按偏移覆盖显示字符串。这是覆盖率最高的通道，一次注入全局生效。
2. **UI 控件树实时巡检**：后台线程扫描 WinForms / WPF / WebView2 视觉树，
   就地把控件文本替换为中文，覆盖非字符串池来源的动态文本。
3. **资产层汉化**：数据库 HTML 模板、想定元数据与 HTML 简报按文件替换，
   首次改动自动保留 `.orig` 原版备份，安装器支持一键还原。

**安全不变式**：只有被 `scripts/triage.py` 判定为 `display` 的字符串才允许进入
`offsets`。SQL 列名、Lua 语句、控件名、路径、型号编号一律排除——把这类字符串
翻译掉会造成 DAL 层 `KeyNotFoundException` 与 Lua 沙箱崩溃（历史故障）。

## 构建

```bash
bash scripts/build.sh
```

产出 `build/CMOLocalizer.dll` 与 `build/CMO_Chinese_Setup.exe`（内嵌 DLL、词库与
8 个数据库模板）。

## 词库流水线

```bash
PY=... python

# 1. 字符串池三分类，生成待译工单
$PY scripts/triage.py

# 2. 多源确定性合并 → dict/CMOLocalizer.json（可重复构建，字节一致）
$PY scripts/build_dict.py

# 3. 验收门禁
$PY scripts/verify_release.py        # 词库完整性与质量
$PY scripts/verify_menu.py           # 菜单 100% 覆盖、无生硬残留
$PY scripts/verify_engine_parse.py   # 引擎扫描器可完整加载词库
```

## 文档

| 文件 | 内容 |
|---|---|
| `docs/MENU_TERMINOLOGY.md` | 菜单军语全量对照表（328 项） |
| `docs/TRANSLATION_BRIEF.md` | 引擎字符串翻译规范 |
| `docs/BRIEFING_BRIEF.md` | 想定简报军事文体翻译规范 |
| `docs/TEST_REPORT.md` | 实机测试报告与证据 |
| `docs/CHANGELOG.md` | 版本变更记录 |

## 安装

```
CMO_Chinese_Setup.exe                     图形界面一键安装 / 还原
CMO_Chinese_Setup.exe /install  "游戏目录"  静默安装
CMO_Chinese_Setup.exe /uninstall "游戏目录"  静默还原
```
