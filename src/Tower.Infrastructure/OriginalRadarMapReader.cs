using System.Globalization;
using System.Text.RegularExpressions;
using Tower.Core;

namespace Tower.Infrastructure;

public static partial class OriginalData
{
    public static RadarMap ReadRadarMap(IEnumerable<string> source, Airport airport)
    {
        var rows = source.Select(raw => raw.Split(';')[0].Trim().Trim('\u001a'))
            .Where(line => line.Length > 0).ToArray();
        var result = new RadarMap();
        var index = Array.FindIndex(rows, line => line.Equals("[Maps]", StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            throw new InvalidDataException("Missing radar map section.");
        }
        double Number(string value) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
            double.IsFinite(number) ? number : throw new InvalidDataException("Invalid radar map number.");
        Position Point(string value)
        {
            var match = Regex.Match(value, @"^(\d{2})(\d{2}(?:\.\d+)?)([NS])/(\d{3})(\d{2}(?:\.\d+)?)([EW])$");
            if (!match.Success)
            {
                throw new InvalidDataException("Invalid radar map coordinate.");
            }
            var latitudeMinutes = Number(match.Groups[2].Value);
            var longitudeMinutes = Number(match.Groups[5].Value);
            var latitude = Number(match.Groups[1].Value) + latitudeMinutes / 60;
            var longitude = Number(match.Groups[4].Value) + longitudeMinutes / 60;
            if (latitudeMinutes >= 60 || longitudeMinutes >= 60 || latitude > 90 || longitude > 180)
            {
                throw new InvalidDataException("Invalid radar map coordinate.");
            }
            return new GeographicFix("", latitude * (match.Groups[3].Value == "S" ? -1 : 1),
                longitude * (match.Groups[6].Value == "W" ? -1 : 1))
                .ProjectFrom(airport.Latitude, airport.Longitude, airport.Observer);
        }
        string[] Next()
        {
            if (++index >= rows.Length)
            {
                throw new InvalidDataException("Incomplete radar map record.");
            }
            return Words(rows[index]);
        }
        while (++index < rows.Length && !rows[index].StartsWith('['))
        {
            var header = Words(rows[index]);
            if (header.Length != 3 || !header[0].Equals("LINE", StringComparison.OrdinalIgnoreCase) &&
                !header[0].Equals("ARC", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Unsupported radar map record.");
            }
            var color = header[1];
            var width = Number(header[2]);
            if (header[0].Equals("LINE", StringComparison.OrdinalIgnoreCase))
            {
                var style = Next();
                double[] dash = style[0].ToLowerInvariant() switch
                {
                    "solid" or "shoreline" or "border" when style.Length == 1 => [],
                    "dash" when style.Length == 3 => [Number(style[1]) * 1852, Number(style[2]) * 1852],
                    "dash_double" when style.Length == 4 =>
                        [Number(style[1]) * 1852, Number(style[3]) * 1852, Number(style[2]) * 1852, Number(style[3]) * 1852],
                    _ => throw new InvalidDataException("Unsupported radar map line style.")
                };
                var points = new List<Position>();
                while (true)
                {
                    var row = Next();
                    if (row.Length == 1 && row[0].Equals("end", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                    if (row.Length != 1 || points.Count >= 10000)
                    {
                        throw new InvalidDataException("Invalid radar map line.");
                    }
                    points.Add(Point(row[0]));
                }
                result.Lines.Add(new(points, color, width, dash));
            }
            else
            {
                string Value(string key)
                {
                    var row = Next();
                    if (row.Length != 2 || !row[0].Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("Invalid radar map arc.");
                    }
                    return row[1];
                }
                result.Arcs.Add(new(Point(Value("center")), Number(Value("radius")) * 1852,
                    Number(Value("start_angle")), Number(Value("end_angle")), Number(Value("degrees_on")),
                    Number(Value("degrees_off")), color, width));
            }
            if (result.Lines.Count + result.Arcs.Count > 10000)
            {
                throw new InvalidDataException("Too many radar map records.");
            }
        }
        result.Validate();
        return result;
    }
}
