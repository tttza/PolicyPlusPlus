using System;
using System.Windows.Forms;

namespace PolicyPlus
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); // moved from manifest to API per WFAC010 guidance
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new UI.Main.Main());
        }
    }
}
