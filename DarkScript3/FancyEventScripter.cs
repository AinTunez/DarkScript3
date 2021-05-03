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
        private readonly EventScripter scripter;
        private readonly InstructionDocs docs;
        private readonly EventCFG.CFGOptions options;

        public FancyEventScripter(EventScripter scripter, InstructionDocs docs, EventCFG.CFGOptions options)
        {
            if (docs.Translator == null) throw new ArgumentException($"Internal error: can't use fancy scripting with {docs.ResourceString}");
            this.scripter = scripter;
            this.docs = docs;
            this.options = options;
        }

        public string Unpack()
        {
            StringWriter writer = new StringWriter();
            Decompile(writer);
            return writer.ToString();
        }

        public EMEVD Pack(string code, string documentName = null)
        {
            FancyJSCompiler.CompileOutput output = new FancyJSCompiler(options).Compile(code, docs);
            try
            {
                return scripter.Pack(output.Code, documentName);
            }
            catch (JSScriptException ex)
            {
                output.RewriteStackFrames(ex, documentName);
                throw ex;
            }
        }

        public string Repack(string code)
        {
            return RepackFull(code).Code;
        }

        public List<FancyJSCompiler.DiffSegment> PreviewPack(string code)
        {
            FancyJSCompiler.CompileOutput output = new FancyJSCompiler(options).Compile(code, docs, printFancyEnums: true);
            return output.GetDiffSegments();
        }

        public FancyJSCompiler.CompileOutput RepackFull(string code)
        {
            FancyJSCompiler.CompileOutput output = new FancyJSCompiler(options).Compile(code, docs, true);
            return output;
        }

        private void Decompile(TextWriter writer)
        {
            EMEDF DOC = docs.DOC;
            InstructionTranslator info = docs.Translator;
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

                string eventName = scripter.EventName(evt.ID);
                if (eventName != null) writer.WriteLine($"// {eventName}");

                for (int insIndex = 0; insIndex < evt.Instructions.Count; insIndex++)
                {
                    Instruction ins = evt.Instructions[insIndex];
                    EMEDF.InstrDoc doc = docs.DOC[ins.Bank]?[ins.ID];
                    if (doc == null)
                    {
                        throw new Exception($"Unknown instruction in event {id}: {ins.Bank}[{ins.ID}] {string.Join(" ", ins.ArgData.Select(b => $"{b:x2}"))}");
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
                        }
                        else
                        {
                            instr.Args = ins.UnpackArgs(argStruct);
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
                                object displayVal = argDoc.GetDisplayValue(instr.Args[argIndex]);
                                if (displayVal is string displayStr)
                                {
                                    instr.Args[argIndex] = new DisplayArg { DisplayValue = displayStr, Value = instr.Args[argIndex] };
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine($@"ERROR: Unable to unpack arguments for ""{funcName}""");
                        sb.AppendLine(ex.ToString());
                        throw new Exception(sb.ToString());
                    }
                    func.Body.Add(instr);
                }
                EventCFG f = new EventCFG((int)evt.ID, options);
                try
                {
                    // This returns warnings, many of which exist in vanilla emevd.
                    // Ignored until we have a nice way to show them.
                    f.Decompile(func, info);
                }
                catch (FancyNotSupportedException)
                {
                    // For the moment, swallow this.
                    // Can find a way to expose the error, but these are basically intentional bail outs, also existing in vanilla emevd.
                    StringBuilder code = new StringBuilder();
                    scripter.UnpackEvent(evt, code);
                    writer.Write(code.ToString());
                    continue;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                func.Print(writer);
                writer.WriteLine();
            }
        }
    }
}
