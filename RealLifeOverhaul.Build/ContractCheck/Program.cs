using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// The maintainer's check of the mod against a version of the game (see ContractCheck.csproj), run by the gate. A check that
// fails prints one line; the last line is ALL CHECKS PASSED or the count that failed.
//
//   dotnet ContractCheck.dll [Managed folder] [--mod path\RealLifeOverhaul.dll] [--locale path\en.json]
//
// After a game update: run it against the new game (docs/contributing/after-a-game-update.md). The game's build fails until
// GameBuild.MadeFor is the new build; every line under "the game contract" names a piece that is missing or changed and
// the parts it switches off; every line under "the mod's code against this game" a type or member the mod's code names
// that the game no longer has, who uses it and the features the running mod switches off for it; every line under "the
// game's keys" a text or data key of the game's that is gone; under "the walk key" a binding, a key read by the game's or
// the mod's code, or a system key that the walk key's default shares. Fix those, run it again until only the build fails, then
// set GameBuild.MadeFor.
internal static partial class Program
{
    private const string DefaultManaged = @"G:\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed";

    private static int _failed, _run;

    private static void Check(bool ok, string what)
    {
        _run++;
        if (!ok)
        {
            _failed++;
            Console.WriteLine("FAIL  " + what);
        }
    }

    private static void Section(string name)
    {
        Console.WriteLine("-- " + name);
    }

    private static int Main(string[] args)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\.."));   // the worktree
        string managed = Environment.GetEnvironmentVariable("BA_MANAGED") ?? DefaultManaged;
        string mod = Path.Combine(root, @"RealLifeOverhaul.Build\bin\Release\RealLifeOverhaul.dll");
        string locale = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--mod" && i + 1 < args.Length)
            {
                mod = args[++i];
            }
            else if (args[i] == "--locale" && i + 1 < args.Length)
            {
                locale = args[++i];
            }
            else
            {
                managed = args[i];
            }
        }
        locale = locale ?? Path.GetFullPath(Path.Combine(managed, @"..\StreamingAssets\locale\en.json"));
        string scripts = Path.Combine(root, @"Assets\Mods\RealLifeOverhaul\Scripts");

        Logic();
        Faults();
        Saves();
        BuildRules();
        PartNames(Path.Combine(root, @"Assets\Mods\RealLifeOverhaul\Locales\en.json"));
        Markers(scripts);
        LookChecks(scripts, Path.Combine(root, @"Assets\Mods\RealLifeOverhaul\Locales"));
        ButtonChecks(scripts, Path.Combine(root, @"Assets\Mods\RealLifeOverhaul\Locales"));
        PagerChecks(scripts, Path.Combine(root, @"Assets\Mods\RealLifeOverhaul\Locales"));
        PhoneLayoutChecks(scripts, Path.Combine(root, @"Assets\Mods\RealLifeOverhaul\Locales"));   // the phone's folders, dock and moves (PhoneLayoutChecks.cs)
        BoxClockChecks(scripts);   // the box's self-check times its own work, not the app's between its calls (BoxClockChecks.cs)

        if (!Directory.Exists(managed))
        {
            Check(false, "the game's Managed folder is not at " + managed + " (give it as the first argument, or set BA_MANAGED)");
        }
        else
        {
            var files = new GameFiles(managed);
            InstalledBuild(managed);
            Contract(files);
            References(files, mod);
            ScanChecks(files, mod);
            Keys(locale, scripts);
            KeyClashes(files, mod);   // the walk key against every key of the game's, the mod's and the system's (KeyClashChecks.cs)
            Languages(managed, root);
        }
        Console.WriteLine(_failed == 0 ? "ALL CHECKS PASSED (" + _run + ")" : _failed + " of " + _run + " CHECKS FAILED");
        return _failed == 0 ? 0 : 1;
    }

    // ---------------------------------------------------------------- the contract against the game on disk

    private static void Contract(GameFiles files)
    {
        Section("the game contract: " + ContractBook.Members.Count + " pieces of the game in " + ContractBook.Parts.Count + " parts, against " + files.Folder);
        Dictionary<GameMember, string> problems = ContractDisk.All(files);
        foreach (GameMember m in ContractBook.Members)
        {
            string p = problems[m];
            if (p != null)
            {
                var parts = string.Join(", ", m.Parts.Select(x => x.Id + (x.Feature != null ? " (" + x.Feature + ")" : string.Empty)));
                Check(false, (m.Optional ? "optional " : string.Empty) + m.Assembly + ": " + m.Label + ": " + p + (parts.Length > 0 ? "; hits " + parts : string.Empty));
            }
        }
        ContractBook.Mark(m => problems[m]);
        List<GamePart> off = ContractBook.OffParts();
        Console.WriteLine("   " + problems.Count(p => p.Value == null) + " of " + problems.Count + " pieces as expected; parts that would be off: "
                          + (off.Count == 0 ? "none" : string.Join(", ", off.Select(p => p.Id))) + "; features hit: "
                          + (off.Count == 0 ? "none" : string.Join(", ", ContractBook.FeaturesHit())) + ".");
        Console.WriteLine("   (the mod is made for Build " + GameBuild.MadeFor + ": set GameBuild.MadeFor to the game's build when this passes for a new one)");

        // Every part stands on something, and every piece names a part except the one optional type that serves the mod's own copy of a drop-down.
        foreach (GamePart part in ContractBook.Parts)
        {
            Check(ContractBook.Members.Any(m => m.Parts.Contains(part)), "the part " + part.Id + " stands on no piece of the game");
        }
        Check(ContractBook.Parts.Select(p => p.Id).Distinct().Count() == ContractBook.Parts.Count, "two parts share an id");
    }

    // Every part has its name for the player in the mod's en.json (the notice and the options page list them by it).
    private static void PartNames(string modLocale)
    {
        Section("the parts' names, in " + modLocale);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(modLocale)))
        {
            foreach (JsonProperty p in doc.RootElement.EnumerateObject())
            {
                keys.Add(p.Name);
            }
        }
        foreach (GamePart part in ContractBook.Parts)
        {
            Check(keys.Contains(part.NameKey), "the part " + part.Id + " has no name " + part.NameKey + " in the mod's en.json");
        }
    }

    // ---------------------------------------------------------------- what the compiled mod names

    private static void References(GameFiles files, string mod)
    {
        Section("the mod's compiled references, " + mod);
        if (!File.Exists(mod))
        {
            Check(false, "the built mod is not at " + mod + " (build RealLifeOverhaul.csproj first, or give --mod)");
            return;
        }
        var refs = new ModReferences(files, mod);
        refs.Run();
        foreach (string missing in refs.Missing)
        {
            Check(false, missing);
        }
        Check(refs.TypesChecked > 500 && refs.MembersChecked > 2000, "fewer references read than the mod has (" + refs.TypesChecked + " types, " + refs.MembersChecked + " members): the reading is broken");
        Console.WriteLine("   " + refs.TypesChecked + " types and " + refs.MembersChecked + " members of the game's and Unity's assemblies named by the mod; " + refs.Missing.Count + " not found.");
    }

    // ---------------------------------------------------------------- the game's own keys the mod names

    private static readonly Regex Literal = new Regex("\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);

    private static void Keys(string locale, string scripts)
    {
        Section("the game's keys, against " + locale);
        if (!File.Exists(locale))
        {
            Check(false, "the game's en.json is not at " + locale + " (give --locale)");
            return;
        }
        var game = new HashSet<string>(StringComparer.Ordinal);
        using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(locale)))
        {
            foreach (JsonProperty p in doc.RootElement.EnumerateObject())
            {
                game.Add(p.Name);
            }
        }
        string listPath = Path.Combine(AppContext.BaseDirectory, "GameKeys.txt");
        var listed = File.ReadAllLines(listPath).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#", StringComparison.Ordinal)).ToList();
        var used = new Dictionary<string, List<string>>(StringComparer.Ordinal);   // key: where the mod's code names it
        foreach (string file in Directory.EnumerateFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string[] lines = File.ReadAllLines(file);
            for (int n = 0; n < lines.Length; n++)
            {
                string line = lines[n].TrimStart();
                if (line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }
                foreach (Match m in Literal.Matches(lines[n]))
                {
                    string key = m.Groups[1].Value;
                    if (key.StartsWith("reallife", StringComparison.Ordinal) || (!game.Contains(key) && !listed.Contains(key)))
                    {
                        continue;
                    }
                    if (!used.TryGetValue(key, out List<string> where))
                    {
                        used[key] = where = new List<string>();
                    }
                    where.Add(Path.GetRelativePath(scripts, file).Replace('\\', '/') + ":" + (n + 1));
                }
            }
        }
        foreach (string key in listed)
        {
            string where = used.TryGetValue(key, out List<string> w) ? " (named at " + string.Join(", ", w.Take(3)) + (w.Count > 3 ? " and " + (w.Count - 3) + " more" : string.Empty) + ")" : string.Empty;
            Check(game.Contains(key), "the game has no key " + key + where);
        }
        foreach (KeyValuePair<string, List<string>> named in used)
        {
            Check(listed.Contains(named.Key), "the mod's code names the game's key " + named.Key + " (" + named.Value[0] + ") but GameKeys.txt does not list it");
        }
        foreach (string key in listed)
        {
            Check(used.ContainsKey(key), "GameKeys.txt lists " + key + ", which the mod's code no longer names");
        }
        Console.WriteLine("   " + listed.Count + " keys of the game's named by the mod; " + listed.Count(k => !game.Contains(k)) + " not in this game's en.json.");
    }

    // ---------------------------------------------------------------- the contract's logic, with types of its own

    private sealed class Sample
    {
#pragma warning disable 0169, 0414, 0649
        private int _count;
        private static string Shared = "x";
        public Dictionary<string, List<int>> Table;
        public static bool Visible { get; private set; }
        public int ReadOnly => 1;
        private void Click(Sample other, float[] amounts)
        {
        }
        private static bool Allowed() => true;
#pragma warning restore 0169, 0414, 0649

        public sealed class Inner
        {
        }
    }

    private class Base<T>
    {
#pragma warning disable 0649
        public T Prefab;
#pragma warning restore 0649
    }

    private sealed class Derived : Base<Sample.Inner>
    {
    }

    private static GameMember Loose(GameMemberKind kind, string name, bool isStatic, string valueType, string[] parameters, bool optional, params GamePart[] parts)
    {
        var m = new GameMember(kind, "ContractCheck", "Program+Sample", name, isStatic, valueType, parameters, optional, parts, register: false);
        return m;
    }

    private static string ProblemOf(GameMember m, Type t)
    {
        return ContractBook.Check(m, t, out _);
    }

    private static void Logic()
    {
        Section("the contract's logic");
        Type s = typeof(Sample);
        Check(ContractBook.TypeText(typeof(Dictionary<string, List<int>>)) == "Dictionary<String,List<Int32>>", "a generic type's text: " + ContractBook.TypeText(typeof(Dictionary<string, List<int>>)));
        Check(ContractBook.TypeText(typeof(Sample.Inner)) == "Sample.Inner" && ContractBook.TypeText(typeof(float[])) == "Single[]"
              && ContractBook.TypeText(typeof(int?)) == "Nullable<Int32>" && ContractBook.TypeText(typeof(Func<Vector?>)) == "Func<Nullable<Program.Vector>>", "nested, array and nullable types' text");

        Check(ProblemOf(Loose(GameMemberKind.Field, "_count", false, "Int32", null, false), s) == null, "a private field is found with its type");
        Check(ProblemOf(Loose(GameMemberKind.Field, "_count", false, "Single", null, false), s) == "the field's type is now Int32, the mod expects Single", "a field of another type is a problem");
        Check(ProblemOf(Loose(GameMemberKind.Field, "_counted", false, "Int32", null, false), s) == "the field is missing", "a renamed field is missing");
        Check(ProblemOf(Loose(GameMemberKind.Field, "_count", true, "Int32", null, false), s) == "the field is no longer static", "a field that is no longer static is a problem");
        Check(ProblemOf(Loose(GameMemberKind.Field, "Shared", true, "String", null, false), s) == null, "a private static field is found");
        Check(ProblemOf(Loose(GameMemberKind.Field, "Table", false, "Dictionary<String,List<Int32>>", null, false), s) == null, "a field of a generic type is found");
        GameMember visible = Loose(GameMemberKind.Property, "Visible", true, "Boolean", null, false);
        visible.NeedsSetter = true;
        Check(ProblemOf(visible, s) == null, "a static property with a private setter is found");
        GameMember readOnly = Loose(GameMemberKind.Property, "ReadOnly", false, "Int32", null, false);
        readOnly.NeedsSetter = true;
        Check(ProblemOf(readOnly, s) == "the property can no longer be set", "a property without a setter is a problem when one is needed");
        Check(ProblemOf(Loose(GameMemberKind.Method, "Click", false, "Void", new[] { "Program.Sample", "Single[]" }, false), s) == null, "a private method is found by its parameters");
        Check(ProblemOf(Loose(GameMemberKind.Method, "Click", false, "Void", new[] { "Program.Sample" }, false), s) == "the method now takes (Program.Sample,Single[]), the mod expects (Program.Sample)", "a method with other parameters is a problem");
        Check(ProblemOf(Loose(GameMemberKind.Method, "Allowed", true, "Boolean", new string[0], false), s) == null, "a private static method with no parameters is found");
        var inherited = new GameMember(GameMemberKind.Field, "ContractCheck", "Program+Derived", "Prefab", false, "Sample.Inner", null, false, null, register: false);
        Check(ContractBook.Check(inherited, typeof(Derived), out _) == null, "a field of a generic base type is found with the derived type's argument");

        // A missing piece switches off the parts on it, and only those; an optional one switches nothing off.
        var problems = new Dictionary<GameMember, string>();
        ContractBook.Mark(m => m == GameContract.RainIntensity ? "the field is missing" : null);
        Check(GameContract.Thunder.Off && ContractBook.OffParts().Count == 1, "a missing piece switches off its one part and no other");
        Check(ContractBook.FeaturesHit().SetEquals(new[] { "events" }), "a missing piece hits only its own feature");
        Check(GameContract.Thunder.Why == "RainHelper.NextRainMaxIntensity: the field is missing", "the part says which piece and what is wrong: " + GameContract.Thunder.Why);
        ContractBook.Mark(m => m == GameContract.PoiBlob ? "the field is missing" : null);
        Check(GameContract.MapPicker.Off && GameContract.MapPlaces.Off && GameContract.NavMarks.Off && ContractBook.OffParts().Count == 3,
            "a piece three parts stand on switches off those three");
        Check(ContractBook.FeaturesHit().SetEquals(new[] { "navigation" }), "...all of the one feature");
        ContractBook.Mark(m => m == GameContract.HudAppTemplate || m == GameContract.PoiDeadZone ? "the field is missing" : null);
        Check(ContractBook.OffParts().Count == 0 && !GameContract.AppButtons.Off && !GameContract.MarkersBehind.Off, "an optional piece that is missing switches nothing off");
        ContractBook.Mark(m => m == GameContract.Switchers[5] ? "the type BillboardAd is not in BigAmbitions" : null);
        Check(GameContract.FarDistricts.Off && ContractBook.OffParts().Count == 1, "one type of a list missing switches off the part that needs the whole list");
        ContractBook.Mark(m => m == GameContract.CtaBehaviours || m == GameContract.LampIsOn ? "the field is missing" : null);
        Check(GameContract.ClickHome.Off && GameContract.ClickLedger.Off && GameContract.ClickBusStop.Off && GameContract.ClickDesk.Off && GameContract.StreetLamps.Off && GameContract.HotelRoom.Off
              && ContractBook.OffParts().Count == 6, "two missing pieces switch off what stands on either");
        Check(ContractBook.FeaturesHit().SetEquals(new[] { "home", "ledger", "transport", "careers_study", "performance", "travel" }), "...and hit those features only");
        ContractBook.Mark(m => null);
        Check(ContractBook.OffParts().Count == 0 && ContractBook.FeaturesHit().Count == 0, "with every piece there, every part is on");
    }

    private struct Vector
    {
    }

    // ---------------------------------------------------------------- faults

    private static void Faults()
    {
        Section("faults");
        var g = new FeatureGuard("traffic", "TrafficAccidents.Update");
        bool off = false;
        for (int i = 0; i < FeatureGuard.Most - 1; i++)
        {
            off |= g.Count(10f + i);
        }
        Check(!off && !g.Off, "a few faults keep the feature on");
        Check(g.Count(14.5f) && g.Off, "a run of faults in a short time switches it off");
        Check(!g.Count(15f) && g.Off && g.Faults == FeatureGuard.Most + 1, "switched off once, and counted after");

        var slow = new FeatureGuard("traffic", "TrafficWatch.Update");
        bool slowOff = false;
        for (int i = 0; i < 20; i++)
        {
            slowOff |= slow.Count(100f + i * (FeatureGuard.Seconds / (FeatureGuard.Most - 1) + 1f));
        }
        Check(!slowOff && !slow.Off, "faults spread over a long time do not switch it off");

        var back = new FeatureGuard("events", "EventDirector.Update");
        for (int i = 0; i < FeatureGuard.Most - 1; i++)
        {
            back.Count(500f + i);
        }
        Check(!back.Count(10f) && !back.Off, "a clock that goes back starts the window again");

        FeatureGuard a1 = FeatureGuards.For("crime", "CrimeDirector.Update");
        FeatureGuard a2 = FeatureGuards.For("crime", "PoliceAttention.Update");
        FeatureGuard b1 = FeatureGuards.For("jobs", "JobDirector.Update");
        Check(FeatureGuards.SwitchOff("crime") && !FeatureGuards.SwitchOff("crime"), "a feature is switched off once");
        Check(a1.Off && a2.Off && !b1.Off, "switching a feature off switches off its every entry point and nothing of another feature");
        Check(FeatureGuards.FeatureOff("crime") && !FeatureGuards.FeatureOff("jobs"), "the feature is known to be off, the other is not");
        FeatureGuards.ForgetForTests();
    }

    // ---------------------------------------------------------------- saves

    private static void Saves()
    {
        Section("save lines");
        var save = new Dictionary<string, string>
        {
            { "reallife:bank", "v=9~h:from a later version" },
            { "reallife:credit", "v=2~h:1|2|3" },
            { "reallife:empty", string.Empty },
        };
        string told = null;
        SaveLines.Told = (key, why) => told = key;
        SaveLines.Unreadable(save, "reallife:bank", "written by a newer version (9)");
        Check(told == "reallife:bank" && SaveLines.IsKept(save, "reallife:bank"), "a line that cannot be read is kept, and said");
        told = null;
        SaveLines.Unreadable(save, "reallife:bank", "again");
        Check(told == null, "...said once");
        SaveLines.Unreadable(save, "reallife:empty", "empty");
        SaveLines.Unreadable(save, "reallife:none", "absent");
        Check(!SaveLines.IsKept(save, "reallife:empty") && !SaveLines.IsKept(save, "reallife:none"), "an empty or absent line has nothing to keep");

        // The feature started afresh and wrote its own line over it, another took its line away: both are put back.
        save["reallife:bank"] = "v=3~h:a fresh bank";
        save["reallife:credit"] = "v=2~h:4|5|6";
        Check(SaveLines.PutBack(save) == 1 && save["reallife:bank"] == "v=9~h:from a later version" && save["reallife:credit"] == "v=2~h:4|5|6",
            "the kept line is put back as it was read; a line that was read is the feature's to write");
        save.Remove("reallife:bank");
        Check(SaveLines.PutBack(save) == 1 && save["reallife:bank"] == "v=9~h:from a later version", "a kept line that was removed is put back");
        Check(SaveLines.PutBack(save) == 0, "nothing to put back when nothing changed it");

        var other = new Dictionary<string, string> { { "reallife:bank", "v=3~h:another save's" } };
        Check(SaveLines.PutBack(other) == 0 && other["reallife:bank"] == "v=3~h:another save's", "another save is not touched");
        SaveLines.Unreadable(other, "reallife:bank", "damaged");
        Check(SaveLines.IsKept(other, "reallife:bank") && !SaveLines.IsKept(save, "reallife:bank"), "reading another save starts the list afresh");
        SaveLines.Forget();
        Check(!SaveLines.IsKept(other, "reallife:bank"), "forgotten when the mod's data is removed from the save");
        SaveLines.Told = null;
    }
}
