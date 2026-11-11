using System.Globalization;
using System.Text.RegularExpressions;
using Tower.Core;

namespace Tower.Infrastructure;

public static partial class OriginalData
{
    public static string ReadScenarioAirportCode(string path)
    {
        var sector = Value(Sections(path), "Sector");
        if (string.IsNullOrWhiteSpace(sector))
        {
            throw new InvalidDataException("Original scenario has no sector.");
        }

        return Path.GetFileNameWithoutExtension(sector).ToUpperInvariant();
    }

    public static Session ReadScenario(string path, Airport airport)
    {
        var sections = Sections(path);
        var waypoints = ReadScenarioWaypoints(sections);
        var schedule = ReadFlightPlans(sections, airport, waypoints);
        return new Session
        {
            Airport = airport,
            Schedule = schedule,
            OriginalScenario = new()
            {
                Waypoints = waypoints,
                FileName = Path.GetFileName(path),
                SourceLines = File.ReadAllLines(path),
                Sector = Value(sections, "Sector") ?? "",
                Seed = Integer(sections, "Seed"),
                DeclaredMinutes = Integer(sections, "Minutes"),
                Description = sections.TryGetValue("Description", out var description)
                    ? description.Select(line => string.Join(' ', line)).ToArray() : []
            },
            Options = new()
            {
                AircraftPerMinute = 0,
                Practice = true,
                Runway = schedule[0].Runway,
                ArrivalRunways = schedule.Where(flight => flight.Direction == FlightDirection.Arrival)
                    .Select(flight => flight.Runway).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            }
        };
    }

    public static List<ScheduledFlight> ReadSchedule(string path, Airport airport)
    {
        var sections = Sections(path);
        return ReadFlightPlans(sections, airport, ReadScenarioWaypoints(sections));
    }

    private static string? Value(Dictionary<string, List<string[]>> sections, string key) =>
        sections.TryGetValue("SimInfo", out var rows)
            ? rows.FirstOrDefault(row => row.Length == 2 && row[0].Equals(key, StringComparison.OrdinalIgnoreCase))?[1]
            : null;

    private static int? Integer(Dictionary<string, List<string[]>> sections, string key)
    {
        var value = Value(sections, key);
        if (value is null)
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number : throw new InvalidDataException($"Invalid original scenario {key}.");
    }

    private static List<ScheduledFlight> ReadFlightPlans(Dictionary<string, List<string[]>> sections, Airport airport,
        IReadOnlyList<OriginalScenarioWaypoint> waypoints)
    {
        var fixes = new Dictionary<string, GeographicFix>(airport.Original.Fixes, StringComparer.OrdinalIgnoreCase);
        foreach (var waypoint in waypoints)
        {
            // Original lookup returns the first matching fix; existing SEC names retain priority.
            fixes.TryAdd(waypoint.Name, waypoint.ToFix());
        }
        var sector = Value(sections, "Sector");
        if (sector is not null && !Path.GetFileNameWithoutExtension(sector).Equals(airport.Code, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Scenario belongs to another airport.");
        }

        if (!sections.TryGetValue("Aircraft", out var rows))
        {
            throw new InvalidDataException("Original scenario has no aircraft section.");
        }

        double NonNegative(string text)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value) || value < 0)
            {
                throw new InvalidDataException("Invalid original flight-plan numeric field.");
            }

            return value;
        }

        double? Bearing(string name) => fixes.TryGetValue(name, out var fix)
            ? fix.BearingFrom(airport.Latitude, airport.Longitude) : null;

        List<OriginalRoutePoint>? ResolveRoute(string[] route)
        {
            var points = new List<OriginalRoutePoint>();
            for (int index = 0; index < route.Length - 1; index++)
            {
                var token = route[index];
                if (token.Equals(airport.Code, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!fixes.TryGetValue(token, out var fix))
                {
                    if (index == 0 || index + 1 >= route.Length - 1 || !airport.Original.Airways.TryGetValue(token, out var airway) ||
                        airway.Between(route[index - 1], route[index + 1]) is not { } expanded)
                    {
                        return null;
                    }
                    foreach (var name in expanded.Skip(1))
                    {
                        if (!fixes.TryGetValue(name, out var expandedFix))
                        {
                            return null;
                        }
                        points.Add(new(name, expandedFix.ProjectFrom(airport.Latitude, airport.Longitude, airport.Observer)));
                    }
                    index++;
                    continue;
                }
                points.Add(new(token, fix.ProjectFrom(airport.Latitude, airport.Longitude, airport.Observer)));
            }
            return points.Count > 0 ? points : null;
        }

        var result = new List<ScheduledFlight>();
        for (var index = 0; index < rows.Count; index++)
        {
            var header = rows[index];
            if (header.Length != 11 || header[4] is not ("a" or "d"))
            {
                throw new InvalidDataException("Invalid original aircraft header.");
            }

            var clock = header[5].Split(':');
            if (clock.Length != 2 || !int.TryParse(clock[0], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) ||
                !int.TryParse(clock[1], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds is < 0 or > 59)
            {
                throw new InvalidDataException("Invalid original aircraft entry time.");
            }

            if (!airport.AircraftTypes.ContainsKey(header[1]))
            {
                throw new InvalidDataException($"Unknown aircraft type {header[1]}.");
            }

            var tokens = new List<string>();
            while (index + 1 < rows.Count && !(rows[index + 1].Length >= 6 && rows[index + 1][5].Contains(':')))
            {
                tokens.AddRange(rows[++index]);
            }

            var route = tokens.TakeWhile(token => !token.StartsWith('@')).ToArray();
            if (route.Length < 3 || !Regex.IsMatch(header[9], @"^[0-7]{1,4}$"))
            {
                throw new InvalidDataException("Invalid original route or squawk code.");
            }

            var gate = tokens.LastOrDefault(token => token.StartsWith("@G", StringComparison.OrdinalIgnoreCase))?[2..].ToUpperInvariant();
            var resolvedRoute = ResolveRoute(route);
            if (gate is not null && (!airport.Nodes.TryGetValue(gate, out var node) || node.Kind != "GATE"))
            {
                throw new InvalidDataException("Unknown scenario gate.");
            }

            var flight = new ScheduledFlight
            {
                CallSign = header[0].ToUpperInvariant(),
                Type = header[1].ToUpperInvariant(),
                Direction = header[4] == "a" ? FlightDirection.Arrival : FlightDirection.Departure,
                TimeSeconds = minutes * 60.0 + seconds,
                Runway = airport.GetRunway(header[10]).Name,
                Gate = gate,
                OriginalPlan = new()
                {
                    Header = string.Join(' ', header),
                    FlightRules = header[2],
                    ControlField = header[3],
                    SpeedFieldKnots = NonNegative(header[6]),
                    Altitude1Feet = NonNegative(header[7]),
                    Altitude2Feet = NonNegative(header[8]),
                    AltitudeReference = OriginalAltitudeReference.Msl,
                    Squawk = header[9].PadLeft(4, '0'),
                    RouteTokens = tokens.ToArray(),
                    Actions = OriginalActionProgress.Parse(tokens),
                    ResolvedRoute = resolvedRoute,
                    RouteResolution = resolvedRoute is null
                        ? "Unresolved route token or no SEC/SIM fixes; retains legacy entry/departure behavior."
                        : "Ordered SEC/SIM fixes and contiguous airway segments projected from the tower; airway traversal permits either direction and local geometry remains approximate.",
                    Origin = route[0],
                    Destination = route[^2],
                    Controller = route[^1],
                    OriginBearingDegrees = Bearing(route[0]),
                    DestinationBearingDegrees = Bearing(route[^2])
                }
            };
            flight.Timing = ReadOriginalTiming(flight, airport);
            result.Add(flight);
        }

        var declared = Integer(sections, "Aircraft");
        if (result.Count == 0 || result.Select(flight => flight.CallSign).Distinct(StringComparer.OrdinalIgnoreCase).Count() != result.Count ||
            declared is { } count && count != result.Count)
        {
            throw new InvalidDataException("Invalid scenario aircraft count or duplicate callsign.");
        }

        return result.OrderBy(flight => flight.TimeSeconds).ToList();
    }
}
