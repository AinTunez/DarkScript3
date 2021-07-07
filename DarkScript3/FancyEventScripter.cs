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

        public EMEVD Pack(string code, string documentName, out FancyJSCompiler.CompileOutput output)
        {
            output = new FancyJSCompiler(options).Compile(code, docs);
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

        public EMEVD Pack(string code, string documentName)
        {
            return Pack(code, documentName, out FancyJSCompiler.CompileOutput _);
        }

        public string Repack(string code, out FancyJSCompiler.CompileOutput output)
        {
            output = new FancyJSCompiler(options).Compile(code, docs, true);
            return output.Code;
        }

        public string Repack(string code)
        {
            return Repack(code, out FancyJSCompiler.CompileOutput _);
        }

        public List<FancyJSCompiler.DiffSegment> PreviewPack(string code)
        {
            FancyJSCompiler.CompileOutput output = new FancyJSCompiler(options).Compile(code, docs, printFancyEnums: true);
            return output.GetDiffSegments();
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
                Dictionary<Parameter, ParamArg> paramArgs = paramNames.ToDictionary(e => e.Key, e => new ParamArg { Name = e.Value });

                EventFunction func = new EventFunction { ID = (int)evt.ID, RestBehavior = evt.RestBehavior, Params = argNameList };

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
                    string funcName = doc.DisplayName;

                    IEnumerable<ArgType> argStruct = doc.Arguments.Select(arg => arg.Type == 8 ? ArgType.UInt32 : (ArgType)arg.Type);

                    Layers layers = ins.Layer is uint l ? new Layers { Mask = l } : null;
                    Instr instr = new Instr { Inner = ins, Cmd = InstructionID(ins.Bank, ins.ID), Name = funcName, Layers = layers };

                    try
                    {
                        instr.Args = docs.UnpackArgsWithParams(ins, insIndex, doc, paramArgs, (argDoc, val) =>
                        {
                            if (argDoc.GetDisplayValue(val) is string displayStr)
                            {
                                return new DisplayArg { DisplayValue = displayStr, Value = val };
                            }
                            return val;
                        });
                    }
                    catch (Exception ex)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine($@"Unable to unpack arguments for {funcName} at Event {evt.ID}[{insIndex}]");
                        sb.AppendLine($@"Instruction arg data: {InstructionDocs.InstrDebugString(ins)}");
                        sb.AppendLine(ex.Message);
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
                    var sb = new StringBuilder();
                    sb.AppendLine($@"Error decompiling Event {evt.ID}");
                    sb.AppendLine(ex.ToString());
                    throw new Exception(sb.ToString());
                }
                func.Print(writer);
                writer.WriteLine();
            }
        }
    }
}
