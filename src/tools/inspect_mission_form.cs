using System;
using System.Reflection;
using System.IO;
using System.Windows.Forms;

class Program {
    static void Main() {
        string gameDir = @"G:\soft\steam\steamapps\common\Command - Modern Operations";
        AppDomain.CurrentDomain.AssemblyResolve += (s, a) => {
            string name = new AssemblyName(a.Name).Name;
            string p1 = Path.Combine(gameDir, name + ".dll");
            if (File.Exists(p1)) return Assembly.LoadFrom(p1);
            return null;
        };

        Assembly asm = Assembly.LoadFrom(Path.Combine(gameDir, "Command.exe"));
        foreach (Type t in asm.GetTypes()) {
            if (t.Name.IndexOf("NewMission", StringComparison.OrdinalIgnoreCase) >= 0 ||
                t.Name.IndexOf("AddMission", StringComparison.OrdinalIgnoreCase) >= 0) {
                Console.WriteLine("Type: " + t.FullName + " (Base: " + (t.BaseType != null ? t.BaseType.FullName : "none") + ")");
                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                    if (typeof(Control).IsAssignableFrom(f.FieldType)) {
                        Console.WriteLine("  Field: " + f.Name + " : " + f.FieldType.FullName);
                    }
                }
                foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                    if (m.Name.Contains("Category") || m.Name.Contains("SelectedIndex") || m.Name.Contains("Load")) {
                        Console.WriteLine("  Method: " + m.Name);
                    }
                }
            }
        }
    }
}
