using System.Globalization;
using System.Text.RegularExpressions;
using Tower.Core;

namespace Tower.Infrastructure;

public static partial class OriginalData
{
    private static List<OriginalScenarioWaypoint> ReadScenarioWaypoints(Dictionary<string, List<string[]>> sections)
    {
        var result = new List<OriginalScenarioWaypoint>();
        if (!sections.TryGetValue("Waypoints", out var rows))
        {
            return result;
        }
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (row.Length < 3 || !names.Add(row[0]))
            {
                throw new InvalidDataException("Invalid or duplicate original scenario waypoint.");
            }
            var coordinate = row[1];
            var compact = Regex.Match(coordinate,
                @"^(?<latd>[0-9]{2})(?<latm>[0-9]{2}(?:\.[0-9]+)?)(?<ns>[NS])/(?<lond>[0-9]{3})(?<lonm>[0-9]{2}(?:\.[0-9]+)?)(?<ew>[EW])$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var dms = compact.Success ? null : Regex.Match(coordinate,
                @"^(?<latd>[0-9]{1,2})D(?<latm>[0-9]{1,2})M(?<lats>[0-9]{1,2}(?:\.[0-9]+)?)S(?<ns>[NS])/(?<lond>[0-9]{1,3})D(?<lonm>[0-9]{1,2})M(?<lons>[0-9]{1,2}(?:\.[0-9]+)?)S(?<ew>[EW])$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var match = compact.Success ? compact : dms!;
            if (!match.Success)
            {
                throw new InvalidDataException("Invalid original scenario waypoint coordinate.");
            }
            double Number(string name) => match.Groups[name].Success
                ? double.Parse(match.Groups[name].Value, CultureInfo.InvariantCulture) : 0;
            var latitudeMinutes = Number("latm");
            var longitudeMinutes = Number("lonm");
            var latitudeSeconds = Number("lats");
            var longitudeSeconds = Number("lons");
            if (latitudeMinutes >= 60 || longitudeMinutes >= 60 || latitudeSeconds >= 60 || longitudeSeconds >= 60)
            {
                throw new InvalidDataException("Invalid original scenario waypoint minutes or seconds.");
            }
            var latitude = Number("latd") + latitudeMinutes / 60 + latitudeSeconds / 3600;
            var longitude = Number("lond") + longitudeMinutes / 60 + longitudeSeconds / 3600;
            if (match.Groups["ns"].Value.Equals("S", StringComparison.OrdinalIgnoreCase))
            {
                latitude = -latitude;
            }
            if (match.Groups["ew"].Value.Equals("W", StringComparison.OrdinalIgnoreCase))
            {
                longitude = -longitude;
            }
            var waypoint = new OriginalScenarioWaypoint(row[0], coordinate, row[2], latitude, longitude);
            if (!waypoint.IsValid())
            {
                throw new InvalidDataException("Invalid original scenario waypoint location.");
            }
            result.Add(waypoint);
        }
        return result;
    }
}
