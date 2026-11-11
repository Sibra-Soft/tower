using Tower.Core;

namespace Tower.Infrastructure;

public static partial class OriginalData
{
    public static List<string> ReadDefaultRunways(string settingsPath, Airport airport)
    {
        var selected = new List<string>();
        if (File.Exists(settingsPath))
        {
            var values = LegacyAirportReader.ReadIni(settingsPath);
            var configured = values.GetValueOrDefault($"Scenario.{airport.Code}ActiveRunways", "");
            foreach (var name in configured.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var runway = airport.Runways.FirstOrDefault(candidate => candidate.Name.TrimStart('0')
                    .Equals(name.TrimStart('0'), StringComparison.OrdinalIgnoreCase));
                if (runway is not null && !selected.Contains(runway.Name, StringComparer.OrdinalIgnoreCase))
                {
                    selected.Add(runway.Name);
                }
            }
        }
        if (selected.Count == 0)
        {
            selected.Add(airport.Runways.FirstOrDefault()?.Name ?? throw new InvalidDataException("Airport has no configured runways."));
        }
        return selected;
    }
}
