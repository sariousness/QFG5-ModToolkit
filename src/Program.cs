using QFG5Extractor.Core;
using System;
using System.Windows.Forms;

namespace QFG5Extractor
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Logger.StartupWatch.Restart();
            Logger.LogStartup("===== QFG5Extractor Performance Baseline START =====");
#if DEBUG
            Logger.LogStartup("Build configuration: Debug");
#else
            Logger.LogStartup("Build configuration: Release");
#endif
            Logger.LogStartup("Program.Main started");
            Logger.LogResource($"Initial process memory: {Logger.GetMemorySnapshot()}");

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new QFG5Extractor.Core.MainForm());
        }
    }
}
