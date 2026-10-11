using System;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

class Program {
    static void Main() {
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
        Type tSide = asm.GetType("Command_Core.Side");
        Type tScenObj = asm.GetType("Command_Core.ScenarioObject");
        Type tScenario = asm.GetType("Command_Core.Scenario");

        MethodInfo loadMethod = tScen.GetMethod("LoadFromFile", BindingFlags.Public | BindingFlags.Static);
        MethodInfo xmlMethod = tScen.GetMethod("GetScenarioObject_AsXML", BindingFlags.Public | BindingFlags.Instance);

        MethodInfo mFromXml = null;
        foreach (MethodInfo m in tSide.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)) {
            if (m.Name == "FromXML" && m.GetParameters().Length == 4) {
                mFromXml = m; break;
            }
        }

        Type dictType = typeof(ConcurrentDictionary<,>).MakeGenericType(typeof(string), tScenObj);

        string[] files = Directory.GetFiles(tutDir, "*.scen", SearchOption.AllDirectories);
        Array.Sort(files);

        int countEncrypted = 0;
        using (StreamWriter sw = new StreamWriter(@"build\encrypted_briefings_dump.tsv", false, Encoding.UTF8)) {
            foreach (string f in files) {
                string rel = f.Substring(tutDir.Length + 1).Replace("\\", "/");
                try {
                    object container = loadMethod.Invoke(null, new object[] { f });
                    string innerXml = (string)xmlMethod.Invoke(container, null);
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(innerXml);
                    XmlNodeList sideNodes = doc.GetElementsByTagName("Side");
                    if (sideNodes != null) {
                        foreach (XmlNode sn in sideNodes) {
                            XmlNode encNode = sn.SelectSingleNode("Briefing_Encrypted");
                            if (encNode != null && !string.IsNullOrEmpty(encNode.InnerText)) {
                                XmlNode nameNode = sn.SelectSingleNode("Name");
                                string sName = nameNode != null ? nameNode.InnerText : "";

                                XmlDocument minDoc = new XmlDocument();
                                XmlElement minSide = minDoc.CreateElement("Side");
                                foreach (XmlNode ch in sn.ChildNodes) {
                                    if (ch.Name == "ID" || ch.Name == "Name" || ch.Name == "Briefing_Encrypted" || ch.Name == "Briefing") {
                                        minSide.AppendChild(minDoc.ImportNode(ch, true));
                                    }
                                }
                                minDoc.AppendChild(minSide);

                                object theDict = Activator.CreateInstance(dictType);
                                object dummyScen = Activator.CreateInstance(tScenario, true);
                                object[] args = new object[] { minSide, dummyScen, theDict, null };

                                object side = mFromXml.Invoke(null, args);
                                if (side != null) {
                                    PropertyInfo pBrief = side.GetType().GetProperty("Briefing", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    string plainBrief = (string)pBrief.GetValue(side, null);
                                    if (!string.IsNullOrEmpty(plainBrief)) {
                                        countEncrypted++;
                                        string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(plainBrief));
                                        sw.WriteLine(rel + "\t" + sName + "\t" + b64);
                                        Console.WriteLine("Decrypted Briefing for [" + rel + "] Side: " + sName + " (" + plainBrief.Length + " chars)");
                                    }
                                }
                            }
                        }
                    }
                } catch (Exception ex) {
                    Console.WriteLine("Error in " + rel + ": " + ex.Message);
                }
            }
        }
        Console.WriteLine("Total decrypted encrypted briefings: " + countEncrypted);
    }
}
