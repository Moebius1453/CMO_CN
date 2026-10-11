using System;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Text;
using System.Collections.Generic;

class Program {
    static string JsonEsc(string s) {
        if (s == null) return "";
        StringBuilder sb = new StringBuilder(s.Length + 20);
        for (int i = 0; i < s.Length; i++) {
            char c = s[i];
            switch (c) {
                case '\\': sb.Append("\\\\"); break;
                case '\"': sb.Append("\\\""); break;
                case '\r': sb.Append("\\r"); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 32) sb.AppendFormat("\\u{0:x4}", (int)c);
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    static void Main(string[] args) {
        string gameDir = @"G:\soft\steam\steamapps\common\Command - Modern Operations";
        string tutDir = Path.Combine(gameDir, @"Scenarios\Tutorials");
        AppDomain.CurrentDomain.AssemblyResolve += (s, a) => {
            string name = new AssemblyName(a.Name).Name;
            string p1 = Path.Combine(gameDir, name + ".dll");
            if (File.Exists(p1)) return Assembly.LoadFrom(p1);
            return null;
        };

        Assembly asm = Assembly.LoadFrom(Path.Combine(gameDir, "Command.exe"));
        Type tScen = asm.GetType("Command_Core.ScenContainer");
        MethodInfo loadMethod = tScen.GetMethod("LoadFromFile", BindingFlags.Public | BindingFlags.Static);
        MethodInfo xmlMethod = tScen.GetMethod("GetScenarioObject_AsXML", BindingFlags.Public | BindingFlags.Instance);

        string[] files = Directory.GetFiles(tutDir, "*.scen", SearchOption.AllDirectories);
        Array.Sort(files);

        StringBuilder sb = new StringBuilder();
        sb.Append("[\n");
        for (int i = 0; i < files.Length; i++) {
            string f = files[i];
            string rel = f.Substring(tutDir.Length + 1).Replace("\\", "/");
            Console.WriteLine("[" + (i+1) + "/" + files.Length + "] " + rel);

            string outerTitle = "", outerDesc = "", outerSetting = "";
            try {
                XmlDocument outerDoc = new XmlDocument();
                outerDoc.Load(f);
                XmlNode nTitle = outerDoc.SelectSingleNode("//ScenTitle");
                XmlNode nDesc = outerDoc.SelectSingleNode("//ScenDescription");
                XmlNode nSet = outerDoc.SelectSingleNode("//ScenSetting");
                if (nTitle != null) outerTitle = nTitle.InnerText;
                if (nDesc != null) outerDesc = nDesc.InnerText;
                if (nSet != null) outerSetting = nSet.InnerText;
            } catch {}

            string innerDesc = "";
            List<string> briefings = new List<string>();
            List<string> messages = new List<string>();

            try {
                object container = loadMethod.Invoke(null, new object[] { f });
                string innerXml = (string)xmlMethod.Invoke(container, null);
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(innerXml);
                XmlNode nIDesc = doc.SelectSingleNode("/Scenario/Description");
                if (nIDesc != null) innerDesc = nIDesc.InnerText;

                XmlNodeList sideNodes = doc.SelectNodes("/Scenario/Sides/Side");
                if (sideNodes != null) {
                    foreach (XmlNode sn in sideNodes) {
                        XmlNode bName = sn.SelectSingleNode("Name");
                        XmlNode bText = sn.SelectSingleNode("Briefing");
                        if (bText != null && !string.IsNullOrEmpty(bText.InnerText)) {
                            briefings.Add((bName != null ? bName.InnerText : "") + "|||" + bText.InnerText);
                        }
                    }
                }

                XmlNodeList msgNodes = doc.SelectNodes("//EventAction_Message");
                if (msgNodes != null) {
                    foreach (XmlNode mn in msgNodes) {
                        XmlNode mDesc = mn.SelectSingleNode("Description");
                        XmlNode mText = mn.SelectSingleNode("Text");
                        if (mText != null && !string.IsNullOrEmpty(mText.InnerText)) {
                            messages.Add((mDesc != null ? mDesc.InnerText : "") + "|||" + mText.InnerText);
                        }
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine("  Error: " + ex.Message);
            }

            if (i > 0) sb.Append(",\n");
            sb.Append("  {\n");
            sb.Append("    \"file\": \"" + JsonEsc(rel) + "\",\n");
            sb.Append("    \"outerTitle\": \"" + JsonEsc(outerTitle) + "\",\n");
            sb.Append("    \"outerSetting\": \"" + JsonEsc(outerSetting) + "\",\n");
            sb.Append("    \"outerDesc\": \"" + JsonEsc(outerDesc) + "\",\n");
            sb.Append("    \"innerDesc\": \"" + JsonEsc(innerDesc) + "\",\n");
            sb.Append("    \"briefings\": [");
            for (int j = 0; j < briefings.Count; j++) {
                if (j > 0) sb.Append(", ");
                sb.Append("\"" + JsonEsc(briefings[j]) + "\"");
            }
            sb.Append("],\n");
            sb.Append("    \"messages\": [");
            for (int j = 0; j < messages.Count; j++) {
                if (j > 0) sb.Append(", ");
                sb.Append("\"" + JsonEsc(messages[j]) + "\"");
            }
            sb.Append("]\n");
            sb.Append("  }");
        }
        sb.Append("\n]\n");
        File.WriteAllText(@"build\all_tutorials_dump.json", sb.ToString(), Encoding.UTF8);
        Console.WriteLine("Saved build\all_tutorials_dump.json");
    }
}
