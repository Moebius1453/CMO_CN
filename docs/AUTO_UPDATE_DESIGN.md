# CMO 汉化自动升级方案（v2，v4.1.6 已实现）

> 状态：**已实现并实机验证**（v4.1.6）。v1（4.1.4 落地）的「设置/状态同存注册表、无回退」实装后暴露了两个真实事故，
> 本版是修正后的方案：**状态以部署实证（安装清单）为准、设置与安装态解耦、回退以整个安装包为单位**。
> 新增/变更章节：§4（设置与状态）、§4.1（安装清单）、§4.2（回退）、§9（卸载语义）。
> 需求来源：用户 2026-10-15「自动升级要给出方案…」+ 2026-10-16 用户实测反馈
> 「我还原了也显示已经安装新版本了，这不就是想回退也回退不了了么，而且之前的最小化托盘设置啥的
> 全都没有了，更新频率也没有了」。
> 界面证据见 `evidence/design/v414_updater/`，发版前必跑清单见 `docs/RELEASE_TEST_SOP.md`，
> 缺陷台账（R13–R18 为本方案引入）见 `docs/BUG_REGISTRY.md`。

---

## 1. 组件划分

| 组件 | 文件 | 职责 | 变化 |
|---|---|---|---|
| 更新助手 | `CMO_Updater.exe` | 定时/开机检查更新、提醒、下载校验、静默调用安装器、回退与失败自回滚 | 新增（v4.1.4）→ v4.1.6 增加回退与安装后自检 |
| 安装器 | `CMO_Chinese_Setup.exe` | 安装/还原汉化、写安装清单、更新设置、提供 `--install/--uninstall/--rollback/--adopt/--list-versions/--purge-settings` | 改造 |
| 状态真相 | 游戏目录 `CMOChinese_Installed.json` | 版本 + 安装时间 + 来源 + 引擎文件 sha256（见 §4.1） | **新增（v4.1.6）** |
| 设置 | `%APPDATA%\CMOChinese\settings.json` | 用户可改的设置（见 §4）；注册表仅作旧版迁移回退 | **v4.1.6 从注册表迁到文件** |
| 安装包缓存 | `%LOCALAPPDATA%\CMOChinese\packages\` | 回退与失败自回滚的素材（保留最近 `snapshot_keep` 个） | **新增（v4.1.6）** |
| 共享实现 | `src/shared/CMOShared.cs` | 设置/清单/缓存/回退/版本门控的唯一实现，编译进两个 EXE | **新增（v4.1.6）** |
| 日志 | `%LOCALAPPDATA%\CMOChinese\updater.log`（+ `shared.log`） | 排障（512 KB 轮转） | 新增 |

更新助手与安装器同一工具链（`csc` + WinForms，无需 .NET 之外的依赖），安装、还原、回退统一走
**安装器命令行的单一路径**，避免两处实现分叉。

更新助手与安装器同一工具链（`csc` + WinForms，无需 .NET 之外的依赖），**不常驻托盘也能工作**（见 §3 检查时机）。
两者共用一份设置，安装器写入、助手读取；助手只读代码路径不依赖游戏是否运行。

## 2. 更新源与协议

- 首选：`GET https://api.github.com/repos/mj8724/CMO_CN/releases/latest`（带 `User-Agent`，GitHub 强制要求）
- 版本比较：解析 `tag_name`（`v4.1.4` → `4.1.4`）与本地 `Version` 用 `System.Version` 逐段比较，忽略 `-beta` 后缀
- 资产选择：优先 `CMO_Chinese_Setup_v<ver>.exe`，退化为 `CMO_Chinese_Setup.exe`
- 校验：发布脚本为每个资产生成 `*.sha256`；下载后逐字节校验，不符即删除并报错
- 镜像兜底（**已确认启用**）：直连失败后依次尝试 `https://ghproxy.net/<原始URL>` 等，镜像 URL 在设置中可改；
  版本信息请求**不走镜像**（保证版本号来源可信）
- 超时：连接 10 s / 单次请求 30 s；只查版本时走 API（KB 级），确认升级后才下载（14 MB）

## 3. 检查时机（严格满足「每次开机 + 每 6 小时」）

| 触发 | 说明 |
|---|---|
| 开机/登录 | `HKCU\...\Run` 启动 `CMO_Updater.exe --boot`，进程启动后**延迟 60 秒**发起检查（避开登录高峰；电池供电时延迟 5 分钟） |
| 常驻托盘 | 助手为常驻托盘图标（已确认）：主窗关闭 = 隐藏到托盘，托盘右键可「立即检查更新 / 打开设置 / 查看日志 / 退出」 |
| 常驻定时 | 每 `CheckIntervalHours`（默认 **6**）小时检查一次；系统休眠唤醒后按 `LastCheckUtc` 补查 |
| 手动 | 安装器/更新助手的「检查升级」按钮 |
| 失败退避 | 网络失败静默忽略，退避 1h→2h→6h；连续失败 3 次才在更新助手窗口显示一行红字，**不弹窗打扰** |

只在新版本时才提醒；`SkippedVersion` 命中的版本不再提示。

## 4. 设置与状态（设置存文件、状态存清单）

### 4.1 安装状态：游戏目录内的安装清单（唯一真相）

`<游戏目录>\CMOChinese_Installed.json`：

```json
{ "schema": 1, "version": "4.1.6", "installed_at": "2026-10-16T01:23:45Z",
  "source": "…\\CMO_Chinese_Setup_v4.1.6.exe",
  "files": { "CMOLocalizer.dll": "<sha256>", "CMOLocalizer.json": "<sha256>" } }
```

- 写入时机：安装成功后**最后一步**原子写（先 `.tmp` 再替换）；还原时删除。
- 读取规则（安装器状态栏、助手托盘「当前版本」、气泡/弹窗「当前版本」全部走这里）：
  - 清单在且哈希一致 → 已安装 v<清单版本>（<安装时间>）；
  - 清单在但哈希不一致 → 「检测到 …与安装清单不一致，建议重新安装」（只对清单记录过的引擎文件判定）；
  - 无清单但引擎文件在（存量 4.1.x 用户 / 绿色包用户）→ **自动 adopt**：按当前文件生成清单，
    版本回退链 = 注册表 `Version` → 已部署资产 `_meta.version` → `unknown`（可用 `--adopt` 手动触发）；
  - 都没有 → 未安装（原版英文环境）。
- 注册表 `Version` 降级为「迁移期回退」，不再是状态来源（历史事故：它只反映**最后运行的那个安装包**的
  `CurrentVersion`，与真实文件无关，导致“还原了还显示已安装新版本”）。

### 4.2 设置文件：`%APPDATA%\CMOChinese\settings.json`

| 字段 | 默认 | 说明 |
|---|---|---|
| `schema` | 1 | 结构版本，便于日后迁移 |
| `auto_check` | true | 自动检查总开关（与「不再提醒」联动，见 §7） |
| `check_interval_hours` | 6 | 检查间隔，可选 0(仅开机)/1/3/6/12/24 |
| `auto_start` | true | 开机自启开关（写/删 Run 键） |
| `notify_mode` | 1 | 0=弹窗 1=托盘气泡 2=静默 |
| `auto_install` | true | 自动下载并静默安装 / 只提醒 |
| `mirror` | `https://ghproxy.net/` | 下载镜像（版本信息仍直连 api.github.com） |
| `tray_on_close` | true | 关闭更新窗口时最小化到托盘（后台继续） |
| `snapshot_keep` | 2 | 保留最近几个安装包缓存（回退用） |
| `last_check_utc` / `skipped_version` / `last_notified_version` | 空 | 调度与提醒状态 |
| `game_path` | 自动探测 | 游戏目录（助手默认用自身所在目录） |
| `pending_install_version` / `pending_previous_version` | 空 | 升级中标记：安装后自检失败则用它回退 |

- 写入为原子写（`.tmp` → `File.Replace`/`Move`）；文件无法识别时备份为 `settings.json.bad` 并回落默认值。
- 旧版注册表值在文件缺失时**一次性迁移**（迁移后仍保留注册表项供旧版本读取，但不再依赖它）。
- 安装器与助手同写一个文件：写前重读，检测到被其它进程改动时记警告，以当前窗口的值为准。
- **还原/卸载不删设置**；只有显式「同时清除我的全部设置」/`--purge-settings` 才清。

### 4.3 回退（v4.1.6 新增）

- 回退单位 = **整个安装包**（`packages\CMO_Chinese_Setup_v<ver>.exe`，安装/升级时自动缓存，按 `snapshot_keep` 清理）。
  理由：真实安装足迹含 Scenarios(323 文件/218MB)、Symbols/Menu(158 文件)、地名集、DB 模板，
  快照引擎资产会得到“旧引擎 + 新资产”的混合态。
- 回退流程（`CMOShared.Rollback.Run`，安装器按钮、助手托盘菜单、`--rollback` 共用）：
  1. 结束 `Command.exe` / `CMO_Updater.exe`；
  2. 运行 `缓存包 --install "<游戏>" --silent`（旧包自然会把全部资产换成它自带的）；
  3. 新版代码**补写安装清单**（旧包不写清单）；
  4. 校验部署的 `CMOLocalizer.dll` sha256 == 缓存包内嵌的 dll sha256，不一致则报错且不登记清单。
- 无缓存时明确拒绝并提示（不静默、不自作主张）。
- 「可回退」提示有门控（见 §5）：仅当 `远端 < 本地` **且本地已缓存该版本包**时才提示。

## 5. 更新状态机

```
IDLE ──检查──▶ CHECKING ──┬─▶ UP_TO_DATE（静默，仅更新 LastCheckUtc）
                          ├─▶ AVAILABLE（远端>本地）──┬─[立即升级]─▶ DOWNLOADING ─▶ VERIFYING ─▶ INSTALLING ─▶ 写清单/缓存包
                          │                            ├─[稍后]     ─▶ 本次会话/24h 内不再提示
                          │                            └─[不再提醒] ─▶ SkippedVersion = 该版本（仍可手动检查）
                          └─▶ ROLLBACK_OFFER（远端<本地 **且** 本地已缓存该版本包）──[立即回退]─▶ Rollback.Run ─▶ 写清单
远端<本地但无缓存（例：本机跑了未发布的新版本）──▶ 静默（只记日志）
DOWNLOADING/VERIFYING 失败 ─▶ ERROR（记录日志 + 提供「手动下载页」链接，不重试）
INSTALLING 失败 ─▶ 下次启动时 PostInstallCheck()：清单版本 ≠ 目标版本 ─▶ 用缓存包自动回滚（无缓存则提示手动修复）
```

状态真相为安装清单（§4.1）：助手启动/每次检查都会 `ResolveInstalled()`（含 adopt 自愈）后再比较版本。

## 6. 安装方式（推荐 A，实现后再补 B）

- **A. 静默运行新版安装器（推荐）**：下载 `CMO_Chinese_Setup_v<ver>.exe` → 运行 `--install "<GamePath>" --silent` →
  退出码 0 且注册表 `Version` 更新即成功。单一代码路径，DLL / 词典 / Scenarios HTML / 自启注册全部与手动安装一致。
- B. 绿色包热替换（备选）：下载 `CMO_Chinese_Pack_<ver>.zip` 覆盖 DLL/词典/Scenarios。体积小，但需自行实现覆盖与还原，
  已决定**本期不做**，避免与安装器行为分叉。
- **游戏运行中**：检测到 `Command.exe` 进程 → 提示「请先退出游戏」，或点「退出游戏并升级」（先发关闭请求，超时 20 s 后强制结束）。

## 7. 「不再提醒」的确切语义（已确认：方案 B）

**已确认行为**：点击「不再提醒」= 关闭自动检查（`AutoCheck=0`）并记住当前版本（`SkippedVersion=<ver>`），
按钮文案切换为「开启提醒」；手动「检查升级」始终可用，随时可恢复自动检查。

参考对比（供追溯）：

> **已确认的决策（2026-10-15 用户拍板）**
> 1. 「不再提醒」= 关闭自动检查 + 记住跳过版本（§7 方案 B）
> 2. 升级方式 = 静默运行新版安装器（§6 方案 A）
> 3. 更新助手**常驻托盘图标**（右键菜单：立即检查更新 / 打开设置 / 查看日志 / 退出；发现新版本用气泡提醒，不打断游戏）
> 4. 更新下载**直连 GitHub + 国内镜像兜底**（版本信息始终走 api.github.com；安装包下载失败后自动尝试镜像）

## 8. 安全与失败处理

- 仅允许 `https://`；域名白名单 `api.github.com` / `github.com` / `objects.githubusercontent.com` /（可选）`ghproxy.net`
- 证书错误直接判失败，**不降级为 http**
- 下载到 `%LOCALAPPDATA%\CMOChinese\update\`，安装完成后清理；`*.sha256` 不符立即删除临时文件
- 安装前把现有 `CMOLocalizer.dll` / `CMOLocalizer.json` 备份为 `*.updbak`，安装失败自动还原
- 全程仅使用 `HKCU`，无需管理员权限；卸载时清理 Run 键、注册表项、`%LOCALAPPDATA%\CMOChinese`（日志可保留）
- 老版本用户（≤ v4.1.3）没有更新助手，需手动装一次带助手的版本，之后即可自动升级

## 9. 卸载与还原一致性（v4.1.6 修正）

安装器的 `--uninstall` 做：还原全部汉化资产（配置/模板/简报/地名集/按钮图/启动器）→ 结束 `CMO_Updater.exe` →
删 Run 键 → 删 `CMO_Updater.exe` → **删安装清单**（状态立即变“未安装”）→ 只清注册表里的 `Version` 值。

- **不再删除设置**（旧版会 `DeleteSubKeyTree` 整个键，用户实测反馈“更新频率/托盘设置全没了”）：
  设置存 `%APPDATA%\CMOChinese\settings.json`，还原不碰它；重装后频率/提醒方式/镜像/自启偏好继续生效。
- 需要连设置一起清掉时：勾选安装器上的「还原时同时清除我的全部设置」，或 `--uninstall … --purge-settings`。
- `%LOCALAPPDATA%\CMOChinese` 下的日志与安装包缓存可保留（回退素材）；真要清可手动删目录。

## 10. 交付物

- `src/updater/CMO_Updater.cs` + `src/shared/CMOShared.cs`（共享状态层）
- `src/installer/CMO_Chinese_Setup.cs`（状态栏/设置控件/回退按钮/命令行）
- `scripts/build.sh`（两 EXE + 测试宿主 `build/CMOSharedTest.exe`）、`scripts/publish_release.py`（`*.sha256` 资产）
- `package.sh` 产物：`CMO_Chinese_Setup_v<ver>.exe`、`CMO_Chinese_Pack_<ver>.zip`、`CMO_Updater.exe`
- 回归：`scripts/verify_regressions.py --static --installer --live`（台账 R13–R18）、
  `scripts/test_installer_ui.py`、`scripts/test_updater_live.py`
