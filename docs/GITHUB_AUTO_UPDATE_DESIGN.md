# 《现代行动：指挥 (CMO)》汉化补丁 GitHub 自动发现与手动确认更新设计方案

## 一、需求背景与核心原则

- **目标**：玩家使用汉化补丁（或安装器）时，能够自动从 GitHub 探测新版本发布，但**严格要求必须由玩家手动确认**后方可执行下载或跳转更新，严禁强制自动后台静默覆盖。
- **痛点与约束**：
  1. 保证游戏运行与安装器启动的极速响应，探测必须是**完全异步、非阻塞、静默失败**（无网络或 GitHub 超时不能弹报错干扰游戏）。
  2. 国内网络对 `api.github.com` 的访问受限（易超时/被拦截），需设计智能双通道（直连 + 国内 GitHub CDN 加速镜像）。
  3. 符合玩家习惯：安装器启动探测 + 游戏内菜单与非侵入式弹窗双层保障。

---

## 二、双轨架构设计

```
                    +-----------------------------------------+
                    |           GitHub Releases 远端发布       |
                    | (mj8724/CMO_CN/releases/latest)         |
                    +--------------------+--------------------+
                                         |
                       +-----------------+-----------------+
                       |                                   |
             [通道 A: 官方 API]                  [通道 B: 加速镜像/Raw]
        api.github.com/repos/...            mirror.ghproxy.com/https://...
                       |                                   |
                       +-----------------+-----------------+
                                         |
                                (JSON/Version 响应)
                                         |
        +--------------------------------+--------------------------------+
        |                                                                 |
        v                                                                 v
【轨道 1：一键安装器 CMO_Chinese_Setup】            【轨道 2：游戏内引擎 CMOLocalizer.dll】
- 启动时后台异步拉取                              - 进入推演 15 秒后低优先级异步拉取
- 发现新版 -> 顶部提示条/气泡                     - 发现新版 -> 顶部【帮助】菜单增加提醒标签
- 点击【立即更新】弹出更新日志                     - 弹出温和对话框询问是否打开 Release 页面
- 玩家手动确认后下载覆盖                          - 玩家手动确认：[前往下载] / [暂不更新]
```

---

## 三、核心技术实现细节

### 1. 远端版本与更新元数据获取协议

#### 通道 A：GitHub Releases 官方 REST API
- **URL**: `https://api.github.com/repos/mj8724/CMO_CN/releases/latest`
- **请求头**: `User-Agent: CMO_Chinese_Localizer/<version>`, `Accept: application/vnd.github.v3+json`
- **解析字段**:
  - `tag_name`: 例如 `"v4.2.0"`（用于语义化版本比对）
  - `name`: 标题，如 `"v4.2.0 — 全数据库百科正文100%全汉化"`
  - `body`: 详细更新说明 Markdown 文本
  - `assets`: 查找 `name == "CMO_Chinese_Setup.exe"` 与 `CMO_Chinese_Pack_*.zip` 的 `browser_download_url`

#### 通道 B：网络超时降级策略（国内加速节点）
若通道 A 在 4 秒内无响应，自动切换至镜像代理：
`https://ghfast.top/https://api.github.com/repos/mj8724/CMO_CN/releases/latest`
或直接读取 Raw 版本文件：
`https://ghfast.top/https://raw.githubusercontent.com/mj8724/CMO_CN/master/VERSION`

---

### 2. 语义化版本比对逻辑 (SemVer)

采用 `System.Version` 解析机制，自动剥离前缀 `v`/`V`：
```csharp
public static bool IsNewerVersion(string currentVerStr, string remoteTagStr)
{
    try
    {
        string c = currentVerStr.Trim().TrimStart('v', 'V');
        string r = remoteTagStr.Trim().TrimStart('v', 'V');
        Version cv = new Version(c);
        Version rv = new Version(r);
        return rv > cv;
    }
    catch { return false; }
}
```

---

### 3. 安装器 (`CMO_Chinese_Setup.exe`) 交互与更新流程

1. **非侵入式探测**：
   在 `InstallerForm_Load` 时启动 `Task.Run(() => CheckUpdateAsync())`，主界面正常显示，不阻塞玩家任何操作。
2. **状态提示**：
   若发现新版，主界面下方或顶部弹出浅绿色/浅蓝色横幅：
   `【发现新版本 v4.2.0】点击查看更新内容并升级`
3. **手动确认弹窗 (Manual Confirmation)**：
   玩家点击后，弹出专门的《更新确认与说明》对话框：
   ```
   +-------------------------------------------------------+
   | 发现 CMO 汉化补丁新版本 v4.2.0 (当前版本: v4.1.1)        |
   +-------------------------------------------------------+
   | 更新日志：                                             |
   | - 完成 6268 款装备数据库百科正文 100% 全量汉化            |
   | - 修复数据库查看器各子类型与国家筛选下拉框漏译            |
   | - 优化地图高缩放级别瓦片渲染稳定性                      |
   |                                                       |
   | 是否下载并更新？                                       |
   |                                                       |
   |   [立即自动下载更新]     [前往 GitHub 网页]    [暂不更新] |
   +-------------------------------------------------------+
   ```
4. **安全下载与无缝替换**：
   - 点击【立即自动下载更新】：通过 `WebClient.DownloadFileAsync` 下载最新 `CMO_Chinese_Setup.exe` 至 `%TEMP%\CMO_Setup_New.exe`。
   - 下载完成校验大小 > 1MB。
   - 自动启动新安装器进程并退出当前程序：
     `Process.Start(tempExePath, "/updated"); Application.Exit();`

---

### 4. 游戏内引擎 (`CMOLocalizer.dll`) 发现与提醒机制

为了不打扰玩家正在进行的作战推演，设计以下规范：
1. **时机控制**：仅在主窗体加载完成 15 秒后触发一次检查。推演中不再重复请求。
2. **检查更新菜单入口**：
   在顶部菜单栏【帮助 (Help)】子菜单底部动态追加：
   `检查汉化新版本 (Check for Chinese Patch Updates)...`
   玩家可随时主动点击手动检查。
3. **弹窗确认**：
   后台探测到新版本后，在 UI 线程通过 `MessageBox.Show` 或内嵌窗口提示：
   `检测到《现代行动：指挥》汉化补丁有新版本 (v4.2.0) 发布！\n是否打开下载网页进行查看？`
   提供【是】与【否】选项。点击【是】调用默认浏览器打开 GitHub Release 页面；点击【否】本局游戏不再提示。

---

## 四、实施计划

1. **阶段一**：在 `CMOLocalizer.cs` 增加异步版本探测服务与【帮助】菜单更新入口。
2. **阶段二**：在 `CMO_Chinese_Setup.cs` 增加图形化新版本横幅与确认弹窗。
3. **阶段三**：配置 GitHub Actions Release 自动打包，使得每次新 release 发布时自动产生符合解析规范的资产文件。
