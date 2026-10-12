# CMO_CN

《Command: Modern Operations（现代行动：指挥）》完整汉化补丁与本地化工程。

基于 .NET `AppDomainManager` 机制加载，不修改游戏 `Command.exe` 主程序文件。

---

## 包含内容

- **界面与菜单**：主界面、顶部工具栏、全部 10 大主菜单及多级子菜单、右键菜单。
- **业务窗口**：航空作业（Air Ops）、就绪飞机（Ready Aircraft）、作战条令（RoE）、任务编辑器、作战序列（OoB）、推演选项等。
- **装备数据库**：飞机、舰船、潜艇、地面设施、武器、传感器等 8 大类别的常规参数、战术能力、工作频段及武器挂载包线。
- **教程与战情消息**：涵盖全部 45 个官方教程（飞行、多域打击、对地打击、水面、潜艇、反潜、电子战、空中加油及马克大叔教程），教程操作指引中的按键（F1~F12 等）与按钮名称已对齐。
- **地图地名**：全球 321 处国家/海域及 4,287 座城市的地名汉化。

---

## 实机截图

### 教程战情消息与就绪飞机窗口
![教程消息与就绪飞机](evidence/live/tutorials/17_message_2_and_ready_aircraft_side_by_side.png)

### 航空作业 (Air Ops) 窗口
![航空作业](evidence/live/tutorials/08_air_ops_ramat_david_fixed.png)

### 数据库查看器（外挂武器与挂载包线）
![数据库查看器](evidence/live/tutorials/07_db_viewer_weapons_translated.png)

### 战场主界面
![战场主界面](evidence/live/tutorials/10_right_panel_flying_frame1.png)

---

## 安装与使用

### 1. 使用安装器（推荐）

下载发布包中的 `CMO_Chinese_Setup.exe`：

- **图形界面**：双击运行，选择游戏安装目录，点击「一键安装汉化」。如需恢复英文原版，点击「还原英文原版」。
- **命令行静默安装**：
  ```cmd
  CMO_Chinese_Setup.exe /install "游戏目录路径"
  ```
- **命令行静默卸载**：
  ```cmd
  CMO_Chinese_Setup.exe /uninstall "游戏目录路径"
  ```

### 2. 手动安装

将发布包解压后复制到游戏根目录（覆盖同名文件前请先备份原文件）：
- `CMOLocalizer.dll` -> 游戏根目录
- `CMOLocalizer.json` -> 游戏根目录
- `Command.exe.config` -> 游戏根目录（添加了 AppDomainManager 引导配置）
- `DB/Templates/*.html` -> 覆盖到 `DB/Templates/` 目录
- `Symbols/Menu/*.png` -> 覆盖到 `Symbols/Menu/` 目录

---

## 从源码构建

需要 Windows 环境，并安装 .NET Framework 4.8 与 Python 3.10+。

```bash
# 1. 生成词库
python scripts/build_db_enums.py
python scripts/build_dict.py

# 2. 编译引擎与安装器
bash scripts/build.sh

# 3. 运行校验门禁
python scripts/verify_all.py
```

编译产物位于 `build/` 目录下：
- `build/CMOLocalizer.dll`
- `build/CMO_Chinese_Setup.exe`

---

## 目录结构

```text
├── src/
│   ├── engine/       # 本地化引擎源码 (CMOLocalizer.cs)
│   └── installer/    # 一键安装器源码 (CMO_Chinese_Setup.cs)
├── dict/
│   ├── CMOLocalizer.json     # 运行时主词库
│   ├── glossary.json         # 核心军语术语表
│   ├── brief_translations.json # 简报与教程译文库
│   └── src/                  # 各模块源词库 (菜单/航空作业/数据库枚举等)
├── assets/           # 汉化后的数据库 HTML 模板
├── scripts/          # 构建、打包、校验与部署脚本
├── docs/             # 术语表、实测报告与变更记录
└── evidence/live/    # 实机测试截图
```

---

## 说明

本项目仅包含汉化补丁相关源码、词库与资源，不包含游戏本体或任何商业文件。请在支持正版游戏的前提下使用。
