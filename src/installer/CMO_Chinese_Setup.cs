using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using Microsoft.Win32;

namespace CMOLocalizerInstaller
{
    public class InstallerForm : Form
    {
        private TextBox txtPath;
        private Button btnBrowse;
        private Button btnInstall;
        private Button btnUninstall;
        private Button btnDonate;
        private TextBox txtLog;
        private Label lblStatus;

        public InstallerForm(string initialPath)
        {
            this.Text = "Command: Modern Operations (CMO) 完整中文汉化一键安装工具 v4.1.0";
            this.Size = new Size(680, 525);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular);

            Label lblTitle = new Label();
            lblTitle.Text = "Command: Modern Operations 完整中文汉化补丁";
            lblTitle.Font = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(20, 60, 120);
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(20, 18);
            this.Controls.Add(lblTitle);

            Label lblSub = new Label();
            lblSub.Text = "采用 CLR AppDomainManager 零侵入内存级双重本地化引擎 | 不修改原版 EXE | 支持一键还原";
            lblSub.ForeColor = Color.DimGray;
            lblSub.AutoSize = true;
            lblSub.Location = new Point(22, 50);
            this.Controls.Add(lblSub);

            Label lblPath = new Label();
            lblPath.Text = "CMO 游戏根目录:";
            lblPath.AutoSize = true;
            lblPath.Location = new Point(22, 90);
            this.Controls.Add(lblPath);

            txtPath = new TextBox();
            txtPath.Location = new Point(22, 115);
            txtPath.Size = new Size(520, 26);
            txtPath.Text = initialPath;
            this.Controls.Add(txtPath);

            btnBrowse = new Button();
            btnBrowse.Text = "浏览目录...";
            btnBrowse.Location = new Point(552, 113);
            btnBrowse.Size = new Size(95, 28);
            btnBrowse.Click += new EventHandler(OnBrowseClick);
            this.Controls.Add(btnBrowse);

            lblStatus = new Label();
            lblStatus.Text = "当前状态: 正在检测...";
            lblStatus.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(22, 155);
            this.Controls.Add(lblStatus);

            btnInstall = new Button();
            btnInstall.Text = "一键安装中文汉化补丁";
            btnInstall.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            btnInstall.BackColor = Color.FromArgb(30, 110, 200);
            btnInstall.ForeColor = Color.White;
            btnInstall.FlatStyle = FlatStyle.Flat;
            btnInstall.Location = new Point(22, 190);
            btnInstall.Size = new Size(300, 46);
            btnInstall.Click += new EventHandler(OnInstallClick);
            this.Controls.Add(btnInstall);

            btnUninstall = new Button();
            btnUninstall.Text = "一键还原原版英文";
            btnUninstall.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Regular);
            btnUninstall.Location = new Point(347, 190);
            btnUninstall.Size = new Size(300, 46);
            btnUninstall.Click += new EventHandler(OnUninstallClick);
            this.Controls.Add(btnUninstall);

            btnDonate = new Button();
            btnDonate.Text = "☕ 觉得好用？请作者喝杯咖啡 / 可乐";
            btnDonate.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
            btnDonate.BackColor = Color.FromArgb(254, 248, 237);
            btnDonate.ForeColor = Color.FromArgb(180, 95, 20);
            btnDonate.FlatStyle = FlatStyle.Flat;
            btnDonate.FlatAppearance.BorderColor = Color.FromArgb(235, 195, 135);
            btnDonate.Location = new Point(22, 246);
            btnDonate.Size = new Size(625, 34);
            btnDonate.Cursor = Cursors.Hand;
            btnDonate.Click += new EventHandler(OnDonateClick);
            this.Controls.Add(btnDonate);

            txtLog = new TextBox();
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.BackColor = Color.FromArgb(245, 247, 250);
            txtLog.Location = new Point(22, 290);
            txtLog.Size = new Size(625, 175);
            this.Controls.Add(txtLog);

            Log("欢迎使用 Command: Modern Operations (CMO) 完整中文汉化安装程序。");
            Log("本汉化涵盖：主界面、菜单、作战条令(RoE)、任务编辑、航空作业、全部45个教程及全球4608处地名。");
            RefreshStatus();
        }

        private void OnDonateClick(object sender, EventArgs e)
        {
            using (DonateForm df = new DonateForm())
            {
                df.ShowDialog(this);
            }
        }

        private void Log(string msg)
        {
            txtLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + "\r\n");
        }

        private void OnBrowseClick(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "请选择 Command: Modern Operations 游戏安装根目录 (包含 Command.exe)";
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtPath.Text = fbd.SelectedPath;
                    RefreshStatus();
                }
            }
        }

        private void RefreshStatus()
        {
            string gp = txtPath.Text.Trim();
            if (string.IsNullOrEmpty(gp) || !File.Exists(Path.Combine(gp, "Command.exe")))
            {
                lblStatus.Text = "当前状态: 未找到 Command.exe，请点击[浏览目录]选择正确的游戏根目录";
                lblStatus.ForeColor = Color.Red;
                return;
            }

            bool isInstalled = InstallerCore.CheckInstalled(gp);
            if (isInstalled)
            {
                lblStatus.Text = "当前状态: 已安装完整中文汉化补丁 (可直接启动游戏)";
                lblStatus.ForeColor = Color.Green;
            }
            else
            {
                lblStatus.Text = "当前状态: 原版英文环境 (未安装汉化补丁)";
                lblStatus.ForeColor = Color.DarkOrange;
            }
        }

        private void OnInstallClick(object sender, EventArgs e)
        {
            string gp = txtPath.Text.Trim();
            string err;
            if (InstallerCore.Install(gp, out err))
            {
                Log("Successfully Installed! 已成功复制 CMOLocalizer.dll 与 CMOLocalizer.json。");
                Log("已自动备份原版配置为 Command.exe.config.cmo_bak 并启用本地化引擎。");
                RefreshStatus();
                MessageBox.Show("CMO 完整中文汉化补丁安装成功！\n现在直接从 Steam 或运行 Command.exe 启动游戏即可显示中文。", "安装成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                Log("[错误] 安装失败: " + err);
                MessageBox.Show("安装失败:\n" + err, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnUninstallClick(object sender, EventArgs e)
        {
            string gp = txtPath.Text.Trim();
            string err;
            if (InstallerCore.Uninstall(gp, out err))
            {
                Log("已成功卸载中文汉化补丁并恢复原版 Command.exe.config 配置。");
                RefreshStatus();
                MessageBox.Show("已无损还原为原版英文环境！", "还原成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                Log("[错误] 还原失败: " + err);
                MessageBox.Show("还原失败:\n" + err, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public class DonateForm : Form
    {
        public DonateForm()
        {
            this.Text = "☕ 请作者喝杯咖啡 / 可乐 - 感谢您的支持与鼓励";
            this.Size = new Size(580, 525);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
            this.BackColor = Color.White;

            Label lblTitle = new Label();
            lblTitle.Text = "❤️ 感谢支持 CMO 中文本地化项目";
            lblTitle.Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(180, 85, 20);
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(20, 16);
            this.Controls.Add(lblTitle);

            Label lblDesc = new Label();
            lblDesc.Text = "本汉化补丁完全免费开源，由爱好者利用业余时间持续维护更新。\n如果您觉得汉化体验还不错，欢迎扫码请作者喝杯咖啡或可乐，感谢您的支持！";
            lblDesc.ForeColor = Color.FromArgb(80, 80, 80);
            lblDesc.AutoSize = true;
            lblDesc.Location = new Point(22, 48);
            this.Controls.Add(lblDesc);

            // 微信支付卡片
            Panel pnlWeChat = new Panel();
            pnlWeChat.Location = new Point(25, 100);
            pnlWeChat.Size = new Size(245, 335);
            pnlWeChat.BorderStyle = BorderStyle.FixedSingle;
            pnlWeChat.BackColor = Color.FromArgb(250, 254, 250);
            this.Controls.Add(pnlWeChat);

            Label lblWeChat = new Label();
            lblWeChat.Text = "微信支付";
            lblWeChat.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            lblWeChat.ForeColor = Color.FromArgb(7, 193, 96);
            lblWeChat.Dock = DockStyle.Top;
            lblWeChat.Height = 32;
            lblWeChat.TextAlign = ContentAlignment.MiddleCenter;
            pnlWeChat.Controls.Add(lblWeChat);

            PictureBox pbWeChat = new PictureBox();
            pbWeChat.Dock = DockStyle.Fill;
            pbWeChat.SizeMode = PictureBoxSizeMode.Zoom;
            pbWeChat.Image = LoadEmbeddedImage("QR_WeChat");
            pnlWeChat.Controls.Add(pbWeChat);
            pbWeChat.BringToFront();

            // 支付宝卡片
            Panel pnlAlipay = new Panel();
            pnlAlipay.Location = new Point(295, 100);
            pnlAlipay.Size = new Size(245, 335);
            pnlAlipay.BorderStyle = BorderStyle.FixedSingle;
            pnlAlipay.BackColor = Color.FromArgb(250, 252, 255);
            this.Controls.Add(pnlAlipay);

            Label lblAlipay = new Label();
            lblAlipay.Text = "支付宝";
            lblAlipay.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            lblAlipay.ForeColor = Color.FromArgb(22, 119, 255);
            lblAlipay.Dock = DockStyle.Top;
            lblAlipay.Height = 32;
            lblAlipay.TextAlign = ContentAlignment.MiddleCenter;
            pnlAlipay.Controls.Add(lblAlipay);

            PictureBox pbAlipay = new PictureBox();
            pbAlipay.Dock = DockStyle.Fill;
            pbAlipay.SizeMode = PictureBoxSizeMode.Zoom;
            pbAlipay.Image = LoadEmbeddedImage("QR_Alipay");
            pnlAlipay.Controls.Add(pbAlipay);
            pbAlipay.BringToFront();

            Button btnClose = new Button();
            btnClose.Text = "关 闭";
            btnClose.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Regular);
            btnClose.Location = new Point(230, 448);
            btnClose.Size = new Size(110, 32);
            btnClose.Click += (s, e) => this.Close();
            this.Controls.Add(btnClose);
        }

        private Image LoadEmbeddedImage(string resName)
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                string[] allRes = asm.GetManifestResourceNames();
                foreach (string name in allRes)
                {
                    if (name.EndsWith(resName, StringComparison.OrdinalIgnoreCase))
                    {
                        using (Stream stream = asm.GetManifestResourceNames() != null ? asm.GetManifestResourceStream(name) : null)
                        {
                            if (stream != null)
                            {
                                using (Image tmp = Image.FromStream(stream))
                                {
                                    return new Bitmap(tmp);
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }
    }

    public static class InstallerCore
    {
        public static string DetectGamePath()
        {
            // 1. 如果安装程序直接放在游戏根目录下运行
            string cur = AppDomain.CurrentDomain.BaseDirectory;
            if (File.Exists(Path.Combine(cur, "Command.exe"))) return cur;

            // 2. 通过注册表检测 Steam 游戏安装路径 (支持 32/64 位及 CurrentUser)
            string[] uninstallKeys = new string[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1076160",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1076160"
            };

            foreach (string sub in uninstallKeys)
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(sub))
                    {
                        if (key != null)
                        {
                            object loc = key.GetValue("InstallLocation");
                            if (loc != null && File.Exists(Path.Combine(loc.ToString(), "Command.exe")))
                                return loc.ToString();
                        }
                    }
                }
                catch { }

                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(sub))
                    {
                        if (key != null)
                        {
                            object loc = key.GetValue("InstallLocation");
                            if (loc != null && File.Exists(Path.Combine(loc.ToString(), "Command.exe")))
                                return loc.ToString();
                        }
                    }
                }
                catch { }
            }

            // 3. 读取 Steam 主目录并解析所有 Steam 库文件夹 (libraryfolders.vdf)
            string steamMainDir = null;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (key != null)
                    {
                        object sp = key.GetValue("SteamPath");
                        if (sp != null) steamMainDir = sp.ToString().Replace('/', '\\');
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(steamMainDir))
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                    {
                        if (key != null)
                        {
                            object ip = key.GetValue("InstallPath");
                            if (ip != null) steamMainDir = ip.ToString().Replace('/', '\\');
                        }
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(steamMainDir) && Directory.Exists(steamMainDir))
            {
                // 检查 Steam 默认主库
                string mainCmo = Path.Combine(steamMainDir, @"steamapps\common\Command - Modern Operations");
                if (File.Exists(Path.Combine(mainCmo, "Command.exe"))) return mainCmo;

                // 解析 libraryfolders.vdf 获取用户的所有自定义多盘符库
                string vdfPath = Path.Combine(steamMainDir, @"steamapps\libraryfolders.vdf");
                if (File.Exists(vdfPath))
                {
                    try
                    {
                        string vdfText = File.ReadAllText(vdfPath);
                        var matches = System.Text.RegularExpressions.Regex.Matches(vdfText, "\"path\"\\s+\"([^\"]+)\"");
                        foreach (System.Text.RegularExpressions.Match m in matches)
                        {
                            if (m.Success && m.Groups.Count > 1)
                            {
                                string libPath = m.Groups[1].Value.Replace("\\\\", "\\");
                                string cmoInLib = Path.Combine(libPath, @"steamapps\common\Command - Modern Operations");
                                if (File.Exists(Path.Combine(cmoInLib, "Command.exe"))) return cmoInLib;
                            }
                        }
                    }
                    catch { }
                }
            }

            // 4. 自动全盘驱动器遍历扫描 (涵盖常见安装盘符与自建库)
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                string[] checkRel = new string[]
                {
                    @"soft\steam\steamapps\common\Command - Modern Operations",
                    @"Steam\steamapps\common\Command - Modern Operations",
                    @"SteamLibrary\steamapps\common\Command - Modern Operations",
                    @"Program Files (x86)\Steam\steamapps\common\Command - Modern Operations",
                    @"Program Files\Steam\steamapps\common\Command - Modern Operations",
                    @"Games\Steam\steamapps\common\Command - Modern Operations",
                    @"Games\SteamLibrary\steamapps\common\Command - Modern Operations"
                };

                foreach (DriveInfo d in drives)
                {
                    if (!d.IsReady) continue;
                    foreach (string rel in checkRel)
                    {
                        string p = Path.Combine(d.RootDirectory.FullName, rel);
                        if (File.Exists(Path.Combine(p, "Command.exe"))) return p;
                    }
                }
            }
            catch { }

            return "";
        }

        public static bool CheckInstalled(string gamePath)
        {
            if (string.IsNullOrEmpty(gamePath)) return false;
            string cfgPath = Path.Combine(gamePath, "Command.exe.config");
            string dllDest = Path.Combine(gamePath, "CMOLocalizer.dll");
            string jsonDest = Path.Combine(gamePath, "CMOLocalizer.json");
            if (!File.Exists(cfgPath) || !File.Exists(dllDest) || !File.Exists(jsonDest)) return false;
            try
            {
                string txt = File.ReadAllText(cfgPath);
                return txt.Contains("CMOLocalizer.LocalizerDomainManager");
            }
            catch
            {
                return false;
            }
        }

        public static bool Install(string gamePath, out string errorMsg)
        {
            errorMsg = "";
            if (string.IsNullOrEmpty(gamePath) || !File.Exists(Path.Combine(gamePath, "Command.exe")))
            {
                errorMsg = "未在目标路径找到 Command.exe，请确认游戏目录是否正确。";
                return false;
            }

            string curDir = AppDomain.CurrentDomain.BaseDirectory;
            string dllSrc = Path.Combine(curDir, "CMOLocalizer.dll");
            string jsonSrc = Path.Combine(curDir, "CMOLocalizer.json");

            if (!File.Exists(dllSrc))
            {
                dllSrc = Path.Combine(curDir, "patch_files", "CMOLocalizer.dll");
                jsonSrc = Path.Combine(curDir, "patch_files", "CMOLocalizer.json");
            }

            try
            {
                string dllDest = Path.Combine(gamePath, "CMOLocalizer.dll");
                string jsonDest = Path.Combine(gamePath, "CMOLocalizer.json");

                if (File.Exists(dllSrc))
                {
                    if (!string.Equals(Path.GetFullPath(dllSrc), Path.GetFullPath(dllDest), StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(dllSrc, dllDest, true);
                    }
                }
                else
                {
                    if (!ExtractEmbeddedResource("CMOLocalizer.dll", dllDest))
                    {
                        errorMsg = "安装包内未找到 CMOLocalizer.dll 资源文件！";
                        return false;
                    }
                }

                if (File.Exists(jsonSrc))
                {
                    if (!string.Equals(Path.GetFullPath(jsonSrc), Path.GetFullPath(jsonDest), StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(jsonSrc, jsonDest, true);
                    }
                }
                else
                {
                    if (!ExtractEmbeddedResource("CMOLocalizer.json", jsonDest))
                    {
                        errorMsg = "安装包内未找到 CMOLocalizer.json 资源文件！";
                        return false;
                    }
                }

                string cfgPath = Path.Combine(gamePath, "Command.exe.config");
                string bakPath = Path.Combine(gamePath, "Command.exe.config.cmo_bak");
                if (!File.Exists(bakPath) && File.Exists(cfgPath))
                {
                    File.Copy(cfgPath, bakPath, true);
                }

                XmlDocument doc = new XmlDocument();
                doc.Load(cfgPath);

                XmlNode runtime = doc.SelectSingleNode("//configuration/runtime");
                if (runtime == null)
                {
                    XmlNode config = doc.SelectSingleNode("//configuration");
                    runtime = doc.CreateElement("runtime");
                    config.AppendChild(runtime);
                }

                XmlNode typeNode = runtime.SelectSingleNode("appDomainManagerType");
                if (typeNode == null)
                {
                    typeNode = doc.CreateElement("appDomainManagerType");
                    XmlAttribute attr = doc.CreateAttribute("value");
                    attr.Value = "CMOLocalizer.LocalizerDomainManager";
                    typeNode.Attributes.Append(attr);
                    runtime.AppendChild(typeNode);
                }
                else
                {
                    typeNode.Attributes["value"].Value = "CMOLocalizer.LocalizerDomainManager";
                }

                XmlNode asmNode = runtime.SelectSingleNode("appDomainManagerAssembly");
                if (asmNode == null)
                {
                    asmNode = doc.CreateElement("appDomainManagerAssembly");
                    XmlAttribute attr = doc.CreateAttribute("value");
                    attr.Value = "CMOLocalizer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
                    asmNode.Attributes.Append(attr);
                    runtime.AppendChild(asmNode);
                }
                else
                {
                    asmNode.Attributes["value"].Value = "CMOLocalizer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
                }

                doc.Save(cfgPath);

                // 释放汉化版武器数据库 HTML 模板 (DB/Templates)
                string tplDir = Path.Combine(gamePath, "DB", "Templates");
                string tplBakDir = Path.Combine(gamePath, "DB", "Templates_Original_Backup");
                if (Directory.Exists(tplDir))
                {
                    if (!Directory.Exists(tplBakDir))
                    {
                        Directory.CreateDirectory(tplBakDir);
                        foreach (string f in Directory.GetFiles(tplDir, "*.html"))
                        {
                            File.Copy(f, Path.Combine(tplBakDir, Path.GetFileName(f)), true);
                        }
                    }
                    string[] tplFiles = new string[] {
                        "Aircraft.html", "Facility.html", "GroundUnit.html", "Platform.html",
                        "Satellite.html", "Ship.html", "Submarine.html", "Weapon.html"
                    };
                    foreach (string tf in tplFiles)
                    {
                        ExtractEmbeddedResource("TPL_" + tf, Path.Combine(tplDir, tf));
                    }
                }

                // 自动扫描并汉化 Scenarios 下的 HTML 战役简报 (带 .orig 备份)
                LocalizeScenarioBriefings(Path.Combine(gamePath, "Scenarios"));

                // 自动汉化 World Wind 标注图层瓦片 (WW/Cache/Stamen Terrain - Labels)
                LocalizeMapLabelTiles(gamePath);

                return true;
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                return false;
            }
        }


        private static void LocalizeScenarioBriefings(string scenDir)
        {
            if (!Directory.Exists(scenDir)) return;
            try
            {
                string[] htmls = Directory.GetFiles(scenDir, "*.htm*", SearchOption.AllDirectories);
                foreach (string h in htmls)
                {
                    if (h.EndsWith(".orig", StringComparison.OrdinalIgnoreCase)) continue;
                    string orig = h + ".orig";
                    if (!File.Exists(orig))
                    {
                        File.Copy(h, orig, true);
                    }
                    string text = File.ReadAllText(orig, Encoding.UTF8);
                    string trans = text
                        .Replace("<h2>Briefing</h2>", "<h2>作战任务简报 (Briefing)</h2>")
                        .Replace("<h1>Introduction</h1>", "<h1>战役背景导言 (Introduction)</h1>")
                        .Replace("<h3>Situation</h3>", "<h3>战场态势 (Situation)</h3>")
                        .Replace("<h3>Timeline:</h3>", "<h3>行动时间线:</h3>")
                        .Replace("<h3>Threat:</h3>", "<h3>敌情威胁评估:</h3>")
                        .Replace("<h3>Assets:</h3>", "<h3>我方兵力配属:</h3>")
                        .Replace("<h3>Task:</h3>", "<h3>作战任务指标:</h3>")
                        .Replace("<h3>Execution</h3>", "<h3>作战实施决心 (Execution)</h3>")
                        .Replace("<h3>Friendly Forces</h3>", "<h3>我方友邻部队 (Friendly Forces)</h3>")
                        .Replace("<h3>Scenario Notes:</h3>", "<h3>想定设计说明:</h3>")
                        .Replace("<B>Original Author:</B>", "<B>想定原作者:</B>")
                        .Replace("<B>Location:</B>", "<B>战区位置:</B>")
                        .Replace("<B>Playable Sides:</B>", "<B>可扮演参战方:</B>")
                        .Replace("You are NOT, REPEAT NOT authorized to use nuclear weapons.", "你严禁（重复：严禁）动用任何核武器。");
                    if (trans != text)
                    {
                        File.WriteAllText(h, trans, Encoding.UTF8);
                    }
                }
            }
            catch { }
        }

        private static void RestoreScenarioBriefings(string scenDir)
        {
            if (!Directory.Exists(scenDir)) return;
            try
            {
                string[] origs = Directory.GetFiles(scenDir, "*.orig", SearchOption.AllDirectories);
                foreach (string orig in origs)
                {
                    string target = orig.Substring(0, orig.Length - 5);
                    File.Copy(orig, target, true);
                    File.Delete(orig);
                }
            }
            catch { }
        }

        private class TilePlace
        {
            public string Name;
            public double Lat;
            public double Lon;
            public int Level;
        }

        private static void LocalizeMapLabelTiles(string gamePath)
        {
            try
            {
                string cacheDir = Path.Combine(gamePath, @"WW\Cache\Stamen Terrain - Labels");
                string backupDir = Path.Combine(gamePath, @"WW\Cache\Stamen Terrain - Labels.orig");
                if (!Directory.Exists(cacheDir)) return;

                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                    foreach (string f in Directory.GetFiles(cacheDir, "*", SearchOption.AllDirectories))
                    {
                        string rel = f.Substring(cacheDir.Length).TrimStart('\\', '/');
                        string dest = Path.Combine(backupDir, rel);
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        File.Copy(f, dest, true);
                    }
                }

                List<TilePlace> list = new List<TilePlace>();
                string synWwp = Path.Combine(gamePath, @"WW\PlacenameSets\Synoptic\0000_0000.wwp");
                string citWwp = Path.Combine(gamePath, @"WW\PlacenameSets\Cities\0000_0000.wwp");

                Action<string, int> loadWwp = (path, lvl) =>
                {
                    if (!File.Exists(path)) return;
                    try
                    {
                        using (BinaryReader br = new BinaryReader(File.OpenRead(path), Encoding.UTF8))
                        {
                            int count = br.ReadInt32();
                            for (int i = 0; i < count; i++)
                            {
                                string name = br.ReadString();
                                float lat = br.ReadSingle();
                                float lon = br.ReadSingle();
                                br.ReadUInt32();
                                list.Add(new TilePlace { Name = name, Lat = lat, Lon = lon, Level = lvl });
                            }
                        }
                    }
                    catch { }
                };

                loadWwp(synWwp, 0);
                loadWwp(citWwp, 1);
                if (list.Count == 0) return;

                string[] pngs = Directory.GetFiles(cacheDir, "*.png", SearchOption.AllDirectories);
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;

                Parallel.ForEach(pngs, pngPath =>
                {
                    try
                    {
                        string norm = pngPath.Replace('\\', '/');
                        string[] parts = norm.Split('/');
                        int z = int.Parse(parts[parts.Length - 3]);
                        int x = int.Parse(parts[parts.Length - 2]);
                        string fName = parts[parts.Length - 1];
                        int y = int.Parse(fName.Substring(0, fName.IndexOf('.')));

                        double n = Math.Pow(2.0, z);
                        double minLon = x / n * 360.0 - 180.0;
                        double maxLon = (x + 1) / n * 360.0 - 180.0;
                        double maxLat = Math.Atan(Math.Sinh(Math.PI * (1.0 - 2.0 * y / n))) * 180.0 / Math.PI;
                        double minLat = Math.Atan(Math.Sinh(Math.PI * (1.0 - 2.0 * (y + 1) / n))) * 180.0 / Math.PI;

                        List<TilePlace> inTile = new List<TilePlace>();
                        foreach (var p in list)
                        {
                            if (p.Lon >= minLon && p.Lon <= maxLon && p.Lat >= minLat && p.Lat <= maxLat)
                            {
                                if (z <= 5) { if (p.Level == 0) inTile.Add(p); }
                                else if (z <= 7) { inTile.Add(p); }
                                else { if (p.Level == 1) inTile.Add(p); }
                            }
                        }

                        using (Bitmap bmp = new Bitmap(256, 256, PixelFormat.Format32bppArgb))
                        {
                            using (Graphics g = Graphics.FromImage(bmp))
                            {
                                g.Clear(Color.Transparent);
                                if (inTile.Count > 0)
                                {
                                    g.SmoothingMode = SmoothingMode.AntiAlias;
                                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                                    inTile.Sort((a, b) => a.Level.CompareTo(b.Level));

                                    List<RectangleF> occupied = new List<RectangleF>();
                                    foreach (var p in inTile)
                                    {
                                        double xExact = (p.Lon + 180.0) / 360.0 * n;
                                        double latRad = p.Lat * Math.PI / 180.0;
                                        double yExact = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n;
                                        float rawPx = (float)((xExact - x) * 256.0);
                                        float rawPy = (float)((yExact - y) * 256.0);

                                        float fontSize = (p.Level == 0) ? (z <= 3 ? 9.5f : (z <= 4 ? 10.5f : 11.5f)) : (z <= 7 ? 9.0f : 8.5f);
                                        FontStyle fStyle = (p.Level == 0) ? FontStyle.Bold : FontStyle.Regular;

                                        using (Font f = new Font("Microsoft YaHei", fontSize, fStyle))
                                        {
                                            SizeF sz = g.MeasureString(p.Name, f);
                                            float halfW = sz.Width * 0.5f;
                                            float halfH = sz.Height * 0.5f;
                                            float px = Math.Max(halfW + 3f, Math.Min(256f - halfW - 3f, rawPx));
                                            float py = Math.Max(halfH + 3f, Math.Min(256f - halfH - 3f, rawPy));

                                            RectangleF bbox = new RectangleF(px - halfW - 2f, py - halfH - 2f, sz.Width + 4f, sz.Height + 4f);
                                            bool collides = false;
                                            foreach (var occ in occupied)
                                            {
                                                if (bbox.IntersectsWith(occ)) { collides = true; break; }
                                            }
                                            if (collides) continue;
                                            occupied.Add(bbox);

                                            using (GraphicsPath gp = new GraphicsPath())
                                            {
                                                gp.AddString(p.Name, new FontFamily("Microsoft YaHei"), (int)fStyle, fontSize, new PointF(px, py), sf);
                                                Color penC = (p.Level == 0) ? Color.FromArgb(220, 15, 15, 15) : Color.FromArgb(200, 25, 25, 25);
                                                float penW = (p.Level == 0) ? 3.0f : 2.2f;
                                                using (Pen pen = new Pen(penC, penW))
                                                {
                                                    pen.LineJoin = LineJoin.Round;
                                                    g.DrawPath(pen, gp);
                                                }
                                                Color txtC = (p.Level == 0) ? Color.FromArgb(255, 255, 255) : Color.FromArgb(240, 240, 240);
                                                using (SolidBrush br = new SolidBrush(txtC))
                                                {
                                                    g.FillPath(br, gp);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            bmp.Save(pngPath, ImageFormat.Png);
                        }
                    }
                    catch { }
                });
            }
            catch { }
        }

        private static void RestoreMapLabelTiles(string gamePath)
        {
            try
            {
                string cacheDir = Path.Combine(gamePath, @"WW\Cache\Stamen Terrain - Labels");
                string backupDir = Path.Combine(gamePath, @"WW\Cache\Stamen Terrain - Labels.orig");
                if (Directory.Exists(backupDir))
                {
                    if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
                    Directory.Move(backupDir, cacheDir);
                }
            }
            catch { }
        }

        private static bool ExtractEmbeddedResource(string resName, string destPath)
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                string[] allRes = asm.GetManifestResourceNames();
                string match = null;
                foreach (string name in allRes)
                {
                    if (name.EndsWith(resName, StringComparison.OrdinalIgnoreCase))
                    {
                        match = name;
                        break;
                    }
                }
                if (match == null) return false;

                using (Stream stream = asm.GetManifestResourceStream(match))
                {
                    if (stream == null) return false;
                    using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(fs);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool Uninstall(string gamePath, out string errorMsg)
        {
            errorMsg = "";
            if (string.IsNullOrEmpty(gamePath) || !File.Exists(Path.Combine(gamePath, "Command.exe")))
            {
                errorMsg = "未在目标路径找到 Command.exe。";
                return false;
            }

            try
            {
                string cfgPath = Path.Combine(gamePath, "Command.exe.config");
                string bakPath = Path.Combine(gamePath, "Command.exe.config.cmo_bak");

                if (File.Exists(bakPath))
                {
                    File.Copy(bakPath, cfgPath, true);
                    File.Delete(bakPath);
                }
                else if (File.Exists(cfgPath))
                {
                    XmlDocument doc = new XmlDocument();
                    doc.Load(cfgPath);
                    XmlNode runtime = doc.SelectSingleNode("//configuration/runtime");
                    if (runtime != null)
                    {
                        XmlNode typeNode = runtime.SelectSingleNode("appDomainManagerType");
                        if (typeNode != null) runtime.RemoveChild(typeNode);
                        XmlNode asmNode = runtime.SelectSingleNode("appDomainManagerAssembly");
                        if (asmNode != null) runtime.RemoveChild(asmNode);
                        doc.Save(cfgPath);
                    }
                }

                string dllDest = Path.Combine(gamePath, "CMOLocalizer.dll");
                string jsonDest = Path.Combine(gamePath, "CMOLocalizer.json");
                string curDir = AppDomain.CurrentDomain.BaseDirectory;
                // Only delete if installer is not running from the game folder itself, or leave backup copy
                if (!string.Equals(Path.GetFullPath(curDir).TrimEnd('\\'), Path.GetFullPath(gamePath).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(dllDest)) File.Delete(dllDest);
                    if (File.Exists(jsonDest)) File.Delete(jsonDest);
                }
                // 还原武器数据库 HTML 模板
                string tplDir = Path.Combine(gamePath, "DB", "Templates");
                string tplBakDir = Path.Combine(gamePath, "DB", "Templates_Original_Backup");
                if (Directory.Exists(tplBakDir) && Directory.Exists(tplDir))
                {
                    foreach (string f in Directory.GetFiles(tplBakDir, "*.html"))
                    {
                        File.Copy(f, Path.Combine(tplDir, Path.GetFileName(f)), true);
                    }
                }

                // 还原 Scenarios 下的 .orig 简报备份
                RestoreScenarioBriefings(Path.Combine(gamePath, "Scenarios"));

                // 还原 World Wind 标注图层瓦片
                RestoreMapLabelTiles(gamePath);

                return true;
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                return false;
            }
        }

        [STAThread]
        static int Main(string[] args)
        {
            string gp = DetectGamePath();

            if (args != null && args.Length > 0)
            {
                string cmd = args[0].ToLowerInvariant();
                if (args.Length > 1 && Directory.Exists(args[1]))
                {
                    gp = args[1];
                }
                string err;
                if (cmd == "--install" || cmd == "/install")
                {
                    bool ok = Install(gp, out err);
                    Console.WriteLine(ok ? "[SUCCESS] Installed to " + gp : "[ERROR] " + err);
                    return ok ? 0 : 1;
                }
                else if (cmd == "--uninstall" || cmd == "/uninstall")
                {
                    bool ok = Uninstall(gp, out err);
                    Console.WriteLine(ok ? "[SUCCESS] Uninstalled from " + gp : "[ERROR] " + err);
                    return ok ? 0 : 1;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm(gp));
            return 0;
        }
    }
}
