using System.ComponentModel;
using Tower.Infrastructure;
using TowerSpeech.Core;

namespace Tower.WinForms.Forms;

public partial class Form_ManageAirports : Form
{
    private string dataRoot = "";
    private bool busy;
    public bool AirportsChanged { get; private set; }

    public Form_ManageAirports() => InitializeComponent();

    public Form_ManageAirports(string dataRoot) : this()
    {
        this.dataRoot = Path.GetFullPath(dataRoot);
    }

    private void Form_ManageAirports_Load(object? sender, EventArgs e)
    {
        if (DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime)
        {
            return;
        }
        if (dataRoot.Length == 0)
        {
            dataRoot = WorkspacePaths.DefaultRoot;
        }
        RefreshAirports();
    }

    private void RefreshAirports()
    {
        var selected = SelectedAirport?.Code;
        Grid_Airports.Rows.Clear();
        foreach (var airport in AirportPackages.Installed(dataRoot))
        {
            var row = Grid_Airports.Rows[Grid_Airports.Rows.Add(airport.Name, airport.Code,
                airport.Version ?? (airport.Code == "CYN" ? "Included" : "Unknown"),
                airport.InstalledAtUtc?.ToLocalTime().ToString("g") ?? (airport.Code == "CYN" ? "—" : "Unknown"), airport.Status)];
            row.Tag = airport;
            if (airport.Code == selected)
            {
                Grid_Airports.CurrentCell = row.Cells[0];
            }
        }
        UpdateButtons();
    }

    private InstalledAirport? SelectedAirport => Grid_Airports.CurrentRow?.Tag as InstalledAirport;

    private void Grid_Airports_SelectionChanged(object? sender, EventArgs e) => UpdateButtons();

    private void UpdateButtons()
    {
        Button_Install.Enabled = !busy;
        Button_Remove.Enabled = !busy && SelectedAirport?.CanRemove == true;
        Button_Close.Enabled = !busy;
        Grid_Airports.Enabled = !busy;
    }

    private void SetBusy(bool value)
    {
        busy = value;
        UseWaitCursor = value;
        UpdateButtons();
    }

    private async void Button_Install_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Install Airport",
            Filter = "Tower airport packages (*.towerairport)|*.towerairport",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        try
        {
            using var packageReadLock = new FileStream(dialog.FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            var manifest = AirportPackages.Inspect(dialog.FileName);
            var replace = AirportPackages.NeedsReplacement(dataRoot, manifest);
            var text = $"Install {manifest.Name} ({manifest.AirportCode})?\r\nVersion: {manifest.Version}\r\n\r\nDestination: {dataRoot}";
            if (replace)
            {
                text += "\r\n\r\nExisting airport files will be replaced and backed up.";
            }
            if (MessageBox.Show(this, text, "Install Airport", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }
            SetBusy(true);
            Label_Status.Text = "Installing " + manifest.Name + "...";
            await Task.Run(() => AirportPackages.Install(dialog.FileName, dataRoot, replace));
            AirportsChanged = true;
            RefreshAirports();
            Label_Status.Text = manifest.Name + " installed successfully.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or
            System.Text.Json.JsonException or ArgumentException or InvalidOperationException)
        {
            Label_Status.Text = "Installation failed.";
            MessageBox.Show(this, exception.Message, "Install Airport", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void Button_Remove_Click(object? sender, EventArgs e)
    {
        var airport = SelectedAirport;
        if (airport?.CanRemove != true)
        {
            return;
        }
        if (MessageBox.Show(this, $"Remove {airport.Name} ({airport.Code})?\r\n\r\nAirport files will be backed up. Shared models, voice files and settings will be kept.",
            "Remove Airport", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }
        try
        {
            SetBusy(true);
            Label_Status.Text = "Removing " + airport.Name + "...";
            await Task.Run(() => AirportPackages.Remove(dataRoot, airport.Code));
            AirportsChanged = true;
            RefreshAirports();
            Label_Status.Text = airport.Name + " removed successfully.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or
            System.Text.Json.JsonException or ArgumentException or InvalidOperationException)
        {
            Label_Status.Text = "Removal failed.";
            MessageBox.Show(this, exception.Message, "Remove Airport", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Form_ManageAirports_FormClosing(object? sender, FormClosingEventArgs e)
    {
        e.Cancel = busy;
    }
}
