using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Tower.Core;
namespace Tower.Infrastructure;

public static partial class OriginalData
{
    private static double N(string s) => double.Parse(s, CultureInfo.InvariantCulture);
    private static void ConnectPushbackAlleys(Airport airport)
    {
        // Only a declared gate connection may provide an apron exit. Nearby roads
        // are not implicitly connected at visual crossings.
        foreach (var alley in airport.Segments.Where(segment => segment.Kind == "ALLEY").ToList())
        {
            if (airport.Nodes[alley.From].Kind != "ALLEYLINK" && airport.Nodes[alley.To].Kind != "ALLEYLINK")
            {
                continue;
            }
            var start = airport.Point(alley.From);
            var end = airport.Point(alley.To);
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var lengthSquared = dx * dx + dy * dy;
            if (lengthSquared < 1)
            {
                continue;
            }
            Position Project(Position point)
            {
                var fraction = Math.Clamp(((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared, 0, 1);
                return new(start.X + fraction * dx, start.Y + fraction * dy);
            }
            var joins = new List<string> { alley.From, alley.To };
            foreach (var pair in airport.PushbackGates.ToList())
            {
                var definition = pair.Value;
                if (definition.AlleyConnectionNode.Length > 0 || !definition.Connections.Contains(alley.Id, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }
                var ramp = airport.Segments.Single(segment => segment.Id == definition.Ramp);
                // Ramps may be written in either direction. The end furthest
                // from the declared alley is the nose-in parking position.
                var fromDistance = airport.Point(ramp.From).Distance(Project(airport.Point(ramp.From)));
                var toDistance = airport.Point(ramp.To).Distance(Project(airport.Point(ramp.To)));
                var parking = fromDistance > toDistance ? ramp.From : ramp.To;
                var connection = parking == ramp.From ? ramp.To : ramp.From;
                var position = Project(airport.Point(connection));
                var join = joins.FirstOrDefault(id => airport.Point(id).Distance(position) < 0.01);
                if (join is null)
                {
                    join = "PUSHBACK-" + pair.Key;
                    while (airport.Nodes.ContainsKey(join))
                    {
                        join += "-JOIN";
                    }
                    airport.Nodes.Add(join, new(join, "TAXIWAY", position));
                    joins.Add(join);
                }
                airport.Nodes[pair.Key] = airport.Nodes[pair.Key] with { Position = airport.Point(parking) };
                var gateEdge = airport.Segments.FindIndex(segment => segment.Id == "GATE-" + pair.Key);
                airport.Segments[gateEdge] = airport.Segments[gateEdge] with { To = parking };
                airport.PushbackGates[pair.Key] = definition with { ConnectionNode = connection, AlleyConnectionNode = join };
            }
            if (joins.Count == 2)
            {
                continue;
            }
            var ordered = joins.OrderBy(id => (airport.Point(id).X - start.X) * dx + (airport.Point(id).Y - start.Y) * dy).ToList();
            airport.Segments.Remove(alley);
            for (var index = 1; index < ordered.Count; index++)
            {
                airport.Segments.Add(alley with
                {
                    Id = index == 1 ? alley.Id : alley.Id + "-PB" + index,
                    From = ordered[index - 1], To = ordered[index]
                });
            }
        }
    }

    private static string[] Words(string s) => s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    public static string? Find(string directory, string name) => Directory.Exists(directory) ? Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).FirstOrDefault(p => Path.GetFileName(p).Equals(name, StringComparison.OrdinalIgnoreCase)) : null;
    public static void Extract(string zip, string destination)
    {
        using var archive = ZipFile.OpenRead(zip);
        var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        if (archive.Entries.Sum(e => e.Length) > 200_000_000)
        {
            throw new InvalidDataException("Archive exceeds 200 MB.");
        }

        foreach (var entry in archive.Entries)
        {
            var path = Path.GetFullPath(Path.Combine(root, entry.FullName.Replace('\\', '/')));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Archive path escapes destination.");
            }

            if (entry.Name.Length == 0)
            {
                Directory.CreateDirectory(path);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            entry.ExtractToFile(path, true);
        }
    }
    public static Dictionary<string, List<string[]>> Sections(string path)
    {
        var result = new Dictionary<string, List<string[]>>(StringComparer.OrdinalIgnoreCase);
        var section = "";
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Split(';')[0].Trim().Trim('\u001a');
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1];
                result.TryAdd(section, []);
                continue;
            }
            if (section.Length > 0)
            {
                result[section].Add(Words(line));
            }
        }
        return result;
    }
    // The local TWR scale is retained at 1 metre/unit. Canyon's resulting runway lengths
    // (2651/2063 m) agree with SEC's 8800/6900 ft to within 2%; not CFG renderer units.
    public static Airport LoadCanyon(string directory) => LoadAirport(directory, "CYN");
    public static IReadOnlyList<string> InstalledAirports(string directory) => !Directory.Exists(directory) ? [] :
        Directory.EnumerateFiles(Path.GetFullPath(directory), "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path).Equals(".TWR", StringComparison.OrdinalIgnoreCase))
            .Where(path => Path.GetDirectoryName(path) == Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) ||
                Path.GetFileName(Path.GetDirectoryName(path))!.Equals(Path.GetFileNameWithoutExtension(path), StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileNameWithoutExtension).OfType<string>().Select(code => code.ToUpperInvariant())
            .Where(code => Regex.IsMatch(code, @"^[A-Z0-9]{3,8}$"))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(code => code == "CYN" ? 0 : 1).ThenBy(code => code, StringComparer.Ordinal).ToArray();
    public static Airport LoadAirport(string directory, string code)
    {
        code = code.ToUpperInvariant();
        if (!Regex.IsMatch(code, @"^[A-Z0-9]{3,8}$"))
        {
            throw new InvalidDataException("Unsupported airport code.");
        }

        string Required(string name) => Find(directory, name) ?? throw new InvalidDataException($"Missing {name}.");
        var sections = Sections(Required(code + ".TWR"));
        var a = new Airport { Code = code, Name = code switch { "CYN" => "Canyon Field", "DCA" => "Washington National", "ORD" => "Chicago O'Hare", _ => code }, Observer = new(0, 0) };
        ReadSourceMetadata(directory, code, a);
        if (sections.TryGetValue("Tower", out var tower))
        {
            var location = tower.FirstOrDefault(t => t[0].Equals("Location", StringComparison.OrdinalIgnoreCase));
            if (location is not null)
            {
                var match = Regex.Match(location[1], @"^(\d+)d(\d+)m([\d.]+)s([NS])/(\d+)d(\d+)m([\d.]+)s([EW])$");
                if (!match.Success)
                {
                    throw new InvalidDataException("Unsupported tower coordinate.");
                }

                a.Latitude = (N(match.Groups[1].Value) + N(match.Groups[2].Value) / 60 + N(match.Groups[3].Value) / 3600) * (match.Groups[4].Value == "S" ? -1 : 1);
                a.Longitude = (N(match.Groups[5].Value) + N(match.Groups[6].Value) / 60 + N(match.Groups[7].Value) / 3600) * (match.Groups[8].Value == "W" ? -1 : 1);
            }
        }
        foreach (var t in sections["Points"])
        {
            a.Nodes.Add(t[0].ToUpperInvariant(), new(t[0].ToUpperInvariant(), t[1].Equals("RwyEnd", StringComparison.OrdinalIgnoreCase) ? "RUNWAY" : t[1].ToUpperInvariant(), new(N(t[2]), N(t[3])), t.Length > 4 ? t[^1] : ""));
        }

        foreach (var t in sections["Segments"])
        {
            a.Segments.Add(new(t[0].ToUpperInvariant(), t[1].ToUpperInvariant(), t[2].ToUpperInvariant(), t[3].ToUpperInvariant()));
        }

        foreach (var t in sections["Routes"])
        {
            var oneWay = t.Length > 1 && t[1].Equals("ONEWAY", StringComparison.OrdinalIgnoreCase);
            var route = t.Skip(oneWay ? 2 : 1).TakeWhile(x => !x.Equals("end", StringComparison.OrdinalIgnoreCase)).Select(x => x.ToUpperInvariant()).ToList();
            a.Routes[t[0]] = route;
            if (!oneWay)
            {
                continue;
            }

            a.OneWayRoutes.Add(t[0]);
            for (var i = 1; i < route.Count; i++)
            {
                var index = a.Segments.FindIndex(e => (e.From == route[i - 1] && e.To == route[i]) || (e.To == route[i - 1] && e.From == route[i]));
                if (index < 0)
                {
                    throw new InvalidDataException($"One-way route {t[0]} has an unconnected segment.");
                }

                a.Segments[index] = a.Segments[index] with
                {
                    From = route[i - 1],
                    To = route[i],
                    OneWay = true
                };
            }
        }
        // Several holdshort points can split the same original segment. Sort all of
        // them along the original edge, rather than repeatedly splitting the first piece.
        var holds = sections["Points"].Where(t => t[1].Equals("Holdshort", StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var group in holds.GroupBy(t => t[4], StringComparer.OrdinalIgnoreCase))
        {
            var edge = a.Segments.Single(s => s.Id.Equals(group.Key, StringComparison.OrdinalIgnoreCase));
            var start = a.Point(edge.From);
            var end = a.Point(edge.To);
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var ordered = group.OrderBy(t => (a.Point(t[0]).X - start.X) * dx + (a.Point(t[0]).Y - start.Y) * dy).Select(t => t[0].ToUpperInvariant()).ToList();
            ordered.Insert(0, edge.From);
            ordered.Add(edge.To);
            a.Segments.Remove(edge);
            for (var i = 1; i < ordered.Count; i++)
            {
                a.Segments.Add(edge with
                {
                    Id = i == 1 ? edge.Id : edge.Id + "-H" + i,
                    From = ordered[i - 1],
                    To = ordered[i]
                });
            }
        }
        foreach (var t in sections["Gates"])
        {
            var ramp = a.Segments.Single(s => s.Id.Equals(t[5], StringComparison.OrdinalIgnoreCase));
            a.Nodes[t[0]] = new(t[0], "GATE", a.Point(ramp.To), t[0]);
            a.Segments.Add(new("GATE-" + t[0], "RAMP", t[0], ramp.To));
            a.PushbackGates.Add(t[0], new(t[1], N(t[2]), N(t[3]), N(t[4]), ramp.Id, ramp.From, code + ".TWR [Gates] " + t[0])
            {
                Connections = t.Skip(6).Select(value => value.ToUpperInvariant()).ToList()
            });
        }
        ConnectPushbackAlleys(a);
        // Ramps without pushback definitions also provide aircraft parking.
        var namedRamps = sections["Gates"].Select(t => t[5]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sections["Segments"].Where(t => t[1].Equals("Ramp", StringComparison.OrdinalIgnoreCase)))
        {
            a.ParkingRampCodes[source[0]] = string.Join(" ", source.Skip(6));
            if (namedRamps.Contains(source[0]) || source.Length < 8 || source[6] is not ("G" or "CR" or "CC"))
            {
                continue;
            }
            var id = source[0].ToUpperInvariant();
            var endpoint = source[3].ToUpperInvariant();
            a.Nodes.Add(id, new(id, "GATE", a.Point(endpoint), id));
            a.Segments.Add(new("GATE-" + id, "RAMP", id, endpoint));
        }
        foreach (var segment in a.Segments.Where(s => s.Kind == "RUNWAY").ToArray())
        {
            foreach (var (start, end) in new[] { (segment.From, segment.To), (segment.To, segment.From) })
            {
                var name = a.Nodes[start].Label;
                var hold = holds.Where(t => t[^1].Equals(name, StringComparison.OrdinalIgnoreCase)).OrderBy(t => a.Point(start).Distance(a.Point(t[0]))).FirstOrDefault()?[0]
                    ?? throw new InvalidDataException($"No departure hold point for runway {name}.");
                var t = a.Point(start);
                var e = a.Point(end);
                var length = t.Distance(e);
                // A declared RwyInt belonging to this runway with a connected off-runway taxiway.
                var exits = sections["Points"].Where(p => p[1].Equals("RwyInt", StringComparison.OrdinalIgnoreCase) && p[4].Equals(segment.Id, StringComparison.OrdinalIgnoreCase))
                    .Select(p => p[0]).Where(id => t.Distance(a.Point(id)) > length * 0.5 && t.Distance(a.Point(id)) < length)
                    .OrderBy(id => t.Distance(a.Point(id))).ToArray();
                string? rollout = null;
                string? exit = null;
                foreach (var candidate in exits)
                {
                    exit = a.Segments.Where(s => s.Kind is not ("RUNWAY" or "VROUTE") && (s.From == candidate || (!s.OneWay && s.To == candidate))).Select(s => s.From == candidate ? s.To : s.From)
                        .FirstOrDefault(id => a.Nodes[id].Kind is not ("RWYINT" or "RUNWAY"));
                    if (exit is not null)
                    {
                        rollout = candidate;
                        break;
                    }
                }
                if (rollout is null || exit is null)
                {
                    throw new InvalidDataException($"No connected off-runway exit for {name}.");
                }

                a.Runways.Add(new()
                {
                    Name = name,
                    PhysicalId = segment.Id,
                    Threshold = start,
                    End = end,
                    Hold = hold,
                    Rollout = rollout,
                    Exit = exit
                });
            }
        }

        // Optional modern extension: restrictions apply to generated traffic, never manual clearances.
        if (sections.TryGetValue("RunwayUse", out var runwayUse))
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in runwayUse)
            {
                if (row.Length != 2 || row[1] is not ("Arrival" or "Departure" or "Both" or "Closed"))
                {
                    throw new InvalidDataException("Invalid RunwayUse entry.");
                }
                var runway = a.GetRunway(row[0]);
                if (!seen.Add(runway.Name))
                {
                    throw new InvalidDataException("Duplicate RunwayUse entry.");
                }
                runway.GeneratedArrivals = row[1] is "Arrival" or "Both";
                runway.GeneratedDepartures = row[1] is "Departure" or "Both";
            }
        }
        if (Find(directory, code + ".PRF") is { } prf)
        {
            ReadPerformance(prf, a);
        }

        foreach (var specification in a.AircraftTypes.Values)
        {
            if (specification.WeightCategory is >= 1 and <= 3)
            {
                a.Original.PatternCategoryByType[specification.Type] = specification.WeightCategory.Value;
            }
        }

        if (a.Original.PatternCategoryByType.Count > 0)
        {
            a.Original.PatternCategoryBasis = "GAMENT.DLL 0x10023720 maps PRF Weight_Category 1/2/3 to runtime 0/1/2; 0x10019CC0 selects PAT cat1/cat2/cat3.";
        }

        if (Find(directory, code + ".TYP") is { } typ)
        {
            AircraftSpecification? specification = null;
            string? group = null;
            foreach (var line in File.ReadLines(typ))
            {
                var heading = line.Trim();
                if (heading.StartsWith('[') && heading.EndsWith(']'))
                {
                    group = heading[1..^1];
                    specification = null;
                }
                foreach (Match m in Regex.Matches(line, @"(\w+):\s*([^\s:]+)"))
                {
                    var key = m.Groups[1].Value;
                    var value = m.Groups[2].Value;
                    if (key == "Type")
                    {
                        if (string.Equals(group, "3DModels", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(group, "CommercialCallsigns", StringComparison.OrdinalIgnoreCase))
                        {
                            specification = null;
                            continue;
                        }
                        a.AircraftTypes.TryGetValue(value, out specification);
                        if (specification is not null)
                        {
                            specification.OriginalTypeGroup = group;
                        }
                    }
                    else if (key == "Airline_ID")
                    {
                        specification = null;
                    }
                    else if (specification is not null && key == "Type_Name")
                    {
                        specification.Name = value.Replace('_', ' ');
                    }
                    else if (specification is not null && key == "Voice_Offset")
                    {
                        specification.VoiceOffset = int.Parse(value, CultureInfo.InvariantCulture);
                    }
                    else if (specification is not null && key == "Fixed_Gear" && bool.TryParse(value, out var fixedGear))
                    {
                        specification.OriginalFixedGear = fixedGear;
                    }
                }
            }
        }
        if (Find(directory, code + ".INF") is { } inf)
        {
            ReadOutlines(inf, a);
        }

        if (Find(directory, code + ".CPL") is { } cpl)
        {
            foreach (var row in Sections(cpl).Values.SelectMany(x => x))
            {
                if (row.Length >= 2 && a.AircraftTypes.ContainsKey(row[1]))
                {
                    a.TrafficPool.Add(new(row[0], row[1]));
                }
            }
        }

        if (Find(directory, code + ".MAP") is { } map)
        {
            a.RadarMap = ReadRadarMap(File.ReadAllLines(map), a);
        }
        try
        {
            var models = new AircraftModelCatalog(directory);
            foreach (var specification in a.AircraftTypes.Values)
            {
                specification.GroundFootprint = models.GetGroundFootprint(specification.Type, specification.WingSpanMetres);
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            // Keep the same missing/corrupt-model fallback as TowerView.
        }
        a.Validate();
        return a;
    }
    public static void ReadPerformance(string path, Airport a)
    {
        AircraftSpecification? current = null;
        string? group = null;
        foreach (var line in File.ReadLines(path))
        {
            var heading = line.Trim();
            if (heading.StartsWith('[') && heading.EndsWith(']'))
            {
                group = heading[1..^1];
                current = null;
            }
            foreach (Match m in Regex.Matches(line, @"(\w+):\s*([^\s:]+)"))
            {
                var key = m.Groups[1].Value;
                var value = m.Groups[2].Value;
                if (key == "Type")
                {
                    current = new()
                    {
                        Type = value,
                        OriginalTypeGroup = group
                    };
                    a.AircraftTypes[value] = current;
                    continue;
                }
                if (current is null || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                {
                    continue;
                }

                if (!double.IsFinite(n))
                {
                    throw new InvalidDataException("Non-finite performance value.");
                }

                current.OriginalPerformance[key] = n;
                if (key is "Airborne_Acceleration_Rate" or "Airborne_Deceleration_Rate" or "Taxi_Acceleration_Rate" or "Taxi_Deceleration_Rate")
                {
                    if (n <= 0)
                    {
                        throw new InvalidDataException("Acceleration and deceleration rates must be positive.");
                    }
                }
                var climbBand = key switch
                {
                    "Surface_to_500" => (AltitudeBand?)AltitudeBand.SurfaceTo500,
                    "501_to_5000" => AltitudeBand.To5000,
                    "5001_to_10000" => AltitudeBand.To10000,
                    "10001_to_25000" => AltitudeBand.To25000,
                    "25001_to_Service_Ceiling" => AltitudeBand.Above25000,
                    _ => null
                };
                var descentBand = key switch
                {
                    "500_to_Surface" => (AltitudeBand?)AltitudeBand.SurfaceTo500,
                    "5000_to_501" => AltitudeBand.To5000,
                    "10000_to_5001" => AltitudeBand.To10000,
                    "25000_to_10001" => AltitudeBand.To25000,
                    "Service_Ceiling_to_25001" => AltitudeBand.Above25000,
                    _ => null
                };
                if (climbBand is not null || descentBand is not null)
                {
                    if (n <= 0)
                    {
                        throw new InvalidDataException("Vertical performance rates must be positive feet per minute.");
                    }

                    if (climbBand is { } c)
                    {
                        current.ClimbRatesFpm[c] = n;
                    }

                    if (descentBand is { } d)
                    {
                        current.DescentRatesFpm[d] = n;
                    }
                }
                if (key == "Weight_Category")
                {
                    if (n != Math.Truncate(n) || n < 1 || n > 3)
                    {
                        throw new InvalidDataException("Unsupported aircraft weight category.");
                    }

                    current.WeightCategory = (int)n;
                }
                if (key == "Pattern_Speed" && n > 0)
                {
                    current.PatternSpeed = n;
                }

                if (key == "Landing_Speed" && n > 0)
                {
                    current.LandingSpeed = n;
                }

                switch (key)
                {
                    case "Takeoff_Distance":
                    case "Landing_Distance":
                    case "Stopping_Distance":
                        if (n <= 0)
                        {
                            throw new InvalidDataException("Runway performance distances must be positive.");
                        }
                        if (key == "Takeoff_Distance")
                        {
                            current.TakeoffDistanceFeet = n;
                        }

                        if (key == "Landing_Distance")
                        {
                            current.LandingDistanceFeet = n;
                        }

                        if (key == "Stopping_Distance")
                        {
                            current.StoppingDistanceFeet = n;
                        }

                        break;
                    case "Airborne_Acceleration_Rate":
                        current.Acceleration.AirborneAcceleration = n;
                        break;
                    case "Airborne_Deceleration_Rate":
                        current.Acceleration.AirborneDeceleration = n;
                        break;
                    case "Taxi_Acceleration_Rate":
                        current.Acceleration.TaxiAcceleration = n;
                        break;
                    case "Taxi_Deceleration_Rate":
                        current.Acceleration.TaxiDeceleration = n;
                        break;
                    case "Approach_Speed":
                        current.ApproachSpeed = n;
                        break;
                    case "Takeoff_Speed":
                        current.TakeoffSpeed = n;
                        break;
                    case "Max_Taxi_Speed":
                        current.TaxiSpeed = n;
                        break;
                    case "Wing_Span":
                        current.WingSpanMetres = n * 0.3048;
                        break;
                    case "Turn_Rate":
                        current.TurnRate = n;
                        break;
                }
            }
        }

        foreach (var s in a.AircraftTypes.Values)
        {
            if (s.ApproachSpeed <= 0 || s.TakeoffSpeed <= 0 || s.TaxiSpeed <= 0 || s.WingSpanMetres <= 0 || s.TurnRate <= 0)
            {
                throw new InvalidDataException("Invalid aircraft performance.");
            }
        }
    }
    public static void ReadOutlines(string path, Airport a)
    {
        List<Position>? points = null;
        foreach (var line in File.ReadLines(path))
        {
            var w = Words(line);
            if (w.Length == 0)
            {
                continue;
            }

            if (w[0].Equals("Type", StringComparison.OrdinalIgnoreCase))
            {
                points = [];
                a.Outlines[w[1].ToUpperInvariant()] = points;
                continue;
            }
            if (points is not null && w.Length == 2 && double.TryParse(w[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) && double.TryParse(w[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                if (x == 0 && y == 0)
                {
                    points = null;
                    continue;
                }
                points.Add(new(x, y));
            }
        }
    }
    public static string GraphicsInventory(string directory)
    {
        var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
        var models = files.Where(p => Path.GetExtension(p).Equals(".DBT", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (models.Length == 0)
        {
            throw new InvalidDataException("No DBT models in archive.");
        }

        foreach (var p in models)
        {
            using var stream = File.OpenRead(p);
            var header = new byte[8];
            if (stream.Read(header) != 8 || System.Text.Encoding.ASCII.GetString(header) != "TW001000")
            {
                throw new InvalidDataException($"Unrecognized model: {Path.GetFileName(p)}");
            }
        }
        var configs = files.Where(p => Path.GetExtension(p).Equals(".CFG", StringComparison.OrdinalIgnoreCase)).Select(p => Path.GetFileName(p) + ": " + string.Join(", ", File.ReadLines(p).Where(x => x.StartsWith("DRIVER=", StringComparison.OrdinalIgnoreCase) || x.StartsWith("IMAGE=", StringComparison.OrdinalIgnoreCase))));
        return $"{models.Length} TW001000 DBT files registered.\n" + string.Join("\n", configs) + "\nTowerView renders aircraft and Canyon scenery DBT models, plus the Washington and O'Hare BIT panoramas.";
    }
}
