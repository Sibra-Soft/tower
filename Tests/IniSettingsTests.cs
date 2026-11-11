using System.Globalization;
using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class IniSettingsTests : IDisposable
{
    [Fact]
    public void Expanded_command_input_roundtrips_and_old_ini_defaults_to_short_input()
    {
        File.WriteAllText(FilePath, "[Commands]\nUnrelated=keep\nExpandInput=On\n");
        var store = new IniSettingsStore(FilePath);
        var settings = new DisplaySettings();
        var options = new ScenarioOptions();
        store.Load(settings, options);
        Assert.True(settings.ExpandCommandInput);
        settings.ExpandCommandInput = false;
        store.Save(settings, options, 0, "");
        store.Load(settings, options);
        Assert.False(settings.ExpandCommandInput);
        Assert.Contains("Unrelated=keep", File.ReadAllText(FilePath));
        File.WriteAllText(FilePath, "[Sound]\nTalk=Off\n");
        settings.ExpandCommandInput = true;
        store.Load(settings, options);
        Assert.False(settings.ExpandCommandInput);
    }

    private readonly string root = Path.Combine(Path.GetTempPath(), "Tower-Ini-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(root, "settings.ini");

    public IniSettingsTests() => Directory.CreateDirectory(root);

    [Fact]
    public void LoadsLegacyFieldsAndApprovedSettings()
    {
        File.WriteAllText(FilePath, "[Display]\nASDE=2.5,12,34,Off,On,Off,On\nBRITE=20,4,5,3,Off,On,Off,On\nShowAircraftLabels=Off\nDefaultWindowLayout=Custom.SET\n[Weather]\nWindDir=250\nWindSpeed=10\nVisibility=11\n[Scenario]\nACPerMin=3,2\nTimeOfDayValue=3338\nPrintStrips=Yes\nPaperType=Strip\n[Sound]\nTalk=Off\n[User]\nLast=2\nSelectedAirport=CYN\n");
        var settings = new DisplaySettings();
        var options = new ScenarioOptions();
        Assert.Equal(2, new IniSettingsStore(FilePath).Load(settings, options));
        Assert.Equal(2.5, settings.AsdeRange);
        Assert.Equal(20, settings.BriteRange);
        Assert.Equal(3, settings.RangeMarks);
        Assert.False(settings.AsdeMap);
        Assert.True(settings.AsdeTaxiways);
        Assert.False(settings.RunwayNames);
        Assert.True(settings.TaxiNames);
        Assert.False(settings.AsdeGateNames);
        Assert.False(settings.BriteMap);
        Assert.False(settings.BriteObstructions);
        Assert.False(settings.SoundTalk);
        Assert.False(settings.ShowAircraftLabels);
        Assert.Equal("Custom.SET", settings.DefaultWindowLayout);
        Assert.Equal("CYN", settings.SelectedAirport);
        Assert.True(settings.PrintStrips && settings.StripPaper);
        Assert.Equal(1.5, options.AircraftPerMinute);
        Assert.Equal(790, options.StartMinutes);
        Assert.Equal(250, options.WindFromDegrees);
        Assert.Equal(11, options.VisibilityNm);
    }

    [Fact]
    public void SavePreservesUnknownFieldsRadarCoordinatesAndBackupThenRoundTrips()
    {
        const string original = "; retain comment\n[Display]\nASDE=1.6,12,34,On,Off,On,Off,Off,extra\nBRITE=14.5,56,78,5,On,On,On,On\n[Weather]\nCloudTop=11200\n[Scenario]\nPilotType=Perfect\nDCAActiveRunways=36\n[External]\nCustom=unchanged\n";
        File.WriteAllText(FilePath, original);
        var store = new IniSettingsStore(FilePath);
        var settings = new DisplaySettings { AsdeRange = 3.25, ShowCompass = false, SelectedAirport = "CYN" };
        var options = new ScenarioOptions { ArrivalRunways = ["9"], StartMinutes = 13 * 60 + 10 };
        store.Save(settings, options, 1, "CYN");
        var text = File.ReadAllText(FilePath);
        Assert.Contains("; retain comment", text);
        Assert.Contains("ASDE=3.25,12,34,On,On,On,On,Off,extra", text);
        Assert.Contains("CloudTop=11200", text);
        Assert.Contains("PilotType=Perfect", text);
        Assert.Contains("Custom=unchanged", text);
        Assert.Contains("DCAActiveRunways=36", text);
        Assert.Contains("CYNActiveRunways=9", text);
        var backup = Assert.Single(Directory.GetFiles(Path.Combine(root, "Backups", "Settings")));
        Assert.Equal(original, File.ReadAllText(backup));
        var loaded = new DisplaySettings();
        var loadedOptions = new ScenarioOptions();
        Assert.Equal(1, store.Load(loaded, loadedOptions));
        Assert.False(loaded.ShowCompass);
        Assert.Equal(settings.AsdeRange, loaded.AsdeRange);
        Assert.Equal(options.StartMinutes, loadedOptions.StartMinutes);
        Assert.Equal(new[] { "9" }, OriginalData.ReadDefaultRunways(FilePath,
            new Airport { Code = "CYN", Runways = [new() { Name = "9" }] }));
        // An unrelated edit made after startup must survive the next save too.
        File.AppendAllText(FilePath, "Later=retained\n");
        store.Save(settings, options, 1, "CYN");
        Assert.Contains("Later=retained", File.ReadAllText(FilePath));
    }

    [Fact]
    public void GateNamesRoundTripInEighthAsdeFieldWithoutAnExtraKey()
    {
        File.WriteAllText(FilePath, "[Display]\nASDE=1.6,0,0,On,On,On,Off\n");
        var store = new IniSettingsStore(FilePath);
        var settings = new DisplaySettings { AsdeGateNames = true };
        store.Load(settings, new());
        Assert.False(settings.AsdeGateNames);
        foreach (var enabled in new[] { true, false })
        {
            settings.AsdeGateNames = enabled;
            store.Save(settings, new(), 0, "");
            var loaded = new DisplaySettings();
            store.Load(loaded, new());
            Assert.Equal(enabled, loaded.AsdeGateNames);
            var text = File.ReadAllText(FilePath);
            Assert.Contains("ASDE=1.6,0,0,On,On,On,Off," + (enabled ? "On" : "Off"), text);
            Assert.DoesNotContain("AsdeGateNames=", text);
        }
    }

    [Fact]
    public void InvalidNumbersAndFlagsRetainDefaults()
    {
        File.WriteAllText(FilePath, "[Display]\nASDE=NaN,0,0,invalid\nBRITE=Infinity,0,0,-2\nShowCompass=unknown\n[Scenario]\nACPerMin=2,0\nTimeOfDayValue=65535\n[Weather]\nWindSpeed=-10\nVisibility=NaN\n[User]\nLast=-1\n");
        var settings = new DisplaySettings();
        var options = new ScenarioOptions();
        Assert.Equal(0, new IniSettingsStore(FilePath).Load(settings, options));
        Assert.Equal(1.6, settings.AsdeRange);
        Assert.Equal(14.5, settings.BriteRange);
        Assert.Equal(5, settings.RangeMarks);
        Assert.True(settings.AsdeMap && settings.ShowCompass);
        Assert.Equal(0.5, options.AircraftPerMinute);
        Assert.Equal(720, options.StartMinutes);
        Assert.Equal(8, options.WindKnots);
    }

    [Fact]
    public void SavesInvariantNumbersAndDoesNotTouchSpeech()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("nl-NL");
            new IniSettingsStore(FilePath).Save(new() { AsdeRange = 2.75 }, new(), 0, "");
            Assert.Contains("ASDE=2.75,", File.ReadAllText(FilePath));
            Assert.False(Directory.Exists(Path.Combine(root, "Data")));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ResolvesRepositoryOrStandaloneRootRegardlessOfWorkingDirectory()
    {
        File.WriteAllText(Path.Combine(root, "Tower.slnx"), "");
        var output = Directory.CreateDirectory(Path.Combine(root, "Bin", "Debug")).FullName;
        Assert.Equal(FilePath, IniSettingsStore.ResolvePath(output));
        var standalone = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "Tower-Standalone-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            Assert.Equal(Path.Combine(standalone, "settings.ini"), IniSettingsStore.ResolvePath(standalone));
        }
        finally
        {
            Directory.Delete(standalone);
        }
    }

    public void Dispose() => Directory.Delete(root, true);
}
