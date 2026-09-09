using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace UVConsole
{
    internal static class Program
    {
        private static string ErrorLogPath()
        {
            try { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UVConsole_STARTUP_ERROR.log"); }
            catch { return "UVConsole_STARTUP_ERROR.log"; }
        }

        private static void WriteCrashLog(string stage, Exception ex)
        {
            try
            {
                StringBuilder b = new StringBuilder();
                b.AppendLine("UV Console by WRCX 212 startup/runtime error");
                b.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                b.AppendLine("Stage: " + stage);
                b.AppendLine("OS: " + Environment.OSVersion);
                b.AppendLine("CLR: " + Environment.Version);
                b.AppendLine();
                b.AppendLine(ex == null ? "Unknown error" : ex.ToString());
                File.WriteAllText(ErrorLogPath(), b.ToString());
            }
            catch { }
        }

        private static void ShowCrash(string stage, Exception ex)
        {
            WriteCrashLog(stage, ex);
            try
            {
                string text = "UV Console could not continue.\r\n\r\n" +
                              (ex == null ? "Unknown startup error." : ex.Message) +
                              "\r\n\r\nA diagnostic file was written to:\r\n" + ErrorLogPath();
                MessageBox.Show(text, "UV Console startup error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                ShowCrash("Windows UI thread", e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Exception ex = e.ExceptionObject as Exception;
                WriteCrashLog("Unhandled AppDomain exception", ex);
            };

            try
            {
                MainForm main = new MainForm();
                Application.Run(main);
            }
            catch (Exception ex)
            {
                ShowCrash("MainForm startup", ex);
            }
        }
    }
}
