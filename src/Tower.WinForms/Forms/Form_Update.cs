using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Tower.Infrastructure;

namespace Tower.WinForms.Forms;

public partial class Form_Update : Form
{
    private GitHubUpdates? updates;
    private string installedVersion = "";
    private TowerUpdate? available;
    private CancellationTokenSource? operation;
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? InstallerPath { get; private set; }

    public Form_Update()
    {
        InitializeComponent();
    }

    public void Configure(GitHubUpdates service, string currentVersion)
    {
        updates = service;
        installedVersion = currentVersion;
    }

    internal static string CurrentVersion => typeof(Form_Start).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(Form_Start).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    private async void Form_Update_Load(object? sender, EventArgs e)
    {
        if (DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime)
        {
            return;
        }
        updates ??= new();
        if (installedVersion.Length == 0)
        {
            installedVersion = CurrentVersion;
        }
        Label_Installed.Text = "Installed version: " + installedVersion.Split('+')[0];
        await CheckAsync();
    }

    private void Busy(bool busy)
    {
        Button_Check.Enabled = !busy;
        Button_Install.Enabled = !busy && available is not null;
        Button_Close.Text = busy ? "Cancel" : "Close";
    }

    private async Task CheckAsync()
    {
        operation = new();
        available = null;
        Busy(true);
        Progress_Download.Style = ProgressBarStyle.Marquee;
        Label_Status.Text = "Checking for updates...";
        TextBox_Notes.Clear();
        try
        {
            var latest = await updates!.LatestAsync(operation.Token);
            if (latest is null)
            {
                Label_Status.Text = "No public stable release is available yet.";
            }
            else if (!latest.IsNewerThan(installedVersion))
            {
                Label_Status.Text = "Tower is up to date.";
            }
            else
            {
                available = latest;
                Label_Status.Text = $"Tower {latest.Version} is available.";
                TextBox_Notes.Text = string.IsNullOrWhiteSpace(latest.Notes) ? "No release notes were provided." : latest.Notes;
            }
        }
        catch (OperationCanceledException)
        {
            Label_Status.Text = "Update check cancelled or timed out. You can try again.";
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            Label_Status.Text = "Could not check for updates. " + exception.Message;
        }
        finally
        {
            operation.Dispose();
            operation = null;
            Progress_Download.Style = ProgressBarStyle.Blocks;
            Progress_Download.Value = 0;
            Busy(false);
        }
    }

    private async void Button_Check_Click(object? sender, EventArgs e) => await CheckAsync();

    private async void Button_Install_Click(object? sender, EventArgs e)
    {
        if (available is not { } update || operation is not null)
        {
            return;
        }
        if (MessageBox.Show(this, $"Download and install Tower {update.Version}? Tower will close after the download is verified.",
            "Update Tower", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }
        operation = new();
        Busy(true);
        Label_Status.Text = "Downloading and verifying the installer...";
        try
        {
            var downloaded = await updates!.DownloadAsync(update, Path.Combine(Path.GetTempPath(), "TowerUpdates"),
                new Progress<int>(value =>
                {
                    if (!IsDisposed && !Disposing)
                    {
                        Progress_Download.Value = value;
                    }
                }), operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            InstallerPath = downloaded;
        }
        catch (OperationCanceledException)
        {
            Label_Status.Text = "Download cancelled. Nothing has been installed.";
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Label_Status.Text = "Could not download the update. " + exception.Message;
        }
        finally
        {
            operation.Dispose();
            operation = null;
            Busy(false);
        }
        if (InstallerPath is not null)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private void Button_Close_Click(object? sender, EventArgs e) => Close();

    private void Form_Update_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (operation is not null)
        {
            operation.Cancel();
            e.Cancel = true;
        }
    }

    private void Form_Update_FormClosed(object? sender, FormClosedEventArgs e) => updates?.Dispose();
}
