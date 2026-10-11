using System;
using System.Reflection;
using System.IO;

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
        Module mod = asm.GetModules()[0];

        Type tStr = asm.GetType("AoIBWWlDJbaf7LijnA.oMu6jVbdhHEH79DDhU");
        FieldInfo fDebug = tStr.GetField("gTbYFnJa7EN", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (fDebug != null) fDebug.SetValue(null, 100);
        MethodInfo decryptMethod = tStr.GetMethod("iX38Xvt6D", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        Type tDb = asm.GetType("Command_Core.DAL.DBFunctions");
        foreach (string mName in new[] { "GetAllWeapons", "EW_GetAllUnitsMatchingTheseEmissions", "EW_GetAllPossibleEmissionsForUnits" }) {
            foreach (MethodInfo m in tDb.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)) {
                if (!m.Name.Contains(mName)) continue;
                Console.WriteLine("=== Method: " + m.Name + " ===");
                MethodBody body = m.GetMethodBody();
                if (body == null) continue;
                byte[] il = body.GetILAsByteArray();
                for (int i = 0; i < il.Length; i++) {
                    if (il[i] == 0x20 && i + 4 < il.Length) {
                        int val = BitConverter.ToInt32(il, i + 1);
                        if (val >= 0 && val < 2500000) {
                            string s = "";
                            try { s = (string)decryptMethod.Invoke(null, new object[] { val }); } catch {}
                            if (!string.IsNullOrEmpty(s)) {
                                Console.WriteLine("  ldc.i4 " + val + " -> " + s.Replace("\r", "\\r").Replace("\n", "\\n"));
                            }
                        }
                    }
                    if ((il[i] == 0x28 || il[i] == 0x6F || il[i] == 0x73) && i + 4 < il.Length) {
                        int tok = BitConverter.ToInt32(il, i + 1);
                        try {
                            MethodBase mb = mod.ResolveMethod(tok);
                            if (mb != null) {
                                Console.WriteLine("  call " + (mb.DeclaringType != null ? mb.DeclaringType.FullName : "") + "." + mb.Name);
                            }
                        } catch {}
                    }
                }
            }
        }
    }
}
