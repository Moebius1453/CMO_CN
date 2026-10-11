using System;
using System.Reflection;
using System.IO;

class Program {
    static void Main() {
        string gameDir = @"G:\soft\steam\steamapps\common\Command - Modern Operations";
        Assembly asm = Assembly.LoadFrom(Path.Combine(gameDir, "Command.exe"));
        Type t = asm.GetType("o9njmt5wgUdejsVZIIuq", false);
        if (t == null) {
            foreach (Type x in asm.GetTypes()) {
                if (x.Name == "o9njmt5wgUdejsVZIIuq") { t = x; break; }
            }
        }
        Console.WriteLine("Type found: " + (t != null));
        if (t != null) {
            Console.WriteLine("FullName: " + t.FullName);
            Console.WriteLine("BaseType: " + t.BaseType);
            foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)) {
                if (m.Name.Contains("lFW55dwdZtB")) {
                    Console.WriteLine("Method: " + m.Name + " ret=" + m.ReturnType);
                    foreach (var p in m.GetParameters()) Console.WriteLine("  param: " + p.ParameterType.FullName + " " + p.Name);
                }
            }
            foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)) {
                Console.WriteLine("Field: " + f.FieldType.FullName + " " + f.Name);
            }
        }
    }
}
