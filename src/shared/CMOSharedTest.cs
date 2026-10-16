// CMOLocalizer 共享状态层自测宿主（测试专用，不进入安装包/绿色包）
//
// 用法：bash scripts/build.sh && build/CMOSharedTest.exe
// 输出：每项一行 PASS/FAIL，退出码 = 失败项数量（0 = 全绿）。
//
// 所有断言都在系统临时目录内完成：CmoPaths 的 Roaming/Local/RegistryPath 全部被覆盖，
// 因此不会碰用户的真实设置文件，也不会碰 HKCU\Software\CMOChinese。
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CMOShared;

internal static class CMOSharedSelfTest
{
    private static int failures;
    private static string root;

    private static void Check(string name, bool ok, string reason)
    {
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (ok ? "" : " -- " + reason));
        if (!ok) failures++;
    }

    private static int Main()
    {
        root = Path.Combine(Path.GetTempPath(), "cmoshared-selftest-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        CmoPaths.RoamingDirOverride = Path.Combine(root, "roaming");
        CmoPaths.LocalDirOverride = Path.Combine(root, "local");
        CmoPaths.RegistryPathOverride = @"Software\CMOChinese__selftest";
        CmoPaths.EnsureDir(CmoPaths.RoamingDirOverride);
        CmoPaths.EnsureDir(CmoPaths.LocalDirOverride);

        try
        {
            SettingsRoundTrip();
            SettingsCorruptedFile();
            SettingsRegistryMigration();
            ManifestRoundTrip();
            ManifestModifiedAndClear();
            ManifestAdopt();
            PackageCachePruneAndPrevious();
            PackageVersionFromRealPackage();
            ReleaseGateTruthTable();
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("FAIL selftest-harness -- " + ex);
        }
        finally
        {
            try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(CmoPaths.RegistryPathOverride, false); } catch { }
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }

        Console.WriteLine(failures == 0
            ? "CMOSharedTest: ALL PASS"
            : "CMOSharedTest: " + failures + " FAILED");
        return failures;
    }

    // 1. 设置原子写往返（含首次写：目标不存在时必须走 File.Move 分支）
    private static void SettingsRoundTrip()
    {
        CmoSettings s = CmoSettings.Load();
        Check("settings-defaults", s.CheckIntervalHours == 6 && s.Mirror == "https://ghproxy.net/" && s.SnapshotKeep == 2,
            "unexpected defaults: interval=" + s.CheckIntervalHours + " mirror=" + s.Mirror);
        // 没有注册表值也没有文件时 Load() 不得凭空写文件（避免污染新用户）
        Check("settings-no-write-on-empty-load", !File.Exists(CmoPaths.SettingsFile),
            "Load() wrote settings.json without any source");

        s.CheckIntervalHours = 3;
        s.NotifyMode = 0;
        s.TrayOnClose = false;
        s.GamePath = @"G:\soft\steam\steamapps\common\Command - Modern Operations";
        s.Save();   // 首次写：目标不存在 → File.Move 分支
        Check("settings-file-created", File.Exists(CmoPaths.SettingsFile), "settings.json not created at " + CmoPaths.SettingsFile);
        Check("settings-no-tmp-left", !File.Exists(CmoPaths.SettingsFile + ".tmp"), "leftover .tmp file");

        CmoSettings back = CmoSettings.Load();
        Check("settings-round-trip",
            back.CheckIntervalHours == 3 && back.NotifyMode == 0 && !back.TrayOnClose && back.GamePath == s.GamePath,
            "read back: interval=" + back.CheckIntervalHours + " notify=" + back.NotifyMode
            + " tray=" + back.TrayOnClose + " game=" + back.GamePath);

        back.CheckIntervalHours = 5;
        back.Save();   // 第二次写：目标已存在 → File.Replace 分支
        Check("settings-rewrite-existing", CmoSettings.Load().CheckIntervalHours == 5,
            "File.Replace 分支未能覆盖已有设置文件");
    }

    // 2. 损坏文件 → 备份为 .bad + 默认值
    private static void SettingsCorruptedFile()
    {
        string path = CmoPaths.SettingsFile;
        File.WriteAllText(path, "{ this is not json ", new UTF8Encoding(false));
        CmoSettings s = CmoSettings.Load();
        Check("settings-corrupt-fallback", s.CheckIntervalHours == 6 && s.CheckIntervalHours != 3,
            "corrupted file was not replaced by defaults (interval=" + s.CheckIntervalHours + ")");
        Check("settings-corrupt-backup", File.Exists(path + ".bad"), "expected backup at " + path + ".bad");
    }

    // 3. 旧注册表值一次性迁移
    private static void SettingsRegistryMigration()
    {
        try { File.Delete(CmoPaths.SettingsFile); } catch { }
        using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(CmoPaths.RegistryPathOverride))
        {
            k.SetValue("AutoCheck", 0, Microsoft.Win32.RegistryValueKind.DWord);
            k.SetValue("CheckIntervalHours", 12, Microsoft.Win32.RegistryValueKind.DWord);
            k.SetValue("NotifyMode", 2, Microsoft.Win32.RegistryValueKind.DWord);
            k.SetValue("Mirror", "https://mirror.example/", Microsoft.Win32.RegistryValueKind.String);
            k.SetValue("GamePath", @"D:\games\CMO", Microsoft.Win32.RegistryValueKind.String);
        }
        CmoSettings s = CmoSettings.Load();
        Check("settings-registry-migration",
            !s.AutoCheck && s.CheckIntervalHours == 12 && s.NotifyMode == 2
            && s.Mirror == "https://mirror.example/" && s.GamePath == @"D:\games\CMO",
            "migrated: auto=" + s.AutoCheck + " interval=" + s.CheckIntervalHours + " notify=" + s.NotifyMode
            + " mirror=" + s.Mirror + " game=" + s.GamePath);
        Check("settings-migration-persisted", File.Exists(CmoPaths.SettingsFile), "migration did not write settings.json");
    }

    // 4. 清单往返
    private static void ManifestRoundTrip()
    {
        string game = Game("round");
        WriteEngineFiles(game, "dll-A", "json-A");
        bool w = InstallManifest.Write(game, "4.1.6", "CMO_Chinese_Setup_v4.1.6.exe", InstallManifest.HashEngineFiles(game));
        InstallManifest m = InstallManifest.Read(game);
        Check("manifest-write-read", w && m != null && m.Version == "4.1.6"
            && m.Files.Count == 2 && m.Files.ContainsKey("CMOLocalizer.dll"),
            "manifest=" + (m == null ? "null" : m.Version + " files=" + m.Files.Count));
        Check("manifest-installed-at", m != null && m.InstalledAt.Length > 10, "installed_at missing");
        string detail;
        Check("manifest-verify-ok", InstallManifest.Verify(game, out detail) == ManifestState.Ok, detail);
    }

    // 5. 文件被改写 → Modified；Clear → NotInstalled
    private static void ManifestModifiedAndClear()
    {
        string game = Game("modified");
        WriteEngineFiles(game, "dll-A", "json-A");
        InstallManifest.Write(game, "4.1.6", "pkg", InstallManifest.HashEngineFiles(game));
        File.WriteAllText(Path.Combine(game, "CMOLocalizer.dll"), "dll-CHANGED");
        string detail;
        Check("manifest-verify-modified",
            InstallManifest.Verify(game, out detail) == ManifestState.Modified && detail.Contains("CMOLocalizer.dll"),
            "expected Modified(CMOLocalizer.dll), got " + detail);
        InstallManifest.Clear(game);
        Check("manifest-clear",
            InstallManifest.Verify(game, out detail) == ManifestState.NotInstalled && InstallManifest.Read(game) == null,
            "after Clear: " + detail);
    }

    // 6. adopt 自愈（存量 4.1.x / 绿色包用户）
    private static void ManifestAdopt()
    {
        string game = Game("adopt");
        WriteEngineFiles(game, "dll-A", "json-A");
        bool ok = InstallManifest.Adopt(game, "4.1.5");
        InstallManifest m = InstallManifest.Read(game);
        Check("manifest-adopt", ok && m != null && m.Version == "4.1.5" && m.Source == "adopted",
            "adopt=" + ok + " manifest=" + (m == null ? "null" : m.Version + "/" + m.Source));

        string empty = Game("adopt-empty");
        Check("manifest-adopt-requires-engine", !InstallManifest.Adopt(empty, "4.1.5"),
            "adopt must fail when engine files are absent");
    }

    // 7. 安装包缓存：Prune 保留最近 N 个、Previous 取正确上一版本
    private static void PackageCachePruneAndPrevious()
    {
        string src = Path.Combine(root, "src-pkgs");
        CmoPaths.EnsureDir(src);
        foreach (string v in new string[] { "4.1.4", "4.1.5", "4.1.6" })
        {
            File.WriteAllText(Path.Combine(src, "CMO_Chinese_Setup_v" + v + ".exe"), "package-" + v);
            VersionStore.Cache(Path.Combine(src, "CMO_Chinese_Setup_v" + v + ".exe"));
        }
        List<string> all = VersionStore.CachedVersions();
        Check("cache-all-versions", all.Count == 3 && all[0] == "4.1.6" && all[2] == "4.1.4",
            "cached=[" + string.Join(",", all.ToArray()) + "]");

        VersionStore.Prune(2);
        List<string> kept = VersionStore.CachedVersions();
        Check("cache-prune-keeps-latest", kept.Count == 2 && kept[0] == "4.1.6" && kept[1] == "4.1.5",
            "after prune(2)=[" + string.Join(",", kept.ToArray()) + "]");
        Check("cache-previous", VersionStore.Previous("4.1.6") == "4.1.5" && VersionStore.Previous("4.1.4") == null
            && VersionStore.Previous("") == "4.1.6",
            "previous(4.1.6)=" + VersionStore.Previous("4.1.6") + " previous(4.1.4)=" + VersionStore.Previous("4.1.4"));
        Check("cache-package-path", VersionStore.CachedPackage("4.1.5") != null && VersionStore.CachedPackage("9.9.9") == null,
            "CachedPackage lookup broken");
    }

    // 7.5 真实安装包的版本戳：必须来自内嵌 CMOLocalizer.json（文件名不可信）
    private static void PackageVersionFromRealPackage()
    {
        string buildDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "build"));
        string[] pkgs = Directory.Exists(buildDir)
            ? Directory.GetFiles(buildDir, "CMO_Chinese_Setup_v*.exe")
            : new string[0];
        if (pkgs.Length == 0)
        {
            Check("pkgversion-real-package", false, "no build/CMO_Chinese_Setup_v*.exe found -- run bash scripts/build.sh first");
            return;
        }
        // 本 API 的不变量：读出的必须是安装包内嵌词典的 _meta.version（资产自带版本戳）。
        // “词典版本戳 == VERSION”由 scripts/verify_all.py 的版本门禁负责（T3）。
        string dictJson = Path.Combine(buildDir, "..", "dict", "CMOLocalizer.json");
        if (File.Exists(dictJson))
        {
            using (FileStream fs = File.OpenRead(dictJson))
            {
                byte[] head = new byte[1024];
                int n = fs.Read(head, 0, head.Length);
                Match dm = Regex.Match(Encoding.UTF8.GetString(head, 0, n), "\"version\"\\s*:\\s*\"([^\"]+)\"");
                if (dm.Success) expectedDict = dm.Groups[1].Value.Trim();
            }
        }
        // 取“刚构建的那个”（按修改时间最新）
        Array.Sort(pkgs, delegate(string a, string b)
        {
            return File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a));
        });
        string chosen = pkgs[0];
        string expected = VersionOfExpectedName();
        Check("pkgversion-real-package", expected.Length > 0
            && string.Equals(VersionStore.PackageVersion(chosen), expected, StringComparison.OrdinalIgnoreCase),
            "PackageVersion(" + Path.GetFileName(chosen) + ")=" + VersionStore.PackageVersion(chosen) + " but VERSION=" + expected);

        // 文件名无版本号时，回退用内嵌词典的 _meta.version
        string stripped = Path.Combine(root, "stripped");
        CmoPaths.EnsureDir(stripped);
        string noNameVer = Path.Combine(stripped, "CMO_Chinese_Setup_custom.exe");
        File.Copy(chosen, noNameVer, true);
        Check("pkgversion-content-fallback",
            expectedDict.Length > 0 && string.Equals(VersionStore.PackageVersion(noNameVer), expectedDict, StringComparison.OrdinalIgnoreCase),
            "content fallback=" + VersionStore.PackageVersion(noNameVer) + " but dict _meta.version=" + expectedDict);

        // 文件名带假版本号 → 以文件名为准（发布资产以 VERSION 命名）
        string fakeDir = Path.Combine(root, "misnamed");
        CmoPaths.EnsureDir(fakeDir);
        string misnamed = Path.Combine(fakeDir, "CMO_Chinese_Setup_v9.9.9.exe");
        File.Copy(chosen, misnamed, true);
        Check("pkgversion-filename-wins",
            VersionStore.PackageVersion(misnamed) == "9.9.9" && VersionStore.VersionOfPackage(misnamed) == "9.9.9",
            "name version=" + VersionStore.PackageVersion(misnamed));
    }

    private static string expectedDict = "";

    private static string VersionOfExpectedName()
    {
        string vf = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "VERSION");
        return File.Exists(vf) ? File.ReadAllText(vf).Trim() : "";
    }

    // 8. 版本门控真值表（台账 R16 的负向用例基础）
    private static void ReleaseGateTruthTable()
    {
        bool newer = ReleaseGate.ShouldOfferRollback("4.1.5", "4.1.6", false);
        bool equal = ReleaseGate.ShouldOfferRollback("4.1.5", "4.1.5", true);
        bool olderNoCache = ReleaseGate.ShouldOfferRollback("4.1.6", "4.1.5", false);
        bool olderWithCache = ReleaseGate.ShouldOfferRollback("4.1.6", "4.1.5", true);
        Check("gate-truth-table", !newer && !equal && !olderNoCache && olderWithCache,
            "newer=" + newer + " equal=" + equal + " olderNoCache=" + olderNoCache + " olderWithCache=" + olderWithCache);
        Check("gate-version-compare",
            ReleaseGate.Compare("v4.1.10", "4.1.9") > 0 && ReleaseGate.Compare("4.1.5", "v4.1.5") == 0
            && ReleaseGate.Compare("4.1.6-beta", "4.1.6") == 0,
            "Compare(4.1.10,4.1.9)=" + ReleaseGate.Compare("v4.1.10", "4.1.9")
            + " Compare(4.1.6-beta,4.1.6)=" + ReleaseGate.Compare("4.1.6-beta", "4.1.6"));
        Check("gate-empty-inputs", !ReleaseGate.ShouldOfferRollback("", "4.1.5", true)
            && !ReleaseGate.ShouldOfferRollback("4.1.6", "", true),
            "empty inputs must not offer rollback");
    }

    private static string Game(string tag)
    {
        string p = Path.Combine(root, "games", tag);
        CmoPaths.EnsureDir(p);
        File.WriteAllText(Path.Combine(p, "Command.exe"), "stub");
        return p;
    }

    private static void WriteEngineFiles(string game, string dll, string json)
    {
        File.WriteAllText(Path.Combine(game, "CMOLocalizer.dll"), dll);
        File.WriteAllText(Path.Combine(game, "CMOLocalizer.json"), json);
    }
}
