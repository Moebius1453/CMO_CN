using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace CMOLocalizer
{
    public class LocalizerDomainManager : CustomAppDomainManager
    {
    }

    public class CustomAppDomainManager : AppDomainManager
    {
        private static string logPath = "";
        private static Dictionary<int, string> offsetMap = new Dictionary<int, string>();
        private static Dictionary<string, string> exactMap = new Dictionary<string, string>(StringComparer.Ordinal);
        private static Dictionary<string, string> partialMap = new Dictionary<string, string>(StringComparer.Ordinal);
        private static Dictionary<string, string> wordsMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, string> normExactMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // High performance dynamic translation cache
        private static readonly Dictionary<string, string> dynamicCache = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly object cacheLock = new object();
        private const int MAX_CACHE_SIZE = 16384;

        private static void Log(string msg)
        {
            try
            {
                if (!string.IsNullOrEmpty(logPath))
                {
                    File.AppendAllText(logPath, string.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1}\r\n", DateTime.Now, msg), Encoding.UTF8);
                }
            }
            catch { }
        }

        public override void InitializeNewDomain(AppDomainSetup appDomainInfo)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                logPath = Path.Combine(baseDir, "CMOLocalizer.log");
                Log("=== CMOLocalizer v4.0 Engine Starting ===");

                // Start hook thread
                Thread hookThread = new Thread(HookWorker);
                hookThread.IsBackground = true;
                hookThread.SetApartmentState(ApartmentState.STA);
                hookThread.Start();

                // 启动启动弹窗与错误提示静默自动消除守护线程
                Thread dialogDismissThread = new Thread(AutoDismissStartupDialogs);
                dialogDismissThread.IsBackground = true;
                dialogDismissThread.Start();
            }
            catch (Exception ex)
            {
                Log("InitializeNewDomain Exception: " + ex.Message);
            }

            base.InitializeNewDomain(appDomainInfo);
        }

        private static void HookWorker()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string dictPath = Path.Combine(baseDir, "CMOLocalizer.json");
                if (File.Exists(dictPath))
                {
                    LoadDictionaries(dictPath);
                    Log(string.Format("Loaded dictionaries: {0} offsets, {1} exact texts, {2} partial terms, {3} word roots.",
                        offsetMap.Count, exactMap.Count, partialMap.Count, wordsMap.Count));
                }
                else
                {
                    Log("Warning: CMOLocalizer.json not found at " + dictPath);
                }

                // Start UI watcher loop in a background thread to translate WinForms controls
                Thread uiThread = new Thread(StartUIWatcher);
                uiThread.IsBackground = true;
                uiThread.Start();

                // Wait for target assembly and types to load
                int tries = 0;
                bool injectedEngine = false;
                while (tries < 120)
                {
                    tries++;
                    Thread.Sleep(500);

                    if (!injectedEngine)
                    {
                        injectedEngine = TryInjectCoreDecryption();
                    }

                    if (injectedEngine)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log("HookWorker Error: " + ex.ToString());
            }
        }

        private static bool IsForbiddenCodeOrEnum(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            // 严禁包含Lua代码/函数调用/SQL/路径/格式串/沙箱白名单
            if (s.StartsWith("ScenEdit_") || s.StartsWith("math.") || s.StartsWith("string.") || s.StartsWith("table.") || s.StartsWith("LUA_") || s.StartsWith("VP_") || s.StartsWith("Tool_") || s.StartsWith("World_")) return true;
            if (s.Contains("ScenEdit_") || s.Contains("SELECT ") || s.Contains("INSERT ") || s.Contains("UPDATE ") || s.Contains("DELETE ") || s.Contains(" FROM ") || s.Contains(" WHERE ") || s.Contains("sqlite")) return true;
            if (s.Contains(" = ") || s.Contains(" =\"") || s.Contains(" ='") || s.Contains("= '") || s.Contains("','") || s.Contains("', '") || s.Contains("({\"") || s.Contains("({'")) return true;
            if (s.Contains("break,do") || s.Contains("elseif") || s.Contains("collectgarbage") || s.Contains("getmetatable") || s.Contains("setmetatable") || s.Contains("theTable")) return true;
            if (s.Contains("ASSOCIATORS OF") || s.Contains("Win32_")) return true;
            if (s.Contains("{0:F") || s.Contains("{0:D") || s.Contains("{0:X")) return true;
            // 知名代码枚举
            if (s == "DockingOps" || s == "AirOps" || s == "CargoOps" || s == "ScenEdit") return true;
            return false;
        }

        private static bool TryInjectCoreDecryption()
        {
            try
            {
                Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();
                foreach (Assembly asm in asms)
                {
                    if (asm.GetName().Name == "Command" || asm.GetName().Name.StartsWith("Command"))
                    {
                        Type tDecrypt = asm.GetType("AoIBWWlDJbaf7LijnA.oMu6jVbdhHEH79DDhU", false);
                        if (tDecrypt != null)
                        {
                            BindingFlags bf = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
                            FieldInfo fDebug = tDecrypt.GetField("gTbYFnJa7EN", bf);
                            FieldInfo fStrings = tDecrypt.GetField("OVoYFvH0GRf", bf);
                            FieldInfo fOffsets = tDecrypt.GetField("vWjYFIwuiK7", bf);
                            FieldInfo fBuf = tDecrypt.GetField("ad6YFOZX46s", bf);

                            if (fDebug != null)
                            {
                                fDebug.SetValue(null, 100);
                                Log("Set gTbYFnJa7EN anti-debug flag to 100.");
                            }

                            if (fStrings != null && fOffsets != null && fBuf != null)
                            {
                                List<string> strList = (List<string>)fStrings.GetValue(null);
                                List<int> offList = (List<int>)fOffsets.GetValue(null);
                                byte[] buf = (byte[])fBuf.GetValue(null);

                                if (strList != null && offList != null && buf != null && offsetMap.Count > 0)
                                {
                                    int count = 0;
                                    lock (strList)
                                    {
                                        foreach (KeyValuePair<int, string> kvp in offsetMap)
                                        {
                                            int off = kvp.Key;
                                            string translated = kvp.Value;
                                            if (IsForbiddenCodeOrEnum(translated))
                                            {
                                                continue;
                                            }
                                            if (off >= 0 && off + 4 <= buf.Length)
                                            {
                                                int newIdx = strList.Count;
                                                strList.Add(translated);
                                                offList.Add(off);

                                                buf[off] = (byte)newIdx;
                                                buf[off + 1] = (byte)(newIdx >> 8);
                                                buf[off + 2] = (byte)(newIdx >> 16);
                                                buf[off + 3] = (byte)(newIdx >> 24);
                                                count++;
                                            }
                                        }
                                    }
                                    Log(string.Format("Successfully pre-populated {0} translated strings into AoIBWWlDJbaf7LijnA string pool!", count));
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("TryInjectCoreDecryption Exception: " + ex.ToString());
            }
            return false;
        }

        private static void StartUIWatcher()
        {
            Log("Starting UI Watcher loop (Adaptive interval)...");
            int loopCount = 0;
            while (true)
            {
                try
                {
                    // Scan faster during startup (first 30s), then settle to 1500ms
                    int interval = (loopCount < 40) ? 600 : 1500;
                    Thread.Sleep(interval);
                    loopCount++;

                    // Process external automated test commands (if any)
                    try
                    {
                        ProcessTestCommands();
                    }
                    catch { }

                    FormCollection openForms = Application.OpenForms;
                    if (openForms != null && openForms.Count > 0)
                    {
                        foreach (Form frm in openForms)
                        {
                            if (frm != null && !frm.IsDisposed)
                            {
                                if (frm.InvokeRequired)
                                {
                                    frm.BeginInvoke((MethodInvoker)delegate
                                    {
                                        try { TranslateControlTree(frm); } catch { }
                                    });
                                }
                                else
                                {
                                    TranslateControlTree(frm);
                                }
                            }
                        }
                    }

                    // Scan WPF Windows (including StartGameWindow / HwndWrapper)
                    try
                    {
                        TranslateWpfWindows();
                    }
                    catch { }
                }
                catch { }
            }
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_CLOSE = 0x0010;
        private const uint BM_CLICK = 0x00F5;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private static uint currentPid = 0;

        private static void AutoDismissStartupDialogs()
        {
            if (currentPid == 0)
            {
                currentPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            }

            int count = 0;
            // 启动前 60 秒高频检测并消除偶发的 Application_Startup 对话框及横幅
            while (count < 300)
            {
                count++;
                try
                {
                    EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
                    {
                        uint pid;
                        GetWindowThreadProcessId(hWnd, out pid);
                        if (pid == currentPid)
                        {
                            StringBuilder sbClass = new StringBuilder(256);
                            GetClassName(hWnd, sbClass, sbClass.Capacity);
                            string cls = sbClass.ToString();

                            StringBuilder sbText = new StringBuilder(512);
                            GetWindowText(hWnd, sbText, sbText.Capacity);
                            string txt = sbText.ToString();

                            // The start-up error is raised by the game's own
                            // WinForms form (class WindowsForms10.Window...), NOT a
                            // #32770 MessageBox, and its title is translated by this
                            // very dictionary -- so match the class prefix plus the
                            // phrase in either language.
                            if (cls == "#32770"
                                || txt.Contains("Application_Startup")
                                || txt.Contains("Error in Application")
                                || txt.Contains("application startup")
                                || txt.Contains("应用程序启动"))
                            {
                                Log("AutoDismissStartupDialogs: Closing dialog '" + txt + "'");
                                // 查找 OK 按钮并点击
                                EnumChildWindows(hWnd, delegate(IntPtr hChild, IntPtr lp)
                                {
                                    StringBuilder btnText = new StringBuilder(64);
                                    GetWindowText(hChild, btnText, btnText.Capacity);
                                    if (btnText.ToString() == "确定" || btnText.ToString().ToUpper() == "OK")
                                    {
                                        SendMessage(hChild, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                                    }
                                    return true;
                                }, IntPtr.Zero);
                                PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                            }
                            else if (txt.Contains("CPEBannerForm") || cls.Contains("CPEBannerForm"))
                            {
                                PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                            }
                        }
                        return true;
                    }, IntPtr.Zero);
                }
                catch { }

                Thread.Sleep(200);
            }
        }

        private static void TranslateWpfWindows()
        {
            try
            {
                if (currentPid == 0)
                {
                    currentPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
                }

                // Enumerate all HwndWrapper windows in current process (covers all WPF windows across any UI thread)
                EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
                {
                    try
                    {
                        uint pid;
                        GetWindowThreadProcessId(hWnd, out pid);
                        if (pid == currentPid)
                        {
                            StringBuilder sb = new StringBuilder(256);
                            GetClassName(hWnd, sb, sb.Capacity);
                            string cls = sb.ToString();
                            if (cls.StartsWith("HwndWrapper"))
                            {
                                System.Windows.Interop.HwndSource src = System.Windows.Interop.HwndSource.FromHwnd(hWnd);
                                if (src != null && src.Dispatcher != null)
                                {
                                    src.Dispatcher.BeginInvoke(new Action(delegate
                                    {
                                        try
                                        {
                                            if (src.RootVisual != null)
                                            {
                                                TranslateWpfElement(src.RootVisual);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Log("WPF Dispatcher invoke error: " + ex.Message);
                                        }
                                    }));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log("EnumWindows item error: " + ex.Message);
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Log("TranslateWpfWindows outer error: " + ex.Message);
            }
        }

        private static int wpfTranslatedCount = 0;

        private static readonly HashSet<int> hookedWpfElements = new HashSet<int>();
        [ThreadStatic]
        private static bool _inWpfTextChanged;

        private static void TranslateWpfElement(System.Windows.DependencyObject dep)
        {
            if (dep == null) return;
            try
            {
                if (dep is System.Windows.Controls.HeaderedContentControl)
                {
                    System.Windows.Controls.HeaderedContentControl hc = (System.Windows.Controls.HeaderedContentControl)dep;
                    if (hc.Header is string)
                    {
                        string s = (string)hc.Header;
                        if (!string.IsNullOrEmpty(s))
                        {
                            string trans = TranslateString(s);
                            if (trans != s) hc.Header = trans;
                        }
                    }
                    else if (hc.Header is System.Windows.DependencyObject)
                    {
                        TranslateWpfElement((System.Windows.DependencyObject)hc.Header);
                    }
                }
                if (dep is System.Windows.Controls.HeaderedItemsControl)
                {
                    System.Windows.Controls.HeaderedItemsControl hic = (System.Windows.Controls.HeaderedItemsControl)dep;
                    if (hic.Header is string)
                    {
                        string s = (string)hic.Header;
                        if (!string.IsNullOrEmpty(s))
                        {
                            string trans = TranslateString(s);
                            if (trans != s) hic.Header = trans;
                        }
                    }
                    else if (hic.Header is System.Windows.DependencyObject)
                    {
                        TranslateWpfElement((System.Windows.DependencyObject)hic.Header);
                    }
                }
                if (dep is System.Windows.Controls.TextBlock)
                {
                    System.Windows.Controls.TextBlock tb = (System.Windows.Controls.TextBlock)dep;
                    int tbHash = tb.GetHashCode();
                    if (!hookedWpfElements.Contains(tbHash))
                    {
                        hookedWpfElements.Add(tbHash);
                        try
                        {
                            var desc = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
                                System.Windows.Controls.TextBlock.TextProperty, typeof(System.Windows.Controls.TextBlock));
                            if (desc != null)
                            {
                                desc.AddValueChanged(tb, (s, e) =>
                                {
                                    if (_inWpfTextChanged) return;
                                    try
                                    {
                                        _inWpfTextChanged = true;
                                        System.Windows.Controls.TextBlock targetTb = s as System.Windows.Controls.TextBlock;
                                        if (targetTb != null && !string.IsNullOrEmpty(targetTb.Text) && !ContainsChinese(targetTb.Text))
                                        {
                                            string tr = TranslateString(targetTb.Text);
                                            if (tr != targetTb.Text) targetTb.Text = tr;
                                        }
                                    }
                                    catch { }
                                    finally { _inWpfTextChanged = false; }
                                });
                            }
                        }
                        catch { }
                    }

                    // 递归处理 Inlines (如包含 Hyperlink 的 Loadout 链接)
                    if (tb.Inlines != null && tb.Inlines.Count > 0)
                    {
                        foreach (System.Windows.Documents.Inline inline in tb.Inlines)
                        {
                            if (inline is System.Windows.Documents.Run)
                            {
                                var run = (System.Windows.Documents.Run)inline;
                                if (!string.IsNullOrEmpty(run.Text) && !ContainsChinese(run.Text))
                                {
                                    string trRun = TranslateString(run.Text);
                                    if (trRun != run.Text) run.Text = trRun;
                                }
                            }
                            else if (inline is System.Windows.Documents.Hyperlink)
                            {
                                var hl = (System.Windows.Documents.Hyperlink)inline;
                                foreach (System.Windows.Documents.Inline sub in hl.Inlines)
                                {
                                    if (sub is System.Windows.Documents.Run)
                                    {
                                        var subRun = (System.Windows.Documents.Run)sub;
                                        if (!string.IsNullOrEmpty(subRun.Text))
                                        {
                                            string trSub = TranslateCellText(subRun.Text);
                                            if (trSub != subRun.Text) subRun.Text = trSub;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (!string.IsNullOrEmpty(tb.Text))
                    {
                        string trans = TranslateString(tb.Text);
                        if (trans != tb.Text)
                        {
                            string old = tb.Text;
                            tb.Text = trans;
                            wpfTranslatedCount++;
                            if (wpfTranslatedCount <= 20) Log("WPF TextBlock Translated: " + old + " -> " + trans);
                        }
                    }
                }
                else if (dep is System.Windows.Controls.ContentControl)
                {
                    System.Windows.Controls.ContentControl cc = (System.Windows.Controls.ContentControl)dep;
                    if (cc.Content is string)
                    {
                        string s = (string)cc.Content;
                        if (!string.IsNullOrEmpty(s))
                        {
                            string trans = TranslateString(s);
                            if (trans != s)
                            {
                                cc.Content = trans;
                                wpfTranslatedCount++;
                                if (wpfTranslatedCount <= 20) Log("WPF ContentControl Translated: " + s + " -> " + trans);
                            }
                        }
                    }
                    else if (cc.Content is System.Windows.DependencyObject)
                    {
                        TranslateWpfElement((System.Windows.DependencyObject)cc.Content);
                    }
                }

                int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(dep);
                for (int i = 0; i < count; i++)
                {
                    TranslateWpfElement(System.Windows.Media.VisualTreeHelper.GetChild(dep, i));
                }
            }
            catch { }
        }

        private static void TranslateControlTree(Control ctrl)
        {
            if (ctrl == null) return;
            if (ctrl.GetType().Name.ToLower().Contains("tree") || ctrl.GetType().Name.ToLower().Contains("scen"))
            {
                Log("TranslateControlTree visited special control: " + ctrl.GetType().FullName);
            }

            TranslateControlText(ctrl);

            if (ctrl is ToolStrip)
            {
                ToolStrip ts = (ToolStrip)ctrl;
                // 如果是顶部战术快捷工具栏 (DarkToolStrip2 或包含大量 ToolStripButton)
                if (ts.Name == "DarkToolStrip2" || (ts.Items.Count >= 10 && ts.Items[0] is ToolStripButton))
                {
                    LocalizeDarkToolStripButtons(ts);
                }

                foreach (ToolStripItem item in ts.Items)
                {
                    TranslateToolStripItem(item);
                }
            }
            else if (ctrl is MenuStrip)
            {
                MenuStrip ms = (MenuStrip)ctrl;
                foreach (ToolStripItem item in ms.Items)
                {
                    TranslateToolStripItem(item);
                }
            }
            else if (ctrl is StatusStrip)
            {
                StatusStrip ss = (StatusStrip)ctrl;
                foreach (ToolStripItem item in ss.Items)
                {
                    TranslateToolStripItem(item);
                }
            }
            else if (ctrl is TabControl)
            {
                TabControl tc = (TabControl)ctrl;
                foreach (TabPage tp in tc.TabPages)
                {
                    string trans = TranslateString(tp.Text);
                    if (trans != tp.Text) tp.Text = trans;
                    TranslateControlTree(tp);
                }
            }
            else if (ctrl is DataGridView)
            {
                DataGridView dgv = (DataGridView)ctrl;
                foreach (DataGridViewColumn col in dgv.Columns)
                {
                    string trans = TranslateString(col.HeaderText);
                    if (trans != col.HeaderText) col.HeaderText = trans;
                }

                // 挂载 CellFormatting 事件监听器进行无侵入单元格动态中文化 (不破坏底层数据绑定)
                int dgvHash = dgv.GetHashCode();
                if (!hookedItems.Contains(dgvHash))
                {
                    hookedItems.Add(dgvHash);
                    dgv.CellFormatting += (s, e) =>
                    {
                        try
                        {
                            string sVal = e.Value as string;
                            if (!string.IsNullOrEmpty(sVal))
                            {
                                string tr = TranslateCellText(sVal);
                                if (tr != sVal)
                                {
                                    e.Value = tr;
                                    e.FormattingApplied = true;
                                }
                            }
                        }
                        catch { }
                    };
                }

                // 动态双语渲染数据库列表单元格中的装备名称与分类 (仅针对非编辑只读字符串单元格)
                try
                {
                    if (dgv.Rows.Count > 0 && dgv.Rows.Count <= 500)
                    {
                        foreach (DataGridViewRow row in dgv.Rows)
                        {
                            foreach (DataGridViewCell cell in row.Cells)
                            {
                                string sVal = cell.Value as string;
                                if (!string.IsNullOrEmpty(sVal) && sVal.Length > 2 && sVal.Length < 100)
                                {
                                    string tCell = TranslateString(sVal);
                                    if (tCell != sVal)
                                    {
                                        cell.ToolTipText = tCell;
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            else if (ctrl is ListView)
            {
                ListView lv = (ListView)ctrl;
                foreach (ColumnHeader ch in lv.Columns)
                {
                    string trans = TranslateString(ch.Text);
                    if (trans != ch.Text) ch.Text = trans;
                }
                try
                {
                    if (lv.Items.Count > 0 && lv.Items.Count <= 500)
                    {
                        foreach (ListViewItem lvi in lv.Items)
                        {
                            string tItem = TranslateString(lvi.Text);
                            if (tItem != lvi.Text) lvi.ToolTipText = tItem;
                        }
                    }
                }
                catch { }
            }
            else if (ctrl is TreeView)
            {
                TreeView tv = (TreeView)ctrl;
                foreach (TreeNode node in tv.Nodes)
                {
                    TranslateTreeNode(node);
                }
            }
            else if (ctrl.GetType().Name == "DarkTreeView")
            {
                TranslateDarkTreeView(ctrl);
            }
            else if (ctrl is WebBrowser)
            {
                WebBrowser wb = (WebBrowser)ctrl;
                TranslateWebBrowser(wb);
            }
            else if (ctrl.GetType().Name.IndexOf("WebView2", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                TranslateWebView2(ctrl);
            }
            else if (ctrl.GetType().Name.IndexOf("ElementHost", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                try
                {
                    PropertyInfo piChild = ctrl.GetType().GetProperty("Child");
                    if (piChild != null)
                    {
                        object childObj = piChild.GetValue(ctrl, null);
                        if (childObj is System.Windows.DependencyObject)
                        {
                            TranslateWpfElement((System.Windows.DependencyObject)childObj);
                        }
                    }
                }
                catch { }
            }
            else if (ctrl.GetType().Name.Contains("ChromiumWebBrowser"))
            {
                TranslateChromiumWebBrowser(ctrl);
            }

            if (ctrl.ContextMenuStrip != null)
            {
                foreach (ToolStripItem item in ctrl.ContextMenuStrip.Items)
                {
                    TranslateToolStripItem(item);
                }
            }

            if (ctrl.HasChildren)
            {
                foreach (Control child in ctrl.Controls)
                {
                    TranslateControlTree(child);
                }
            }
        }


        private static readonly HashSet<int> hookedBrowsers = new HashSet<int>();
        private static void TranslateWebView2(Control ctrl)
        {
            if (ctrl == null) return;
            try
            {
                PropertyInfo propCore = ctrl.GetType().GetProperty("CoreWebView2");
                if (propCore == null) return;
                object core = propCore.GetValue(ctrl, null);
                if (core == null) return;

                MethodInfo miExec = ctrl.GetType().GetMethod("ExecuteScriptAsync", new Type[] { typeof(string) });
                if (miExec == null) miExec = core.GetType().GetMethod("ExecuteScriptAsync", new Type[] { typeof(string) });
                if (miExec == null) return;

                object target = (miExec.DeclaringType.IsAssignableFrom(ctrl.GetType())) ? (object)ctrl : core;

                string jsExtract = @"(function(){
                    try {
                        if (!document || !document.body || document.readyState === 'loading') return '';
                        if (!window.__cmo_nodes) window.__cmo_nodes = [];
                        var walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, null, false);
                        var node;
                        var items = [];
                        while ((node = walker.nextNode())) {
                            if (node.__cmo_zh) continue;
                            var p = node.parentNode ? node.parentNode.tagName : '';
                            if (p === 'SCRIPT' || p === 'STYLE') continue;
                            var val = node.nodeValue;
                            if (val && val.trim().length > 0) {
                                node.__cmo_zh = true;
                                var idx = window.__cmo_nodes.length;
                                window.__cmo_nodes.push(node);
                                items.push(idx + '\t' + val);
                            }
                        }
                        if (items.length === 0) return '';
                        return encodeURIComponent(items.join('\n===CMO_SEP===\n'));
                    } catch(e) { return ''; }
                })()";

                System.Threading.Tasks.Task<string> task = miExec.Invoke(target, new object[] { jsExtract }) as System.Threading.Tasks.Task<string>;
                if (task == null) return;

                task.ContinueWith((t) =>
                {
                    try
                    {
                        if (t.IsFaulted || t.IsCanceled) return;
                        string rawJson = t.Result;
                        if (string.IsNullOrEmpty(rawJson) || rawJson == "\"\"" || rawJson == "null") return;

                        string encoded = rawJson.Trim('\"');
                        if (string.IsNullOrEmpty(encoded)) return;
                        string payload = Uri.UnescapeDataString(encoded);
                        if (string.IsNullOrEmpty(payload)) return;

                        string[] parts = payload.Split(new string[] { "\n===CMO_SEP===\n" }, StringSplitOptions.RemoveEmptyEntries);
                        List<string> updates = new List<string>();

                        for (int i = 0; i < parts.Length; i++)
                        {
                            string item = parts[i];
                            int tabPos = item.IndexOf('\t');
                            if (tabPos <= 0) continue;

                            string idxStr = item.Substring(0, tabPos);
                            string origText = item.Substring(tabPos + 1);
                            if (string.IsNullOrEmpty(origText.Trim())) continue;

                            int startIdx = 0;
                            while (startIdx < origText.Length && char.IsWhiteSpace(origText[startIdx])) startIdx++;
                            string leadSpace = origText.Substring(0, startIdx);

                            int endIdx = origText.Length - 1;
                            while (endIdx >= startIdx && char.IsWhiteSpace(origText[endIdx])) endIdx--;
                            string trailSpace = origText.Substring(endIdx + 1);

                            string coreText = origText.Substring(startIdx, endIdx - startIdx + 1);
                            string translated = TranslateBriefingText(coreText);

                            if (!string.IsNullOrEmpty(translated) && translated != coreText)
                            {
                                string finalText = leadSpace + translated + trailSpace;
                                updates.Add(idxStr + "\t" + finalText);
                            }
                        }

                        if (updates.Count > 0)
                        {
                            string updatePayload = Uri.EscapeDataString(string.Join("\n===CMO_SEP===\n", updates.ToArray()));
                            string jsApply = @"(function(){
                                try {
                                    if (!window.__cmo_nodes) return;
                                    var raw = decodeURIComponent('" + updatePayload + @"');
                                    var parts = raw.split('\n===CMO_SEP===\n');
                                    for (var i = 0; i < parts.length; i++) {
                                        var p = parts[i];
                                        var tab = p.indexOf('\t');
                                        if (tab > 0) {
                                            var idx = parseInt(p.substring(0, tab), 10);
                                            var txt = p.substring(tab + 1);
                                            var n = window.__cmo_nodes[idx];
                                            if (n) {
                                                n.nodeValue = txt;
                                                n.__cmo_zh = true;
                                            }
                                        }
                                    }
                                } catch(e) {}
                            })()";

                            if (!ctrl.IsDisposed && ctrl.IsHandleCreated)
                            {
                                ctrl.BeginInvoke(new Action(() =>
                                {
                                    try
                                    {
                                        miExec.Invoke(target, new object[] { jsApply });
                                    }
                                    catch { }
                                }));
                            }
                        }
                    }
                    catch { }
                });
            }
            catch { }
        }

        private static string TranslateBriefingText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // 1. 数据库武器射程与高度列 (e.g. "200 ft AGL - 65000 ft ASL")
            if (text.Contains(" ft AGL") || text.Contains(" ft ASL") || text.Contains(" m AGL") || text.Contains(" m ASL"))
            {
                text = text.Replace(" ft AGL", " 英尺(真高AGL)")
                           .Replace(" ft ASL", " 英尺(海拔ASL)")
                           .Replace(" m AGL", " 米(真高AGL)")
                           .Replace(" m ASL", " 米(海拔ASL)");
            }

            // 数据库动力装置油门档位与高度层 (e.g. "Band 1, Loiter Speed", "145.31 kg per minute")
            if (text.Contains("Band ") || text.Contains("kg per minute"))
            {
                text = text.Replace("Band 1", "高度层 1")
                           .Replace("Band 2", "高度层 2")
                           .Replace("Band 3", "高度层 3")
                           .Replace("Band 4", "高度层 4")
                           .Replace("Loiter Speed", "盘旋待机速度")
                           .Replace("Cruise Speed", "巡航速度")
                           .Replace("Military Speed", "军用推力速度")
                           .Replace("Afterburner Speed", "加力推力速度")
                           .Replace("kg per minute", "公斤/分钟");
            }

            // 数据库战机预设挂载方案飞行剖面 (e.g. "3150 nm Strike radius, Hi-Hi-Hi profile...")
            if (text.Contains("Strike radius") || text.Contains("Ferry Range") || text.Contains("profile.") || text.Contains("reserves."))
            {
                return TranslateFlightProfile(text);
            }

            // 2. 数据库外挂武器包线详情汇总列 (e.g. "Guided Weapon. Targets: ... 最高航速: ... Warhead: ... Can be guided by: ...")
            if (text.Contains("Targets:") || text.Contains("打击目标:") || text.Contains("Warhead:") || text.Contains("战斗部:") || text.StartsWith("Guided Weapon") || text.StartsWith("Unguided Weapon"))
            {
                return TranslateWeaponSummary(text);
            }

            // 3. 复合枚举串 (如 "Late 1980s Technology, 水面搜索, Ground Search (Fixed)")，即使部分已包含中文，也需翻译剩余英文字段
            if (text.Contains(", ") && Regex.IsMatch(text, @"[A-Za-z]{3,}"))
            {
                string trComp = TranslateCommaComposite(text);
                if (trComp != text) return trComp;
            }

            if (ContainsChinese(text)) return text;

            // Direct exact / normalized match via TranslateString
            string trans = TranslateString(text);
            if (trans != text) return trans;

            string norm = Regex.Replace(text.Trim(), @"\s+", " ");
            if (normExactMap.ContainsKey(norm)) return normExactMap[norm];

            // Database Viewer dynamic numeric patterns (e.g. "15 seconds Observe...", "30 seconds (Cadet)")
            if (norm.EndsWith("seconds Observe, Orient, Decide, and Act (reaction time)"))
            {
                return norm.Replace("seconds Observe, Orient, Decide, and Act (reaction time)", "秒 (观察-判断-决策-行动 OODA 反应时间)");
            }
            if (norm.EndsWith("seconds (Novice Proficiency Level)"))
            {
                return norm.Replace("seconds (Novice Proficiency Level)", "秒 (新手熟练度)");
            }
            if (norm.EndsWith("seconds (Cadet)"))
            {
                return norm.Replace("seconds (Cadet)", "秒 (学员熟练度)");
            }
            if (norm.EndsWith("seconds (Regular)"))
            {
                return norm.Replace("seconds (Regular)", "秒 (普通熟练度)");
            }
            if (norm.EndsWith("seconds (Veteran)"))
            {
                return norm.Replace("seconds (Veteran)", "秒 (老手熟练度)");
            }
            if (norm.EndsWith("seconds (Ace)"))
            {
                return norm.Replace("seconds (Ace)", "秒 (王牌熟练度)");
            }
            if (Regex.IsMatch(norm, @"^\d+(\.\d+)?\s+seconds$"))
            {
                return norm.Replace("seconds", "秒");
            }
            if (norm.StartsWith("Performance Details for "))
            {
                return "战术性能详情：" + norm.Substring("Performance Details for ".Length);
            }
            if (norm.StartsWith("Operating bands (search & track): "))
            {
                return "工作频段 (搜索与跟踪): " + norm.Substring("Operating bands (search & track): ".Length);
            }
            if (norm.StartsWith("Operating bands (FC / illumination): "))
            {
                return "工作频段 (火控与照射): " + norm.Substring("Operating bands (FC / illumination): ".Length);
            }
            if (norm.StartsWith("Operating bands: "))
            {
                return "工作频段: " + norm.Substring("Operating bands: ".Length);
            }
            if (norm.StartsWith("Max Target Illumination: "))
            {
                return "最大目标照射通道数: " + norm.Substring("Max Target Illumination: ".Length);
            }
            if (norm.StartsWith("Visual Zoom Detection/Classification: "))
            {
                return "可见光变焦探测/识别: " + norm.Substring("Visual Zoom Detection/Classification: ".Length);
            }
            if (norm.StartsWith("InfraRed Zoom Detection/Classification: "))
            {
                return "红外变焦探测/识别: " + norm.Substring("InfraRed Zoom Detection/Classification: ".Length);
            }

            // 逗号分隔的复合枚举项 (例如: "Late 1980s Technology, Ground Search (Fixed), Ground Search (Mobile)")
            if (text.Contains(", "))
            {
                string trComp = TranslateCommaComposite(text);
                if (trComp != text) return trComp;
            }

            // Multi-line or multi-sentence breakdown
            if (text.Contains(". ") || text.Contains("?\n") || text.Contains("!\n") || text.Contains("\n"))
            {
                string[] sentences = Regex.Split(text, @"(?<=[.?!])\s+|\r?\n");
                if (sentences.Length > 1)
                {
                    StringBuilder sb = new StringBuilder();
                    bool anyTrans = false;
                    for (int s = 0; s < sentences.Length; s++)
                    {
                        string sent = sentences[s].Trim();
                        if (string.IsNullOrEmpty(sent)) continue;
                        string st = TranslateString(sent);
                        if (st == sent)
                        {
                            string normSent = Regex.Replace(sent, @"\s+", " ");
                            if (normExactMap.ContainsKey(normSent)) st = normExactMap[normSent];
                        }
                        if (st != sent) anyTrans = true;
                        if (sb.Length > 0) sb.Append(" ");
                        sb.Append(st);
                    }
                    if (anyTrans) return sb.ToString();
                }
            }

            return text;
        }

        private static void TranslateChromiumWebBrowser(Control ctrl)
        {
            if (ctrl == null) return;
            try
            {
                // 反射调用 ExecuteScriptAsync 进行简报 HTML 中文化
                // ChromiumWebBrowser 包含 ExecuteScriptAsync 方法
                MethodInfo execMethod = ctrl.GetType().GetMethod("ExecuteScriptAsync", new Type[] { typeof(string) });
                if (execMethod != null)
                {
                    // 仅当包含英文且尚未标记为已翻译时注入翻译脚本
                    string js = @"(function() {
                        if (window.__cmo_translated) return;
                        if (!document.body) return;
                        window.__cmo_translated = true;
                        // 替换常见的简报关键词
                        var html = document.body.innerHTML;
                        var dict = {
                            'SITUATION': '战场态势 (SITUATION)',
                            'ENEMY FORCES': '敌方力量 (ENEMY FORCES)',
                            'FRIENDLY FORCES': '友军力量 (FRIENDLY FORCES)',
                            'MISSION': '任务简报 (MISSION)',
                            'EXECUTION': '作战执行 (EXECUTION)',
                            'COMMAND AND SIGNAL': '指挥与通信 (COMMAND & SIGNAL)',
                            'ORDER OF BATTLE': '战斗序列 (ORDER OF BATTLE)',
                            'RULES OF ENGAGEMENT': '交战规则 (ROE)',
                            'TASKS': '主要任务 (TASKS)'
                        };
                        for (var k in dict) {
                            var reg = new RegExp(k, 'g');
                            html = html.replace(reg, dict[k]);
                        }
                        document.body.innerHTML = html;
                    })();";
                    execMethod.Invoke(ctrl, new object[] { js });
                }
            }
            catch { }
        }

        private static void TranslateWebBrowser(WebBrowser wb)
        {
            if (wb == null) return;
            int hash = wb.GetHashCode();
            if (!hookedBrowsers.Contains(hash))
            {
                hookedBrowsers.Add(hash);
                wb.DocumentCompleted += (s, e) =>
                {
                    try
                    {
                        if (wb.Document != null && wb.Document.Body != null)
                        {
                            // 针对实时生成的 HTML (如数据库详情/简报)，动态替换英文字段
                            string html = wb.Document.Body.InnerHtml;
                            if (!string.IsNullOrEmpty(html) && !html.Contains("<!-- CMO_ZH_INJECTED -->"))
                            {
                                // 替换常见数据库英文字段
                                string translated = html
                                    .Replace("Aircraft size:", "机体尺寸:")
                                    .Replace("Average Climb Rate:", "平均爬升率:")
                                    .Replace("Service Ceiling:", "实用升限:")
                                    .Replace("Combat Radius:", "作战半径:")
                                    .Replace("Fuel Capacity:", "载油量:")
                                    .Replace("Damage Points:", "结构抗毁点数 (DP):")
                                    .Replace("Category:", "装备大类:")
                                    .Replace("Length:", "全长 / 舰长:")
                                    .Replace("Width:", "宽度:")
                                    .Replace("Height:", "高度:")
                                    .Replace("Weight:", "空重:")
                                    .Replace("Payload:", "有效载荷:")
                                    .Replace("Sensors:", "传感器:")
                                    .Replace("Weapons:", "武器挂载:")
                                    .Replace("Missions:", "分配任务:");

                                // 双语装备名称悬浮与替换支持
                                foreach (var kvp in exactMap)
                                {
                                    if (kvp.Key.Length >= 5 && translated.Contains(kvp.Key))
                                    {
                                        translated = translated.Replace(kvp.Key, kvp.Key + " 【" + kvp.Value + "】");
                                    }
                                }
                                translated += "<!-- CMO_ZH_INJECTED -->";
                                wb.Document.Body.InnerHtml = translated;
                            }
                        }
                    }
                    catch { }
                };
            }
        }

        private static void TranslateTreeNode(TreeNode node)
        {
            if (node == null) return;
            string trans = TranslateString(node.Text);
            if (trans != node.Text) node.Text = trans;
            foreach (TreeNode child in node.Nodes)
            {
                TranslateTreeNode(child);
            }
        }

        private static PropertyInfo _dtvNodesProp;
        private static PropertyInfo _dtnTextProp;
        private static PropertyInfo _dtnNodesProp;

        private static void TranslateDarkTreeView(Control ctrl)
        {
            if (ctrl == null) return;
            try
            {
                if (_dtvNodesProp == null)
                {
                    _dtvNodesProp = ctrl.GetType().GetProperty("Nodes", BindingFlags.Public | BindingFlags.Instance);
                }
                if (_dtvNodesProp == null) return;

                System.Collections.IEnumerable nodes = _dtvNodesProp.GetValue(ctrl, null) as System.Collections.IEnumerable;
                if (nodes == null) return;

                bool anyChanged = false;
                foreach (object node in nodes)
                {
                    if (TranslateDarkTreeNode(node))
                    {
                        anyChanged = true;
                    }
                }
                if (anyChanged)
                {
                    ctrl.Invalidate();
                }
            }
            catch { }
        }

        private static bool TranslateDarkTreeNode(object node)
        {
            if (node == null) return false;
            bool changed = false;
            try
            {
                Type t = node.GetType();
                if (_dtnTextProp == null)
                {
                    _dtnTextProp = t.GetProperty("Text", BindingFlags.Public | BindingFlags.Instance);
                }
                if (_dtnNodesProp == null)
                {
                    _dtnNodesProp = t.GetProperty("Nodes", BindingFlags.Public | BindingFlags.Instance);
                }

                if (_dtnTextProp != null)
                {
                    string raw = _dtnTextProp.GetValue(node, null) as string;
                    if (!string.IsNullOrEmpty(raw))
                    {
                        string trans = TranslateString(raw);
                        if (!string.IsNullOrEmpty(trans) && trans != raw)
                        {
                            _dtnTextProp.SetValue(node, trans, null);
                            changed = true;
                        }
                    }
                }

                if (_dtnNodesProp != null)
                {
                    System.Collections.IEnumerable children = _dtnNodesProp.GetValue(node, null) as System.Collections.IEnumerable;
                    if (children != null)
                    {
                        foreach (object child in children)
                        {
                            if (TranslateDarkTreeNode(child))
                            {
                                changed = true;
                            }
                        }
                    }
                }
            }
            catch { }
            return changed;
        }

        private static readonly Dictionary<int, string> buttonIndexToImageName = new Dictionary<int, string>()
        {
            { 0, "Btn_Auto_Engage_Target.png" },
            { 1, "Btn_Manually_Engage_Target.png" },
            { 2, "Btn_Plot_Course.png" },
            { 3, "Btn_Throttle_Altitude.png" },
            { 4, "Btn_Formation_Editor.png" },
            { 5, "Btn_Magazines.png" },
            { 6, "Btn_Aircraft_Operations.png" },
            { 7, "Btn_Boat_Operations.png" },
            { 8, "Btn_Mount_Weapons.png" },
            { 9, "Btn_Sensors.png" },
            { 10, "Btn_Systems_Damage.png" },
            { 11, "Btn_Unit-Group_Doctrine.png" },
            { 12, "Btn_Mission_Editor.png" }
        };

        private static readonly Dictionary<string, System.Drawing.Image> cachedButtonImages = new Dictionary<string, System.Drawing.Image>(StringComparer.OrdinalIgnoreCase);
        private static bool buttonImagesLoaded = false;

        private static void LoadButtonImages()
        {
            if (buttonImagesLoaded) return;
            buttonImagesLoaded = true;
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string menuDir = Path.Combine(baseDir, @"Symbols\Menu");
                if (Directory.Exists(menuDir))
                {
                    foreach (var kvp in buttonIndexToImageName)
                    {
                        string imgPath = Path.Combine(menuDir, kvp.Value);
                        if (File.Exists(imgPath))
                        {
                            using (var bmp = new System.Drawing.Bitmap(imgPath))
                            {
                                cachedButtonImages[kvp.Value] = new System.Drawing.Bitmap(bmp);
                            }
                        }
                    }
                    Log("Successfully pre-loaded " + cachedButtonImages.Count + " localized button images from Symbols\\Menu.");
                }
            }
            catch (Exception ex)
            {
                Log("LoadButtonImages error: " + ex.Message);
            }
        }

        private static readonly HashSet<int> processedToolStrips = new HashSet<int>();

        private static void LocalizeDarkToolStripButtons(ToolStrip ts)
        {
            if (ts == null) return;
            int hash = ts.GetHashCode();
            if (processedToolStrips.Contains(hash)) return;

            LoadButtonImages();

            int btnIndex = 0;
            for (int i = 0; i < ts.Items.Count; i++)
            {
                ToolStripItem itm = ts.Items[i];
                if (itm is ToolStripButton)
                {
                    if (buttonIndexToImageName.ContainsKey(btnIndex))
                    {
                        string imgName = buttonIndexToImageName[btnIndex];
                        if (cachedButtonImages.ContainsKey(imgName))
                        {
                            itm.Image = cachedButtonImages[imgName];
                            itm.DisplayStyle = ToolStripItemDisplayStyle.Image;
                        }
                    }
                    btnIndex++;
                }
            }
            processedToolStrips.Add(hash);
            Log("DarkToolStrip2 localized: updated " + btnIndex + " button images with Chinese bitmaps!");
        }

        
        private static void OnDropDownOpeningHandler(object sender, EventArgs e)
        {
            try
            {
                ToolStripDropDownItem ddi = sender as ToolStripDropDownItem;
                if (ddi != null && ddi.HasDropDownItems)
                {
                    foreach (ToolStripItem sub in ddi.DropDownItems)
                    {
                        TranslateToolStripItem(sub);
                    }
                    if (ddi.Owner != null)
                    {
                        ddi.Owner.BeginInvoke((MethodInvoker)delegate
                        {
                            try
                            {
                                foreach (ToolStripItem sub in ddi.DropDownItems)
                                {
                                    TranslateToolStripItem(sub);
                                }
                            }
                            catch { }
                        });
                    }
                }
            }
            catch { }
        }

        private static readonly HashSet<int> hookedItems = new HashSet<int>();
        [ThreadStatic]
        private static bool _inTextChanged;

        private static void TranslateToolStripItem(ToolStripItem item)
        {
            if (item == null) return;

            int itemHash = item.GetHashCode();
            if (!hookedItems.Contains(itemHash))
            {
                hookedItems.Add(itemHash);
                item.TextChanged += (s, e) =>
                {
                    if (_inTextChanged) return;
                    try
                    {
                        _inTextChanged = true;
                        ToolStripItem it = s as ToolStripItem;
                        if (it != null && !string.IsNullOrEmpty(it.Text) && !ContainsChinese(it.Text))
                        {
                            string tr = TranslateString(it.Text);
                            if (tr != it.Text && ContainsChinese(tr))
                            {
                                it.Text = tr;
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        _inTextChanged = false;
                    }
                };
            }

            string t = item.Text;
            if (!string.IsNullOrEmpty(t))
            {
                string trans = TranslateString(t);
                if (trans != t)
                {
                    item.Text = trans;
                    translatedControlCount++;
                    if (translatedControlCount == 1 || translatedControlCount % 50 == 0)
                    {
                        Log("UI Menu/ToolStrip Translated count: " + translatedControlCount + " (Sample: " + t + " -> " + trans + ")");
                    }
                }
            }

            if (!string.IsNullOrEmpty(item.ToolTipText))
            {
                string tipTrans = TranslateString(item.ToolTipText);
                if (tipTrans != item.ToolTipText)
                {
                    item.ToolTipText = tipTrans;
                }
            }

            if (item is ToolStripDropDownItem)
            {
                ToolStripDropDownItem ddi = (ToolStripDropDownItem)item;
                ddi.DropDownOpening -= OnDropDownOpeningHandler;
                ddi.DropDownOpening += OnDropDownOpeningHandler;
                if (ddi.HasDropDownItems)
                {
                    foreach (ToolStripItem sub in ddi.DropDownItems)
                    {
                        TranslateToolStripItem(sub);
                    }
                }
            }
        }

        private static int translatedControlCount = 0;

        private static string TranslateWeaponSummary(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string s = text;
            s = s.Replace("Guided Weapon. ", "制导武器。 ")
                 .Replace("Unguided Weapon. ", "非制导武器。 ")
                 .Replace("Decoy. ", "诱饵弹。 ")
                 .Replace("Sensor Pod. ", "传感器/电子战吊舱。 ")
                 .Replace("Torpedo. ", "鱼雷。 ")
                 .Replace("Naval Mine. ", "水雷。 ")
                 .Replace("Depth Charge. ", "深水炸弹。 ")
                 .Replace("Sonobuoy. ", "声呐浮标。 ")
                 .Replace("Fuel Tank. ", "外挂副油箱。 ");

            int tgtIdx = s.IndexOf("Targets: ");
            if (tgtIdx < 0) tgtIdx = s.IndexOf("打击目标: ");
            if (tgtIdx >= 0)
            {
                int prefixLen = s.IndexOf("Targets: ") >= 0 ? "Targets: ".Length : "打击目标: ".Length;
                int dotIdx = s.IndexOf(". ", tgtIdx + prefixLen);
                if (dotIdx > tgtIdx)
                {
                    string targetsPart = s.Substring(tgtIdx + prefixLen, dotIdx - (tgtIdx + prefixLen));
                    string trTargets = TranslateCommaComposite(targetsPart);
                    s = s.Substring(0, tgtIdx) + "打击目标: " + trTargets + s.Substring(dotIdx);
                }
            }

            s = s.Replace("Max Speed: ", "最高航速: ")
                 .Replace("Max Range: ", "最大射程: ")
                 .Replace(" kts. ", " 节。 ")
                 .Replace(" nm. ", " 海里。 ")
                 .Replace("Warheads: ", "战斗部: ")
                 .Replace("Warhead: ", "战斗部: ")
                 .Replace("Can be guided by: ", "制导设备: ")
                 .Replace("Can be directed by: ", "可由以下传感器引导: ")
                 .Replace("Gun. ", "航炮/火炮。 ")
                 .Replace("Deployment depth: ", "布设深度: ");

            s = s.Replace("Blast-Frag", "破片杀伤")
                 .Replace("Blast/Frag", "破片杀伤")
                 .Replace(" Frag", " 破片")
                 .Replace(" HEAT", " 破甲")
                 .Replace(" Penetrator", " 钻地侵彻")
                 .Replace(" HE.", " 高爆。")
                 .Replace(" HE ", " 高爆 ")
                 .Replace(" Nuclear", " 核战斗部")
                 .Replace(" Continuous Rod", " 连续杆")
                 .Replace(" Submunitions", " 子母弹");

            return s;
        }

        private static string TranslateFlightProfile(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Replace(" minutes at ", " 分钟 @ ")
                       .Replace(" min at ", " 分钟 @ ")
                       .Replace(" minutes combat in AB", " 分钟加力空战")
                       .Replace(" min combat in AB", " 分钟加力空战")
                       .Replace(" minutes in AB", " 分钟加力飞行")
                       .Replace(" nm CAP.", " 海里战斗空中巡逻 (CAP)。")
                       .Replace(" nm CAP", " 海里战斗空中巡逻 (CAP)")
                       .Replace(" nm AEW.", " 海里空中预警巡逻 (AEW)。")
                       .Replace(" nm ASW patrol.", " 海里反潜巡逻。")
                       .Replace(" nm Maritime Surveillance.", " 海里海上监视巡逻。")
                       .Replace(" nm Ferry Range.", " 海里转场航程。")
                       .Replace(" nm Strike radius, ", " 海里打击作战半径，")
                       .Replace("Cruise at optimum altitude and speed.", "按最佳高度与速度巡航。")
                       .Replace("Cruise at ", "巡航高度 ")
                       .Replace("Weapon release at max range.", "在最大射程发射武器。")
                       .Replace("No form-up time.", "无编队集结时间。")
                       .Replace(" profile.", " 飞行剖面。")
                       .Replace("Radius: ", "作战半径: ")
                       .Replace("Transit at ", "巡航转场高度 ")
                       .Replace("Ingress at ", "突防进入高度 ")
                       .Replace("Egress at ", "脱离返航高度 ")
                       .Replace(", on-station loiter", "，阵位巡逻待机")
                       .Replace(", on-station ", "，阵位滞空 ")
                       .Replace("Form-up ", "编队集结 ")
                       .Replace(" at 2k ft.", " @ 高度 2千英尺。")
                       .Replace(" at 36k ft", " @ 36千英尺")
                       .Replace("% reserves and ", "% 备份油量与 ")
                       .Replace("% reserves", "% 备份油量")
                       .Replace(" min loiter", " 分钟盘旋待机")
                       .Replace("Hi-Lo-Hi", "高-低-高 (Hi-Lo-Hi)")
                       .Replace("Lo-Lo-Lo", "低-低-低 (Lo-Lo-Lo)")
                       .Replace("Hi-Hi-Hi", "高-高-高 (Hi-Hi-Hi)")
                       .Replace("low-level dash", "低空突防冲刺")
                       .Replace("k ft", "千英尺")
                       .Replace(" nm", " 海里");
        }

        private static string TranslateLoadoutValue(string val)
        {
            if (string.IsNullOrEmpty(val)) return val;
            if (val == "-" || val == "无" || val == "无限制") return val;

            if (exactMap.ContainsKey(val)) return exactMap[val];
            string norm = Regex.Replace(val.Trim(), @"\s+", " ");
            if (normExactMap.ContainsKey(norm)) return normExactMap[norm];

            if (val.StartsWith("Shotgun:") || val.StartsWith("Winchester:"))
            {
                return val.Replace("Shotgun: ", "主武器耗尽返航 (Shotgun): ")
                          .Replace("Winchester: ", "弹尽返航 (Winchester): ")
                          .Replace("Return to base after one engagement with WVR weapons.", "在进行一次视距内交战后返航。")
                          .Replace("Return to base after one engagement with BVR weapons.", "在进行一次超视距交战后返航。")
                          .Replace("Allow targets of opportunity with air-to-air guns.", "允许使用航炮攻击机会目标。");
            }

            if (val.EndsWith(" capable") || val.EndsWith(" 能力") || val.StartsWith("Day and night") || val.StartsWith("Day-only") || val.StartsWith("Night-only"))
            {
                string cap = val.Replace(" capable", "").Replace(" 能力", "").Trim();
                return cap.Replace("Day and night", "昼夜")
                          .Replace("Day-only", "仅昼间")
                          .Replace("Night-only", "仅夜间")
                          .Replace("All-weather", "全天候")
                          + "作战能力";
            }

            if (val.Contains(" minutes at ") || val.Contains(" nm CAP") || val.Contains("Transit at ") || val.Contains("Ingress at ") || val.Contains(" profile"))
            {
                return TranslateFlightProfile(val);
            }

            if (val.Contains(", "))
            {
                return TranslateCommaComposite(val);
            }

            string tr = TranslateString(val);
            if (tr != val) return tr;
            return val;
        }

        private static string TranslateCommaComposite(string text)
        {
            if (string.IsNullOrEmpty(text) || !text.Contains(",")) return TranslateString(text);
            string[] items = text.Split(new string[] { ", " }, StringSplitOptions.None);
            if (items.Length <= 1) return TranslateString(text);

            List<string> trItems = new List<string>();
            bool anyTrans = false;
            for (int i = 0; i < items.Length; i++)
            {
                string it = items[i].Trim();
                string tr = TranslateString(it);
                if (tr == it && it.EndsWith(" Technology"))
                {
                    string gen = it.Substring(0, it.Length - " Technology".Length).Trim();
                    string trGen = TranslateString(gen);
                    if (trGen != gen) tr = trGen + "技术水平";
                }
                if (tr != it) anyTrans = true;
                trItems.Add(tr);
            }
            return anyTrans ? string.Join("，", trItems.ToArray()) : text;
        }

        private static string TranslateCellText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            string tr = TranslateString(text);
            if (tr != text) return tr;

            if (text.Contains("Reserve"))
            {
                text = text.Replace("(Reserve [Available])", "(后备/预备挂载 [可用])")
                           .Replace("Reserve [Available]", "后备/预备挂载 [可用]")
                           .Replace("(Reserve)", "(后备/预备挂载)");
            }

            if (text.Contains("Maintenance"))
            {
                text = text.Replace("(Maintenance [Unavailable])", "(维护检修 [不可用])")
                           .Replace("Maintenance [Unavailable]", "维护检修 [不可用]");
            }

            if (text.Contains("CAEW"))
            {
                text = text.Replace("Conformal Airborne Early Warning (CAEW)", "保形机载早期预警 (CAEW)");
            }

            if (Regex.IsMatch(text, @"^\d+\s*(hr|小时)(\s*\d+\s*(min|分钟))?$|^\d+\s*(min|分钟)$"))
            {
                return text.Replace("hr", "小时").Replace("min", "分钟").Replace("sec", "秒");
            }

            if (text.Contains(" sorties @ ") || text.Contains(" flying time") || text.Contains("Day and night") || text.Contains("架次") || text.Contains("downtime"))
            {
                text = text.Replace("Day and night", "昼夜")
                           .Replace("Day-only", "仅昼间")
                           .Replace("Night-only", "仅夜间")
                           .Replace(" sorties @ ", " 架次 @ ")
                           .Replace(" flying time", " 飞行时间")
                           .Replace(" downtime", " 停机整备")
                           .Replace(" hr", " 小时")
                           .Replace(" min", " 分钟");
            }

            if (text.Contains("Enabled") || text.Contains("已启用") || text.Contains("Day/Night"))
            {
                text = text.Replace("Enabled, ", "已启用，")
                           .Replace("Enabled", "已启用")
                           .Replace("Day/Night:", "昼/夜:")
                           .Replace("Day/Night", "昼/夜");
            }
            if (text.StartsWith("Disabled"))
            {
                return text.Replace("Disabled (Doctrine)", "已禁用 (条令限制)").Replace("Disabled", "已禁用");
            }

            return text;
        }

        private static string TranslateLoadoutDetailText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string[] lines = text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
            StringBuilder sb = new StringBuilder();
            bool anyTrans = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trLine = line;

                if (line.StartsWith("Pre-briefed weapon state ")
                    || line.StartsWith("Effective pre-briefed weapon state ")
                    || line.StartsWith("Loadout's default weapon state ")
                    || line.StartsWith("预设武器状态")
                    || line.StartsWith("预先简报的武器状态")
                    || line.StartsWith("生效的预设武器状态")
                    || line.StartsWith("挂载方案默认武器状态"))
                {
                    string prefix = "预设武器状态: ";
                    if (line.StartsWith("Effective") || line.StartsWith("生效")) prefix = "生效的预设武器状态: ";
                    else if (line.StartsWith("Loadout's") || line.StartsWith("挂载")) prefix = "挂载方案默认武器状态: ";

                    string rest = line.Replace("Effective pre-briefed weapon state ", "")
                                      .Replace("Loadout's default weapon state ", "")
                                      .Replace("Pre-briefed weapon state ", "")
                                      .Replace("预先简报的武器状态 ", "")
                                      .Replace("生效的预设武器状态: ", "")
                                      .Replace("挂载方案默认武器状态: ", "")
                                      .Replace("预设武器状态: ", "")
                                      .Trim();
                    string trRest = exactMap.ContainsKey(rest) ? exactMap[rest] : TranslateString(rest);
                    trLine = prefix + trRest;
                    if (trLine != line) anyTrans = true;
                }
                else
                {
                    int colonIdx = line.IndexOf(':');
                    if (colonIdx < 0) colonIdx = line.IndexOf('：');
                    if (colonIdx > 0 && colonIdx < line.Length - 1)
                    {
                        string key = line.Substring(0, colonIdx).Trim();
                        string val = line.Substring(colonIdx + 1).Trim();
                        string trKey = exactMap.ContainsKey(key) ? exactMap[key] : (normExactMap.ContainsKey(key) ? normExactMap[key] : TranslateString(key));
                        string trVal = TranslateLoadoutValue(val);
                        trLine = trKey + ": " + trVal;
                        if (trLine != line) anyTrans = true;
                    }
                }

                if (i > 0) sb.Append("\r\n");
                sb.Append(trLine);
            }

            return anyTrans ? sb.ToString() : text;
        }

        private static void TranslateControlText(Control ctrl)
        {
            if (ctrl is Form)
            {
                string fText = ctrl.Text;
                if (!string.IsNullOrEmpty(fText))
                {
                    if (fText.StartsWith("Air Ops - "))
                    {
                        ctrl.Text = "航空作业 - " + fText.Substring("Air Ops - ".Length);
                        return;
                    }
                    if (fText.StartsWith("Docking Ops - "))
                    {
                        ctrl.Text = "船坞作业 - " + fText.Substring("Docking Ops - ".Length);
                        return;
                    }
                    if (fText.StartsWith("Mission Editor for side: "))
                    {
                        ctrl.Text = "任务编辑器 - 所属阵营: " + fText.Substring("Mission Editor for side: ".Length);
                        return;
                    }
                    if (fText.Contains("Special Messages"))
                    {
                        ctrl.Text = fText.Replace("Special Messages", "专用战情消息");
                        return;
                    }
                    if (fText.StartsWith("就绪飞机:") && fText.Contains(" at "))
                    {
                        ctrl.Text = fText.Replace(" at ", " 位于 ");
                        return;
                    }
                }
            }

            if (ctrl is TextBoxBase)
            {
                TextBoxBase tb = (TextBoxBase)ctrl;
                if (tb.ReadOnly && !string.IsNullOrEmpty(tb.Text))
                {
                    string trTb = TranslateLoadoutDetailText(tb.Text);
                    if (trTb != tb.Text)
                    {
                        tb.Text = trTb;
                    }
                }
                else if (!string.IsNullOrEmpty(tb.Text) && tb.Text.StartsWith("Enter Database ID for this "))
                {
                    tb.Text = "输入该型装备的数据库 ID...";
                }
                return;
            }

            // ComboBox, ListControl and UpDownBase hold internal query/code keys
            // or user inputs. Overwriting their .Text destroys SelectedIndex and breaks game logic!
            if (ctrl is ComboBox || ctrl is ListControl || ctrl is UpDownBase)
            {
                return;
            }

            string t = ctrl.Text;
            if (!string.IsNullOrEmpty(t))
            {
                // 处理单行或多行挂载详情面板 (如 AirOps / ReadyAircraft 右下角的各项 Label)
                if (t.Contains("挂载") || t.Contains("作战能力") || t.Contains("飞行剖面") || t.Contains("武器状态") || t.Contains("攻击高度") ||
                    t.Contains("Loadout") || t.Contains("Pre-briefed") || t.Contains("Effective pre-briefed") || t.Contains("Unlimited") ||
                    t.StartsWith("TimeOfDay") || t.StartsWith("NumberOfLoadouts") || t.Contains("Capable") || t.Contains("capable"))
                {
                    string trMulti = TranslateLoadoutDetailText(t);
                    if (trMulti != t)
                    {
                        ctrl.Text = trMulti;
                        return;
                    }
                }
                if (t.Contains("Special Messages"))
                {
                    ctrl.Text = t.Replace("Special Messages", "专用战情消息");
                    return;
                }
                // 处理 "展开 飞机构列表" 遗留误译修正与 Occupied 动态统计
                if (t.Contains("飞机构列表"))
                {
                    ctrl.Text = t.Replace("飞机构列表", "飞机列表").Replace("展开 ", "展开");
                    return;
                }
                if (t.StartsWith("Occupied "))
                {
                    ctrl.Text = "已占用 " + t.Substring("Occupied ".Length);
                    return;
                }
                if (t.Contains(" sorties @ ") || t.Contains("最长时间") || t.Contains("maximum airborne time") || t.Contains("架次 @"))
                {
                    string sTr = t.Replace(" sorties @ ", " 架次 @ ")
                                  .Replace(" maximum airborne time and ", " 最长滞空时间，")
                                  .Replace(" 最长滞空时间与 ", " 最长滞空时间，")
                                  .Replace(" turnaround, with ", " 再次出动周转，")
                                  .Replace(" 再次出动准备，包含 ", " 再次出动周转，")
                                  .Replace(" standdown ready time", " 停航恢复时间")
                                  .Replace(" hr", " 小时")
                                  .Replace(" min", " 分钟");
                    if (sTr != t)
                    {
                        ctrl.Text = sTr;
                        return;
                    }
                }

                string trans = TranslateString(t);
                if (trans != t)
                {
                    ctrl.Text = trans;
                    translatedControlCount++;
                    if (translatedControlCount == 1 || translatedControlCount % 50 == 0)
                    {
                        Log("UI Controls Translated count: " + translatedControlCount + " (Sample: " + t + " -> " + trans + ")");
                    }
                }
            }
        }

        private static bool ContainsChinese(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] >= 0x4e00 && s[i] <= 0x9fa5) return true;
            }
            return false;
        }

        public static string TranslateString(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            // 1. Exact match & Trimmed exact match (highest priority before path filtering)
            if (exactMap.ContainsKey(raw)) return exactMap[raw];
            string trimmedExact = raw.Trim();
            if (exactMap.ContainsKey(trimmedExact))
            {
                return raw.Replace(trimmedExact, exactMap[trimmedExact]);
            }
            // 路径、扩展名与文件过滤器直接跳过，防止破坏游戏文件检索与想定树加载
            if (raw.Contains("\\") || raw.Contains("://") || raw.Contains(".scen") || raw.Contains(".save") || raw.Contains(".db3") || raw.Contains(".dll") || raw.Contains(".ini") || raw.Contains(".xml") || raw.Contains("*. ") || raw.Contains("|*."))
            {
                return raw;
            }

            // 特殊复合前缀拆解：即使前缀已经被 offsets 汉化，后续英文想定标题/地域名/年份仍需递归汉化
            if (raw.StartsWith("[未授权] ") || raw.StartsWith("[UNLICENSED] "))
            {
                string rest = raw.Substring(raw.IndexOf(']') + 1).Trim();
                string tRest = TranslateString(rest);
                return "[未授权] " + tRest;
            }
            if (raw.StartsWith("作战地域:") || raw.StartsWith("作战地域：") || raw.StartsWith("位置 / 设置:"))
            {
                int colonIdx = raw.IndexOf(':');
                if (colonIdx < 0) colonIdx = raw.IndexOf('：');
                string prefix = (colonIdx >= 0) ? raw.Substring(0, colonIdx + 1).Trim() : "作战地域：";
                string body = (colonIdx >= 0) ? raw.Substring(colonIdx + 1).Trim() : raw;
                int idxY = body.IndexOf("--");
                if (idxY > 0)
                {
                    string setting = body.Substring(0, idxY).Trim();
                    string rest = body.Substring(idxY + 2).Trim();
                    rest = Regex.Replace(rest, @"\bYear\s*:\s*", "年份: ", RegexOptions.IgnoreCase);
                    string tSet = TranslateString(setting);
                    return prefix + " " + tSet + "  --  " + rest;
                }
            }
            if (raw.StartsWith("Uses: "))
            {
                return "使用数据库: " + raw.Substring(6).Trim();
            }

            // 如果文本中已经含有中文，说明已经被翻译过或者是中文字符串，坚决避免重复翻译与递归替换！
            if (ContainsChinese(raw)) return raw;

            // Check cache first
            lock (cacheLock)
            {
                string cached;
                if (dynamicCache.TryGetValue(raw, out cached))
                {
                    return cached;
                }
            }

            string result = DoTranslate(raw);

            // Store in cache
            lock (cacheLock)
            {
                if (dynamicCache.Count < MAX_CACHE_SIZE)
                {
                    dynamicCache[raw] = result;
                }
            }

            if (result == raw && !ContainsChinese(raw))
            {
                AuditUntranslatedLive(raw);
            }

            return result;
        }

        private static string DoTranslate(string raw)
        {
            if (IsForbiddenCodeOrEnum(raw)) return raw;
            // 1. Exact match
            if (exactMap.ContainsKey(raw)) return exactMap[raw];

            // 2. Trimmed match
            string trimmed = raw.Trim();
            if (exactMap.ContainsKey(trimmed))
            {
                return raw.Replace(trimmed, exactMap[trimmed]);
            }

            // 2.1 Normalized whitespace exact match
            string normRaw = Regex.Replace(trimmed, @"\s+", " ");
            if (normExactMap.ContainsKey(normRaw))
            {
                return normExactMap[normRaw];
            }
            if (normRaw.EndsWith(":") || normRaw.EndsWith("："))
            {
                string bare = normRaw.Substring(0, normRaw.Length - 1).Trim();
                if (normExactMap.ContainsKey(bare))
                {
                    return normExactMap[bare] + "：";
                }
            }

            // 3. Match with shortcut & prefix e.g. "&File" -> "文件(&F)" or "File (&F)"
            if (trimmed.StartsWith("&") && trimmed.Length > 1)
            {
                string noAmp = trimmed.Substring(1);
                if (exactMap.ContainsKey(noAmp))
                {
                    return exactMap[noAmp] + "(&" + trimmed[1] + ")";
                }
            }
            if (trimmed.EndsWith(")") && trimmed.Length > 4 && trimmed[trimmed.Length - 4] == '(' && trimmed[trimmed.Length - 3] == '&')
            {
                string baseText = trimmed.Substring(0, trimmed.Length - 4).Trim();
                if (exactMap.ContainsKey(baseText))
                {
                    return exactMap[baseText] + trimmed.Substring(trimmed.Length - 4);
                }
            }

            // 4. Match with trailing ellipsis e.g. "Open..." -> "打开..."
            if (trimmed.EndsWith("...") && trimmed.Length > 3)
            {
                string baseText = trimmed.Substring(0, trimmed.Length - 3).Trim();
                if (exactMap.ContainsKey(baseText))
                {
                    return exactMap[baseText] + "...";
                }
            }

            // 4.1 Match numbered prefix e.g. "1. Blue Dawn" -> "1. 蓝色拂晓"
            int dotIdx = trimmed.IndexOf('.');
            if (dotIdx > 0 && dotIdx < 5)
            {
                string numPrefix = trimmed.Substring(0, dotIdx + 1);
                string rest = trimmed.Substring(dotIdx + 1).Trim();
                if (exactMap.ContainsKey(rest))
                {
                    return numPrefix + " " + exactMap[rest];
                }
                if (exactMap.ContainsKey(trimmed))
                {
                    return exactMap[trimmed];
                }
            }

            // 5. Enclosed brackets e.g. "[Selected Unit]" -> "[当前选中单位]"
            if (trimmed.Length >= 3)
            {
                if ((trimmed.StartsWith("[") && trimmed.EndsWith("]")) ||
                    (trimmed.StartsWith("(") && trimmed.EndsWith(")")) ||
                    (trimmed.StartsWith("{") && trimmed.EndsWith("}")))
                {
                    char openChar = trimmed[0];
                    char closeChar = trimmed[trimmed.Length - 1];
                    string inner = trimmed.Substring(1, trimmed.Length - 2).Trim();
                    string innerTrans = DoTranslate(inner);
                    if (innerTrans != inner)
                    {
                        return openChar + innerTrans + closeChar;
                    }
                }
            }

            // 6. Colon composite. Only for label-like keys: a long sentence that
            // happens to contain a colon must not be torn in half.
            int colonIdx = trimmed.IndexOf(':');
            if (colonIdx > 0 && colonIdx < trimmed.Length - 1 && colonIdx <= 40)
            {
                string left = trimmed.Substring(0, colonIdx).Trim();
                string right = trimmed.Substring(colonIdx + 1).Trim();
                if (left.Length <= 32 && right.Length <= 64)
                {
                    string transLeft = DoTranslate(left);
                    string transRight = DoTranslate(right);
                    if (transLeft != left && transRight != right)
                    {
                        return transLeft + "：" + transRight;
                    }
                    if (transLeft != left)
                    {
                        return transLeft + "：" + right;
                    }
                }
            }

            // 7. Dash composite "A - B". Same guard: never split prose.
            int dashIdx = trimmed.IndexOf(" - ");
            if (dashIdx > 0 && dashIdx < trimmed.Length - 3)
            {
                string left = trimmed.Substring(0, dashIdx).Trim();
                string right = trimmed.Substring(dashIdx + 3).Trim();
                if (left.Length <= 32 && right.Length <= 48 &&
                    left.IndexOf(' ') < 0 && right.IndexOf(' ') < 0)
                {
                    string transLeft = DoTranslate(left);
                    string transRight = DoTranslate(right);
                    if (transLeft != left || transRight != right)
                    {
                        return transLeft + " - " + transRight;
                    }
                }
            }

            // 8. No word-level composition. Word-by-word assembly produced garbled
            // mixed-language output and corrupted UI text; every display string is
            // translated as a whole through exactMap or the offset string pool.
            return raw;
        }

        private static void LoadDictionaries(string jsonPath)
        {
            try
            {
                string jsonText = File.ReadAllText(jsonPath, Encoding.UTF8);
                ParseJsonSections(jsonText);
            }
            catch (Exception ex)
            {
                Log("Error loading dictionaries: " + ex.ToString());
            }
        }

        private static void ParseJsonSections(string text)
        {
            // Section keys must be located at the TOP level of the root object.
            // A plain IndexOf would match `_meta.counts.offsets` instead and shift
            // every section boundary, silently loading zero entries.
            int idxOffsets = FindTopLevelKey(text, "offsets");
            int idxExact = FindTopLevelKey(text, "exact");
            int idxPartial = FindTopLevelKey(text, "partial");
            int idxWords = FindTopLevelKey(text, "words");

            List<SectionPos> sections = new List<SectionPos>();
            if (idxOffsets >= 0) sections.Add(new SectionPos { Name = "offsets", Pos = idxOffsets });
            if (idxExact >= 0) sections.Add(new SectionPos { Name = "exact", Pos = idxExact });
            if (idxPartial >= 0) sections.Add(new SectionPos { Name = "partial", Pos = idxPartial });
            if (idxWords >= 0) sections.Add(new SectionPos { Name = "words", Pos = idxWords });

            sections.Sort((a, b) => a.Pos.CompareTo(b.Pos));

            for (int i = 0; i < sections.Count; i++)
            {
                int start = sections[i].Pos;
                int end = (i + 1 < sections.Count) ? sections[i + 1].Pos : text.Length;
                string block = text.Substring(start, end - start);
                string name = sections[i].Name;

                ParsePairs(block, name == "offsets", name == "exact", name == "partial", name == "words");
            }
        }

        private class SectionPos
        {
            public string Name;
            public int Pos;
        }

        /// <summary>
        /// Return the index of a quoted key that sits directly inside the root
        /// object (depth 1), or -1. Nested containers such as `_meta.counts` are
        /// at depth 2 and therefore never match.
        /// </summary>
        private static int FindTopLevelKey(string text, string key)
        {
            int depth = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"')
                {
                    int j = i + 1;
                    bool matches = j + key.Length < text.Length
                        && text[j + key.Length] == '"'
                        && string.CompareOrdinal(text, j, key, 0, key.Length) == 0;
                    if (matches && depth == 1)
                    {
                        return i;
                    }
                    // Skip the whole string literal, honouring escapes.
                    i = j;
                    while (i < text.Length)
                    {
                        if (text[i] == '\\') { i += 2; continue; }
                        if (text[i] == '"') break;
                        i++;
                    }
                    continue;
                }
                if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') depth--;
            }
            return -1;
        }

        private static void ParsePairs(string block, bool isOffset, bool isExact, bool isPartial, bool isWords)
        {
            int p = 0;
            while (p < block.Length)
            {
                // Find start quote of key
                int keyStart = -1;
                while (p < block.Length)
                {
                    if (block[p] == '\"') { keyStart = p + 1; p++; break; }
                    p++;
                }
                if (keyStart < 0 || p >= block.Length) break;

                // Find closing quote of key (handling escaped quotes)
                StringBuilder sbKey = new StringBuilder();
                while (p < block.Length)
                {
                    if (block[p] == '\\' && p + 1 < block.Length)
                    {
                        char c = block[p + 1];
                        if (c == '"') sbKey.Append('"');
                        else if (c == '\\') sbKey.Append('\\');
                        else if (c == '/') sbKey.Append('/');
                        else if (c == 'b') sbKey.Append('\b');
                        else if (c == 'f') sbKey.Append('\f');
                        else if (c == 'n') sbKey.Append('\n');
                        else if (c == 'r') sbKey.Append('\r');
                        else if (c == 't') sbKey.Append('\t');
                        else if (c == 'u' && p + 5 < block.Length)
                        {
                            int code;
                            if (int.TryParse(block.Substring(p + 2, 4),
                                    NumberStyles.HexNumber,
                                    CultureInfo.InvariantCulture, out code))
                            {
                                sbKey.Append((char)code);
                                p += 6;
                                continue;
                            }
                            sbKey.Append('\\'); sbKey.Append(c);
                        }
                        else { sbKey.Append('\\'); sbKey.Append(c); }
                        p += 2;
                    }
                    else if (block[p] == '\"')
                    {
                        p++;
                        break;
                    }
                    else
                    {
                        sbKey.Append(block[p]);
                        p++;
                    }
                }
                string key = sbKey.ToString();

                // Skip until ':'
                while (p < block.Length && block[p] != ':') p++;
                if (p >= block.Length) break;
                p++; // skip ':'

                // Skip whitespace until value quote or open brace/bracket
                while (p < block.Length && block[p] != '\"' && block[p] != '{' && block[p] != '[') p++;
                if (p >= block.Length || block[p] != '\"') continue;
                p++; // skip open quote of value

                // Find closing quote of value
                StringBuilder sbVal = new StringBuilder();
                while (p < block.Length)
                {
                    if (block[p] == '\\' && p + 1 < block.Length)
                    {
                        char c = block[p + 1];
                        if (c == '"') sbVal.Append('"');
                        else if (c == '\\') sbVal.Append('\\');
                        else if (c == '/') sbVal.Append('/');
                        else if (c == 'b') sbVal.Append('\b');
                        else if (c == 'f') sbVal.Append('\f');
                        else if (c == 'n') sbVal.Append('\n');
                        else if (c == 'r') sbVal.Append('\r');
                        else if (c == 't') sbVal.Append('\t');
                        else if (c == 'u' && p + 5 < block.Length)
                        {
                            int code;
                            if (int.TryParse(block.Substring(p + 2, 4),
                                    NumberStyles.HexNumber,
                                    CultureInfo.InvariantCulture, out code))
                            {
                                sbVal.Append((char)code);
                                p += 6;
                                continue;
                            }
                            sbVal.Append('\\'); sbVal.Append(c);
                        }
                        else { sbVal.Append('\\'); sbVal.Append(c); }
                        p += 2;
                    }
                    else if (block[p] == '\"')
                    {
                        p++;
                        break;
                    }
                    else
                    {
                        sbVal.Append(block[p]);
                        p++;
                    }
                }
                string val = sbVal.ToString();

                if (key == "offsets" || key == "exact" || key == "partial" || key == "words") continue;

                if (isOffset)
                {
                    int off;
                    if (int.TryParse(key, out off))
                    {
                        offsetMap[off] = val;
                    }
                }
                else if (isExact)
                {
                    exactMap[key] = val;
                    string nKey = Regex.Replace(key.Trim(), @"\s+", " ");
                    if (nKey.Length > 0 && !normExactMap.ContainsKey(nKey))
                    {
                        normExactMap[nKey] = val;
                    }
                }
                else if (isPartial)
                {
                    partialMap[key] = val;
                }
                else if (isWords)
                {
                    wordsMap[key] = val;
                }
            }
        }

        private static readonly HashSet<string> liveAuditSeen = new HashSet<string>(StringComparer.Ordinal);
        private static readonly object auditLock = new object();
        private static string auditLogPath = "";

        private static void AuditUntranslatedLive(string raw)
        {
            try
            {
                if (string.IsNullOrEmpty(raw) || raw.Length < 2 || raw.Length > 300) return;
                if (ContainsChinese(raw)) return;
                if (raw.Contains("\\") || raw.Contains("://") || raw.Contains(".dll") || raw.Contains(".exe") || raw.Contains(".db3") || raw.Contains(".scen") || raw.Contains(".png")) return;

                bool hasAlpha = false;
                for (int i = 0; i < raw.Length; i++)
                {
                    if ((raw[i] >= 'A' && raw[i] <= 'Z') || (raw[i] >= 'a' && raw[i] <= 'z'))
                    {
                        hasAlpha = true;
                        break;
                    }
                }
                if (!hasAlpha) return;

                lock (auditLock)
                {
                    if (liveAuditSeen.Add(raw))
                    {
                        if (string.IsNullOrEmpty(auditLogPath))
                        {
                            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                            auditLogPath = Path.Combine(baseDir, "CMOLocalizer_LiveAudit.log");
                        }
                        File.AppendAllText(auditLogPath, raw.Replace("\r", "\\r").Replace("\n", "\\n") + "\r\n", Encoding.UTF8);
                    }
                }
            }
            catch { }
        }

        private static string JsonEsc(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }

        private static MenuStrip FindMainMenuStrip(out Form ownerForm)
        {
            ownerForm = null;
            FormCollection forms = Application.OpenForms;
            if (forms == null) return null;
            foreach (Form frm in forms)
            {
                if (frm == null || frm.IsDisposed) continue;
                if (frm.MainMenuStrip != null)
                {
                    ownerForm = frm;
                    return frm.MainMenuStrip;
                }
                foreach (Control c in frm.Controls)
                {
                    if (c is MenuStrip)
                    {
                        ownerForm = frm;
                        return (MenuStrip)c;
                    }
                }
            }
            return null;
        }

        private static void DumpMenuItemJson(ToolStripItem item, int idx, StringBuilder sb)
        {
            sb.Append("{\"index\":").Append(idx)
              .Append(",\"name\":\"").Append(JsonEsc(item.Name))
              .Append("\",\"text\":\"").Append(JsonEsc(item.Text))
              .Append("\",\"type\":\"").Append(JsonEsc(item.GetType().Name))
              .Append("\",\"enabled\":").Append(item.Enabled ? "true" : "false")
              .Append(",\"visible\":").Append(item.Visible ? "true" : "false");

            ToolStripDropDownItem ddi = item as ToolStripDropDownItem;
            if (ddi != null && ddi.DropDownItems != null && ddi.DropDownItems.Count > 0)
            {
                sb.Append(",\"children\":[");
                for (int i = 0; i < ddi.DropDownItems.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    DumpMenuItemJson(ddi.DropDownItems[i], i, sb);
                }
                sb.Append("]");
            }
            sb.Append("}");
        }

        private static ToolStripItem FindMenuItemByText(ToolStripItemCollection items, string target)
        {
            if (items == null) return null;
            foreach (ToolStripItem item in items)
            {
                if (item == null) continue;
                string cleanText = (item.Text ?? "").Replace("&", "").Trim();
                if (string.Equals(cleanText, target, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.Name, target, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
                ToolStripDropDownItem ddi = item as ToolStripDropDownItem;
                if (ddi != null && ddi.HasDropDownItems)
                {
                    ToolStripItem sub = FindMenuItemByText(ddi.DropDownItems, target);
                    if (sub != null) return sub;
                }
            }
            foreach (ToolStripItem item in items)
            {
                if (item == null) continue;
                string cleanText = (item.Text ?? "").Replace("&", "").Trim();
                if (cleanText.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return item;
                }
                ToolStripDropDownItem ddi = item as ToolStripDropDownItem;
                if (ddi != null && ddi.HasDropDownItems)
                {
                    ToolStripItem sub = FindMenuItemByText(ddi.DropDownItems, target);
                    if (sub != null) return sub;
                }
            }
            return null;
        }

        private static void CollectControlTexts(Control ctrl, List<string> list)
        {
            if (ctrl == null || ctrl.IsDisposed) return;
            if (!string.IsNullOrEmpty(ctrl.Text))
            {
                list.Add(ctrl.GetType().Name + ":" + ctrl.Name + "=" + ctrl.Text);
            }
            if (ctrl is TabControl)
            {
                foreach (TabPage tp in ((TabControl)ctrl).TabPages)
                {
                    list.Add("TabPage:" + tp.Name + "=" + tp.Text);
                    CollectControlTexts(tp, list);
                }
            }
            else if (ctrl is DataGridView)
            {
                foreach (DataGridViewColumn col in ((DataGridView)ctrl).Columns)
                {
                    list.Add("DGVCol:" + col.Name + "=" + col.HeaderText);
                }
            }
            else if (ctrl is ListView)
            {
                foreach (ColumnHeader ch in ((ListView)ctrl).Columns)
                {
                    list.Add("LVCol:" + ch.Text);
                }
            }
            else if (ctrl is ToolStrip)
            {
                foreach (ToolStripItem tsi in ((ToolStrip)ctrl).Items)
                {
                    list.Add("TSItem:" + tsi.Name + "=" + tsi.Text);
                }
            }
            foreach (Control child in ctrl.Controls)
            {
                CollectControlTexts(child, list);
            }
        }

        private static void ProcessTestCommands()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string cmdFile = Path.Combine(baseDir, "CMOLocalizer_Cmd.txt");
            if (!File.Exists(cmdFile)) return;

            string rawCmd = "";
            try
            {
                rawCmd = File.ReadAllText(cmdFile, Encoding.UTF8).Trim();
                File.Delete(cmdFile);
            }
            catch (Exception ex)
            {
                Log("ProcessTestCommands read error: " + ex.Message);
                return;
            }

            if (string.IsNullOrEmpty(rawCmd)) return;
            string resFile = Path.Combine(baseDir, "CMOLocalizer_CmdResult.json");

            try
            {
                string response = ExecuteTestCommand(rawCmd);
                File.WriteAllText(resFile, response, Encoding.UTF8);
                Log("ProcessTestCommands executed: " + rawCmd);
            }
            catch (Exception ex)
            {
                string err = "{\"status\":\"error\",\"message\":\"" + JsonEsc(ex.Message) + "\"}";
                try { File.WriteAllText(resFile, err, Encoding.UTF8); } catch { }
                Log("ProcessTestCommands exception: " + ex.ToString());
            }
        }

        private static string ExecuteTestCommand(string rawCmd)
        {
            string cmd = rawCmd;
            string arg = "";
            int sp = rawCmd.IndexOf(' ');
            if (sp > 0)
            {
                cmd = rawCmd.Substring(0, sp).Trim().ToUpperInvariant();
                arg = rawCmd.Substring(sp + 1).Trim();
            }
            else
            {
                cmd = rawCmd.Trim().ToUpperInvariant();
            }

            if (cmd == "RELOAD_DICT")
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string dictPath = Path.Combine(baseDir, "CMOLocalizer.json");
                if (File.Exists(dictPath))
                {
                    exactMap.Clear();
                    normExactMap.Clear();
                    lock (cacheLock) { dynamicCache.Clear(); }
                    LoadDictionaries(dictPath);
                    return "{\"status\":\"ok\",\"exact\":" + exactMap.Count + ",\"offsets\":" + offsetMap.Count + "}";
                }
                return "{\"status\":\"error\",\"message\":\"CMOLocalizer.json not found\"}";
            }

            Form mainFrm;
            MenuStrip ms = FindMainMenuStrip(out mainFrm);

            if (cmd == "DUMP_MENUS")
            {
                if (ms == null) return "{\"status\":\"error\",\"message\":\"MenuStrip not found\"}";
                StringBuilder sb = new StringBuilder();
                sb.Append("{\"status\":\"ok\",\"menus\":[");
                for (int i = 0; i < ms.Items.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    DumpMenuItemJson(ms.Items[i], i, sb);
                }
                sb.Append("]}");
                return sb.ToString();
            }

            if (cmd == "SHOW_MENU")
            {
                if (ms == null || mainFrm == null) return "{\"status\":\"error\",\"message\":\"MenuStrip not found\"}";
                int idx = int.Parse(arg);
                string menuText = "";
                mainFrm.BeginInvoke((MethodInvoker)delegate
                {
                    try
                    {
                        foreach (ToolStripItem it in ms.Items)
                        {
                            ToolStripDropDownItem d = it as ToolStripDropDownItem;
                            if (d != null) d.HideDropDown();
                        }
                        if (idx >= 0 && idx < ms.Items.Count)
                        {
                            ToolStripDropDownItem target = ms.Items[idx] as ToolStripDropDownItem;
                            if (target != null)
                            {
                                TranslateToolStripItem(target);
                                target.ShowDropDown();
                            }
                        }
                    }
                    catch { }
                });
                if (idx >= 0 && idx < ms.Items.Count) menuText = ms.Items[idx].Text;
                return "{\"status\":\"ok\",\"action\":\"SHOW_MENU\",\"index\":" + idx + ",\"text\":\"" + JsonEsc(menuText) + "\"}";
            }

            if (cmd == "SHOW_SUBMENU")
            {
                if (ms == null || mainFrm == null) return "{\"status\":\"error\",\"message\":\"MenuStrip not found\"}";
                string[] parts = arg.Split(':');
                int pIdx = int.Parse(parts[0]);
                int cIdx = int.Parse(parts[1]);
                string subText = "";
                if (pIdx >= 0 && pIdx < ms.Items.Count)
                {
                    ToolStripDropDownItem pItem = ms.Items[pIdx] as ToolStripDropDownItem;
                    if (pItem != null && cIdx >= 0 && cIdx < pItem.DropDownItems.Count)
                    {
                        subText = pItem.DropDownItems[cIdx].Text;
                    }
                }
                mainFrm.BeginInvoke((MethodInvoker)delegate
                {
                    try
                    {
                        foreach (ToolStripItem it in ms.Items)
                        {
                            ToolStripDropDownItem d = it as ToolStripDropDownItem;
                            if (d != null) d.HideDropDown();
                        }
                        if (pIdx >= 0 && pIdx < ms.Items.Count)
                        {
                            ToolStripDropDownItem pItem = ms.Items[pIdx] as ToolStripDropDownItem;
                            if (pItem != null)
                            {
                                TranslateToolStripItem(pItem);
                                pItem.ShowDropDown();
                                if (cIdx >= 0 && cIdx < pItem.DropDownItems.Count)
                                {
                                    ToolStripDropDownItem cItem = pItem.DropDownItems[cIdx] as ToolStripDropDownItem;
                                    if (cItem != null)
                                    {
                                        TranslateToolStripItem(cItem);
                                        cItem.Select();
                                        cItem.ShowDropDown();
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                });
                return "{\"status\":\"ok\",\"action\":\"SHOW_SUBMENU\",\"parent\":" + pIdx + ",\"child\":" + cIdx + ",\"text\":\"" + JsonEsc(subText) + "\"}";
            }

            if (cmd == "HIDE_MENUS")
            {
                if (ms != null && mainFrm != null)
                {
                    mainFrm.BeginInvoke((MethodInvoker)delegate
                    {
                        try
                        {
                            foreach (ToolStripItem it in ms.Items)
                            {
                                ToolStripDropDownItem d = it as ToolStripDropDownItem;
                                if (d != null) d.HideDropDown();
                            }
                        }
                        catch { }
                    });
                }
                return "{\"status\":\"ok\",\"action\":\"HIDE_MENUS\"}";
            }

            if (cmd == "CLICK_MENU")
            {
                if (ms == null || mainFrm == null) return "{\"status\":\"error\",\"message\":\"MenuStrip not found\"}";
                ToolStripItem found = FindMenuItemByText(ms.Items, arg);
                if (found == null) return "{\"status\":\"error\",\"message\":\"Menu item not found: " + JsonEsc(arg) + "\"}";
                string foundText = found.Text;
                string foundName = found.Name;
                mainFrm.BeginInvoke((MethodInvoker)delegate
                {
                    try { found.PerformClick(); } catch { }
                });
                return "{\"status\":\"ok\",\"action\":\"CLICK_MENU\",\"name\":\"" + JsonEsc(foundName) + "\",\"text\":\"" + JsonEsc(foundText) + "\"}";
            }

            if (cmd == "DUMP_OPEN_FORMS")
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("{\"status\":\"ok\",\"forms\":[");
                FormCollection forms = Application.OpenForms;
                bool first = true;
                if (forms != null)
                {
                    foreach (Form f in forms)
                    {
                        if (f == null || f.IsDisposed) continue;
                        if (!first) sb.Append(",");
                        first = false;
                        List<string> ctrls = new List<string>();
                        try { CollectControlTexts(f, ctrls); } catch { }
                        sb.Append("{\"name\":\"").Append(JsonEsc(f.Name))
                          .Append("\",\"type\":\"").Append(JsonEsc(f.GetType().FullName))
                          .Append("\",\"text\":\"").Append(JsonEsc(f.Text))
                          .Append("\",\"visible\":").Append(f.Visible ? "true" : "false")
                          .Append(",\"hwnd\":").Append(f.Handle.ToInt64())
                          .Append(",\"bounds\":\"").Append(f.Bounds.ToString())
                          .Append("\",\"controls\":[");
                        for (int i = 0; i < ctrls.Count && i < 200; i++)
                        {
                            if (i > 0) sb.Append(",");
                            sb.Append("\"").Append(JsonEsc(ctrls[i])).Append("\"");
                        }
                        sb.Append("]}");
                    }
                }
                sb.Append("]}");
                return sb.ToString();
            }

            if (cmd == "CLOSE_FORM")
            {
                FormCollection forms = Application.OpenForms;
                int closedCount = 0;
                if (forms != null)
                {
                    for (int i = forms.Count - 1; i >= 0; i--)
                    {
                        Form f = forms[i];
                        if (f == null || f.IsDisposed || f == mainFrm) continue;
                        if (arg == "ALL" || f.Name.IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0 || f.Text.IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            try
                            {
                                f.BeginInvoke((MethodInvoker)delegate { try { f.Close(); } catch { } });
                                closedCount++;
                            }
                            catch { }
                        }
                    }
                }
                return "{\"status\":\"ok\",\"closed\":" + closedCount + "}";
            }

            if (cmd == "CLICK_BUTTON")
            {
                FormCollection forms = Application.OpenForms;
                string clickedInfo = null;
                if (forms != null)
                {
                    foreach (Form f in forms)
                    {
                        if (f == null || f.IsDisposed || !f.Visible) continue;
                        Button btn = FindButtonByTextOrName(f, arg);
                        if (btn != null)
                        {
                            string bName = btn.Name;
                            string bText = btn.Text;
                            f.BeginInvoke((MethodInvoker)delegate
                            {
                                try { btn.PerformClick(); } catch { }
                            });
                            clickedInfo = f.Name + "." + bName + "(" + bText + ")";
                            break;
                        }
                        ToolStripItem tsi = FindToolStripItemInControl(f, arg);
                        if (tsi != null)
                        {
                            string tName = tsi.Name;
                            string tText = tsi.Text;
                            f.BeginInvoke((MethodInvoker)delegate
                            {
                                try { tsi.PerformClick(); } catch { }
                            });
                            clickedInfo = f.Name + ".ToolStripItem." + tName + "(" + tText + ")";
                            break;
                        }
                    }
                }
                if (clickedInfo != null)
                {
                    return "{\"status\":\"ok\",\"clicked\":\"" + JsonEsc(clickedInfo) + "\"}";
                }
                return "{\"status\":\"error\",\"message\":\"Button not found: " + JsonEsc(arg) + "\"}";
            }

            if (cmd == "RUN_LUA")
            {
                try
                {
                    Type tLua = null;
                    foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        tLua = a.GetType("Command_Core.Lua.LuaSandBox");
                        if (tLua != null) break;
                    }
                    if (tLua != null)
                    {
                        MethodInfo mSing = tLua.GetMethod("Singleton", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        object sandBox = mSing != null ? mSing.Invoke(null, null) : null;
                        if (sandBox != null)
                        {
                            MethodInfo mRun = tLua.GetMethod("RunScript", new Type[] { typeof(string), typeof(bool), typeof(string), typeof(string) });
                            if (mRun != null)
                            {
                                object res = mRun.Invoke(sandBox, new object[] { arg, true, "", "" });
                                return "{\"status\":\"ok\",\"action\":\"RUN_LUA\"}";
                            }
                        }
                    }
                    return "{\"status\":\"error\",\"message\":\"LuaSandBox not available\"}";
                }
                catch (Exception ex)
                {
                    return "{\"status\":\"error\",\"message\":\"" + JsonEsc(ex.Message) + "\"}";
                }
            }

            if (cmd == "CLICK_TOOLBAR")
            {
                int targetIdx = 0;
                int.TryParse(arg, out targetIdx);
                ToolStripItem tsi = FindToolbarButtonByIndex(mainFrm, targetIdx);
                if (tsi != null && mainFrm != null)
                {
                    mainFrm.BeginInvoke((MethodInvoker)delegate
                    {
                        try { tsi.PerformClick(); } catch { }
                    });
                    return "{\"status\":\"ok\",\"action\":\"CLICK_TOOLBAR\",\"index\":" + targetIdx + "}";
                }
                return "{\"status\":\"error\",\"message\":\"Toolbar button not found: " + arg + "\"}";
            }

            if (cmd == "DB_INSPECT" || cmd == "DB_SELECT_ROW")
            {
                Form dbForm = null;
                FormCollection forms = Application.OpenForms;
                if (forms != null)
                {
                    foreach (Form f in forms)
                    {
                        if (f != null && !f.IsDisposed && f.GetType().Name.IndexOf("InternalDBViewer", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            dbForm = f; break;
                        }
                    }
                }
                if (dbForm == null) return "{\"status\":\"error\",\"message\":\"InternalDBViewer not open\"}";

                FieldInfo fHtml = dbForm.GetType().GetField("RenderedHTMLString", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo fId = dbForm.GetType().GetField("SelectedObjectID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo fName = dbForm.GetType().GetField("SelectedObjectName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo fList = dbForm.GetType().GetField("dXkHcikCGK6", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo mDisplay = dbForm.GetType().GetMethod("DisplaySelection", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (cmd == "DB_SELECT_ROW")
                {
                    int row = 0;
                    int.TryParse(arg, out row);
                    dbForm.BeginInvoke((MethodInvoker)delegate
                    {
                        try
                        {
                            if (fList != null)
                            {
                                object lv = fList.GetValue(dbForm);
                                if (lv != null)
                                {
                                    PropertyInfo piSel = lv.GetType().GetProperty("SelectedIndices");
                                    if (piSel != null)
                                    {
                                        var selList = piSel.GetValue(lv, null) as System.Collections.IList;
                                        if (selList != null)
                                        {
                                            selList.Clear();
                                            selList.Add(row);
                                        }
                                    }
                                }
                            }
                            if (mDisplay != null) mDisplay.Invoke(dbForm, null);
                        }
                        catch (Exception ex) { Log("DB_SELECT_ROW error: " + ex.Message); }
                    });
                    return "{\"status\":\"ok\",\"action\":\"DB_SELECT_ROW\",\"row\":" + row + "}";
                }

                string html = fHtml != null ? fHtml.GetValue(dbForm) as string : "";
                int objId = fId != null ? (int)fId.GetValue(dbForm) : -1;
                string objName = fName != null ? fName.GetValue(dbForm) as string : "";
                return "{\"status\":\"ok\",\"id\":" + objId + ",\"name\":\"" + JsonEsc(objName) + "\",\"html_len\":" + (html != null ? html.Length : 0) + ",\"html_sample\":\"" + JsonEsc(html != null ? html.Substring(0, Math.Min(350, html.Length)) : "") + "\"}";
            }

            if (cmd == "DB_NAVIGATE_ID")
            {
                Form dbForm = null;
                FormCollection forms = Application.OpenForms;
                if (forms != null)
                {
                    foreach (Form f in forms)
                    {
                        if (f != null && !f.IsDisposed && f.GetType().Name.IndexOf("InternalDBViewer", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            dbForm = f; break;
                        }
                    }
                }
                if (dbForm == null) return "{\"status\":\"error\",\"message\":\"InternalDBViewer not open\"}";

                string[] parts = arg.Split(' ');
                string objType = parts.Length > 0 ? parts[0] : "Sensor";
                string objIdStr = parts.Length > 1 ? parts[1] : "1438";

                dbForm.BeginInvoke((MethodInvoker)delegate
                {
                    try
                    {
                        Control[] cbList = dbForm.Controls.Find("CB_ObjectType", true);
                        if (cbList.Length > 0 && cbList[0] is ComboBox)
                        {
                            ComboBox cb = (ComboBox)cbList[0];
                            for (int i = 0; i < cb.Items.Count; i++)
                            {
                                if (cb.Items[i].ToString().IndexOf(objType, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    cb.SelectedIndex = i;
                                    break;
                                }
                            }
                        }
                        Control[] tbList = dbForm.Controls.Find("tb_ID", true);
                        if (tbList.Length > 0)
                        {
                            tbList[0].Text = objIdStr;
                        }
                        Control[] btnList = dbForm.Controls.Find("btn_Go", true);
                        if (btnList.Length > 0 && btnList[0] is Button)
                        {
                            ((Button)btnList[0]).PerformClick();
                        }
                    }
                    catch (Exception ex) { Log("DB_NAVIGATE_ID error: " + ex.Message); }
                });
                return "{\"status\":\"ok\",\"action\":\"DB_NAVIGATE_ID\",\"type\":\"" + objType + "\",\"id\":\"" + objIdStr + "\"}";
            }

            if (cmd == "SCEN_EXPAND_ALL" || cmd == "SCEN_SELECT" || cmd == "SCEN_LOAD")
            {
                Form loadScenForm = null;
                Control dtvCtrl = null;
                Button btnLoad = null;

                FormCollection forms = Application.OpenForms;
                if (forms != null)
                {
                    foreach (Form f in forms)
                    {
                        if (f == null || f.IsDisposed) continue;
                        if (f.GetType().Name.IndexOf("LoadScenario", StringComparison.OrdinalIgnoreCase) >= 0 || f.Name.IndexOf("LoadScenario", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            loadScenForm = f;
                            dtvCtrl = FindControlByTypeOrName(f, "DarkTreeView");
                            btnLoad = FindControlByTypeOrName(f, "Button2") as Button;
                            break;
                        }
                    }
                }

                if (loadScenForm == null) return "{\"status\":\"error\",\"message\":\"LoadScenario window not open\"}";

                if (cmd == "SCEN_LOAD")
                {
                    if (btnLoad != null)
                    {
                        loadScenForm.BeginInvoke((MethodInvoker)delegate
                        {
                            try { btnLoad.PerformClick(); } catch { }
                        });
                        return "{\"status\":\"ok\",\"action\":\"SCEN_LOAD\"}";
                    }
                    return "{\"status\":\"error\",\"message\":\"Load button not found\"}";
                }

                if (dtvCtrl == null) return "{\"status\":\"error\",\"message\":\"DarkTreeView not found in LoadScenario\"}";

                string matchFound = null;
                loadScenForm.BeginInvoke((MethodInvoker)delegate
                {
                    try
                    {
                        TranslateDarkTreeView(dtvCtrl);
                        PropertyInfo piNodes = dtvCtrl.GetType().GetProperty("Nodes");
                        if (piNodes != null)
                        {
                            var nodes = piNodes.GetValue(dtvCtrl, null) as System.Collections.IEnumerable;
                            if (nodes != null)
                            {
                                if (cmd == "SCEN_EXPAND_ALL")
                                {
                                    ExpandAllDarkNodes(nodes);
                                    TranslateDarkTreeView(dtvCtrl);
                                    dtvCtrl.Invalidate();
                                }
                                else if (cmd == "SCEN_SELECT")
                                {
                                    matchFound = SelectDarkNodeRecursive(dtvCtrl, nodes, arg);
                                    TranslateDarkTreeView(dtvCtrl);
                                    dtvCtrl.Invalidate();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log("SCEN command error: " + ex.Message);
                    }
                });

                return "{\"status\":\"ok\",\"action\":\"" + cmd + "\",\"query\":\"" + JsonEsc(arg) + "\"}";
            }

            return "{\"status\":\"error\",\"message\":\"Unknown command: " + JsonEsc(cmd) + "\"}";
        }

        private static Control FindControlByTypeOrName(Control root, string target)
        {
            if (root == null || root.IsDisposed) return null;
            if (string.Equals(root.GetType().Name, target, StringComparison.OrdinalIgnoreCase)
                || string.Equals(root.Name, target, StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }
            foreach (Control child in root.Controls)
            {
                Control found = FindControlByTypeOrName(child, target);
                if (found != null) return found;
            }
            return null;
        }

        private static Button FindButtonByTextOrName(Control root, string target)
        {
            if (root == null || root.IsDisposed) return null;
            if (root is Button)
            {
                Button b = (Button)root;
                if ((b.Text != null && b.Text.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (b.Name != null && b.Name.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return b;
                }
            }
            foreach (Control child in root.Controls)
            {
                Button found = FindButtonByTextOrName(child, target);
                if (found != null) return found;
            }
            return null;
        }

        private static ToolStripItem FindToolStripItemInControl(Control root, string target)
        {
            if (root == null || root.IsDisposed) return null;
            if (root is ToolStrip)
            {
                ToolStrip ts = (ToolStrip)root;
                ToolStripItem found = FindMenuItemByText(ts.Items, target);
                if (found != null) return found;
            }
            foreach (Control child in root.Controls)
            {
                ToolStripItem found = FindToolStripItemInControl(child, target);
                if (found != null) return found;
            }
            return null;
        }

        private static ToolStripItem FindToolbarButtonByIndex(Control root, int targetBtnIndex)
        {
            if (root == null || root.IsDisposed) return null;
            if (root is ToolStrip && !(root is MenuStrip) && !(root is StatusStrip))
            {
                ToolStrip ts = (ToolStrip)root;
                int btnIdx = 0;
                for (int i = 0; i < ts.Items.Count; i++)
                {
                    if (ts.Items[i] is ToolStripButton)
                    {
                        if (btnIdx == targetBtnIndex) return ts.Items[i];
                        btnIdx++;
                    }
                }
            }
            foreach (Control child in root.Controls)
            {
                ToolStripItem found = FindToolbarButtonByIndex(child, targetBtnIndex);
                if (found != null) return found;
            }
            return null;
        }

        private static void ExpandAllDarkNodes(System.Collections.IEnumerable nodes)
        {
            if (nodes == null) return;
            foreach (object n in nodes)
            {
                if (n == null) continue;
                PropertyInfo piExp = n.GetType().GetProperty("Expanded");
                if (piExp != null) piExp.SetValue(n, true, null);
                PropertyInfo piCh = n.GetType().GetProperty("Nodes");
                if (piCh != null) ExpandAllDarkNodes(piCh.GetValue(n, null) as System.Collections.IEnumerable);
            }
        }

        private static string SelectDarkNodeRecursive(Control dtv, System.Collections.IEnumerable nodes, string target)
        {
            if (nodes == null || string.IsNullOrEmpty(target)) return null;
            foreach (object n in nodes)
            {
                if (n == null) continue;
                PropertyInfo piTxt = n.GetType().GetProperty("Text");
                string txt = piTxt != null ? piTxt.GetValue(n, null) as string : "";
                PropertyInfo piTag = n.GetType().GetProperty("Tag");
                string tag = piTag != null && piTag.GetValue(n, null) != null ? piTag.GetValue(n, null).ToString() : "";

                bool isMatch = (txt != null && txt.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0)
                            || (tag != null && tag.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0);

                if (isMatch)
                {
                    // Expand all parents
                    object parent = n;
                    while (parent != null)
                    {
                        PropertyInfo piP = parent.GetType().GetProperty("ParentNode");
                        parent = piP != null ? piP.GetValue(parent, null) : null;
                        if (parent != null)
                        {
                            PropertyInfo piExp = parent.GetType().GetProperty("Expanded");
                            if (piExp != null) piExp.SetValue(parent, true, null);
                        }
                    }

                    // Select this node
                    PropertyInfo piSel = dtv.GetType().GetProperty("SelectedNodes");
                    if (piSel != null)
                    {
                        object selList = piSel.GetValue(dtv, null);
                        if (selList != null)
                        {
                            MethodInfo miClear = selList.GetType().GetMethod("Clear");
                            MethodInfo miAdd = selList.GetType().GetMethod("Add");
                            if (miClear != null) miClear.Invoke(selList, null);
                            if (miAdd != null) miAdd.Invoke(selList, new object[] { n });
                        }
                    }
                    return txt;
                }

                PropertyInfo piCh = n.GetType().GetProperty("Nodes");
                if (piCh != null)
                {
                    string subMatch = SelectDarkNodeRecursive(dtv, piCh.GetValue(n, null) as System.Collections.IEnumerable, target);
                    if (subMatch != null)
                    {
                        PropertyInfo piExp = n.GetType().GetProperty("Expanded");
                        if (piExp != null) piExp.SetValue(n, true, null);
                        return subMatch;
                    }
                }
            }
            return null;
        }
    }
}
