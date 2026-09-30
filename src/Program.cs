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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new QFG5Extractor.Core.MainForm());
        }
    }
}
