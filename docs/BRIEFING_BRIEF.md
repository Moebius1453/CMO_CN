# CMO 想定简报翻译规范（简报翻译员必读）

本文件是《Command: Modern Operations》**想定简报正文**的翻译契约，与
`docs/TRANSLATION_BRIEF.md` 的通用规则叠加适用（通用规则仍然全部有效）。

## 0. 任务形态

输入 `dict/brief_chunks/briefNN.json`：

```json
{ "Your objectives are the destruction of fifteen preplanned targets...": ["Chains Of War/BlueDawnBriefing.html"] }
```

- **键**是从 HTML 里抽出的英文正文片段（已做实体解码与空白折叠）。
- 值是该片段出现的文件列表，只是提示来源，**不要翻译它**。
- 输出写到 `dict/brief_src/briefNN.json`，格式同为 `{"英文原文": "中文译文"}`，
  **键必须与输入键逐字符一致**。

## 1. 文体要求

这是**军事想定简报**，语气要像解放军作战文书与兵棋推演简报：

- 叙述背景用**书面军事叙述体**：简洁、客观、信息密度高。
- 作战指令用**命令式**：「摧毁」「夺取」「掩护」「保持无线电静默」。
- 不要口语化，不要网络用语，不要营销腔。
- 中文语序，不要英文语序直搬。长句拆成中文习惯的短句。
- 段落长的可拆成 2–3 句中文，但**不要增删信息**，不要加原文没有的内容。

## 2. 专有名词处理

| 类别 | 处理 | 例子 |
|---|---|---|
| 国家/地区 | 标准中文名 | North Korea → 朝鲜；Falkland Islands → 福克兰群岛 |
| 部队番号 | 意译 + 保留番号 | 5th Eskadra → 第五分舰队 |
| 舰名 | 意译并加舰种 | USS Nimitz → 尼米兹号航母 |
| 机型 | 保留英文型号 + 中文绰号 | Su-24 Fencer → Su-24「击剑手」 |
| 地名 | 标准中文译名 | Bluff Cove → 布拉夫湾 |
| 人名 | 通行译名 | Kim Jong-un → 金正恩 |
| 日期/时间 | 保留数字，Zulu → 标准时 | 05-01-2018 / 00:00:00 Zulu → 2018-05-01 / 00:00:00 标准时 |
| 代号/行动名 | 意译 | Operation Paraquet → 白尾海雕行动 |

## 3. HTML 与实体

- 输入已经过实体解码，**译文里不要再写 HTML 标签或 `&nbsp;`**。
- 译文不要包含换行符。
- 首尾空格由程序处理，你只需给出干净的句子。

## 4. 必须保留的成分

- 格式化占位符 `{0}` `%s` 原样保留。
- 数字、坐标、单位（nm/kt/ft/m/kg）保持原样。
- 型号编号（F-16C、Su-35S、SA-10、AIM-120D）保持原样。
- 军语缩写首次出现给「中文全称 (缩写)」：SEAD → 压制敌防空 (SEAD)。

## 5. 参考术语

先读 `dict/glossary.json`，术语必须与之一致。补充常用对照：

| 英文 | 中文 |
|---|---|
| Briefing | 作战简报 |
| Objectives | 作战目标 |
| Situation | 态势 |
| Execution | 作战实施 |
| Timeline | 行动时间线 |
| Threat | 威胁 |
| Air Superiority | 制空权 |
| Standoff | 防区外 |
| Sortie | 出动架次 |
| Radar picket | 雷达警戒舰 |
| Carrier Battle Group (CVBG) | 航母战斗群 |
| Rules of Engagement | 交战规则 |
| Collateral damage | 附带损伤 |
| Escort | 护航 |
| Interdiction | 阻断打击 |
| Attrition | 消耗战 |

## 6. 输出

`dict/brief_src/briefNN.json`，单个扁平 JSON 对象，UTF-8 无 BOM，可直接 `json.load`。
不要写 markdown 代码块、注释或额外字段。
必须自检：条目数等于输入条目数、键集合与输入键集合完全一致。
