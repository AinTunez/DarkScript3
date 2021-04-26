using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using SoulsFormats;
using static DarkScript3.ScriptAst;
using static DarkScript3.InstructionTranslator;
using static SoulsFormats.EMEVD.Instruction;
using static SoulsFormats.EMEVD;

namespace DarkScript3
{
    public class FancyEventScripter
    {
        private EventScripter scripter;
        private InstructionDocs docs;
        private EventCFG.CFGOptions options;

        // Some remaining things to do.
        // Side-by-side compilation diff viewing
        // Dialogue box for confirming conversion
        // Font size, and remember preference
        // Good line reporting in CFG compilation
        // Good line reporting in post-compilation packing, line mapping

        public FancyEventScripter(EventScripter scripter, InstructionDocs docs, EventCFG.CFGOptions options = null)
        {
            if (docs.Translator == null) throw new ArgumentException($"Internal error: can't use fancy scripting with {docs.ResourceString}");
            this.scripter = scripter;
            this.docs = docs;
            this.options = options ?? EventCFG.CFGOptions.DEFAULT;
        }

        private EventCFG.CFGOptions UpdateOptions()
        {
            options.RestrictConditionGroupCount = docs.RestrictConditionGroups;
            return options;
        }

        public EMEVD Pack(string code, string documentName = null)
        {
            string output = new FancyJSCompiler(UpdateOptions()).Compile(code, docs).Code;
            if (output == null) throw new Exception();
            return scripter.Pack(output, documentName);
        }

        public string Unpack()
        {
            StringWriter writer = new StringWriter();
            Decompile(writer);
            return writer.ToString();
        }

        public string Repack(string code, string documentName = null)
        {
            FancyJSCompiler.CompileOutput output = new FancyJSCompiler(UpdateOptions()).Compile(code, docs, true);
            if (output.Code == null) throw new Exception();
            scripter.Pack(output.Code, documentName);
            StringWriter writer = new StringWriter();
            Decompile(writer, output);
            return writer.ToString();
        }

        private void Decompile(TextWriter writer, FancyJSCompiler.CompileOutput decorations = null)
        {
            EMEDF DOC = docs.DOC;
            InstructionTranslator info = docs.Translator;
            bool decorate = decorations != null && scripter.EVD.Events.Count == decorations.Funcs.Count;
            for (int i = 0; i < scripter.EVD.Events.Count; i++)
            {
                Event evt = scripter.EVD.Events[i];
                string id = evt.ID.ToString();
                string restBehavior = evt.RestBehavior.ToString();

                Dictionary<Parameter, string> paramNames = docs.ParamNames(evt);
                List<string> argNameList = paramNames.Values.Distinct().ToList();
                string evtArgs = string.Join(", ", argNameList);

                EventFunction func = new EventFunction { ID = (int)evt.ID, RestBehavior = evt.RestBehavior, Params = argNameList };

                List<Intermediate> decorateInstrs = null;
                if (decorate)
                {
                    EventFunction decoratedFunc = decorations.Funcs[i];
                    func.EndComments = decoratedFunc.EndComments;
                    decorateInstrs = decoratedFunc.Body;
                    // If we are automatically translating the source into a new source file, we are copying
                    // in parts of the old source file here, avoid mixing tabs and spaces (which the function
                    // printer extensively uses).
                    writer.Write(decoratedFunc.Header.Replace("\t", SingleIndent));
                }
                else
                {
                    string eventName = scripter.EventName(evt.ID);
                    if (eventName != null) writer.WriteLine($"// {eventName}");
                }

                for (int insIndex = 0; insIndex < evt.Instructions.Count; insIndex++)
                {
                    Instruction ins = evt.Instructions[insIndex];
                    EMEDF.InstrDoc doc = docs.DOC[ins.Bank][ins.ID];
                    if (doc == null)
                    {
                        Console.WriteLine($"ZZZ: {ins.Bank}[{ins.ID}] {string.Join(" ", ins.ArgData.Select(b => $"{b:x2}"))}");
                        continue;
                    }
                    string funcName = InstructionDocs.TitleCaseName(doc.Name);

                    IEnumerable<ArgType> argStruct = doc.Arguments.Select(arg => arg.Type == 8 ? ArgType.UInt32 : (ArgType)arg.Type);

                    Layers layers = ins.Layer is uint l ? new Layers { Mask = l } : null;
                    Instr instr = new Instr { Inner = ins, Cmd = InstructionID(ins.Bank, ins.ID), Name = funcName, Layers = layers };
                    if (decorateInstrs != null)
                    {
                        instr.Decorations = decorateInstrs[insIndex].Decorations;
                    }

                    try
                    {
                        // TODO: Should deduplicate this more with InstructionDocs ArgumentString/ArgumentStringInitializer.
                        // Also EventScripter, which has good error checking.
                        if (docs.IsVariableLength(doc))
                        {
                            argStruct = Enumerable.Repeat(ArgType.Int32, ins.ArgData.Length / 4);
                            instr.Args = ins.UnpackArgs(argStruct);

                            List<uint> positions = docs.GetArgBytePositions(argStruct, 6);
                            for (int argIndex = 0; argIndex < instr.Args.Count(); argIndex++)
                            {
                                uint bytePos = positions[argIndex];
                                foreach (Parameter prm in paramNames.Keys)
                                {
                                    if (prm.InstructionIndex == insIndex && bytePos == prm.TargetStartByte)
                                    {
                                        instr.Args[argIndex + 2] = new ParamArg { Name = paramNames[prm] };
                                    }
                                }
                            }
                            instr.DisplayArgs = instr.Args.ToList();
                        }
                        else
                        {
                            instr.Args = ins.UnpackArgs(argStruct);
                            instr.DisplayArgs = new List<object>();
                            for (int argIndex = 0; argIndex < instr.Args.Count(); argIndex++)
                            {
                                EMEDF.ArgDoc argDoc = doc.Arguments[argIndex];
                                uint bytePos = docs.FuncBytePositions[doc][argIndex];

                                foreach (Parameter prm in paramNames.Keys)
                                {
                                    if (prm.InstructionIndex == insIndex && bytePos == prm.TargetStartByte)
                                    {
                                        instr.Args[argIndex] = new ParamArg { Name = paramNames[prm] };
                                    }
                                }
                                instr.DisplayArgs.Add(argDoc.GetDisplayValue(instr.Args[argIndex].ToString()));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine($@"ERROR XXX: Unable to unpack arguments for ""{funcName}""");
                        sb.AppendLine(ex.ToString());
                        throw new Exception(sb.ToString());
                    }
                    func.Body.Add(instr);
                }
                EventCFG f = new EventCFG((int)evt.ID, UpdateOptions());
                try
                {
                    // This returns warnings. Ignored until we have a nice way to show them.
                    f.Decompile(func, info);
                }
                catch (FancyNotSupportedException)
                {
                    // For the moment, swallow this.
                    // Can find a way to expose the error, but these are basically intentional bail outs.
                    StringBuilder code = new StringBuilder();
                    scripter.UnpackEvent(evt, code);
                    writer.Write(code.ToString());
                    continue;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    continue;
                }
                func.Print(writer);
                if (!decorate)
                {
                    writer.WriteLine();
                }
            }
            if (decorate)
            {
                writer.WriteLine(decorations.Footer.Replace("\t", SingleIndent));
            }
        }
    }
}
