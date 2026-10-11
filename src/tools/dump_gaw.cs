using System;
using System.Reflection;
using System.IO;

class Program {
    static void Main() {
        string gameDir = @"G:\soft\steam\steamapps\common\Command - Modern Operations";
        Assembly asm = Assembly.LoadFrom(Path.Combine(gameDir, "Command.exe"));
        Module mod = asm.GetModules()[0];
        Type tDb = asm.GetType("Command_Core.DAL.DBFunctions");
        foreach (MethodInfo m in tDb.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)) {
            if (m.Name == "GetAllWeapons" || m.Name == "EW_GetAllUnitsMatchingTheseEmissions") {
                var body = m.GetMethodBody();
                byte[] il = body.GetILAsByteArray();
                Console.WriteLine(m.Name + " (" + m.GetParameters().Length + " params) IL len=" + il.Length);
                for (int i = 0; i < il.Length; i++) {
                    Console.Write(il[i].ToString("X2") + " ");
                    if ((i + 1) % 24 == 0) Console.WriteLine();
                }
                Console.WriteLine("\n---");
            }
        }
    }
}
