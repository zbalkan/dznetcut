using System;
using System.Windows.Forms;
using dznetcut.CLI;
using dznetcut.GUI;

namespace dznetcut
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            var parsedArguments = CliArguments.Parse(args ?? Array.Empty<string>());
            if (parsedArguments.LaunchGui)
            {
                LaunchGui();
                return;
            }

            var commandRouter = new CliCommandRouter(Console.WriteLine);
            Environment.ExitCode = commandRouter.Execute(parsedArguments);
        }

        private static void LaunchGui()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ScannerForm());
        }
    }
}
