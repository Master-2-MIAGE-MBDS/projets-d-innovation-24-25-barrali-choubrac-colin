using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using DeepBridgeWindowsApp.UI.Forms;
using DeepBridgeWindowsApp.Utils;

namespace DeepBridgeWindowsApp
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                Console.WriteLine("DeepBridge DICOM Viewer Starting...");

                // Initialize logger
                Logger.Initialize(enableFileLogging: true);
                Logger.Info("Application starting", "Program");

                Application.SetHighDpiMode(HighDpiMode.SystemAware);
                Application.SetCompatibleTextRenderingDefault(false);

                // Set up exception handling
                Application.ThreadException += Application_ThreadException;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                // Run the main form
                Application.Run(new MainForm());

                Logger.Info("Application exiting normally", "Program");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unhandled exception occurred: {ex.Message}\n\nSee log for details.",
                               "Application Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.Error("Application crashed", ex, "Program");
            }
        }

        private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            Logger.Error("Unhandled UI thread exception", e.Exception, "Program");
            MessageBox.Show($"An error occurred: {e.Exception.Message}\n\nSee log for details.",
                           "Application Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            Logger.Error("Unhandled application exception", ex, "Program");
            MessageBox.Show($"A fatal error occurred: {ex?.Message ?? "Unknown error"}\n\nSee log for details.",
                           "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}