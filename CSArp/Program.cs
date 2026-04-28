using System;
using System.Windows.Forms;

namespace CSArp
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ScannerForm());
        }
    }
}