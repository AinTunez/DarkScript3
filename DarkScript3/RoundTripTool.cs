using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using SoulsFormats;
using static SoulsFormats.EMEVD;

namespace DarkScript3
{
    /// <summary>
    /// Routines for generating files to check round trip conversion of packing and unpacking.
    /// </summary>
    public class RoundTripTool
    {
        private static readonly Dictionary<string, string> defaultGameDirs = new Dictionary<string, string>
        {
            ["ds1"] = @"C:\Program Files (x86)\Steam\steamapps\common\Dark Souls Prepare to Die Edition\DATA\event",
            ["ds1r"] = @"C:\Program Files (x86)\Steam\steamapps\common\DARK SOULS REMASTERED\event",
            ["ds3"] = @"C:\Program Files (x86)\Steam\steamapps\common\DARK SOULS III\Game\event",
            ["sekiro"] = @"C:\Program Files (x86)\Steam\steamapps\common\Sekiro\event"
        };
        private static readonly Dictionary<string, EMEVD.Game> gameTypes = new Dictionary<string, Game>
        {
            ["ds1"] = Game.DarkSouls1,
            ["bb"] = Game.Bloodborne,
            ["ds2"] = Game.Bloodborne,
            ["ds2scholar"] = Game.Bloodborne,
            ["ds3"] = Game.DarkSouls3,
            ["sekiro"] = Game.Sekiro,
        };

        public static void Run(string[] args)
        {
            string game = args[0];
            string inDir = args[1];
            if (defaultGameDirs.TryGetValue(inDir, out string gameDir)) inDir = gameDir;
            string outDir = args[2];
            // Fancy recompilation
            List<string> emevdPaths = Directory.GetFiles(inDir, "*.emevd").Concat(Directory.GetFiles(inDir, "*.emevd.dcx")).ToList();
            InstructionDocs docs = new InstructionDocs($"{game}-common.emedf.json");
            if (!Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }
            Dictionary<string, StringBuilder> contents = new Dictionary<string, StringBuilder>();
            string recordText(string type, string name, Func<object> func)
            {
                if (!contents.TryGetValue(type, out StringBuilder sb))
                {
                    contents[type] = sb = new StringBuilder();
                }
                try
                {
                    object ret = func();
                    if (ret is string text)
                    {
                        sb.AppendLine($"/* ------------------- {name} ------------------- */");
                        sb.AppendLine(text);
                        return text;
                    }
                    else
                    {
                        // Do nothing other than indicate success
                        return "";
                    }
                }
                catch (Exception e)
                {
                    sb.AppendLine($"/* ------------------- {name} ------------------- */");
                    sb.AppendLine($"/* {e} */");
                    Console.WriteLine(name + ": " + e + "\n");
                    return null;
                }
            }
            foreach (string emevdPath in emevdPaths)
            {
                string name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(emevdPath));
                if (game == "ds3" && name.StartsWith("m2")) continue;
                Console.WriteLine("--------------------------" + name);
                EventScripter scripter = new EventScripter(emevdPath, docs);
                string reg1 = recordText("reg1", name, () => scripter.Unpack());
                if (reg1 == null) continue;
                if (args.Contains("reg"))
                {
                    if (recordText("reg2", name + "-compile", () => scripter.Pack(reg1, name)) != null)
                    {
                        recordText("reg2", name, () => scripter.Unpack());
                    }
                }
                if (args.Contains("fancy"))
                {
                    EventCFG.CFGOptions options = args.Contains("min") ? EventCFG.CFGOptions.MIN : EventCFG.CFGOptions.DEFAULT;
                    scripter = new EventScripter(emevdPath, docs);
                    FancyEventScripter fes = new FancyEventScripter(scripter, docs, options);
                    if (args.Contains("unit"))
                    {
                        fes.Pack(TEST_CASES);
                        Console.WriteLine(scripter.Unpack());
                        Console.WriteLine(fes.Repack(TEST_CASES));
                        return;
                    }
                    string fancy1 = recordText("fancy1", name, () => fes.Unpack());
                    if (fancy1 != null)
                    {
                        if (args.Contains("repack"))
                        {
                            recordText("fancy2", name, () => fes.Repack(fancy1));
                        }
                        else
                        {
                            if (recordText("reg3", name + "-compile", () => fes.Pack(fancy1, name)) != null)
                            {
                                recordText("reg3", name, () => scripter.Unpack());
                            }
                        }
                    }
                }
            }
            foreach (KeyValuePair<string, StringBuilder> entry in contents)
            {
                using (TextWriter writer = File.CreateText($@"{outDir}\{game}_{entry.Key}.js"))
                {
                    writer.Write(entry.Value);
                }
            }
        }

        public static readonly string TEST_CASES = @"
// Should produce warnings but otherwise work
$Event(1, Default, function() {
    Goto(L0);
L0: L1:
    Goto(L0);
L2: L1:
    NoOp();
});

$Event(2, Default, function() {
    if (badcond) {
        SetEventFlag(1, ON);
    }
    worsecond = worsecond;
    EndIf(worstcond.Passed);
});

// More cases
$Event(100, Default, function() {
    if (EventFlag(1)) {
    }
    SetEventFlag(1, ON);
    if (InArea(10000, 15)) {
    } else {
        SetEventFlag(2, ON);
    }
    if (InArea(10000, 30)) {
    } else {
    }
    SetEventFlag(3, ON);
});

$Event(101, Default, function() {
    if (EventFlag(100)) {
        if (!EventFlag(150)) {
            SetEventFlag(150, ON);
        }
        if (EventFlag(200)) {
            SetEventFlag(300, ON);
        } else {
            SetEventFlag(400, ON);
        }
    }
});

$Event(102, Default, function() {
    c = EventFlag(10);
    if (EventFlag(99)) {
        c2 = c && EventFlag(20);
        WaitFor(c2);
    } else {
        WaitFor(c);
    }
    Label0();
});

$Event(103, Default, function() {
    // Comments
    if (EventFlag(99)) {  // Wait for state change
        WaitFor(/* negated */ !EventFlag(99));
    } else {
        WaitFor(EventFlag(99));
    }  // If state change
L0:  // Jump target
    /* Then the event
     * flag is set */
S0: // End
    NoOp();
});
";
    }
}
