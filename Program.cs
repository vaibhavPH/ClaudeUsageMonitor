namespace ClaudeUsageMonitor;

static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    static void Main()
    {
        try
        {
            // Single instance check
            _mutex = new Mutex(true, "ClaudeUsageMonitor_SingleInstance", out bool isNew);
            if (!isNew)
            {
                MessageBox.Show("Claude Usage Monitor is already running.\nCheck the system tray.",
                    "Already Running", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) =>
            {
                Console.Error.WriteLine($"Thread exception: {e.Exception}");
                MessageBox.Show(e.Exception.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                Console.Error.WriteLine($"Unhandled exception: {e.ExceptionObject}");
                MessageBox.Show(e.ExceptionObject.ToString(), "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            Console.WriteLine("Starting Claude Usage Monitor...");
            ApplicationConfiguration.Initialize();
            Console.WriteLine("Initialized. Creating form...");
            var form = new Form1();
            Console.WriteLine("Form created. Running application...");
            Application.Run(form);

            GC.KeepAlive(_mutex);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Startup error: {ex}");
            MessageBox.Show(ex.ToString(), "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
