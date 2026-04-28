using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace CSArp.Model.Utilities
{
    public static class DebugOutput
    {
        private static RichTextBox _logTextBox;

        public static void Init(RichTextBox logTextBox) => _logTextBox = logTextBox ?? throw new ArgumentNullException(nameof(logTextBox));

        public static void Print(string output)
        {
            if (_logTextBox == null)
            {
                throw new ArgumentNullException(nameof(_logTextBox));
            }

            try
            {
                var datetimenow = DateTime.Now.ToString();
                _ = _logTextBox.Invoke(new Action(() => {
                    _logTextBox.AppendText(datetimenow + " : " + output + "\n");
                    _logTextBox.SelectionStart = _logTextBox.TextLength;
                    _logTextBox.ScrollToCaret();
                }));

                Debug.Print(output);
            }
            catch (InvalidOperationException) { }
        }
    }
}