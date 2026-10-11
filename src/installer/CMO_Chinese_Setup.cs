using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
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
        private TextBox txtLog;
        private Label lblStatus;

        public InstallerForm(string initialPath)
        {
            this.Text = "Command: Modern Operations (CMO) 专家级中文汉化一键安装工具 v4.0.0";
            this.Size = new Size(680, 480);
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
            lblSub.Text = "采用 CLR AppDomainManager 无损内存级双重本地化引擎 | 不修改原版 EXE | 支持随时一键还原";
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
            btnInstall.Size = new Size(300, 48);
            btnInstall.Click += new EventHandler(OnInstallClick);
            this.Controls.Add(btnInstall);

            btnUninstall = new Button();
            btnUninstall.Text = "一键还原原版英文";
            btnUninstall.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Regular);
            btnUninstall.Location = new Point(347, 190);
            btnUninstall.Size = new Size(300, 48);
            btnUninstall.Click += new EventHandler(OnUninstallClick);
            this.Controls.Add(btnUninstall);

            txtLog = new TextBox();
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.BackColor = Color.FromArgb(245, 247, 250);
            txtLog.Location = new Point(22, 255);
            txtLog.Size = new Size(625, 165);
            this.Controls.Add(txtLog);

            Log("欢迎使用 Command: Modern Operations (CMO) 专家级中文汉化安装程序。");
            Log("本汉化涵盖：主界面菜单、作战条令(Doctrine)、交战规则(ROE)、任务编辑器、推演消息日志、热键与数据库视图。");
            RefreshStatus();
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

    public static class InstallerCore
    {
        public static string DetectGamePath()
        {
            string cur = AppDomain.CurrentDomain.BaseDirectory;
            if (File.Exists(Path.Combine(cur, "Command.exe"))) return cur;

            string[] commonPaths = new string[]
            {
                @"G:\soft\steam\steamapps\common\Command - Modern Operations",
                @"C:\Program Files (x86)\Steam\steamapps\common\Command - Modern Operations",
                @"D:\Steam\steamapps\common\Command - Modern Operations",
                @"D:\SteamLibrary\steamapps\common\Command - Modern Operations",
                @"E:\Steam\steamapps\common\Command - Modern Operations",
                @"E:\SteamLibrary\steamapps\common\Command - Modern Operations"
            };

            foreach (string p in commonPaths)
            {
                if (File.Exists(Path.Combine(p, "Command.exe"))) return p;
            }

            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1076160"))
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
