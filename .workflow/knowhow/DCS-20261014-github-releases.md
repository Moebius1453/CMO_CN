---
title: 发布标准必须更新GitHub Releases与安装包
type: decision
created: 2026-10-14T11:44:56.051Z
lifecycleStatus: active
---

# 发布标准：必须同步更新 GitHub Releases 与安装包资产

用户明确要求（2026-10-14）：“以后让你发布都要更新 releases 以及安装包”。

发布标准工作流：
1. 更新 `VERSION`（版本号增加），同步更新 `src/installer/CMO_Chinese_Setup.cs` 标题与 `docs/CHANGELOG.md` 记录。
2. 运行 `scripts/build_dict.py` 更新主词典元数据，运行 `scripts/package.sh` 构建 `CMOLocalizer.dll`、`CMO_Chinese_Setup.exe`（单文件安装器）与 `CMO_Chinese_Pack_<ver>.zip`（绿色包）。
3. 提交 git commit 并打 git tag（如 `v4.1.2`），推送到 GitHub `master` 及其 tags。
4. **核心要求**：必须执行 `scripts/publish_release.py`，调用 GitHub API 在 GitHub 仓库页面创建/更新 Release（包含更新日志），并**必须将 `CMO_Chinese_Setup.exe` 和 `CMO_Chinese_Pack_<ver>.zip` 完整上传至 Release Assets**！
