# 变更记录

## v4.0.0 — 全量汉化与可验证交付

首个纳入 git 版本控制、带机械验收门禁的正式版本。

### 新增：可重现词库流水线

- `scripts/triage.py`：把引擎 28,833 条字符串池三分为
  `display`（可偏移注入）/ `label`（仅 UI 层）/ `internal`（禁译），
  并给出偏移级注入的安全判据。
- `scripts/build_dict.py`：多源确定性合并，连续两次运行产物字节一致（已验证）。
- `scripts/verify_release.py` / `verify_menu.py` / `verify_engine_parse.py`：
  三道验收门禁，覆盖词库完整性、菜单覆盖率与引擎可加载性。
- `scripts/build.sh` / `package.sh`：csc 复现构建 DLL、内嵌资源安装器与绿色包。

### 修复：三个此前未被发现的严重缺陷

1. **词库整体未生效（最严重）**
   引擎用 `text.IndexOf("\"offsets\"")` 定位分节，而新增的 `_meta.counts.offsets`
   含同名字面量，命中位置在真正的分节之前，导致四个分节边界全部错位。
   实机日志为 `Loaded dictionaries: 0 offsets, 0 exact texts, ... 21908 word roots`
   —— **上一轮交付的汉化实际上一条都没生效**。
   修复：只在根对象深度 1 处匹配分节键；`_meta` 计数键改名为 `n_offsets` 等。

2. **转义解码不完整**
   原实现只识别 `\" \\ \n \r \t`，对 `\uXXXX`、`\/`、`\b`、`\f` 会原样保留，
   含 NUL 的词条永远匹配不上。已补全为完整 JSON 转义集。

3. **词库累积过程中被写入 Lua 语句**
   增量构建时 `local roll = 10`、`endtime = '` 等可执行代码进入 `offsets`，
   正是此前 `DAL KeyNotFoundException` 与 Lua 沙箱崩溃的失效模式。
   现已建立硬性不变式：**仅 `display` 类字符串可进入 `offsets`**。

### 汉化内容

- **菜单**：328 个菜单项逐条精修（10 个顶级菜单全部子项），
  修正 `Database Viewer`→`数据库查看器`、`Do not show` 误译等，
  统一全角标点与「中文全称 (缩写)」格式；产出
  `docs/MENU_TERMINOLOGY.md` 全量对照表。326/326 已译、0 生硬残留。
- **引擎字符串**：可读 UI 字符串 12,772 条，覆盖 11,000+ 条；
  注入字符串池 9,500+ 条，使得界面文本在源头即为中文。
- **数据库**：8 个详情页模板汉化并改用 Microsoft YaHei 字体，
  66 条字段标签经军语审查。
- **想定元数据**：259 个想定标题、123 个地理作战区域 100% 覆盖。
- **想定简报**：323 个 HTML 简报正文（461,700 字符 / 3,629 片段），
  实测发现上一轮仅翻译了标题行、正文仍为英文，本轮全量重译。
- **地图地名**：`WW/PlacenameSets` 下 321 条国家/海域标注汉化
  （Cities 城市名待字体渲染验证后处理）。

### 移除

- 4,885 条机翻简报碎片（`Zulu` 误译「祖鲁语」、句中断裂片段），
  其正确交付路径是编辑 `.scen`/`.html` 文件，非 UI 词库。
- 590 条从未被引擎读取的 `partial` 死配置。

## v3.0 及更早

见 `docs/project_memory.md`、`docs/CHECKPOINT_V2.md`、
`docs/SUBWINDOW_CHECKPOINT.md` 等上一轮遗留档案。
