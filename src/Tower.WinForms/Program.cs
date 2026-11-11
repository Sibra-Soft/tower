using Tower.Core;
using Tower.Infrastructure;

namespace Tower.WinForms;

internal static class Program
{
    public static Airport Airport = Airport.Training();
    public static ScenarioOptions Options = new();
    public static VoicePlayer Voice { get; } = new();
    public static Core Application = new();
    public static DisplaySettings Settings { get; set; } = new();
    public static List<ControllerProfile> Profiles = [];
    internal static string? PendingUpdateInstaller { get; set; }

    [STAThread]
    private static int Main()
    {
        using (Application)
        {
            Application.Execute();
        }

        if (PendingUpdateInstaller is { } installer)
        {
            try
            {
                using var helper = System.Diagnostics.Process.Start(UpdateHandoff.StartInfo(installer, AppContext.BaseDirectory, Environment.ProcessId));
            }
            catch (Exception exception) when (exception is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                MessageBox.Show("Tower has closed, but the installer could not be started. Run this downloaded installer manually:\n"
                    + installer + "\n\n" + exception.Message, "Tower Update", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        return 0;
    }
}
