namespace KidsK_ify;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using (var splash = new SplashForm())
        {
            splash.Show();
            splash.Refresh();
            Thread.Sleep(1100);
            splash.Close();
        }
        Application.Run(new MainForm());
    }
}
