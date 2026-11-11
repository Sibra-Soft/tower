using System.Diagnostics;
using System.Text;

namespace Tower.WinForms;

internal static class UpdateHandoff
{
    internal static ProcessStartInfo StartInfo(string installer, string installationDirectory, int parentId)
    {
        if (!Path.IsPathFullyQualified(installer) || !File.Exists(installer) || parentId <= 0)
        {
            throw new IOException("The verified installer is no longer available.");
        }
        static string Literal(string value) => "'" + value.Replace("'", "''") + "'";
        // NSIS /D must be last and unquoted; Start-Process joins ArgumentList.
        // The helper waits for this process, not just for its last visible form.
        var script = "$ErrorActionPreference = 'Stop'\r\n"
            + $"Wait-Process -Id {parentId} -ErrorAction SilentlyContinue\r\n"
            + "Start-Process -FilePath " + Literal(installer) + " -ArgumentList "
            + Literal("/D=" + Path.GetFullPath(installationDirectory).TrimEnd(Path.DirectorySeparatorChar)) + "\r\n";
        var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        info.ArgumentList.Add("-NoProfile");
        info.ArgumentList.Add("-NonInteractive");
        info.ArgumentList.Add("-WindowStyle");
        info.ArgumentList.Add("Hidden");
        info.ArgumentList.Add("-EncodedCommand");
        info.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
        return info;
    }
}
