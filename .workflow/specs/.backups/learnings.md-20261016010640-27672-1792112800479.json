---
title: "Learnings"
readMode: optional
priority: medium
category: learning
keywords:
  - bug
  - lesson
  - gotcha
  - learning
---

# Learnings

Add entries with: `/maestro-spec learning <description>`

## Entries

<spec-entry category="learning" keywords="session-knowledge,manual" date="2026-10-15" sid="S-20261015-7191e208310ad5f7" title="发版铁律：缺陷必须登记台账，发版前必须重跑回归 SOP" sourceRef="session:ksyn-2107ce852f0cfe00:KDC-7191e208310ad5f7">

### 发版铁律：缺陷必须登记台账，发版前必须重跑回归 SOP

# 发版铁律：缺陷必须登记台账，发版前必须重跑回归 SOP

## 规则（用户 2026-10-15 明确要求）
1. **每个用户可感知的缺陷都要登记**在 `docs/BUG_REGISTRY.md`（编号 R<n>，只增不改）：
   现象 / 根因 / 改动位置 / 用户可见验收 / 回归测试点。
2. **每个缺陷都必须落一条可执行回归用例**到 `scripts/verify_regressions.py`
   （能自动化就自动化；`--static` 为源码/资产/词库断言，`--live` 为实机用例），
   并在 `docs/CHANGELOG.md` 对应版本段落说明。
3. **每次发版前必须按 `docs/RELEASE_TEST_SOP.md` 执行**：
   环境校验（R0：游戏目录词库与 `dict/` 逐字节一致、引擎含本轮修复标记）→
   `verify_all.py` 10/10 → `verify_regressions.py` 全绿（static+live）→
   `package.sh`（内含 static 回归硬门禁 + 密钥门禁 + 资产覆盖断言）→
   `publish_release.py`（校验 24h 内、同版本、含 live 且全通过的回归报告，否则拒绝上传）→
   发布后自检（latest/HTTP 206/sha256/升级链/实装复测）。

## 为什么（本规则针对的真实事故）
- v4.0.0 已修过一次「数据库查看器详情空白」（改写 ComboBox.Text 破坏游戏分派），
  v4.1.3 的下拉框全量汉化从**通用路径**绕过了当时的专用保护，导致 v4.1.5 复发；
  因为当时**没有台账、没有回归用例**，同一问题隔了两个版本又回到用户面前。
- v4.1.5 发版前还抓到一次真实环境漂移：游戏目录被旧安装包覆盖成不含修复的引擎，
  使回归“失败”实为环境错 → 因此增加 R0 环境校验。
- R0 不能用整文件哈希：csc 每次编译 MVID/时间戳都变，重新 build 后哈希必变；
  应判「词库逐字节一致」+「引擎二进制含关键修复符号」。

## 必须遵守
- 新增缺陷时三件套缺一不可：台账条目 + 回归用例 + CHANGELOG 条目。
- 回归用例是唯一权威：改了引擎/词库/资产却让某条用例失效，必须同步更新用例并说明原因，
  不允许删除用例或降低阈值来「通过」。
- 打包/发布门禁失败必须修复后重跑；仅允许显式 `--force` 绕过并在提交信息/发布说明留痕。
- 手工才能在真实用户路径上验证的项（如消息日志多行教程消息）在台账标注「手动」，
  发版前由人完成并记录证据。

</spec-entry>
