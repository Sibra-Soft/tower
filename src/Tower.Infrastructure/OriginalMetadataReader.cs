using System.Globalization;
using System.Text.RegularExpressions;
using Tower.Core;
namespace Tower.Infrastructure;

public static partial class OriginalData
{
    private static void ReadSourceMetadata(string directory, string code, Airport airport)
    {
        var metadata = airport.Original;
        foreach (var extension in new[] { "TWR", "SEC", "PAT", "PRF", "CFG", "MAP", "SPF" })
        {
            var path = Find(directory, code + "." + extension);
            if (path is null)
            {
                continue;
            }

            var lines = File.ReadAllLines(path);
            metadata.Files[code + "." + extension] = lines;
            if (extension == "SEC")
            {
                ReadAirways(lines, metadata);
            }
            if (extension == "SPF")
            {
                var info = Sections(path).GetValueOrDefault("AirportInfo");
                if (info is null || info.Count != 3 || info[1].Length != 1 || !info[1][0].Equals(code, StringComparison.OrdinalIgnoreCase) ||
                    info[2].Length != 1 || !int.TryParse(info[2][0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var difficulty) || difficulty < 0 || difficulty > 100)
                {
                    throw new InvalidDataException("Invalid SPF AirportInfo section.");
                }

                airport.Name = string.Join(' ', info[0]);
                airport.Difficulty = difficulty;
            }
            var section = "";
            foreach (var raw in lines)
            {
                var line = raw.Split(';')[0].Trim();
                if (line.StartsWith('['))
                {
                    section = line;
                    continue;
                }
                var words = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                double? Number(string value) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : null;
                if (words.Length < 2)
                {
                    if (extension == "SEC" && section is "[ArrivalFlow]" or "[DepartureFlow]" && words.Length > 0)
                    {
                        throw new InvalidDataException("Invalid sector traffic flow entry.");
                    }
                    continue;
                }

                if (extension == "TWR" && section == "[Tower]")
                {
                    if (words[0] == "Altitude")
                    {
                        metadata.TowerElevationFeetMsl = Number(words[1]);
                    }

                    if (words[0] == "CabHeightBAO")
                    {
                        metadata.CabHeightBao = Number(words[1]);
                    }
                }
                if (extension == "SEC")
                {
                    if (section is "[ArrivalFlow]" or "[DepartureFlow]")
                    {
                        if (words.Length != 2 || !int.TryParse(words[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent) || percent < 0)
                        {
                            throw new InvalidDataException("Invalid sector traffic flow entry.");
                        }
                        var flow = section == "[ArrivalFlow]" ? metadata.ArrivalFlow : metadata.DepartureFlow;
                        flow.Add(new(words[0], percent));
                    }
                    if (section == "[Fixes]" && words.Length > 3)
                    {
                        var coordinate = Regex.Match(words[3], @"^(\d{2})(\d{2}(?:\.\d+)?)([NS])/(\d{3})(\d{2}(?:\.\d+)?)([EW])$");
                        if (coordinate.Success)
                        {
                            var latitudeMinutes = Number(coordinate.Groups[2].Value)!.Value;
                            var longitudeMinutes = Number(coordinate.Groups[5].Value)!.Value;
                            var latitude = Number(coordinate.Groups[1].Value)!.Value + latitudeMinutes / 60;
                            var longitude = Number(coordinate.Groups[4].Value)!.Value + longitudeMinutes / 60;
                            if (latitudeMinutes >= 60 || longitudeMinutes >= 60 || latitude > 90 || longitude > 180)
                            {
                                throw new InvalidDataException("Invalid sector fix coordinate.");
                            }
                            metadata.Fixes[words[0]] = new(words[0], latitude * (coordinate.Groups[3].Value == "S" ? -1 : 1),
                                longitude * (coordinate.Groups[6].Value == "W" ? -1 : 1)) { SourceKind = words[1] };
                        }
                    }
                    if (words[0] == "MagneticVar")
                    {
                        metadata.MagneticVariationDegrees = Number(words[1]);
                    }

                    if (section == "[Airports]" && words[0] == code && words.Length > 2)
                    {
                        metadata.SectorElevationFeetMsl = Number(words[2]);
                    }
                }
                if (extension == "PAT" && Number(words[1]) is { } value)
                {
                    metadata.PatternValues[words[0]] = value;
                }
            }
        }
        if (metadata.PatternValues.Count > 0)
        {
            metadata.PatternMetresPerUnit = OriginalPatternUnits.MetresPerSourceUnit;
            metadata.PatternUsesFixedPointRounding = true;
            metadata.PatternDistanceBasis = "GAMENT.DLL PAT reader: trunc(source * 78.03 + 0.5) / 256 metres; source distances are approximately feet.";
        }
        if (metadata.TowerElevationFeetMsl is { } elevation)
        {
            metadata.UseOriginalGeneratedPerformance = true;
            metadata.UseGeneratedDepartureRoutes = true;
            metadata.UseGeneratedArrivalRoutes = true;
            metadata.FieldElevationFeetMsl = elevation;
            metadata.AltitudeBasis = "GAMENT.DLL 0x100028A0 reads TWR Altitude as field elevation; 0x100078B0 stores absolute altitude and subtracts that field for AGL. SEC elevation is retained separately.";
        }
    }

    private static void ReadAirways(string[] lines, OriginalMetadata metadata)
    {
        bool inAirways = false;
        OriginalAirway? airway = null;
        var names = new List<string>();
        foreach (var raw in lines)
        {
            var line = raw.Split(';')[0].Trim().Trim('\u001a');
            if (line.StartsWith('['))
            {
                inAirways = line.Equals("[Airways]", StringComparison.OrdinalIgnoreCase);
                airway = null;
                names.Clear();
                continue;
            }
            if (!inAirways || line.Length == 0)
            {
                continue;
            }
            var words = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 4 && words[1].Contains('/') && words[2].Contains('/'))
            {
                airway = new() { SourceFlags = string.Join(' ', words.Skip(3)) };
                // Duplicate definitions are retained as separate segments, never joined implicitly.
                if (!metadata.Airways.TryAdd(words[0], airway))
                {
                    airway = metadata.Airways[words[0]];
                }
                names.Clear();
                continue;
            }
            if (airway is null)
            {
                continue;
            }
            foreach (var word in words)
            {
                if (word == "0")
                {
                    if (names.Count > 0)
                    {
                        airway.Segments.Add(names.ToArray());
                        names.Clear();
                    }
                }
                else
                {
                    names.Add(word);
                }
            }
        }
    }
}
