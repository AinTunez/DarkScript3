using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Runtime.InteropServices;

namespace DarkScript3
{
    static class Program
    {
        // https://stackoverflow.com/questions/7198639/c-sharp-application-both-gui-and-commandline
        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            bool commandLine = args.Length > 0 && args[0] == "/cmd";
#if DEBUG
            commandLine = args.Length > 0;
#endif
            if (commandLine)
            {
                if (args[0] == "/cmd")
                {
                    args = args.Skip(1).ToArray();
                }
                // Command line things for testing
                AttachConsole(-1);
                // These are part of the release binary.
                if (args.Contains("html"))
                {
                    EMEDF2HTML.Generate(args);
                }
                else if (args.Contains("-decompile"))
                {
                    RoundTripTool.Decompile(args);
                }
#if DEBUG
                // The rest of these have quite unstructured arguments, so don't include them in the release binary.
                else if (args.Contains("test"))
                {
                    // new CondTestingTool().Run(args);
                    new CondTestingTool().DumpTypes();
                }
                else
                {
                    RoundTripTool.Run(args);
                }
#endif
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new GUI());
        }
    }
}
