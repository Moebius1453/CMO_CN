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

        private static void TranslateWpfElement(System.Windows.DependencyObject dep)
        {
            if (dep == null) return;
            try
            {
                if (dep is System.Windows.Controls.TextBlock)
                {
                    System.Windows.Controls.TextBlock tb = (System.Windows.Controls.TextBlock)dep;
                    if (!string.IsNullOrEmpty(tb.Text))
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
                }
                else if (dep is System.Windows.Controls.HeaderedContentControl)
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
            if (ContainsChinese(text)) return text;

            // Direct exact / normalized match via TranslateString
            string trans = TranslateString(text);
            if (trans != text) return trans;

            string norm = Regex.Replace(text.Trim(), @"\s+", " ");
            if (normExactMap.ContainsKey(norm)) return normExactMap[norm];

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
                }
            }
            catch { }
        }

        private static void TranslateToolStripItem(ToolStripItem item)
        {
            if (item == null) return;
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

        private static void TranslateControlText(Control ctrl)
        {
            string t = ctrl.Text;
            if (!string.IsNullOrEmpty(t))
            {
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
    }
}
