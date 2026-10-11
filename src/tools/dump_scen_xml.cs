using System;
using System.IO;
using System.Reflection;

class Program {
    static void Main(string[] args) {
        if (args.Length < 1) return;
        string scenPath = args[0];
        string gameDir = @"G:\soft\steam\steamapps\common\Command - Modern Operations";
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

        object container = loadMethod.Invoke(null, new object[] { scenPath });
        string xml = (string)xmlMethod.Invoke(container, null);
        Console.WriteLine("Scenario loaded successfully! XML len: " + xml.Length);
        if (args.Length > 1) {
            File.WriteAllText(args[1], xml, System.Text.Encoding.UTF8);
            Console.WriteLine("Saved to: " + args[1]);
        }
    }
}
