using System;
using System.Reflection;
using System.IO;

class Program {
    static void Main() {
        string gameDir = @"G:\soft\steam\steamapps\common\Command - Modern Operations";
        AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += (s, a) => {
            string req = a.Name;
            try {
                string name = new AssemblyName(req).Name;
                string p1 = Path.Combine(gameDir, name + ".dll");
                if (File.Exists(p1)) return Assembly.ReflectionOnlyLoadFrom(p1);
            } catch { }
            try { return Assembly.ReflectionOnlyLoad(req); } catch { }
            return null;
        };
        Assembly asm = Assembly.ReflectionOnlyLoadFrom(Path.Combine(gameDir, "Command.exe"));
        Type t = asm.GetType("Command.DarkUIComboBox");
        Console.WriteLine("Type: " + t.FullName + " base=" + t.BaseType.FullName);
        Console.WriteLine("--- declared members ---");
        foreach (MemberInfo m in t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)) {
            MethodInfo mi = m as MethodInfo;
            if (mi != null) {
                Console.WriteLine("  M " + (mi.IsPublic ? "pub" : mi.IsFamily ? "prot" : mi.IsPrivate ? "priv" : "asm") + " " + mi.ReturnType.Name + " " + mi.Name + "/" + mi.GetParameters().Length);
            } else if (m is FieldInfo) {
                Console.WriteLine("  F " + ((FieldInfo)m).FieldType.Name + " " + m.Name);
            } else if (m is PropertyInfo) {
                Console.WriteLine("  P " + ((PropertyInfo)m).PropertyType.Name + " " + m.Name);
            } else if (m is EventInfo) {
                Console.WriteLine("  E " + m.Name);
            } else {
                Console.WriteLine("  ? " + m.MemberType + " " + m.Name);
            }
        }
    }
}
