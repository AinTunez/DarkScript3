using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SoulsFormats;
using static SoulsFormats.EMEVD.Instruction;

namespace DarkScript3
{
    /// <summary>
    /// Hacky pile of scripts for testing condition group behavior edge cases.
    /// </summary>
    public class CondTestingTool
    {
        // Static data
        private EMEDF doc;
        private Dictionary<string, (int, int)> docByName;
        private SortedDictionary<int, string> testItemLots;
        private List<int> testItemLotOrder;

        // Added in tests
        private EMEVD emevd;
        private int i = 1;
        private int evBase = 5950;
        private int falseFlag = 11305405;
        private int trueFlag = 11305406;
        private int flagBase = 11305410;
        private List<int> addEvents = new List<int>();

        public void RunTests()
        {
            doc = EMEDF.ReadFile($"DarkScript3/Resources/sekiro-common.emedf.json");
            docByName = doc.Classes.SelectMany(c => c.Instructions.Select(i => (i, (int)c.Index))).ToDictionary(i => i.Item1.Name, i => (i.Item2, (int)i.Item1.Index));
            testItemLots = new SortedDictionary<int, string>
            {
                [60210] = "Remnant Gyoubu",
                [60220] = "Remnant Butterfly",
                [60230] = "Remnant Genichiro",
                [60240] = "Remnant Monkeys",
                [60250] = "Remnant Ape",
                [60260] = "Remnant Monk",
                [62208] = "Rice",
                [62214] = "Fine Snow",
            };
            testItemLotOrder = testItemLots.Keys.ToList();

            string dir = @"C:\Program Files (x86)\Steam\steamapps\common\Sekiro";
            emevd = EMEVD.Read($@"{dir}\event\common.emevd.dcx");
            EMEVD.Event runner = new EMEVD.Event(evBase);
            emevd.Events.Add(runner);
            emevd.Events[0].Instructions.Add(new EMEVD.Instruction(2000, 0, new List<object> { 0, (uint)runner.ID, 0 }));
            runner.Instructions.Add(ParseAdd($"Set Event Flag ({falseFlag},0)"));
            runner.Instructions.Add(ParseAdd($"Set Event Flag ({trueFlag},1)"));
            // Wait for sip
            runner.Instructions.Add(ParseAdd($"IF Character Has SpEffect (0,10000,3000,1,0,1)"));
            RunTest();
            foreach (int id in addEvents)
            {
                runner.Instructions.Add(new EMEVD.Instruction(2000, 0, new List<object> { 0, id, 0 }));
            }

            emevd.Write($@"{dir}\condtest\event\common.emevd.dcx");
            emevd.Write($@"{dir}\condtest\event\common.emevd", DCX.Type.None);
        }

        private void TestOrder()
        {
            // Test executing commands out of order
            // cond[1] &= true
            // cond[2] &= cond[1]
            // cond[1] &= false
            // test cond[2]
            foreach (bool start in new[] { true, false })
            {
                foreach (bool and in new[] { true, false })
                {
                    int reg1 = and ? 1 : -1;
                    int reg2 = and ? 2 : -2;
                    EMEVD.Event ev = new EMEVD.Event(evBase + i);
                    ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg1},1,0,{(start ? trueFlag : falseFlag)})"));
                    ev.Instructions.Add(ParseAdd($"IF Condition Group ({reg2},1,{reg1})"));
                    ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg1},1,0,{(start ? falseFlag : trueFlag)})"));
                    AddItemTest(ev, $"{(start ? "initial true" : "initial false")} and {(and ? "AND" : "OR")}", reg2);
                }
            }
            /*
            If 2 in initial true and AND, then awarding Fine Snow
            If -2 in initial true and OR, then awarding Rice
            If 2 in initial false and AND, then awarding Remnant Monk
            If -2 in initial false and OR, then awarding Remnant Ape
            Expected output if order is important: initial trues only
            Expected outout if order is not important: initial true and OR; initial false and AND
            Actual output: initial trues only
            Result: order is important
            (caveat from later: unimportant after the first frame of a MAIN evaluation)
            */
        }

        private void TestUndefined()
        {
            // skip if condition group compiled/uncompiled (true/false, and/or)
            foreach (bool compiled in new[] { true, false })
            {
                foreach (bool check in new[] { true, false })
                {
                    foreach (bool and in new[] { true, false })
                    {
                        int reg = and ? 1 : -1;
                        int val = check ? 1 : 0;
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State ({(compiled ? "Compiled" : "Uncompiled")}) (1,{val},{reg})"));
                        AddItemTest(ev, $"{(compiled ? "compiled" : "uncompiled")} {check} check for {(and ? "AND" : "OR")}");
                    }
                }
            }
            /*
            1. If compiled True check for AND, then awarding Fine Snow
            2. If compiled True check for OR, then awarding Rice
            3. If compiled False check for AND, then awarding Remnant Monk
            4. If compiled False check for OR, then awarding Remnant Ape
            5. If uncompiled True check for AND, then awarding Remnant Monkeys
            6. If uncompiled True check for OR, then awarding Remnant Genichiro
            7. If uncompiled False check for AND, then awarding Remnant Butterfly
            8. If uncompiled False check for OR, then awarding Remnant Gyoubu
            Expected output if compiled default false: 1 2
            Expected output if compiled default true: 3 4
            Expected output if uncompiled default false: 5 6
            Expected output if uncompiled default true: 7 8
            Actual output: fine snow, rice, butterfly, gyoubu
            Result: Compiled default false, uncompiled default true
            */
        }

        private void TestDefinedPostEval()
        {
            // Use and/or register as true/false
            // Main eval if true/false
            // Skip if compiled/uncompiled true/false after
            foreach (bool compiled in new[] { true, false })
            {
                foreach (bool start in new[] { true, false })
                {
                    foreach (bool and in new[] { true, false })
                    {
                        int reg = and ? 1 : -1;
                        int val = start ? 1 : 0;
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg},1,0,{(start ? trueFlag : falseFlag)})"));
                        ev.Instructions.Add(ParseAdd($"IF Condition Group (0,{val},{reg})"));
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State ({(compiled ? "Compiled" : "Uncompiled")}) (1,{val},{reg})"));
                        AddItemTest(ev, $"{(compiled ? "compiled" : "uncompiled")} {start} check for {(and ? "AND" : "OR")}");
                    }
                }
            }
            /*
            1. If compiled True check for AND, then awarding Fine Snow
            2. If compiled True check for OR, then awarding Rice
            3. If compiled False check for AND, then awarding Remnant Monk
            4. If compiled False check for OR, then awarding Remnant Ape
            5. If uncompiled True check for AND, then awarding Remnant Monkeys
            6. If uncompiled True check for OR, then awarding Remnant Genichiro
            7. If uncompiled False check for AND, then awarding Remnant Butterfly
            8. If uncompiled False check for OR, then awarding Remnant Gyoubu
            Expected if compiled values hold: none of 1 2 3 4 (skipping if state matches flag)
            Expected if uncompiled values ignored: 7 8, since uncompiled default true
            Actual output: 7 8
            Result: Uncompiled registers get reset after main evaluation
            */
        }

        private void TestChangeState()
        {
            // Set event flag to true/false
            // Set condition group compiled/uncompiled to event flag state
            // Change event flag state
            // Evaluate condition group compiled/uncompiled state
            foreach (bool compiled in new[] { true, false })
            {
                foreach (bool start in new[] { true, false })
                {
                    foreach (bool and in new[] { true, false })
                    {
                        int flag = flagBase + i;
                        int reg = and ? 1 : -1;
                        int val = start ? 1 : 0;
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"Set Event Flag ({flag},{val})"));
                        // ev.Instructions.Add(ParseAdd($"WAIT Fixed Time (Frames) (1)"));
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg},{val},0,{flag})"));
                        if (compiled)
                        {
                            ev.Instructions.Add(ParseAdd($"IF Condition Group (0,1,{reg})"));
                        }
                        else
                        {
                            ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State (Uncompiled) (1,1,{reg})"));
                            ev.Instructions.Add(ParseAdd("Label 0 ()"));
                        }
                        ev.Instructions.Add(ParseAdd($"Set Event Flag ({flag},{1 - val})"));
                        // ev.Instructions.Add(ParseAdd($"WAIT Fixed Time (Frames) (1)"));
                        bool redefine = true;
                        if (redefine) ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg},{val},0,{flag})"));
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State ({(compiled ? "Compiled" : "Uncompiled")}) (1,1,{reg})"));
                        AddItemTest(ev, $"{(compiled ? "compiled" : "uncompiled")} {start} check for {(and ? "AND" : "OR")}");
                    }
                }
            }
            /*
            1. If compiled True check for AND, then awarding Fine Snow
            2. If compiled True check for OR, then awarding Rice
            3. If compiled False check for AND, then awarding Remnant Monk
            4. If compiled False check for OR, then awarding Remnant Ape
            5. If uncompiled True check for AND, then awarding Remnant Monkeys
            6. If uncompiled True check for OR, then awarding Remnant Genichiro
            7. If uncompiled False check for AND, then awarding Remnant Butterfly
            8. If uncompiled False check for OR, then awarding Remnant Gyoubu
            Expected if compiled is fixed: none of 1 2 3 4
            Expected if uncompiled is fixed: none of 5 6 7 8
            Expected if uncompiled can change: 5 6 7 8
            Actual output: Nothing
            Result: Uncompiled condition groups are set at the time of evaluation only
            Output with redefining cond group: 5 7.
            Result: Uncompiled condition groups can be and/or together with re-evaluation, and will be false/true after switching values.
            */
        }

        private void TestClearGroups()
        {
            // Set condition group state compiled/uncompiled to true/false
            // Optionally clear compiled state
            // Evaluate compiled/uncompiled condition state
            foreach (bool clear in new[] { true, false })
            {
                foreach (bool compiled in new[] { true, false })
                {
                    foreach (bool start in new[] { true, false })
                    {
                        int reg = 1;
                        int val = start ? 1 : 0;
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg},1,0,{(start ? trueFlag : falseFlag)})"));
                        if (compiled)
                        {
                            ev.Instructions.Add(ParseAdd($"IF Condition Group (0,{val},{reg})"));
                            // ev.Instructions.Add(ParseAdd($"WAIT For Condition Group State ({val},{reg})"));
                        }
                        else
                        {
                            ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State (Uncompiled) (1,1,{reg})"));
                            ev.Instructions.Add(ParseAdd("Label 0 ()"));
                        }
                        if (clear)
                        {
                            ev.Instructions.Add(ParseAdd($"Clear Compiled Condition Group State (0)"));
                        }
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State ({(compiled ? "Compiled" : "Uncompiled")}) (1,1,{reg})"));
                        AddItemTest(ev, $"{(clear ? "Cleared" : "Not cleared")} {(compiled ? "compiled" : "uncompiled")} evaluation of {start} value");
                    }
                }
            }
            /*
            1. If Cleared compiled evaluation of True value, then awarding Fine Snow
            2. If Cleared compiled evaluation of False value, then awarding Rice
            3. If Cleared uncompiled evaluation of True value, then awarding Remnant Monk
            4. If Cleared uncompiled evaluation of False value, then awarding Remnant Ape
            5. If Not cleared compiled evaluation of True value, then awarding Remnant Monkeys
            6. If Not cleared compiled evaluation of False value, then awarding Remnant Genichiro
            7. If Not cleared uncompiled evaluation of True value, then awarding Remnant Butterfly
            8. If Not cleared uncompiled evaluation of False value, then awarding Remnant Gyoubu
            Expected for not cleared: 6 8
            Expected if clearing only affects compiled conditions, then switching to default value of false: 1 2 4
            Expected if clearing also affects uncompiled conditions: 1 2 3 4
            Output: fine snow, rice, ape, genichiro, gyoubu
            Result: Clearing only resets compiled conditions, by changing them to false again
            Output with waiting for condition group state: fine snow, rice, ape, genichiro, gyoubu
            */
        }

        private void TestUnrelatedCompilation()
        {
            // Unrelated uncompiled group states after evaluation of related/unrelated groups
            // cond[1] &= true/false
            // cond[2] &= true/false
            // compile cond[2]
            // Check compiled/uncompiled state of 1/2

            // Slight variant: 
            // cond[1] &= true
            // cond[2] &= cond[1]
            // cond[1] &= false
            // compile cond[2]
            foreach (bool compiled in new[] { true, false })
            {
                foreach (bool start in new[] { true, false })
                {
                    foreach (bool check in new[] { true, false })
                    {
                        int reg1 = 1;
                        int reg2 = 2;
                        int val = start ? 1 : 0;
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg1},1,0,{(start ? trueFlag : falseFlag)})"));
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg2},1,0,{(start ? trueFlag : falseFlag)})"));
                        ev.Instructions.Add(ParseAdd($"IF Condition Group (0,{val},{reg2})"));
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State ({(compiled ? "Compiled" : "Uncompiled")}) (1,0,{(check ? reg2 : reg1)})"));
                        AddItemTest(ev, $"{(compiled ? "compiled" : "uncompiled")} check for {start} {(check ? "compiled" : "uncompiled")} register");
                    }
                }
            }
            /*
            1. If compiled check for True compiled register, then awarding Fine Snow
            2. If compiled check for True uncompiled register, then awarding Rice
            3. If compiled check for False compiled register, then awarding Remnant Monk
            4. If compiled check for False uncompiled register, then awarding Remnant Ape
            5. If uncompiled check for True compiled register, then awarding Remnant Monkeys
            6. If uncompiled check for True uncompiled register, then awarding Remnant Genichiro
            7. If uncompiled check for False compiled register, then awarding Remnant Butterfly
            8. If uncompiled check for False uncompiled register, then awarding Remnant Gyoubu
            Output: fine snow, rice, screen monkeys, genichiro, lady butterfly, gyoubu
            Result: after evaluation, all uncompiled groups get reset (become true), and compiled checks are true if register was true, even if not evaluated directly
            */
        }

        private void TestMixedCompilation()
        {
            // Combination of unrelated compilation
            // cond[1] &= true/false
            // cond[2] &= true/false
            // compile cond[2]
            // Check compiled/uncompiled state of 1/2

            // And ordered compilation
            // cond[1] &= true
            // cond[2] &= cond[1]
            // cond[1] &= false
            // test cond[2]

            // Slight variant: 
            // cond[1] &= true
            // cond[2] &= cond[1]
            // cond[1] &= false
            // Compile cond[2]
            // Evaluate compiled and uncompiled state of cond[1]
            foreach (bool compiled in new[] { true, false })
            {
                foreach (bool start in new[] { true, false })
                {
                    foreach (bool and in new[] { true, false })
                    {
                        int reg1 = and ? 1 : -1;
                        int reg2 = and ? 2 : -2;
                        int val = start ? 1 : 0;
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg1},1,0,{(start ? trueFlag : falseFlag)})"));
                        ev.Instructions.Add(ParseAdd($"IF Condition Group ({reg2},1,{reg1})"));
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg1},1,0,{(start ? falseFlag : trueFlag)})"));
                        ev.Instructions.Add(ParseAdd($"IF Condition Group (0,{val},{reg2})"));
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State ({(compiled ? "Compiled" : "Uncompiled")}) (1,0,{reg1})"));
                        AddItemTest(ev, $"{(compiled ? "compiled" : "uncompiled")} check for {start} {(and ? "AND" : "OR")} {!start} register");
                    }
                }
            }
            /*
            1. If compiled check for True AND False register, then awarding Fine Snow
            2. If compiled check for True OR False register, then awarding Rice
            3. If compiled check for False AND True register, then awarding Remnant Monk
            4. If compiled check for False OR True register, then awarding Remnant Ape
            5. If uncompiled check for True AND False register, then awarding Remnant Monkeys
            6. If uncompiled check for True OR False register, then awarding Remnant Genichiro
            7. If uncompiled check for False AND True register, then awarding Remnant Butterfly
            8. If uncompiled check for False OR True register, then awarding Remnant Gyoubu
            Expected if uncompiled check will return true always: 5 6 7 8
            Expected if compiled state includes later value: 2 4
            Expected if compiled state excludes later value: 1 2
            Output: 1 4 5 6 7 8 ? But trying again, 1->2?
            Inconclusive. Try again with only compiled checks.
            */
        }

        private void TestMixedCompilation2()
        {
            // cond[1] &= true/false
            // cond[2] &= cond[1]
            // cond[1] &= false/false
            // Compile cond[2]
            // Evaluate compiled state of cond[1]
            foreach (bool and in new[] { true, false })
            {
                foreach (bool start1 in new[] { true, false })
                {
                    foreach (bool start2 in new[] { true, false })
                    {
                        int reg1 = and ? 1 : -1;
                        int reg2 = and ? 2 : -2;
                        int val = start1 ? 1 : 0;
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg1},1,0,{(start1 ? trueFlag : falseFlag)})"));
                        ev.Instructions.Add(ParseAdd($"IF Condition Group ({reg2},1,{reg1})"));
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg1},1,0,{(start2 ? trueFlag : falseFlag)})"));
                        ev.Instructions.Add(ParseAdd($"IF Condition Group (0,{val},{reg2})"));
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State (Compiled) (1,0,{reg1})"));
                        AddItemTest(ev, $"compiled check for {start1} {(and ? "AND" : "OR")} {start2} register");
                    }
                }
            }
            /*
            1. If compiled check for True AND True register, then awarding Fine Snow
            2. If compiled check for True AND False register, then awarding Rice
            3. If compiled check for False AND True register, then awarding Remnant Monk
            4. If compiled check for False AND False register, then awarding Remnant Ape
            5. If compiled check for True OR True register, then awarding Remnant Monkeys
            6. If compiled check for True OR False register, then awarding Remnant Genichiro
            7. If compiled check for False OR True register, then awarding Remnant Butterfly
            8. If compiled check for False OR False register, then awarding Remnant Gyoubu
            Output: fine snow, screen monkeys, genichiro, butterfly
            Result: Compiled condition group state is the state at evaluation time
            */
        }

        private void TestDelayedCompileOrder()
        {
            // cond[1] &= flag1
            // cond[2] &= cond[1]
            // cond[1] &= flag2
            // Compile cond[2]
            // (After 2 seconds, set flag1/flag2 to true)
            // Does it evaluate?
            List<int> flagsToSet = new List<int>();
            foreach (bool and in new[] { true, false })
            {
                foreach (bool setalt in new[] { true, false })
                {
                    int reg1 = and ? 1 : -1;
                    int reg2 = and ? 2 : -2;
                    int flag1 = flagBase + i * 2;
                    int flag2 = flagBase + i * 2 + 1;
                    flagsToSet.Add(flag1);
                    EMEVD.Event ev = new EMEVD.Event(evBase + i);
                    ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg1},1,0,{(setalt ? flag1 : flag2)})"));
                    ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg1},1,0,{(setalt ? flag2 : flag1)})"));
                    ev.Instructions.Add(ParseAdd($"IF Condition Group ({reg2},1,{reg1})"));
                    ev.Instructions.Add(ParseAdd($"IF Condition Group (0,1,{reg2})"));
                    if (setalt)
                    {
                        // This test is messed up, just move on
                    }
                    AddItemTest(ev, $"{(setalt ? "set flag" : "unset flag")} check for {(and ? "AND" : "OR")} register");
                }
            }
            EMEVD.Event setter = new EMEVD.Event(evBase + i);
            // setter.Instructions.Add(ParseAdd($"WAIT Fixed Time (Frames) (20)"));
            foreach (int flag in flagsToSet)
            {
                setter.Instructions.Add(ParseAdd($"Set Event Flag ({flag},1)"));
            }
            AddEvent(setter);
            /*
            1. If set flag check for AND register, then awarding Fine Snow
            2. If unset flag check for AND register, then awarding Rice
            3. If set flag check for OR register, then awarding Remnant Monk
            4. If unset flag check for OR register, then awarding Remnant Ape
            Output with 1 frame delay: monk, ape
            Output with no delay or 0 frame delay: fine snow, monk, ape
            Result: Ordering does not matter for main group evaluation
            */
        }

        private void TestDelayedUndefinedEvaluation()
        {
            // Recall: Compiled default false, uncompiled default true
            // cond[5] &= cond[1]
            // cond[5] &= flag
            // Compile cond[5]
            // (After delay seconds, set flag to true)
            List<int> flagsToSet = new List<int>();
            foreach (bool delay in new[] { true, false })
            {
                foreach (bool truecond in new[] { true, false })
                {
                    foreach (bool and in new[] { true, false })
                    {
                        int reg = and ? 5 : -5;
                        int flag = trueFlag;
                        if (delay)
                        {
                            flag = flagBase + i;
                            flagsToSet.Add(flag);
                        }
                        int val = truecond ? 1 : 0;
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg},1,0,{flag})"));
                        ev.Instructions.Add(ParseAdd($"IF Condition Group ({reg},{val},-1)"));
                        ev.Instructions.Add(ParseAdd($"IF Condition Group (0,1,{reg})"));
                        AddItemTest(ev, $"{(delay ? "delay" : "immediate")} {truecond} check for {(and ? "AND" : "OR")} register");
                    }
                }
            }
            EMEVD.Event setter = new EMEVD.Event(evBase + i);
            setter.Instructions.Add(ParseAdd($"WAIT Fixed Time (Frames) (40)"));
            foreach (int flag in flagsToSet)
            {
                setter.Instructions.Add(ParseAdd($"Set Event Flag ({flag},1)"));
            }
            AddEvent(setter);
            /*
            1. If delay True check for AND register, then awarding Fine Snow
            2. If delay True check for OR register, then awarding Rice
            3. If delay False check for AND register, then awarding Remnant Monk
            4. If delay False check for OR register, then awarding Remnant Ape
            5. If immediate True check for AND register, then awarding Remnant Monkeys
            6. If immediate True check for OR register, then awarding Remnant Genichiro
            7. If immediate False check for AND register, then awarding Remnant Butterfly
            8. If immediate False check for OR register, then awarding Remnant Gyoubu
            Output: 2 5 6 8. Then after delay: 1 4. Never: 3 7
            Note that compiled is default false, uncompiled is default true.
            In this context, AND(01) is considered true always.
            Output if OR(01) is used instead: 2 5 6 8. Then 1 4. So same.
            */
        }

        private void TestUncompiledUse()
        {
            foreach (bool compiled in new[] { true, false })
            {
                foreach (bool start in new[] { true, false })
                {
                    foreach (bool check in new[] { true, false })
                    {
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"IF Event Flag (1,1,0,{(start ? trueFlag : falseFlag)})"));
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State ({(compiled ? "Compiled" : "Uncompiled")}) (1,{(check ? 1 : 0)},1)"));
                        AddItemTest(ev, $"{(compiled ? "compiled" : "uncompiled")} {check} check for {start}");
                    }
                }
            }
            /*
            1. If compiled True check for True, then awarding Fine Snow
            2. If compiled False check for True, then awarding Rice
            3. If compiled True check for False, then awarding Remnant Monk
            4. If compiled False check for False, then awarding Remnant Ape
            5. If uncompiled True check for True, then awarding Remnant Monkeys
            6. If uncompiled False check for True, then awarding Remnant Genichiro
            7. If uncompiled True check for False, then awarding Remnant Butterfly
            8. If uncompiled False check for False, then awarding Remnant Gyoubu
            Expected if uncompiled holds: 6 7 (not skipped), and not 5 8
            Expected if compiled ignores uncompiled: 1 3, with true check vs false register
            Output: 1 3 6 7
            */
        }

        private void TestUncompiledCompileUse()
        {
            foreach (bool direct in new[] { true, false })
            {
                foreach (bool start in new[] { true, false })
                {
                    foreach (bool check in new[] { true, false })
                    {
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"IF Event Flag (1,1,0,{(start ? trueFlag : falseFlag)})"));
                        if (!direct)
                        {
                            ev.Instructions.Add(ParseAdd($"IF Condition Group (2,1,1)"));
                        }
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State (Uncompiled) (1,1,{(direct ? 1 : 2)})"));
                        ev.Instructions.Add(ParseAdd($"Label 0 ()"));
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State (Compiled) (1,{(check ? 1 : 0)},1)"));
                        AddItemTest(ev, $"{(direct ? "direct" : "indirect")} {check} check for {start}");
                    }
                }
            }
            /*
            1. If direct True check for True, then awarding Fine Snow
            2. If direct False check for True, then awarding Rice
            3. If direct True check for False, then awarding Remnant Monk
            4. If direct False check for False, then awarding Remnant Ape
            5. If indirect True check for True, then awarding Remnant Monkeys
            6. If indirect False check for True, then awarding Remnant Genichiro
            7. If indirect True check for False, then awarding Remnant Butterfly
            8. If indirect False check for False, then awarding Remnant Gyoubu
            Expected if no possible influence: 1 3 5 7 (check is true)
            Result: Uncompiled condition groups have no effect on compiled checks
            */
        }

        private void TestMultiCompile()
        {
            // Use and/or register as true/false
            // Main eval if true/false
            // Skip if compiled/uncompiled true/false after
            foreach (bool direct in new[] { true, false })
            {
                foreach (bool start in new[] { true, false })
                {
                    foreach (bool check in new[] { true, false })
                    {
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"IF Event Flag (1,1,0,{(start ? trueFlag : falseFlag)})"));
                        ev.Instructions.Add(ParseAdd($"IF Condition Group (0,{(start ? 1 : 0)},1)"));
                        ev.Instructions.Add(ParseAdd($"IF Event Flag (2,1,0,{(start ? trueFlag : falseFlag)})"));
                        bool interfere = true;
                        if (interfere)
                        {
                            ev.Instructions.Add(ParseAdd($"IF Event Flag (1,1,0,{(start ? falseFlag : trueFlag)})"));
                        }
                        ev.Instructions.Add(ParseAdd($"IF Condition Group (0,{(start ? 1 : 0)},2)"));
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State (Compiled) (1,{(check ? 1 : 0)},{(direct ? 2 : 1)})"));
                        AddItemTest(ev, $"{(direct ? "direct" : "indirect")} {check} check for {start}");
                    }
                }
            }
            /*
            1. If direct True check for True, then awarding Fine Snow
            2. If direct False check for True, then awarding Rice
            3. If direct True check for False, then awarding Remnant Monk
            4. If direct False check for False, then awarding Remnant Ape
            5. If indirect True check for True, then awarding Remnant Monkeys
            6. If indirect False check for True, then awarding Remnant Genichiro
            7. If indirect True check for False, then awarding Remnant Butterfly
            8. If indirect False check for False, then awarding Remnant Gyoubu
            Expected direct output: when check != start, or 2 3
            Expected indirect output if reset: 5 7, where check is true
            Output: 2 3 6 7
            Result: Compilation only affects used condition groups
            With interference: output is 6 8 instead for indirect, when check is true (meaning: 1 is undefined, so it is false?).
            */
        }

        private void TestDelayedMultiCompile()
        {
            // Use and/or register as true/false
            // Main eval if true/false
            // Skip if compiled/uncompiled true/false after
            List<int> flagsToSet = new List<int>();
            foreach (bool start2 in new[] { true, false })
            {
                foreach (bool start in new[] { true, false })
                {
                    foreach (bool check in new[] { true, false })
                    {
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        int flag = flagBase + i;
                        flagsToSet.Add(flag);
                        int reg = -1;
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg},1,0,{(start ? trueFlag : falseFlag)})"));
                        ev.Instructions.Add(ParseAdd($"IF Condition Group (0,{(start ? 1 : 0)},{reg})"));
                        ev.Instructions.Add(ParseAdd($"IF Event Flag (2,1,0,{flag})"));
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg},1,0,{(start2 ? trueFlag : falseFlag)})"));
                        ev.Instructions.Add(ParseAdd($"IF Condition Group (0,1,2)"));
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State (Compiled) (1,{(check ? 1 : 0)},{reg})"));
                        AddItemTest(ev, $"{check} check for {start}->{start2}");
                    }
                }
            }
            EMEVD.Event setter = new EMEVD.Event(evBase + i);
            setter.Instructions.Add(ParseAdd($"WAIT Fixed Time (Frames) (40)"));
            foreach (int flag in flagsToSet)
            {
                setter.Instructions.Add(ParseAdd($"Set Event Flag ({flag},1)"));
            }
            AddEvent(setter);

            /*
            1. If True check for True->True, then awarding Fine Snow
            2. If False check for True->True, then awarding Rice
            3. If True check for False->True, then awarding Remnant Monk
            4. If False check for False->True, then awarding Remnant Ape
            5. If True check for True->False, then awarding Remnant Monkeys
            6. If False check for True->False, then awarding Remnant Genichiro
            7. If True check for False->False, then awarding Remnant Butterfly
            8. If False check for False->False, then awarding Remnant Gyoubu
            Result with AND/OR: 2 4 6 7
            Result with no start2 set: 2 3 6 7
            Setting the register makes a difference if initial is false, and second is true
            Conclusion: Can't rely on main group evaluation to always set compiled state, so never inline groups with more than one usage, to be safe.
            */
        }

        private void TestDefinedPostWait()
        {
            // Variant of TestDefinedPostWait
            // Instead of main group eval, use wait command
            foreach (bool compiled in new[] { true, false })
            {
                foreach (bool start in new[] { true, false })
                {
                    foreach (bool and in new[] { true, false })
                    {
                        int reg = and ? 1 : -1;
                        int val = start ? 1 : 0;
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg},1,0,{(start ? trueFlag : falseFlag)})"));
                        // ev.Instructions.Add(ParseAdd($"IF Condition Group (0,{val},{reg})"));
                        ev.Instructions.Add(ParseAdd($"WAIT For Condition Group State ({val},{reg})"));
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State ({(compiled ? "Compiled" : "Uncompiled")}) (1,{val},{reg})"));
                        AddItemTest(ev, $"{(compiled ? "compiled" : "uncompiled")} {start} check for {(and ? "AND" : "OR")}");
                    }
                }
            }
            /*
            1. If compiled True check for AND, then awarding Fine Snow
            2. If compiled True check for OR, then awarding Rice
            3. If compiled False check for AND, then awarding Remnant Monk
            4. If compiled False check for OR, then awarding Remnant Ape
            5. If uncompiled True check for AND, then awarding Remnant Monkeys
            6. If uncompiled True check for OR, then awarding Remnant Genichiro
            7. If uncompiled False check for AND, then awarding Remnant Butterfly
            8. If uncompiled False check for OR, then awarding Remnant Gyoubu
            Expected if compiled values hold: none of 1 2 3 4 (skipping if state matches flag)
            Expected if uncompiled values ignored: 7 8, since uncompiled default true
            Actual output from main eval: only 7 8
            Actual output of this:
            */
        }

        // Possible further investigation:
        // Check out Sekiro 11105791 for ClearCompiledConditionGroupState on uncompiled
        // Likewise, Sekiro 20004010 for using compiled condition groups from OR of uncompiled
        // Compiled condition groups - are these retained across multiple MAIN groups? DS1 11010001
        // Does default state of compiled condition group change after single evaluation?

        // Compiled default false, uncompiled default true
        private void RunTest()
        {
            TestClearGroups();
            // TestDefinedPostEval();
            // TestChangeState();
            // TestUnrelatedCompilation();
            // TestMixedCompilation();
            // TestMixedCompilation2();
            // TestDelayedCompileOrder();
            // TestDelayedUndefinedEvaluation();
            // TestUncompiledUse();
            // TestUncompiledCompileUse();
            // TestMultiCompile();
            // TestDelayedMultiCompile();
            // TestDefinedPostWait();
        }

        private void AddEvent(EMEVD.Event ev)
        {
            emevd.Events.Add(ev);
            addEvents.Add((int)ev.ID);
            i++;
        }
        private void AddItemTest(EMEVD.Event ev, string desc, int group = -99)
        {
            int itemLot = testItemLotOrder.Last();
            testItemLotOrder.Remove(itemLot);
            if (group > -99)
            {
                ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State (Uncompiled) (1,0,{group})"));
            }
            ev.Instructions.Add(ParseAdd($"Award Item Lot ({itemLot})"));
            Console.WriteLine($"{i}. If{(group > -99 ? $" {group} in" : "")} {desc}, then awarding {testItemLots[itemLot]}");
            AddEvent(ev);
        }

        // Very simple command parsing scheme.
        private static (string, List<string>) ParseCommandString(string add)
        {
            int sparen = add.LastIndexOf('(');
            int eparen = add.LastIndexOf(')');
            if (sparen == -1 || eparen == -1) throw new Exception($"Bad command string {add}");
            string cmd = add.Substring(0, sparen).Trim();
            return (cmd, add.Substring(sparen + 1, eparen - sparen - 1).Split(',').Where(arg => !string.IsNullOrWhiteSpace(arg)).ToList());
        }

        private EMEVD.Instruction ParseAdd(string add)
        {
            (string cmd, List<string> addArgs) = ParseCommandString(add);
            if (!docByName.TryGetValue(cmd, out (int, int) docId)) throw new Exception($"Unrecognized command '{cmd}'");
            EMEDF.InstrDoc addDoc = doc[docId.Item1][docId.Item2];
            List<ArgType> argTypes = addDoc.Arguments.Select(arg => arg.Type == 8 ? ArgType.UInt32 : (ArgType)arg.Type).ToList();
            if (addArgs.Count != argTypes.Count) throw new Exception($"Expected {argTypes.Count} arguments for {cmd}, given {addArgs.Count} in {add}");
            return new EMEVD.Instruction(docId.Item1, docId.Item2, addArgs.Select((a, j) => ParseArg(a, argTypes[j])));
        }

        private static object ParseArg(string arg, ArgType type)
        {
            switch (type)
            {
                case ArgType.Byte:
                    return byte.Parse(arg);
                case ArgType.UInt16:
                    return ushort.Parse(arg);
                case ArgType.UInt32:
                    return uint.Parse(arg);
                case ArgType.SByte:
                    return sbyte.Parse(arg);
                case ArgType.Int16:
                    return short.Parse(arg);
                case ArgType.Int32:
                    return int.Parse(arg);
                case ArgType.Single:
                    return float.Parse(arg);
                default:
                    throw new Exception($"Unrecognized arg type {type}");
            }
        }
    }
}
