using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SoulsFormats;

namespace CompareEMEVD
{
    class Program
    {
        static void Main(string[] args)
        {
            var evdA = EMEVD.Read(@"C:\Program Files (x86)\Steam\steamapps\common\DARK SOULS REMASTERED\event\m11_00_00_00.emevd.dcx.bak");
            var evdB = EMEVD.Read(@"C:\Program Files (x86)\Steam\steamapps\common\DARK SOULS REMASTERED\event\m11_00_00_00.emevd.dcx");

            for (int i = 0; i < evdA.Events.Count; i++)
            {
                var evtA = evdA.Events[i];
                var evtB = evdB.Events[i];

                for (int k = 0; k < evtA.Instructions.Count; k++)
                {
                    var insA = evtA.Instructions[k];
                    var insB = evtB.Instructions[k];

                    if (!insA.ArgData.SequenceEqual(insB.ArgData))
                    {
                        Console.WriteLine($"ArgData Mismatch: Event {evtA.ID}, Instruction {k}");
                        Console.WriteLine($@"{insA.Bank}[{insA.ID}]");
                        Console.WriteLine($@"A: [{string.Join(", ", insA.ArgData)}]");
                        Console.WriteLine($@"B: [{string.Join(", ", insB.ArgData)}]");
                    }
                }
            }
            Console.ReadLine();
        }
    }
}
