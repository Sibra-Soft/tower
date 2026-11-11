using Tower.Core;
using Tower.Infrastructure;
using Tower.WinForms.Forms;
using TowerSpeech.Core;

namespace Tower.WinForms;

internal sealed partial class Core : IDisposable
{
    private string OriginalDirectory => WorkspacePaths.DefaultRoot;

    public readonly List<Airport> AvailableAirports = [];
    private readonly List<IDisposable> assets = [];

    private static string SettingsPath => IniSettingsStore.ResolvePath(AppContext.BaseDirectory);
    private static string ProfilesPath => Path.Combine(Path.GetDirectoryName(SettingsPath)!, "controllers.json");
    private static double FiniteRange(double value, double min, double max, double fallback) => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    public static void SelectAirport(Airport selected)
    {
        Program.Airport = selected;
        Program.Settings.SelectedAirport = selected.Code;
        var runways = OriginalData.ReadDefaultRunways(SettingsPath, selected);
        Program.Options.Runway = runways[0];
        Program.Options.ArrivalRunways = runways;
        Program.Voice.ReloadSpeechWorkspace();
    }

    public void Execute()
    {
        try
        {
            var controllerIndex = new IniSettingsStore(SettingsPath).Load(Program.Settings, Program.Options);

            if (File.Exists(ProfilesPath))
            {
                Program.Profiles = JsonStore.Read<List<ControllerProfile>>(ProfilesPath);
            }
            if (Program.Profiles.Count > 0)
            {
                Program.Settings.SelectedController = Program.Profiles[Math.Clamp(controllerIndex, 0, Program.Profiles.Count - 1)].Id;
            }
        }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException)
        {
            MessageBox.Show($"Settings could not be loaded: {e.Message}", "Tower");
        }

        Program.Settings.BriteRange = FiniteRange(Program.Settings.BriteRange, 2, 50, 14.5);
        Program.Settings.DataDirectory = OriginalDirectory;
        Program.Settings.AsdeRange = FiniteRange(Program.Settings.AsdeRange, 0.1, 10, 1.6);
        Program.Settings.RangeMarks = FiniteRange(Program.Settings.RangeMarks, 0.1, 25, 5);

        if (Program.Profiles.Count == 0)
        {
            Program.Profiles.Add(new()
            {
                Name = "Controller"
            });
        }

        if (!Program.Profiles.Any(p => p.Id == Program.Settings.SelectedController))
        {
            Program.Settings.SelectedController = Program.Profiles[0].Id;
        }

        try
        {
            AirportPackages.RecoverInterrupted(OriginalDirectory);
            ReloadAirports();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            MessageBox.Show("Airport data could not be loaded or recovered. " + exception.Message, "Tower", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (AvailableAirports.Count == 0)
        {
            MessageBox.Show("No airport data was found in " + OriginalDirectory + ". Restore the game Data folder before starting Tower.", "Tower", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        SelectAirport(AvailableAirports.FirstOrDefault(a => a.Code == Program.Settings.SelectedAirport) ?? AvailableAirports[0]);

        using var startForm = new Form_Start();
        Application.Run(startForm);
    }

    public void ReloadAirports()
    {
        var airports = OriginalData.InstalledAirports(OriginalDirectory).Select(code => OriginalData.LoadAirport(OriginalDirectory, code)).ToArray();
        AvailableAirports.Clear();
        AvailableAirports.AddRange(airports);
    }

    public void Dispose()
    {
        Program.Voice.Dispose();

        foreach (var item in assets)
        {
            item.Dispose();
        }
    }

    public Session OpenOriginalScenario(string path)
    {
        var code = OriginalData.ReadScenarioAirportCode(path);
        var airport = OriginalData.LoadAirport(OriginalDirectory, code);
        var session = OriginalData.ReadScenario(path, airport);
        session.ControllerId = Program.Settings.SelectedController;
        return session;
    }

    public void SaveSettings()
    {
        SaveDisplaySettings();
        JsonStore.Write(ProfilesPath, Program.Profiles);
    }

    public PerformanceReward? RecordPerformance(Session session)
    {
        if (session.Options.Practice || session.ResultsRecorded)
        {
            return null;
        }
        var profile = Program.Profiles.FirstOrDefault(profile => profile.Id == session.ControllerId)
            ?? throw new InvalidOperationException("The controller for this session could not be found.");
        if (profile.RecordedSessions.Contains(session.Id))
        {
            session.ResultsRecorded = true;
            return null;
        }
        // Commit a copy first, so a failed disk write can be retried without duplicate credit.
        var copy = System.Text.Json.JsonSerializer.Deserialize<ControllerProfile>(
            System.Text.Json.JsonSerializer.Serialize(profile, JsonStore.Options), JsonStore.Options)!;
        var reward = ControllerPerformance.Record(copy, session, Random.Shared.Next(100));
        var profiles = Program.Profiles.Select(value => value.Id == copy.Id ? copy : value).ToList();
        JsonStore.Write(ProfilesPath, profiles);
        Program.Profiles = profiles;
        session.ResultsRecorded = true;
        return reward;
    }

    public void SetSoundTalk(bool enabled)
    {
        var previous = Program.Settings.SoundTalk;
        Program.Settings.SoundTalk = enabled;
        try
        {
            SaveDisplaySettings();
        }
        catch
        {
            Program.Settings.SoundTalk = previous;
            throw;
        }
        if (!enabled)
        {
            Program.Voice.ClearPending();
        }
    }

    public void SaveDisplaySettings() => new IniSettingsStore(SettingsPath).Save(Program.Settings, Program.Options,
        Program.Profiles.FindIndex(profile => profile.Id == Program.Settings.SelectedController), Program.Airport.Code);
}
