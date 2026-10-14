using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.IO;

class Program {
    static Dictionary<short, OpCode> ops = new Dictionary<short, OpCode>();
    static Dictionary<int, string> tokens = new Dictionary<int, string>();

    static void BuildOps() {
        foreach (FieldInfo f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)) {
            OpCode op = (OpCode)f.GetValue(null);
            ops[op.Value] = op;
        }
    }

    static void Main() {
        BuildOps();
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
        foreach (Module mod in asm.GetModules()) {
            try {
                foreach (Type t in mod.GetTypes()) {
                    tokens[t.MetadataToken] = t.FullName;
                    foreach (MemberInfo mi in t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)) {
                        try { tokens[mi.MetadataToken] = t.FullName + "::" + mi.Name; } catch { }
                    }
                }
            } catch { }
        }

        Type combo = asm.GetType("Command.DarkUIComboBox");
        foreach (string target in new string[] { "OnPaint", "OnDrawItem", "AlternateOnPaint" }) {
            MethodInfo m = combo.GetMethod(target, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Console.WriteLine("================ " + target + " ================");
            Dump(m);
        }
    }

    static void Dump(MethodInfo m) {
        MethodBody mb = m.GetMethodBody();
        if (mb == null) { Console.WriteLine("no body"); return; }
        byte[] il = mb.GetILAsByteArray();
        int i = 0;
        while (i < il.Length) {
            int start = i;
            short code;
            byte b = il[i++];
            if (b == 0xfe) { code = (short)(0xfe00 | il[i++]); } else { code = b; }
            OpCode op;
            if (!ops.TryGetValue(code, out op)) { Console.WriteLine("  ??? " + code.ToString("x")); break; }
            int tok = 0;
            switch (op.OperandType) {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineI: i += 1; break;
                case OperandType.ShortInlineVar: i += 1; break;
                case OperandType.InlineVar: i += 2; break;
                case OperandType.InlineI: case OperandType.InlineBrTarget: case OperandType.InlineField: case OperandType.InlineMethod: case OperandType.InlineSig: case OperandType.InlineString: case OperandType.InlineTok: case OperandType.InlineType:
                    tok = BitConverter.ToInt32(il, i); i += 4; break;
                case OperandType.InlineI8: i += 8; break;
                case OperandType.ShortInlineR: i += 4; break;
                case OperandType.InlineR: i += 8; break;
                case OperandType.InlineSwitch: int n = BitConverter.ToInt32(il, i); i += 4 + n * 4; break;
                default: i += 4; break;
            }
            string extra = "";
            if (tok != 0) {
                string nm;
                if (tokens.TryGetValue(tok, out nm)) extra = nm;
                else extra = "tok:" + tok.ToString("x8");
            }
            Console.WriteLine("  " + start.ToString("x4") + "  " + op.Name + " " + extra);
        }
    }
}
