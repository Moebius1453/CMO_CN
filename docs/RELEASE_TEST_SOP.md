# 发版前测试 SOP（每次发版必须逐条执行）

> 目标：**同一个问题不允许出现两次**。用户反馈过的每个问题都登记在 `docs/BUG_REGISTRY.md`，
> 发版前按本 SOP 重跑全部回归用例；任何一条 FAIL 都不允许发包，直到修复或（必要时）明确降级记录。
>
> 相关文件：`docs/BUG_REGISTRY.md`（台账）、`scripts/verify_regressions.py`（可执行回归）、
> `scripts/verify_all.py`（代码/词库门禁）、`scripts/package.sh`（打包）、`scripts/publish_release.py`（发布）。

## 0. 环境校验（先做，否则后面全是假结论）

```bash
python scripts/verify_regressions.py --static    # 含 R0：游戏目录词库与 dict/ 同哈希 + 引擎含本轮修复标记
```

- 游戏目录装的**必须**是含本轮修复的产物。历史踩坑：发版前被旧安装包覆盖回不含修复的引擎，
  回归“失败”实为环境错（当时日志/现象表现为：类型下拉框又变中文、详情面板 html_len 退回 1541）——
  R0 就是为这类事故设的。
- R0 不用整文件哈希：csc 每次编译 MVID/时间戳都变，重新 build 后哈希必变；
  改判「词库逐字节一致」+「引擎二进制包含关键修复符号（`DEPLOY_MARKERS`）」。
- 若 R0 FAIL：先 `bash scripts/deploy.sh`（或运行本轮安装包）重新部署，再继续。

## 1. 代码/词库门禁

```bash
python scripts/verify_all.py        # 必须 10/10（dictionary/menu/engine parse/scenario meta/
                                    # briefings/placenames/db templates/version/secrets/repository）
```

## 2. 回归用例（台账全覆盖，含实机）

```bash
python scripts/verify_regressions.py            # static + live，必须全绿
```

- `live` 用例需要**游戏正在运行**（`Command.exe`）；脚本会自己 `CLICK_MENU 数据库查看器`
  并逐类点击条目取证，无需人工操作。
- 覆盖：R0 环境一致 / R1 数据库查看器八类详情渲染 + 类型框原生 token + 无频闪 /
  R2 汉化地名集资产 / R5 中文按钮图 / R6 offsets 无 SQL / R7 术语门禁 / R8 禁止改写下拉框 .Text。
- 结果写入 `build/regression_report.json`（含 VERSION、UTC 时间戳、逐条结果），**发布脚本会校验它**。
- 详细清单与手工补充项（R9/R10/R11 抽样）见台账 `docs/BUG_REGISTRY.md`。

## 3. 打包

```bash
bash scripts/package.sh
```

- 内部已串：密钥门禁（工作区+全历史）→ `build.sh` → **static 回归硬门禁** → 资产覆盖率断言。
- 任一门禁失败即中止，不得手动绕过；确需绕过必须在提交信息与本文件记录原因。

## 4. 发布

```bash
python scripts/publish_release.py
```

- 前置校验：`build/regression_report.json` 必须 ①版本号等于当前 `VERSION`、
  ②包含 `live` 用例且全通过、③生成时间在 24 小时内。否则拒绝上传（可用 `--force` 显式跳过，
  但必须在发布说明/提交信息中写明原因）。
- 发布说明自动取自 `docs/CHANGELOG.md` 的 `## v<version>` 段落，故**先写 CHANGELOG 再发布**。
- 版本号：`VERSION` 文件 + 安装器 `CurrentVersion`/窗口标题 + 更新助手 `UpdaterVersion` 四处同步。

## 5. 发布后自检

1. `https://api.github.com/repos/mj8724/CMO_CN/releases/latest` → tag 等于本次版本；
2. 四个资产（安装包/绿色包 + 两个 `.sha256`）用带 `Range` 的请求验证 HTTP 206；
3. sha256 资产内容与本地 `build/*.sha256` 一致；
4. 绿色包条目数（当前 690）与 `DB/Templates 8 / WW/PlacenameSets 2 / Symbols/Menu 28 / Launcher 1` 齐全；
5. 升级链：注册表 `HKCU\Software\CMOChinese\Version` 设为上一版本 → 运行 `CMO_Updater.exe --check`
   → 必须弹出「发现新版本 v<ver>」并显示安装包大小；测试后把注册表版本改回真实值；
6. 实装一次安装包（或绿色包），再跑一次 `python scripts/verify_regressions.py --live` 确认发布物本身可用；
7. **清单与设置校验（v4.1.6 起）**：实装后游戏目录的 `CMOChinese_Installed.json` 版本必须等于本次 `VERSION`；
   `CMO_Chinese_Setup.exe --list-versions "<游戏>"` 的 `installed` 必须与之一致；装完可跑一次
   `python scripts/verify_regressions.py --static --installer`，其中 R13 会验证「安装写清单 / 还原清清单 /
   设置保留（未勾选清除时）/ 回退后清单=目标版本且引擎与缓存包一致」。

> 注意：发布后若在本地又跑了一次 `package.sh`，`build/*.sha256` 描述的是**新构建**，
> 与已发布资产不再对应（csc 每次编译 MVID/时间戳不同）。要重新发布必须整批重传，
> 不要把旧的 `.sha256` 当成新包的摘要。

> 顺序提醒：`package.sh` 内的 static 门禁用 `--no-report` 运行，不会覆盖 `build/regression_report.json`；
> 但发布前最后一次**必须**是带 `--live` 的运行，发布脚本才看得到 live 全通过。

## 6. 新问题入库（强制）

发现/修复任何用户可感知问题时，必须同时完成三件事：

1. `docs/BUG_REGISTRY.md` 增一条 `R<n>`：现象、根因、改动位置、用户可见验收、回归测试点；
2. `scripts/verify_regressions.py` 增一条可执行用例（能自动化就自动化；实在不能自动化的标「手动」并在台账说明）；
3. `docs/CHANGELOG.md` 对应版本段落说明修复内容与用户影响。

> 只有台账 + 用例都在，下次发版才可能真的把这个问题测住。
