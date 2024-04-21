using System;
using System.Collections.Generic;
using System.Linq;
namespace TestConsoleApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Contains("maptest"))
            {
                MapTest.Run(args).Wait();
            }
            else if (args.Contains("fmgdata"))
            {
                FmgData.Run(args);
            }
        }
    }
}
