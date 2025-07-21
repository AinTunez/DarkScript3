using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using SoulsFormats;
using static DarkScript3.DocAutocomplete;
using static DarkScript3.InitData;
using static DarkScript3.ScriptAst;
using static SoulsFormats.EMEVD.Instruction;
using SourceNode = DarkScript3.FancyJSCompiler.SourceNode;

namespace DarkScript3
{
    public class InitData
    {
        // All analyzed files. These must have EmevdPath set.
        public Dictionary<string, FileInit> Files { get; set; } = new();
        // Full path, used for looking up linked files
        public string BaseDir { get; set; }

        public enum Lookup
        {
            // Event is returned and it has Args defined
            Found,
            // No event is returned
            NotFound,
            // Multiple events in the file are applicable so the behavior is undefined
            Duplicate,
            // Event is returned but it's declared in source using unnamed parameters
            Unconverted,
            // Event is returned and it has Errors defined
            Error, 
        }

        public class FileInit
        {
            // Name of the file
            public readonly string Name;
            // Whether this was loaded through a link, or has a link name
            public bool Linked { get; set; }
            // The only event per id in the file
            public Dictionary<long, EventInit> Events { get; set; } = new();
            // Multiple events per id (compilation error)
            public Dictionary<long, List<EventInit>> DupeEvents { get; set; } = new();
            // Full path of emevd file.
            public string EmevdPath { get; set; }
            // Whether the reference file is the JS source
            public bool IsSource { get; set; }
            // Last known modification time of the file
            public DateTime ModifiedTime { get; set; }
            // Whether to ignore file checks for in-memory testing
            internal bool ForceAuthoritative { get; set; }

            public FileInit(string Name, bool isSource)
            {
                this.Name = Name;
                Linked = ValidLinkedNames.Contains(Name);
                IsSource = isSource;
            }

            public void AddEvent(EventInit eventMeta)
            {
                if (DupeEvents.TryGetValue(eventMeta.ID, out List<EventInit> existEvents))
                {
                    existEvents.Add(eventMeta);
                }
                else if (Events.TryGetValue(eventMeta.ID, out EventInit existEvent))
                {
                    DupeEvents[eventMeta.ID] = new() { existEvent, eventMeta };
                    Events.Remove(eventMeta.ID);
                }
                else
                {
                    Events[eventMeta.ID] = eventMeta;
                }
            }

            public Lookup TryGetEvent(long id, out EventInit eventInit)
            {
                if (Events.TryGetValue(id, out eventInit))
                {
                    if (eventInit.Errors != null)
                    {
                        return Lookup.Error;
                    }
                    if (eventInit.Unconverted)
                    {
                        return Lookup.Unconverted;
                    }
                    return Lookup.Found;
                }
                // Duplicates are disallowed mainly because during non-fancy compilation, the event can't
                // be uniquely identified, at least not without source tracking/modification.
                // Multiple definitions is well-defined for linked files but not for same-file init.
                return DupeEvents.ContainsKey(id) ? Lookup.Duplicate : Lookup.NotFound;
            }

            // For cache refresh purposes. The file at the path should exist.
            public bool SetSourceInfo(string path)
            {
                FileInfo fileInfo = new FileInfo(path);
                string fileName = GetEmevdName(fileInfo.Name);
                if (!fileInfo.Exists || fileName != Name)
                {
                    // This happens when game files are loaded in-memory from game dir.
                    // It shouldn't happen for linked files which must be present in the project dir.
                    EmevdPath = null;
                    return false;
                }
                if (path.EndsWith(".js"))
                {
                    EmevdPath = fileInfo.FullName.Substring(0, fileInfo.FullName.Length - 3);
                    IsSource = true;
                }
                else
                {
                    EmevdPath = fileInfo.FullName;
                    IsSource = false;
                }
                ModifiedTime = fileInfo.LastWriteTime;
                return true;
            }

            public bool IsStale()
            {
                if (ForceAuthoritative)
                {
                    return false;
                }
                if (!IsAuthoritative(out string filePath))
                {
                    return true;
                }
                FileInfo fileInfo = new FileInfo(filePath);
                return fileInfo.LastWriteTime > ModifiedTime;
            }

            public bool IsAuthoritative(out string filePath)
            {
                filePath = null;
                if (ForceAuthoritative)
                {
                    return true;
                }
                if (EmevdPath == null || !TryGetLinkedFilePath(Path.GetDirectoryName(EmevdPath), Name, out filePath))
                {
                    return false;
                }
                if (filePath.EndsWith(".js") != IsSource)
                {
                    return false;
                }
                string linkEmevd = filePath.EndsWith(".js") ? filePath[..^3] : filePath;
                return filePath == EmevdPath;
            }

            public bool IsAuthoritative() => IsAuthoritative(out _);
        }

        public class EventInit
        {
            // Event id, a non-negative number
            public long ID { get; set; }
            // emeld name or JS comment
            public string Name { get; set; }
            // If it can't be given a signature, the issue
            public List<string> Errors { get; set; }
            // If an unconverted source event
            public bool Unconverted { get; set; }
            // If set, inferred args from the emevd or source parameters
            public List<InitArg> Args { get; set; }
            // If args are set, the length in bytes
            public int ArgLength { get; set; }
        }

        public class InitArg
        {
            // Doc containing at least the Type and EnumName+EnumDoc of the arg. May also contain MetaData.
            // Because this ArgDoc can be used standalone, it's a unique copy of it with its own DisplayName.
            public EMEDF.ArgDoc ArgDoc { get; set; }
            // Unique name for the arg from source or analysis.
            public string Name => ArgDoc?.DisplayName;
            // TODO: Data for nonstandard args
            public int Offset { get; set; }
            public int Width { get; set; }
        }

        public class Links
        {
            // Shared ref for convenience
            public readonly InitData Data;
            // Name of file being compiled/decompiled
            public readonly string MainName;
            // Other linked files in link order
            public readonly List<string> Linked;
            // The file being compiled/decompiled
            public FileInit Main { get; set; }
            // Event autocomplete, regenerated on update.
            // This is currently unused, because event ids may be difficult to distinguish, even with names
            public List<DocAutocompleteItem> Items { get; set; }

            public Links(string MainName, InitData Data, List<string> Linked, FileInit Main = null)
            {
                this.Data = Data;
                this.MainName = MainName;
                this.Linked = Linked;
                this.Main = Main;
            }

            public Lookup TryGetLinkedEvent(long id, out EventInit eventInit)
            {
                if (Linked != null)
                {
                    foreach (string name in Linked)
                    {
                        if (Data.Files.TryGetValue(name, out FileInit fileInit))
                        {
                            Lookup lookup = fileInit.TryGetEvent(id, out eventInit);
                            if (lookup != Lookup.NotFound)
                            {
                                return lookup;
                            }
                        }
                    }
                }
                eventInit = null;
                return Lookup.NotFound;
            }

            public Lookup TryGetEvent(long id, out EventInit eventInit)
            {
                // TODO: Make sure required files are loaded before getting here
                Lookup linkLookup = TryGetLinkedEvent(id, out eventInit);
                if (linkLookup != Lookup.NotFound || Main == null)
                {
                    return linkLookup;
                }
                return Main.TryGetEvent(id, out eventInit);
            }

            public IEnumerable<FileInit> GetLinkedFiles()
            {
                if (Linked != null)
                {
                    foreach (string name in Linked)
                    {
                        if (Data.Files.TryGetValue(name, out FileInit fileInit))
                        {
                            yield return fileInit;
                        }
                    }
                }
                if (Main != null)
                {
                    yield return Main;
                }
            }

            public void UpdateInitData()
            {
                if (Main != null)
                {
                    Data.Files[Main.Name] = Main;
                }
            }
        }

        // Instance methods

        public void ClearUnused(IReadOnlyCollection<string> usedPaths)
        {
            Files = new(Files.Where(e => e.Value.Linked || usedPaths.Contains(e.Value.EmevdPath)));
        }

        // Utilities

        private static readonly List<string> linkSuffixes = new()
        {
            // Games should exclusively use either dcx or non-dcx files.
            // Default to dcx in the case of an undcxed file for some reason.
            ".emevd.dcx.js", ".emevd.dcx", ".emevd.js", ".emevd"
        };
        public static bool TryGetLinkedFilePath(string baseDir, string name, out string linkPath)
        {
            string basePath = Path.Combine(baseDir, name);
            bool chalice = name == "m29";
            foreach (string linkSuffix in linkSuffixes)
            {
                linkPath = basePath + linkSuffix;
                if (File.Exists(linkPath))
                {
                    return true;
                }
                else if (chalice)
                {
                    // Look in parent directory for anything linking m29
                    linkPath = Path.Combine(Path.GetDirectoryName(baseDir), name + linkSuffix);
                    if (File.Exists(linkPath))
                    {
                        return true;
                    }
                }
            }
            linkPath = null;
            return false;
        }

        public static string GetEmevdName(string path)
        {
            string name = Path.GetFileName(path);
            int dot = name.IndexOf('.');
            return dot == -1 ? name : name.Substring(0, dot);
        }

        public static int GetStableSortKey(string path)
        {
            // Stable sort key for decompilation order, assuming paths are in order too
            // Make sure common_func goes before common due to Nightreign link
            string name = GetEmevdName(path);
            return name == "common_func" ? 0 : 1;
        }

        public static readonly List<string> ValidLinkedNames = new()
        {
            // Bloodborne. In the parent directory if the map or directory starts with "m29_"
            "m29",
            // Bloodborne and AC6 (unused maps only)
            "common",
            // Other games. Technically m60/m61 is linked in Elden Ring but no cross-file init.
            "common_func",
        };

        private static (int, int) GetParamPair(EMEVD.Parameter p) => ((int)p.SourceStartByte, p.ByteCount);
        private static string ShowPair((int, int) p) => $"X{p.Item1}_{p.Item2}";
        private static string ShowParam(EMEVD.Parameter p) => ShowPair(GetParamPair(p));

        public static bool TryParseParam(string str, out int sourceStartByte, out int length)
        {
            sourceStartByte = 0;
            length = 0;
            if (!str.StartsWith('X'))
            {
                return false;
            }
            string[] parts = str.Substring(1).Split('_');
            return parts.Length == 2 && int.TryParse(parts[0], out sourceStartByte) && int.TryParse(parts[1], out length);
        }

        // Event arg analysis

        public static FileInit FromEmevd(InstructionDocs docs, EMEVD emevd, string path, Dictionary<long, string> eventNames = null)
        {
            // Possible outcomes: perfect reconstruction (all vanilla events), extra annotations (iffy custom events), giving up
            // TODO non-perfect reconstruction
            FileInit ret = new(GetEmevdName(path), false);
            ret.SetSourceInfo(path);
            foreach (EMEVD.Event e in emevd.Events)
            {
                EventInit eventInit = new EventInit
                {
                    ID = e.ID,
                };
                ret.AddEvent(eventInit);
                if (eventNames != null && eventNames.TryGetValue(e.ID, out string eventName))
                {
                    eventInit.Name = eventName;
                }
                if (e.Parameters.Count == 0)
                {
                    eventInit.Args = new();
                    continue;
                }
                List<string> errors = new();
                Dictionary<(int, int), List<EMEDF.ArgDoc>> paramDocs =
                    e.Parameters.Select(GetParamPair).Distinct().ToDictionary(p => p, _ => new List<EMEDF.ArgDoc>());
                Dictionary<int, List<EMEVD.Parameter>> instrParameters =
                    e.Parameters.GroupBy(p => p.InstructionIndex).ToDictionary(g => (int)g.Key, g => g.ToList());
                for (int i = 0; i < e.Instructions.Count; i++)
                {
                    if (!instrParameters.TryGetValue(i, out List<EMEVD.Parameter> ps))
                    {
                        continue;
                    }
                    EMEVD.Instruction ins = e.Instructions[i];
                    EMEDF.InstrDoc instrDoc = docs.DOC[ins.Bank]?[ins.ID];
                    if (instrDoc == null)
                    {
                        errors.Add($"Unknown instruction {InstructionDocs.FormatInstructionID(ins.Bank, ins.ID)} #{i} has parameters [{string.Join(", ", ps.Select(ShowParam))}]");
                        continue;
                    }
                    else if (docs.IsVariableLength(instrDoc))
                    {
                        errors.Add($"Init instruction {instrDoc.DisplayName} #{i} has parameters [{string.Join(", ", ps.Select(ShowParam))}]");
                        continue;
                    }
                    List<ArgType> argStruct = instrDoc.Arguments.Select(arg => arg.Type == 8 ? ArgType.UInt32 : (ArgType)arg.Type).ToList();
                    List<object> args;
                    try
                    {
                        args = InstructionDocs.UnpackArgsSafe(ins.ArgData, argStruct);
                    }
                    catch (Exception ex)
                    {
                        if (!docs.ManualFixAC6(ins, out args))
                        {
                            errors.Add($"Instruction {instrDoc.DisplayName} #{i} with parameters [{string.Join(", ", ps.Select(ShowParam))}] could not be parsed: {ex.Message}");
                            continue;
                        }
                    }
                    List<int> positions = docs.FuncBytePositions[instrDoc];
                    foreach (EMEVD.Parameter p in ps)
                    {
                        int argIndex = positions.IndexOf((int)p.TargetStartByte);
                        // Last position is instruction length
                        if (argIndex == -1 || argIndex == positions.Count - 1)
                        {
                            errors.Add($"Instruction {instrDoc.DisplayName} #{i} has parameter {ShowParam(p)} at offset {p.TargetStartByte}, but only valid offsets are [{string.Join(", ", positions.SkipLast(1))}]");
                            continue;
                        }
                        // Assume FuncBytePositions is consistent with InstrDoc
                        EMEDF.ArgDoc argDoc = instrDoc.Arguments[argIndex];
                        paramDocs[GetParamPair(p)].Add(argDoc);
                    }
                }
                if (errors.Count > 0)
                {
                    eventInit.Errors = errors;
                    continue;
                }
                AddPositionalArgs(eventInit, paramDocs);
            }
            return ret;
        }

        public static void AddFromSource(FileInit fileInit, InstructionDocs docs, EventFunction func)
        {
            if (func.ID is not long eventId)
            {
                // Pretend it doesn't exist for the purpose of init
                // Compiler could implement some basic inlining if needed
                return;
            }
            // func expectations:
            // Flat list of instructions, where Instr names are all in EMEDF
            // Params may come in many forms (non-unique, mixed)
            EventInit eventInit = new EventInit { ID = eventId };
            fileInit.AddEvent(eventInit);
            eventInit.Name = func.Name;
            if (func.Params.Count == 0)
            {
                eventInit.Args = new();
                return;
            }
            List<string> errors = new();
            int xCount = func.Params.Count(a => a.StartsWith('X'));
            bool xArgs = xCount == func.Params.Count;
            if (!xArgs && xCount != 0)
            {
                errors.Add("Arguments should either all start with X, indicating position/width, or none start with X");
            }
            Dictionary<string, List<EMEDF.ArgDoc>> nameDocs =
                func.Params.Distinct().ToDictionary(p => p, _ => new List<EMEDF.ArgDoc>());
            if (nameDocs.Count != func.Params.Count)
            {
                errors.Add("Duplicate names not allowed for arguments");
            }
            Dictionary<string, (int, int)> argVals = new();
            if (xArgs)
            {
                foreach (string arg in func.Params)
                {
                    if (TryParseParam(arg, out int p1, out int p2))
                    {
                        argVals[arg] = (p1, p2);
                    }
                    else
                    {
                        errors.Add($"Invalid parameter name {arg}");
                    }
                }
            }
            if (errors.Count > 0)
            {
                eventInit.Errors = errors;
                return;
            }
            Dictionary<(int, int), List<EMEDF.ArgDoc>> paramDocs =
                argVals.Values.Distinct().ToDictionary(p => p, _ => new List<EMEDF.ArgDoc>());
            foreach (Intermediate im in func.Body)
            {
                if (im is not Instr instr) continue;
                Dictionary<int, string> paramArgs = null;
                for (int i = 0; i < instr.Args.Count; i++)
                {
                    object arg = instr.Args[i];
                    if (arg is SourceNode sourceNode && sourceNode.GetName(out string name) && nameDocs.ContainsKey(name))
                    {
                        paramArgs ??= new();
                        paramArgs[i] = name;
                    }
                }
                if (paramArgs == null)
                {
                    continue;
                }
                if (!docs.Functions.TryGetValue(instr.Name, out (int, int) id))
                {
                    errors.Add($"Unknown instruction {instr.Name} has parameters [{string.Join(", ", paramArgs.Values)}]");
                }
                EMEDF.InstrDoc instrDoc = docs.DOC[id.Item1][id.Item2];
                if (docs.IsVariableLength(instrDoc))
                {
                    errors.Add($"Init instruction {instr.Name} has parameters [{string.Join(", ", paramArgs.Values)}]");
                    continue;
                }
                foreach ((int argIndex, string name) in paramArgs)
                {
                    if (argIndex >= instrDoc.Arguments.Count)
                    {
                        errors.Add($"Parameter {name} used at arg #{argIndex + 1} of {instr.Name} but only {instrDoc.Arguments.Count} args allowed");
                    }
                    EMEDF.ArgDoc argDoc = instrDoc.Arguments[argIndex];
                    nameDocs[name].Add(argDoc);
                    if (argVals.TryGetValue(name, out (int, int) pair))
                    {
                        paramDocs[pair].Add(argDoc);
                    }
                }
            }
            if (errors.Count > 0)
            {
                eventInit.Errors = errors;
                return;
            }
            if (xArgs)
            {
                // These are given new names, but Offset/Width should allow reconstructing them
                eventInit.Unconverted = true;
                AddPositionalArgs(eventInit, paramDocs);
                return;
            }
            // Otherwise for named parameters, infer the arg struct from usages. We can do this in one pass
            List<InitArg> mainArgs = new();
            int lastPos = 0;
            foreach (string name in func.Params)
            {
                InitArg initArg = new InitArg();
                List<EMEDF.ArgDoc> argDocs = nameDocs[name];
                if (argDocs.Count == 0)
                {
                    if (!name.StartsWith("unused"))
                    {
                        errors.Add($"Unused parameter {name} must either be removed or its name must begin with \"unused\". It will be treated as an int (4 byte) parameter in that case.");
                    }
                }
                else
                {
                    if (name.StartsWith("unused"))
                    {
                        errors.Add($"Parameter {name} appears in the event so its name must not begin with \"unused\".");
                    }
                    EMEDF.ArgDoc merged = MergeArgDocs(name, argDocs, errors);
                    if (merged != null)
                    {
                        initArg.ArgDoc = merged;
                        initArg.Width = InstructionDocs.ByteLengthFromType(merged.Type);
                    }
                }
                if (initArg.ArgDoc == null)
                {
                    // This is fine with unused prefix, error otherwise
                    initArg.ArgDoc = new EMEDF.ArgDoc
                    {
                        Name = "Unknown",
                        DisplayName = name,
                        Type = (long)ArgType.Int32,
                    };
                    initArg.Width = 4;
                }
                int width = initArg.Width;
                // Pad
                if (lastPos % width > 0)
                {
                    lastPos += width - (lastPos % width);
                }
                initArg.Offset = lastPos;
                lastPos += width;
                mainArgs.Add(initArg);
            }
            if (errors.Count > 0)
            {
                eventInit.Errors = errors;
                return;
            }
            else
            {
                if (lastPos % 4 > 0)
                {
                    lastPos += 4 - (lastPos % 4);
                }
                eventInit.Args = mainArgs;
                eventInit.ArgLength = lastPos;
            }
        }

        private static void AddPositionalArgs(EventInit eventInit, Dictionary<(int, int), List<EMEDF.ArgDoc>> paramDocs)
        {
            List<string> errors = new();
            // Disallow multiple parameter widths per offset
            Dictionary<int, EMEDF.ArgDoc> posArgs = new();
            foreach (var g in paramDocs.OrderBy(e => e.Key).GroupBy(p => p.Key.Item1))
            {
                if (g.Count() > 1)
                {
                    errors.Add($"Parameter has multiple widths: {string.Join(", ", g.Select(e => e.Key).Select(ShowPair))}");
                    continue;
                }
                ((int, int) p, List<EMEDF.ArgDoc> argDocs) = g.First();
                (int pos, int width) = p;
                if (width == 0 || pos % width != 0)
                {
                    errors.Add($"Invalid parameter width/alignment: {ShowPair(p)}");
                    continue;
                }
                EMEDF.ArgDoc merged = MergeArgDocs(ShowPair(p), argDocs, errors, width);
                if (merged != null)
                {
                    posArgs[pos] = merged;
                }
            }
            if (errors.Count > 0)
            {
                // Unable to construct some arguments
                eventInit.Errors = errors;
                return;
            }
            // Check everything is properly packed together. Disallow weird overlaps.
            // If there are gaps, maybe unused args could fill in the gaps, or else explicitly include offsets.
            List<InitArg> mainArgs = new();
            int lastPos = 0;
            Dictionary<string, int> nameCounts = new();
            string prevParam = null;
            foreach ((int pos, EMEDF.ArgDoc argDoc) in posArgs.OrderBy(x => x.Key))
            {
                int width = InstructionDocs.ByteLengthFromType(argDoc.Type);
                // Pad
                if (lastPos % width > 0)
                {
                    lastPos += width - (lastPos % width);
                }
                if (pos != lastPos)
                {
                    // TODO: Partial reconstruction
                    errors.Add($"Misaligned param: expected X{lastPos}_{width} following {prevParam ?? "previous param"}, got X{pos}_{width}");
                }
                string name = argDoc.DisplayName;
                if (nameCounts.TryGetValue(name, out int count))
                {
                    count++;
                    // If this comes from MergeArcDocs, it's always a fresh copy
                    argDoc.DisplayName += count;
                }
                else
                {
                    count = 1;
                }
                nameCounts[name] = count;
                InitArg initArg = new InitArg
                {
                    ArgDoc = argDoc,
                    Offset = pos,
                    Width = width,
                };
                prevParam = $"X{pos}_{width}";
                lastPos = pos + width;
                mainArgs.Add(initArg);
            }
            if (errors.Count > 0)
            {
                eventInit.Errors = errors;
                return;
            }
            else
            {
                if (lastPos % 4 > 0)
                {
                    lastPos += 4 - (lastPos % 4);
                }
                eventInit.Args = mainArgs;
                eventInit.ArgLength = lastPos;
            }
        }

        public static readonly HashSet<string> SideNames = new() { "Left-hand Side", "Right-hand Side" };
        private static readonly Regex DenumberRe = new Regex(@"[0-9]*$");

        private static EMEDF.ArgDoc MergeArgDocs(string paramName, List<EMEDF.ArgDoc> argDocs, List<string> errors, int expectWidth = -1)
        {
            // Check for consistency across all known usages.
            // Multiple widths can also be interchangeable but better to disallow that than try to make partial replacements work.
            // The main exception is comparison types, to allow any type (assuming emedf default is 0), since modders can't
            // easily widen enum types like fromsoft can.
            List<EMEDF.ArgDoc> regArgDocs = argDocs;
            if (argDocs.Count == 0)
            {
                errors.Add($"Parameter {paramName} is unused");
                return null;
            }
            if (argDocs.Any(a => a.Name.EndsWith("Side")))
            {
                regArgDocs = argDocs.Where(a => !SideNames.Contains(a.Name)).ToList();
            }
            // Disallow multiple types per parameter
            List<long> types = regArgDocs.Select(a => a.Type).Distinct().ToList();
            if (types.Count > 1)
            {
                // TODO: This should include the instruction name as well, and below error messages
                errors.Add($"Parameter {paramName} appears in multiple instructions with different types: {string.Join(", ", argDocs.Select(InstructionDocs.ArgDocDebugString).Distinct())}");
                return null;
            }
            // This could be a violation for vanilla scripts, but fallback to underlying type is fine. Could be a warning
            // In Sekiro 20005580 etc., enum is used with comparison, so comparison types ignored here
            List<string> enumTypes = regArgDocs.Select(a => a.EnumName).Distinct().ToList();
            bool consistentEnum = enumTypes.Count == 1;
            // Types should be consistent with each other but also consistent with declared width, if any
            if (expectWidth > 0)
            {
                List<int> otherWidths = types.Select(InstructionDocs.ByteLengthFromType).Distinct().ToList();
                otherWidths.Remove(expectWidth);
                if (otherWidths.Count > 0)
                {
                    errors.Add($"Parameter {paramName} has width {expectWidth} but appears in instruction with a width of {string.Join(",", otherWidths)}: {string.Join(", ", argDocs.Select(InstructionDocs.ArgDocDebugString).Distinct())}");
                    return null;
                }
            }
            // For comparison-only types, preserve the type only
            if (regArgDocs.Count == 0)
            {
                EMEDF.ArgDoc sideDoc = argDocs.Last().Clone();
                sideDoc.DisplayName = "value";
                return sideDoc;
            }

            // Most of this is figuring out the name. Try to balance convenience and clarity
            // Also just preemptively copy for unique name purposes so that LookupArgDocs is comprehensive
            EMEDF.ArgDoc retDoc = regArgDocs.MaxBy(a => a.MetaType?.Priority ?? -1).Clone();
            string displayName = retDoc.DisplayName;
            if (retDoc.MetaType != null)
            {
                // This could be precalculated, but at least for multinames with type/id pairs,
                // try to use the metatype name for the id.
                EMEDF.DarkScriptType metaType = retDoc.MetaType;
                if (metaType.MultiNames != null && metaType.OverrideEnum != null && retDoc.Name.EndsWith("ID"))
                {
                    metaType = metaType.Clone();
                    metaType.Name = metaType.MultiNames.Last();
                    metaType.MultiNames = null;
                    metaType.OverrideEnum = null;
                    metaType.OverrideTypes = null;
                    retDoc.MetaType = metaType;
                }
                // Note, this is not necessary if not repacking/unpacking
                if (metaType.Name != null && metaType.Name != "Other")
                {
                    displayName = InstructionDocs.ArgCaseName(metaType.DetailName ?? metaType.Name);
                }
            }
            if (char.IsNumber(displayName[displayName.Length - 1]))
            {
                displayName = DenumberRe.Replace(retDoc.DisplayName, "");
            }
            // Can this be precalculated/part of config? May be too far outside of metatype scope
            displayName = displayName.Replace("character", "chr").Replace("object", "obj");
            // Use this to determine if given name is source-authoritative or not
            if (expectWidth == -1)
            {
                displayName = paramName;
            }
            retDoc.DisplayName = displayName;
            if (retDoc.EnumDoc != null && !consistentEnum)
            {
                retDoc.EnumDoc = null;
                retDoc.EnumName = null;
            }
            return retDoc;
        }

        // Reasons why an event cannot be converted (mainly EventInit.Errors).
        // For the most part, these should not occur in vanilla scripts.
        public class ConvertError
        {
            public string Message { get; set; }
            public Intermediate Im { get; set; }
        }

        // Repack

        public static List<ConvertError> RewriteInits(EventFunction func, InstructionDocs docs, Links links)
        {
            // This also rewrites the full AST
            List<ConvertError> errors = new();
            Intermediate rewriteImArgs(Intermediate im)
            {
                if (im is not Instr instr || !docs.Functions.TryGetValue(instr.Name, out (int, int) insId))
                {
                    return null;
                }
                EMEDF.InstrDoc instrDoc = docs.DOC[insId.Item1][insId.Item2];
                if (!(instrDoc.Name.StartsWith("Initialize") && docs.IsVariableLength(instrDoc) && !instr.Name.StartsWith('$')))
                {
                    return null;
                }
                int idIndex = instrDoc.Arguments.FindIndex(a => a.Name == "Event ID");
                if (idIndex >= 0 && idIndex < instr.Args.Count
                    && instr.Args[idIndex] is SourceNode idSource
                    && long.TryParse(idSource.Source, out long eventId))
                {
                    eventId = InstructionDocs.FixEventID(eventId);
                    Lookup lookup = links.TryGetEvent(eventId, out EventInit eventInit);
                    if (lookup != Lookup.Found)
                    {
                        // Report only non-vanilla issues
                        if (lookup == Lookup.Duplicate)
                        {
                            errors.Add(new ConvertError { Message = $"Can't convert init: Event {eventId} has multiple definitions", Im = im });
                        }
                        else if (lookup == Lookup.Unconverted)
                        {
                            errors.Add(new ConvertError { Message = $"Can't convert init: Event {eventId} still uses X0_4-style parameters", Im = im });
                        }
                        return null;
                    }
                    int prefixLength = idIndex + 1;
                    int initLength = instr.Args.Count - prefixLength;
                    int reqBytes = eventInit.ArgLength == 0 ? 4 : eventInit.ArgLength;
                    // This duplicates some of the checking done by InstructionDocs for better error messages
                    if (initLength != reqBytes / 4)
                    {
                        errors.Add(new ConvertError { Message = $"Can't convert init: Event {eventId} requires {reqBytes / 4} parameter args but given {initLength}", Im = im });
                        return null;
                    }
                    object[] byteArgs = new object[instr.Args.Count];
                    for (int i = 0; i < idIndex; i++)
                    {
                        // Keep source values. Assume these are word-sized
                        byteArgs[i] = 0;
                    }
                    byteArgs[idIndex] = (uint)eventId;
                    List<string> errorArgs = null;
                    for (int i = idIndex + 1; i < instr.Args.Count; i++)
                    {
                        // In theory, could pass through any int arguments, but too complicated to unpack it
                        if (instr.Args[i] is SourceNode source && source.GetIntValue(out int val))
                        {
                            byteArgs[i] = val;
                        }
                        else
                        {
                            errorArgs ??= new();
                            errorArgs.Add(instr.Args[i].ToString());
                        }
                    }
                    if (errorArgs != null)
                    {
                        errors.Add(new ConvertError { Message = $"Can't convert init: Can't parse arguments as integers or floatArg(float): {string.Join(", ", errorArgs)}", Im = im });
                        return null;
                    }
                    EMEVD.Instruction roughInstr = new EMEVD.Instruction(insId.Item1, insId.Item2, byteArgs);
                    Instr reInstr;
                    try
                    {
                        Dictionary<EMEVD.Parameter, string> fakeParams = new();
                        // TODO: Maybe a way to force named init?
                        reInstr = docs.UnpackArgsWithParams(roughInstr, -1, instrDoc, fakeParams, FancyEventScripter.GetDisplayArg, links: links);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(new ConvertError { Message = $"Can't convert init: Failed to reparse: {ex.Message}", Im = im });
                        return null;
                    }
                    if (!reInstr.Name.StartsWith('$'))
                    {
                        errors.Add(new ConvertError { Message = $"Can't convert init: Could not be parsed using {eventId} param types", Im = im });
                        return null;
                    }
                    for (int i = 0; i < idIndex; i++)
                    {
                        // These were given as 0
                        reInstr.Args[i] = instr.Args[i];
                    }
                    // This is pretty ad-hoc. For some reason ID can't be copied
                    reInstr.Layers = instr.Layers;
                    reInstr.Labels = instr.Labels;
                    instr.MoveDecorationsTo(reInstr);
                    return reInstr;
                }
                else
                {
                    errors.Add(new ConvertError { Message = $"Can't convert init: Unknown event id", Im = im });
                }
                return null;
            }
            func.Body = Intermediate.Rewrite(func.Body, rewriteImArgs);
            return errors;
        }

        public static List<ConvertError> ConvertEventFunction(EventFunction func, FileInit fileInit)
        {
            // Unlike AddFromSource, which depends on Instr, this rewrites the full AST
            List<ConvertError> errors = new();
            if (func.Params.Count == 0)
            {
                return errors;
            }
            if (func.ID is not long eventId)
            {
                errors.Add(new ConvertError { Message = $"Event id is not a number" });
                return errors;
            }
            Lookup lookup = fileInit.TryGetEvent(eventId, out EventInit eventInit);
            if (lookup != Lookup.Unconverted)
            {
                if (lookup == Lookup.Error)
                {
                    errors.AddRange(eventInit.Errors.Select(err => new ConvertError { Message = err }));
                }
                return errors;
            }
            // Rename arguments first so that the inits can be marked as converted.
            // First find condition names. It would be better to rename conditions in the case of
            // collisions, like adding Cond to the end, but coordinating all of it is annoying.
            HashSet<string> usedNames = new();
            Cond findCondNames(Cond cond)
            {
                if (cond is CondRef condRef)
                {
                    usedNames.Add(condRef.Name);
                }
                return null;
            }
            Intermediate findImNames(Intermediate im)
            {
                if (im is JSStatement js && js.Declared != null)
                {
                    usedNames.UnionWith(js.Declared);
                }
                if (im is CondAssign assign)
                {
                    usedNames.Add(assign.ToVar);
                }
                if (im is CondIntermediate condIm)
                {
                    condIm.Cond.RewriteCond(findCondNames);
                }
                return null;
            }
            Intermediate.Rewrite(func.Body, findImNames);
            Dictionary<(int, int), string> renamePos = new();
            foreach (InitArg initArg in eventInit.Args)
            {
                if (usedNames.Contains(initArg.Name))
                {
                    string arg = initArg.Name + "Arg";
                    int suffix = 2;
                    while (usedNames.Contains(arg))
                    {
                        arg = initArg.Name + "Arg" + suffix;
                        suffix++;
                    }
                    // These ArgDocs should be unique, but defensive copy just in case
                    initArg.ArgDoc = initArg.ArgDoc.Clone();
                    initArg.ArgDoc.Name = arg;
                }
                usedNames.Add(initArg.Name);
                renamePos[(initArg.Offset, initArg.Width)] = initArg.Name;
                // if (eventId == 130) Console.WriteLine($"Got {initArg.Offset},{initArg.Width} -> {initArg.Name} args with names {string.Join(",", func.Params)}");
            }
            Dictionary<string, string> rename = new();
            foreach (string arg in func.Params)
            {
                if (TryParseParam(arg, out int p1, out int p2))
                {
                    if (renamePos.TryGetValue((p1, p2), out string name))
                    {
                        rename[arg] = name;
                    }
                    else
                    {
                        errors.Add(new ConvertError { Message = $"Internal error: Parameter name {arg} ({p1}, {p2}) has no data associated with it, out of {string.Join(", ", renamePos.Keys)}" });
                    }
                }
                else
                {
                    errors.Add(new ConvertError { Message = $"Internal error: Invalid parameter name {arg} found when converting from X0_4-style parameters" });
                }
            }
            if (errors.Count > 0)
            {
                return errors;
            }
            func.Params = eventInit.Args.Select(a => a.Name).ToList();
            object rewriteArg(object arg)
            {
                // This loses positional info but it should be fine at this point
                if (arg is SourceNode sourceNode && sourceNode.GetName(out string name) && rename.TryGetValue(name, out string newName))
                {
                    return newName;
                }
                return arg;
            }
            void rewriteArgs(List<object> args)
            {
                for (int i = 0; i < args.Count; i++)
                {
                    args[i] = rewriteArg(args[i]);
                }
            }
            Cond rewriteCondArgs(Cond cond)
            {
                if (cond is CompareCond cmp)
                {
                    if (cmp.CmdLhs != null)
                    {
                        rewriteCondArgs(cmp.CmdLhs);
                    }
                    else
                    {
                        cmp.Lhs = rewriteArg(cmp.Lhs);
                    }
                    cmp.Rhs = rewriteArg(cmp.Rhs);
                }
                else if (cond is CmdCond cmd)
                {
                    rewriteArgs(cmd.Args);
                }
                // This mutate in-place so rewrite like this is not necessary
                return null;
            }
            Intermediate rewriteImArgs(Intermediate im)
            {
                if (im is CondIntermediate condIm)
                {
                    condIm.Cond = condIm.Cond.RewriteCond(rewriteCondArgs);
                }
                else if (im is Instr instr)
                {
                    rewriteArgs(instr.Args);
                }
                return null;
            }
            func.Body = Intermediate.Rewrite(func.Body, rewriteImArgs);
            eventInit.Unconverted = false;
            return errors;
        }

        // IDE functionality

        public readonly record struct DocID(string Func, long Event = -1);

        public static void UpdateItems(Links links)
        {
            // Not currently used
            SortedDictionary<long, DocAutocompleteItem> items = new();
            foreach (FileInit fileInit in links.GetLinkedFiles())
            {
                foreach ((long id, EventInit eventInit) in fileInit.Events)
                {
                    if (items.ContainsKey(id))
                    {
                        continue;
                    }
                }
            }
            links.Items = items.Values.ToList();
        }
    }
}
