# CMO 汉化自动升级方案（v4.1.4 计划）

> 状态：**设计稿，等待确认**（文档中的按钮文字即最终代码字符串，界面见 `evidence/design/v414_updater/`）
> 需求来源：用户 2026-10-15 —— 「自动升级要给出方案；要有开机启动开关；每次开机以及每 6 小时检查更新；
> 安装器里增加『不再提醒』与『检查升级』按钮；先设计界面给我看」。

---

## 1. 组件划分

| 组件 | 文件 | 职责 | 变化 |
|---|---|---|---|
| 更新助手 | `CMO_Updater.exe`（新增，约 120 KB） | 定时/开机检查更新、提醒、下载校验、静默调用安装器、失败回滚 | **新增** |
| 安装器 | `CMO_Chinese_Setup.exe` | 安装/还原汉化、写设置、新增两个按钮；对外提供静默开关 | 改造 |
| 设置与状态 | `HKCU\Software\CMOChinese` | 唯一状态源（见 §4） | 新增 |
| 日志 | `%LOCALAPPDATA%\CMOChinese\updater.log` | 排障（512 KB × 5 轮转） | 新增 |

更新助手与安装器同一工具链（`csc` + WinForms，无需 .NET 之外的依赖），**不常驻托盘也能工作**（见 §3 检查时机）。
两者共用一份设置，安装器写入、助手读取；助手只读代码路径不依赖游戏是否运行。

## 2. 更新源与协议

- 首选：`GET https://api.github.com/repos/mj8724/CMO_CN/releases/latest`（带 `User-Agent`，GitHub 强制要求）
- 版本比较：解析 `tag_name`（`v4.1.4` → `4.1.4`）与本地 `Version` 用 `System.Version` 逐段比较，忽略 `-beta` 后缀
- 资产选择：优先 `CMO_Chinese_Setup_v<ver>.exe`，退化为 `CMO_Chinese_Setup.exe`
- 校验：发布脚本为每个资产生成 `*.sha256`；下载后逐字节校验，不符即删除并报错
- 镜像兜底（可选开关）：直连失败后依次尝试 `https://ghproxy.net/<原始URL>` 等，镜像 URL 在设置中可改
- 超时：连接 10 s / 单次请求 30 s；只查版本时走 API（KB 级），确认升级后才下载（14 MB）

## 3. 检查时机（严格满足「每次开机 + 每 6 小时」）

| 触发 | 说明 |
|---|---|
| 开机/登录 | `HKCU\...\Run` 启动 `CMO_Updater.exe --boot`，进程启动后**延迟 60 秒**发起检查（避开登录高峰；电池供电时延迟 5 分钟） |
| 常驻定时 | 每 `CheckIntervalHours`（默认 **6**）小时检查一次；系统休眠唤醒后按 `LastCheckUtc` 补查 |
| 手动 | 安装器/更新助手的「检查升级」按钮 |
| 失败退避 | 网络失败静默忽略，退避 1h→2h→6h；连续失败 3 次才在更新助手窗口显示一行红字，**不弹窗打扰** |

只在新版本时才提醒；`SkippedVersion` 命中的版本不再提示。

## 4. 设置与状态（`HKCU\Software\CMOChinese`）

| 键名 | 类型 | 默认 | 说明 |
|---|---|---|---|
| `GamePath` | REG_SZ | 自动探测 | 安装器写入，助手安装时复用 |
| `Version` | REG_SZ | — | 已安装汉化版本 |
| `AutoCheck` | DWORD | 1 | 自动检查总开关（与「不再提醒」联动，见 §7） |
| `CheckIntervalHours` | DWORD | 6 | 检查间隔，可选 1/3/6/12/24 |
| `AutoStart` | DWORD | 1 | 开机启动开关（写/删 Run 键） |
| `SkippedVersion` | REG_SZ | 空 | 「不再提醒」针对的版本号 |
| `LastCheckUtc` | REG_SZ | 空 | 上次检查时间 |
| `LastNotifiedVersion` | REG_SZ | 空 | 上次提醒的版本 |
| `ProxyOverride` | REG_SZ | 空 | 可选自定义代理；空 = 跟随系统 IE 代理 |

## 5. 更新状态机

```
IDLE ──检查──▶ CHECKING ──┬─▶ UP_TO_DATE（静默，仅更新 LastCheckUtc）
                          └─▶ AVAILABLE ──┬─[立即升级]─▶ DOWNLOADING ─▶ VERIFYING ─▶ INSTALLING ─▶ DONE（提示重启游戏）
                                          ├─[稍后]     ─▶ 本次会话/24h 内不再提示
                                          └─[不再提醒] ─▶ SkippedVersion = 该版本（仍可手动检查）
DOWNLOADING/VERIFYING 失败 ─▶ ERROR（记录日志 + 提供「手动下载页」链接，不重试）
INSTALLING 失败 ─▶ ROLLBACK（还原安装前备份）─▶ ERROR
```

## 6. 安装方式（推荐 A，实现后再补 B）

- **A. 静默运行新版安装器（推荐）**：下载 `CMO_Chinese_Setup_v<ver>.exe` → 运行 `--install "<GamePath>" --silent` →
  退出码 0 且注册表 `Version` 更新即成功。单一代码路径，DLL / 词典 / Scenarios HTML / 自启注册全部与手动安装一致。
- B. 绿色包热替换（备选）：下载 `CMO_Chinese_Pack_<ver>.zip` 覆盖 DLL/词典/Scenarios。体积小，但需自行实现覆盖与还原，
  已决定**本期不做**，避免与安装器行为分叉。
- **游戏运行中**：检测到 `Command.exe` 进程 → 提示「请先退出游戏」，或点「退出游戏并升级」（先发关闭请求，超时 20 s 后强制结束）。

## 7. 「不再提醒」的确切语义（待用户拍板，推荐方案 B）

| 方案 | 行为 | 优点 | 风险 |
|---|---|---|---|
| A. 跳过当前版本 | `SkippedVersion=4.1.4`，下个版本仍提醒 | 精细 | 用户以为彻底关了，实际还会被提醒 |
| **B. 关闭自动检查 + 记住跳过版本（推荐）** | `AutoCheck=0`、`SkippedVersion=当前版本`；按钮变为「开启提醒」，随时可恢复；手动「检查升级」始终可用 | 符合大众直觉，一键静音 | 用户会长时间停留在旧版本 |

> 文档按 B 编写界面；若选 A，仅按钮行为与提示文案改变，界面布局不变。

## 8. 安全与失败处理

- 仅允许 `https://`；域名白名单 `api.github.com` / `github.com` / `objects.githubusercontent.com` /（可选）`ghproxy.net`
- 证书错误直接判失败，**不降级为 http**
- 下载到 `%LOCALAPPDATA%\CMOChinese\update\`，安装完成后清理；`*.sha256` 不符立即删除临时文件
- 安装前把现有 `CMOLocalizer.dll` / `CMOLocalizer.json` 备份为 `*.updbak`，安装失败自动还原
- 全程仅使用 `HKCU`，无需管理员权限；卸载时清理 Run 键、注册表项、`%LOCALAPPDATA%\CMOChinese`（日志可保留）
- 老版本用户（≤ v4.1.3）没有更新助手，需手动装一次带助手的版本，之后即可自动升级

## 9. 卸载与还原一致性

安装器的 `--uninstall` 除现有还原逻辑外，新增：结束运行中的 `CMO_Updater.exe`、删除 Run 键、删除注册表项、
删除 `%LOCALAPPDATA%\CMOChinese`，保证「还原原版英文」后系统里不留后台进程与残留设置。

## 10. 交付物

- `src/updater/CMO_Updater.cs`（新）+ `scripts/build.sh` 增加编译与内嵌
- `src/installer/CMO_Chinese_Setup.cs` 改造（UI 两按钮 + 自启开关 + 静默模式 + 设置读写）
- `scripts/publish_release.py` 生成 `*.sha256` 资产
- `package.sh` 产物：`CMO_Chinese_Setup_v<ver>.exe`、`CMO_Chinese_Pack_<ver>.zip`、`CMO_Updater.exe`
- 验收：模拟「有新版本」的临时 Release → 开机检查、6 小时定时、手动检查、不再提醒、失败回滚 各一条实机路径
