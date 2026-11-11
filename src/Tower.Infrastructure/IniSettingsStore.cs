using System.Globalization;
using Tower.Core;

namespace Tower.Infrastructure;

// Only these explicitly approved additions are written alongside legacy fields.
public sealed class IniSettingsStore(string path)
{
    private static readonly string[] VisibilityKeys =
    [
        nameof(DisplaySettings.ShowAsde), nameof(DisplaySettings.ShowBrite), nameof(DisplaySettings.ShowStrips),
        nameof(DisplaySettings.ShowButtons), nameof(DisplaySettings.ShowCompass), nameof(DisplaySettings.ShowGauges),
        nameof(DisplaySettings.ShowAircraftLabels)
    ];

    public static string ResolvePath(string applicationDirectory)
    {
        for (var directory = new DirectoryInfo(applicationDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Tower.slnx")))
            {
                return Path.Combine(directory.FullName, "settings.ini");
            }
        }
        return Path.Combine(Path.GetFullPath(applicationDirectory), "settings.ini");
    }

    public int Load(DisplaySettings settings, ScenarioOptions options)
    {
        var values = File.Exists(path) ? LegacyAirportReader.ReadIni(path) : new Dictionary<string, string>();
        string Read(string section, string key, string fallback = "") => values.GetValueOrDefault(section + "." + key, fallback);
        double Number(string value, double min, double max, double fallback) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
            double.IsFinite(number) && number >= min && number <= max ? number : fallback;
        var asde = Read("Display", "ASDE").Split(',');
        var brite = Read("Display", "BRITE").Split(',');
        string Part(string[] fields, int index) => index < fields.Length ? fields[index].Trim() : "";
        settings.AsdeRange = Number(Part(asde, 0), 0.1, 10, settings.AsdeRange);
        settings.BriteRange = Number(Part(brite, 0), 2, 50, settings.BriteRange);
        settings.RangeMarks = Number(Part(brite, 3), 0.1, 25, settings.RangeMarks);
        settings.AsdeMap = Flag(Part(asde, 3), settings.AsdeMap);
        settings.AsdeTaxiways = Flag(Part(asde, 4), settings.AsdeTaxiways);
        settings.RunwayNames = Flag(Part(asde, 5), settings.RunwayNames);
        settings.TaxiNames = Flag(Part(asde, 6), settings.TaxiNames);
        settings.AsdeGateNames = Flag(Part(asde, 7), false);
        settings.BriteMap = Flag(Part(brite, 4), settings.BriteMap);
        settings.BriteTaxiways = Flag(Part(brite, 5), settings.BriteTaxiways);
        settings.BriteObstructions = Flag(Part(brite, 6), settings.BriteObstructions);
        settings.ShowRangeMarks = Flag(Part(brite, 7), settings.ShowRangeMarks);
        foreach (var key in new[] { "RunwayLights", "TaxiwayLights", "Beacon", "ObstructionLights" }.Concat(VisibilityKeys))
        {
            var property = typeof(DisplaySettings).GetProperty(key)!;
            property.SetValue(settings, Flag(Read("Display", key), (bool)property.GetValue(settings)!));
        }
        settings.DefaultWindowLayout = Read("Display", "DefaultWindowLayout", settings.DefaultWindowLayout);
        settings.SelectedAirport = Read("User", "SelectedAirport", settings.SelectedAirport);
        settings.SoundTalk = Flag(Read("Sound", "Talk"), settings.SoundTalk);
        settings.ExpandCommandInput = Flag(Read("Commands", "ExpandInput"), false);
        settings.PrintStrips = Flag(Read("Scenario", "PrintStrips"), settings.PrintStrips);
        settings.StripPaper = Read("Scenario", "PaperType", settings.StripPaper ? "Strip" : "Plain").Equals("Strip", StringComparison.OrdinalIgnoreCase);
        var rate = Read("Scenario", "ACPerMin").Split(',');
        if (rate.Length == 2 && int.TryParse(rate[0], out var aircraft) && int.TryParse(rate[1], out var minutes) &&
            aircraft is >= 1 and <= 99 && minutes is >= 1 and <= 99 && (double)aircraft / minutes <= 1.5)
        {
            options.TrafficAircraftCount = aircraft;
            options.TrafficIntervalMinutes = minutes;
            options.AircraftPerMinute = (double)aircraft / minutes;
        }
        options.WindFromDegrees = Number(Read("Weather", "WindDir"), 0, 360, options.WindFromDegrees);
        options.WindKnots = Number(Read("Weather", "WindSpeed"), 0, 80, options.WindKnots);
        options.VisibilityNm = Number(Read("Weather", "Visibility"), 0.1, 100, options.VisibilityNm);
        // Legacy time packs hours and minutes in the high and low bytes.
        if (int.TryParse(Read("Scenario", "TimeOfDayValue"), out var packed) &&
            packed >= 0 && (packed >> 8) < 24 && (packed & 255) < 60)
        {
            options.StartMinutes = (packed >> 8) * 60 + (packed & 255);
        }
        return int.TryParse(Read("User", "Last"), out var last) && last >= 0 ? last : 0;
    }

    public void Save(DisplaySettings settings, ScenarioOptions options, int controllerIndex, string airportCode)
    {
        // Re-read on every save: retain comments, unknown fields and edits made since load.
        var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : [];
        void Set(string section, string key, string value)
        {
            var start = lines.FindIndex(line => line.Trim().Equals("[" + section + "]", StringComparison.OrdinalIgnoreCase));
            if (start < 0)
            {
                lines.Add("[" + section + "]");
                start = lines.Count - 1;
            }
            var end = start + 1;
            while (end < lines.Count && !lines[end].TrimStart().StartsWith('['))
            {
                var split = lines[end].IndexOf('=');
                if (split > 0 && lines[end][..split].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    lines[end] = lines[end][..(split + 1)] + value;
                    return;
                }
                end++;
            }
            lines.Insert(end, key + "=" + value);
        }
        void Radar(string key, int count, params (int Index, string Value)[] changes)
        {
            var values = File.Exists(path) ? LegacyAirportReader.ReadIni(path) : [];
            var parts = values.GetValueOrDefault("Display." + key, "").Split(',').ToList();
            while (parts.Count < count)
            {
                parts.Add(parts.Count is 1 or 2 ? "0" : "Off");
            }
            foreach (var change in changes)
            {
                parts[change.Index] = change.Value;
            }
            Set("Display", key, string.Join(',', parts));
        }
        string Number(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);
        string On(bool value) => value ? "On" : "Off";
        Radar("ASDE", 8, (0, Number(settings.AsdeRange)), (3, On(settings.AsdeMap)),
            (4, On(settings.AsdeTaxiways)), (5, On(settings.RunwayNames)), (6, On(settings.TaxiNames)), (7, On(settings.AsdeGateNames)));
        Radar("BRITE", 8, (0, Number(settings.BriteRange)), (3, Number(settings.RangeMarks)),
            (4, On(settings.BriteMap)), (5, On(settings.BriteTaxiways)), (6, On(settings.BriteObstructions)), (7, On(settings.ShowRangeMarks)));
        foreach (var key in new[] { "RunwayLights", "TaxiwayLights", "Beacon", "ObstructionLights" }.Concat(VisibilityKeys))
        {
            Set("Display", key, On((bool)typeof(DisplaySettings).GetProperty(key)!.GetValue(settings)!));
        }
        Set("Display", "DefaultWindowLayout", settings.DefaultWindowLayout);
        Set("User", "SelectedAirport", settings.SelectedAirport);
        Set("User", "Last", Math.Max(0, controllerIndex).ToString(CultureInfo.InvariantCulture));
        Set("Sound", "Talk", On(settings.SoundTalk));
        Set("Commands", "ExpandInput", On(settings.ExpandCommandInput));
        Set("Scenario", "PrintStrips", settings.PrintStrips ? "Yes" : "No");
        Set("Scenario", "PaperType", settings.StripPaper ? "Strip" : "Plain");
        Set("Scenario", "ACPerMin", $"{options.TrafficAircraftCount},{options.TrafficIntervalMinutes}");
        Set("Scenario", "TimeOfDayValue", ((options.StartMinutes / 60 << 8) | options.StartMinutes % 60).ToString(CultureInfo.InvariantCulture));
        Set("Weather", "WindDir", Number(options.WindFromDegrees));
        Set("Weather", "WindSpeed", Number(options.WindKnots));
        Set("Weather", "Visibility", Number(options.VisibilityNm));
        if (airportCode.Length > 0 && options.ArrivalRunways.Count > 0)
        {
            Set("Scenario", airportCode + "ActiveRunways", string.Join(',', options.ArrivalRunways));
        }
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, string.Join(Environment.NewLine, lines) + Environment.NewLine);
            if (File.Exists(fullPath))
            {
                var backups = Path.Combine(Path.GetDirectoryName(fullPath)!, "Backups", "Settings");
                Directory.CreateDirectory(backups);
                File.Replace(temporary, fullPath, Path.Combine(backups, "settings-" + Guid.NewGuid().ToString("N") + ".ini"));
            }
            else
            {
                File.Move(temporary, fullPath);
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static bool Flag(string value, bool fallback) => value.Trim().ToUpperInvariant() switch
    {
        "ON" or "YES" or "TRUE" or "1" => true,
        "OFF" or "NO" or "FALSE" or "0" => false,
        _ => fallback
    };
}
