using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tower.Core;
namespace Tower.Infrastructure;

public static class JsonStore
{
    public static JsonSerializerOptions Options { get; } = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? throw new InvalidDataException("Empty document.");
    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, Options));
        File.Move(temporary, path, true);
    }
    public static Session LoadSession(string path)
    {
        var s = Read<Session>(path);
        if (s.Version != 1)
        {
            throw new InvalidDataException("Unsupported scenario version.");
        }

        s.Airport.Validate();
        if (s.TrafficFlowRandomState == 0)
        {
            throw new InvalidDataException("Invalid saved traffic flow random state.");
        }
        if (s.FreeTraffic is { } traffic && (traffic.InitialSeed == 0 ||
            !double.IsFinite(traffic.NextSpawnSeconds) || traffic.NextSpawnSeconds < 0 || s.OriginalScenario is not null || s.Schedule.Count > 0))
        {
            throw new InvalidDataException("Invalid saved free traffic generator.");
        }
        if (s.OriginalScenario is { } originalScenario &&
            (originalScenario.Waypoints is null || originalScenario.Waypoints.Any(waypoint => waypoint is null || !waypoint.IsValid()) ||
             originalScenario.Waypoints.Select(waypoint => waypoint.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != originalScenario.Waypoints.Count))
        {
            throw new InvalidDataException("Invalid saved original scenario waypoints.");
        }
        _ = s.Airport.GetRunway(s.Options.Runway);
        foreach (var runway in s.Options.ArrivalRunways)
        {
            _ = s.Airport.GetRunway(runway);
        }

        if (!double.IsFinite(s.ElapsedSeconds) || s.ElapsedSeconds < 0 || !double.IsFinite(s.SpawnAccumulator) || s.SpawnAccumulator < 0 ||
            !double.IsFinite(s.Options.AircraftPerMinute) || s.Options.AircraftPerMinute < 0 || s.Options.AircraftPerMinute > 99 ||
            !double.IsFinite(s.Options.WindKnots) || s.Options.WindKnots < 0 || s.Options.WindKnots > 80 ||
            !double.IsFinite(s.Options.WindFromDegrees) || s.Options.WindFromDegrees < 0 || s.Options.WindFromDegrees > 360 ||
            !double.IsFinite(s.Options.VisibilityNm) || s.Options.VisibilityNm < 0.1 || s.Options.VisibilityNm > 100)
        {
            throw new InvalidDataException("Invalid scenario timing or weather.");
        }

        var scheduled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var departureIds = new HashSet<string>(StringComparer.Ordinal);
        var departureRunways = new List<Runway>();
        if (s.RunwayDepartures is null)
        {
            throw new InvalidDataException("Invalid runway departure history.");
        }
        foreach (var departure in s.RunwayDepartures)
        {
            if (departure is null || string.IsNullOrWhiteSpace(departure.Id) || !departureIds.Add(departure.Id) ||
                string.IsNullOrWhiteSpace(departure.CallSign) || !double.IsFinite(departure.StartedSeconds) ||
                departure.StartedSeconds < 0 || departure.StartedSeconds > s.ElapsedSeconds ||
                departure.WeightCategory is not (null or 1 or 2 or 3) ||
                !double.IsFinite(departure.Position.X) || !double.IsFinite(departure.Position.Y))
            {
                throw new InvalidDataException("Invalid runway departure history.");
            }
            var runway = s.Airport.GetRunway(departure.Runway);
            if (departureRunways.Any(other =>
                !string.IsNullOrWhiteSpace(runway.PhysicalId) && runway.PhysicalId.Equals(other.PhysicalId, StringComparison.OrdinalIgnoreCase) ||
                runway.Threshold == other.Threshold && runway.End == other.End || runway.Threshold == other.End && runway.End == other.Threshold))
            {
                throw new InvalidDataException("Duplicate physical runway in departure history.");
            }
            departureRunways.Add(runway);
        }
        foreach (var f in s.Schedule)
        {
            if (f.Timing is { } timing && (!timing.IsValid(f.Direction) || timing.EntrySeconds != f.TimeSeconds || f.OriginalPlan is null ||
                f.Direction == FlightDirection.Arrival && (f.OriginalPlan.ResolvedRoute is not { Count: > 0 } ||
                    f.OriginalPlan.AltitudeReference != OriginalAltitudeReference.Msl || s.Airport.Original.SourceMslToAgl(f.OriginalPlan.Altitude1Feet) is null)))
            {
                throw new InvalidDataException("Invalid saved original flight timing.");
            }
            if (f.OriginalPlan is { } originalPlan && !originalPlan.IsValid())
            {
                throw new InvalidDataException("Invalid saved original flight plan.");
            }
            _ = s.Airport.GetRunway(f.Runway);
            if (string.IsNullOrWhiteSpace(f.CallSign) || !scheduled.Add(f.CallSign) || !double.IsFinite(f.TimeSeconds) || f.TimeSeconds < 0 || !Enum.IsDefined(f.Direction))
            {
                throw new InvalidDataException("Invalid scheduled flight.");
            }

            if (f.Gate is not null && (!s.Airport.Nodes.TryGetValue(f.Gate, out var gate) || gate.Kind != "GATE"))
            {
                throw new InvalidDataException("Invalid scheduled gate.");
            }

            if (!f.Spawned && s.Aircraft.Any(a => a.CallSign.Equals(f.CallSign, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException("Unspawned flight already exists.");
            }
        }
        var callsigns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in s.Aircraft)
        {
            if (a.GroundTrackDegrees is { } track && (!double.IsFinite(track) || track < 0 || track >= 360) ||
                !double.IsFinite(a.GroundSpeedKnots) || a.GroundSpeedKnots < 0)
            {
                throw new InvalidDataException("Invalid saved ground velocity.");
            }
            if (a.ApproachSpeedOverrideKnots is { } approachSpeed && (!double.IsFinite(approachSpeed) || approachSpeed <= 0))
            {
                throw new InvalidDataException("Invalid saved approach speed instruction.");
            }
            if (a.GoAroundApproachRequests is < 0 or > 2)
            {
                throw new InvalidDataException("Invalid saved go-around approach request count.");
            }
            if (!double.IsFinite(a.BriteLabelBearing) || a.BriteLabelBearing is < 0 or >= 360)
            {
                throw new InvalidDataException("Invalid saved BRITE label bearing.");
            }
            if (!double.IsFinite(a.AsdeLabelBearing) || a.AsdeLabelBearing is < 0 or >= 360)
            {
                throw new InvalidDataException("Invalid saved ASDE label bearing.");
            }
            if (a.Pushback is { } pushback && (!pushback.IsValid() || a.Direction != FlightDirection.Departure ||
                !s.Airport.Nodes.ContainsKey(pushback.ConnectionNode) ||
                a.Phase == FlightPhase.Pushback && (pushback.Completed || pushback.Samples.Count < 2 || pushback.SampleIndex >= pushback.Samples.Count) ||
                a.Phase == FlightPhase.ReadyForTaxi && !pushback.Completed) ||
                a.Phase is FlightPhase.Pushback or FlightPhase.ReadyForTaxi && a.Pushback is null)
            {
                throw new InvalidDataException("Invalid saved pushback progress.");
            }
            if (a.Pushback is { } pushbackRoute)
            {
                if (!pushbackRoute.Completed && a.Phase is not (FlightPhase.AtGate or FlightPhase.Pushback) ||
                    a.Phase == FlightPhase.AtGate && pushbackRoute.Samples.Count != 0 ||
                    pushbackRoute.Samples.Count > 0 && (pushbackRoute.Samples[^1].Position.Distance(s.Airport.Point(pushbackRoute.ConnectionNode)) > 0.001 ||
                        Math.Abs(RouteMovement.AngleDifference(pushbackRoute.Samples[^1].Heading, pushbackRoute.FinalHeading)) > 0.001))
                {
                    throw new InvalidDataException("Saved pushback route does not join the airport graph.");
                }
            }
            if (a.FreeTraffic is { } freeFlight && (!freeFlight.IsValid() || a.OriginalPlan is not null ||
                freeFlight.AwaitingEntry && (a.Phase != FlightPhase.Vectoring || a.Direction != FlightDirection.Arrival)))
            {
                throw new InvalidDataException("Invalid saved free traffic flight.");
            }
            if (a.Movement is { } movement && !movement.IsValid() || a.PathExitHeading is { } exitHeading &&
                (!double.IsFinite(exitHeading) || exitHeading < 0 || exitHeading >= 360) || a.DepartureTurnHeading is { } turnHeading &&
                (!double.IsFinite(turnHeading) || turnHeading < 0 || turnHeading >= 360))
            {
                throw new InvalidDataException("Invalid saved route motion.");
            }
            if (a.GeneratedPerformance is { } performance && (!performance.IsValid() || a.OriginalPlan is not null))
            {
                throw new InvalidDataException("Invalid saved generated flight performance.");
            }
            if (a.GeneratedDeparture is { } departure && (!departure.IsValid() || a.Direction != FlightDirection.Departure ||
                a.OriginalPlan is not null || a.GeneratedRoute is null || a.GeneratedPerformance is null ||
                !departure.Destination.Name.Equals(a.GeneratedRoute.Destination, StringComparison.OrdinalIgnoreCase) ||
                departure.Stage == GeneratedDepartureStage.Finished && a.Phase != FlightPhase.Completed ||
                departure.Stage is GeneratedDepartureStage.InitialClimb or GeneratedDepartureStage.ToDestination && a.Phase == FlightPhase.Completed ||
                departure.Stage == GeneratedDepartureStage.ToDestination && a.Phase != FlightPhase.Departing &&
                    !(a.Phase == FlightPhase.Vectoring && a.ResumeAfterTurn && a.ResumePhase == FlightPhase.Departing) ||
                departure.Stage is GeneratedDepartureStage.InitialClimb or GeneratedDepartureStage.ToDestination &&
                    (!a.FollowOriginalDepartureRoute || !a.FollowOriginalDepartureAltitude)))
            {
                throw new InvalidDataException("Invalid saved generated departure progress.");
            }
            if (a.GeneratedArrival is { } arrival && (!arrival.IsValid(s.ElapsedSeconds) || a.Direction != FlightDirection.Arrival ||
                a.OriginalPlan is not null || a.InitialContact is not null || a.GeneratedRoute is null || a.GeneratedPerformance is null ||
                !arrival.Origin.Name.Equals(a.GeneratedRoute.Origin, StringComparison.OrdinalIgnoreCase) ||
                arrival.Stage == GeneratedArrivalStage.Inbound && (a.PathIndex != arrival.NextPointIndex ||
                    !a.Path.SequenceEqual(arrival.Points.Select(p => p.Position)) ||
                    !(a.Phase == FlightPhase.Circuit || a.Phase == FlightPhase.Vectoring && a.ResumeAfterTurn && a.ResumePhase == FlightPhase.Circuit))))
            {
                throw new InvalidDataException("Invalid saved generated arrival progress.");
            }
            if (a.GeneratedRoute is { } generated && (a.OriginalPlan is not null ||
                string.IsNullOrWhiteSpace(generated.Origin) || string.IsNullOrWhiteSpace(generated.Destination) ||
                !double.IsFinite(generated.BearingDegrees) || generated.BearingDegrees < 0 || generated.BearingDegrees >= 360))
            {
                throw new InvalidDataException("Invalid saved generated traffic route.");
            }
            if (a.InitialContact is { } contact && (!contact.IsValid(s.ElapsedSeconds) || a.OriginalPlan is null ||
                a.Direction == FlightDirection.Arrival && !contact.EntryReached &&
                (a.OriginalRoute is null || contact.EntryPointIndex is null || contact.EntryPointIndex >= a.OriginalRoute.Points.Count - 1 || contact.InitialSpeedKnots <= 0)))
            {
                throw new InvalidDataException("Invalid saved initial contact state.");
            }
            if (a.OriginalActions is { } actions && (!actions.IsValid() || a.OriginalPlan is null))
            {
                throw new InvalidDataException("Invalid saved original action progress.");
            }
            if (a.OriginalRoute is { } route && (!route.IsValid() || a.OriginalPlan is null))
            {
                throw new InvalidDataException("Invalid saved original route progress.");
            }
            if (a.OriginalPlan is { } originalPlan && !originalPlan.IsValid())
            {
                throw new InvalidDataException("Invalid saved original flight plan.");
            }
            if (a.HoldShortRunway is not null)
            {
                _ = s.Airport.GetRunway(a.HoldShortRunway);
            }
            var validReachedHoldPhase = a.Phase is FlightPhase.TaxiIn or FlightPhase.TaxiOut ||
                a.Phase == FlightPhase.HoldingShort && a.HoldShortRunway is { } heldRunway &&
                s.Airport.GetRunway(heldRunway).PhysicalId == s.Airport.GetRunway(a.Runway).PhysicalId;
            if (a.HoldShortReached == true && (!a.Holding || a.HoldShortRunway is null || !validReachedHoldPhase))
            {
                throw new InvalidDataException("Invalid saved runway hold state.");
            }

            var spec = a.Specification;
            if (new[] { spec.TakeoffDistanceFeet, spec.LandingDistanceFeet, spec.StoppingDistanceFeet }
                .Any(value => value is { } distance && (!double.IsFinite(distance) || distance <= 0)) ||
                new[] { a.RunwayAccelerationMetresPerSecondSquared, a.RunwayDecelerationMetresPerSecondSquared }
                .Any(value => value is { } acceleration && (!double.IsFinite(acceleration) || acceleration < 0)))
            {
                throw new InvalidDataException("Invalid runway performance data.");
            }
            if (!spec.Acceleration.IsValid())
            {
                throw new InvalidDataException("Invalid acceleration profile.");
            }
            if (spec.ClimbRatesFpm.Concat(spec.DescentRatesFpm).Any(pair => !Enum.IsDefined(pair.Key) || !double.IsFinite(pair.Value) || pair.Value <= 0))
            {
                throw new InvalidDataException("Invalid vertical performance table.");
            }

            if (new[] { spec.ApproachSpeed, spec.TakeoffSpeed, spec.TaxiSpeed, spec.WingSpanMetres, spec.TurnRate }.Any(x => !double.IsFinite(x) || x <= 0))
            {
                throw new InvalidDataException("Invalid aircraft specification.");
            }

            _ = s.Airport.GetRunway(a.Runway);
            _ = s.Airport.Point(a.Gate);
            if (string.IsNullOrWhiteSpace(a.CallSign) || !callsigns.Add(a.CallSign) || !Enum.IsDefined(a.Phase) || !Enum.IsDefined(a.LandingIntent) || !Enum.IsDefined(a.ResumePhase) ||
                !double.IsFinite(a.VerticalSpeedFpm) || !double.IsFinite(a.TurnRemaining) || a.TurnRemaining < 0 || a.TurnRemaining > 360 ||
                a.TurnDirection is not (-1 or 1) || !double.IsFinite(a.WaitingSeconds) || a.WaitingSeconds < 0 ||
                !double.IsFinite(a.ReportFinalNm) || a.ReportFinalNm < 0 || a.ReportFinalNm > 20 || a.PathIndex < 0 || a.PathIndex > a.Path.Count ||
                !double.IsFinite(a.Position.X) || !double.IsFinite(a.Position.Y) || !double.IsFinite(a.SpeedKnots) || a.SpeedKnots < 0 ||
                !double.IsFinite(a.AltitudeFeet) || a.AltitudeFeet < 0 || !double.IsFinite(a.TargetSpeedKnots) || !double.IsFinite(a.Heading) ||
                !double.IsFinite(a.PitchDegrees) || Math.Abs(a.PitchDegrees) > 30 ||
                !double.IsFinite(a.BankDegrees) || Math.Abs(a.BankDegrees) > 90 ||
                a.Animation is { } animation && !animation.IsValid() ||
                a.TouchdownDistanceMetres is { } touchdown && (!double.IsFinite(touchdown) || touchdown < 0 || touchdown > 1000) ||
                a.Path.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)))
            {
                throw new InvalidDataException("Invalid aircraft in saved scenario.");
            }
        }
        return s;
    }
}
public sealed class ControllerProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Controller";
    public HashSet<string> RecordedSessions { get; set; } = [];
    public Dictionary<string, List<int>> AirportRatings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<int, int> RewardCounts { get; set; } = [];
    [JsonIgnore]
    public int AverageRating
    {
        get
        {
            var ratings = AirportRatings.Values.SelectMany(ratings => ratings).ToArray();
            return ratings.Length == 0 ? 0 : (int)(ratings.Sum(value => (long)value) / ratings.Length);
        }
    }
    public int Sessions
    {
        get; set;
    }
    public int Score
    {
        get; set;
    }
    public int Arrivals
    {
        get; set;
    }
    public int Departures
    {
        get; set;
    }
    public int Incidents
    {
        get; set;
    }
    public override string ToString() => Name;
}
public sealed class DisplaySettings
{
    public bool ExpandCommandInput { get; set; }
    public string DefaultWindowLayout { get; set; } = "";
    public string SelectedAirport { get; set; } = "";
    public double BriteRange { get; set; } = 14.5;
    public double RangeMarks { get; set; } = 5;
    public double AsdeRange { get; set; } = 1.6;
    public bool BriteMap { get; set; } = true;
    public bool BriteTaxiways { get; set; } = true;
    public bool BriteObstructions
    {
        get; set;
    }
    public bool ShowRangeMarks { get; set; } = true;
    public bool AsdeMap { get; set; } = true;
    public bool AsdeTaxiways { get; set; } = true;
    public bool RunwayNames { get; set; } = true;
    public bool TaxiNames { get; set; } = true;
    public bool AsdeGateNames { get; set; }
    public bool RunwayLights
    {
        get; set;
    }
    public bool TaxiwayLights
    {
        get; set;
    }
    public bool Beacon
    {
        get; set;
    }
    public bool ObstructionLights
    {
        get; set;
    }
    public bool ShowAsde { get; set; } = true;
    public bool ShowBrite { get; set; } = true;
    public bool ShowStrips { get; set; } = true;
    public bool ShowButtons { get; set; } = true;
    public bool ShowCompass { get; set; } = true;
    public bool ShowAircraftLabels { get; set; } = true;
    public bool ShowGauges { get; set; } = true;
    public bool SoundTalk { get; set; } = true;

    public bool PrintStrips
    {
        get; set;
    }
    public bool StripPaper
    {
        get; set;
    }
    public string DataDirectory { get; set; } = "";
    public string SelectedController { get; set; } = "";
}
public static class LegacyAirportReader
{
    public static Dictionary<string, string> ReadIni(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var section = "";
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1];
                continue;
            }
            var split = line.IndexOf('=');
            if (split > 0)
            {
                result[section + "." + line[..split].Trim()] = line[(split + 1)..].Trim();
            }
        }
        return result;
    }
    // Parses the existing project's TWR grammar; never guesses physical scale or runway endpoints.
    public static void ReadMap(string path, Airport airport, double metresPerUnit)
    {
        if (!double.IsFinite(metresPerUnit) || metresPerUnit <= 0)
        {
            throw new InvalidDataException("Map scale must be positive.");
        }

        var section = "";
        var lineNumber = 0;
        foreach (var raw in File.ReadLines(path))
        {
            lineNumber++;
            var line = raw.Split(';')[0].Trim();
            if (line.Length == 0 || line.Equals("end", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].ToUpperInvariant();
                continue;
            }
            var t = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            try
            {
                switch (section)
                {
                    case "POINTS":
                        if (t.Length < 4)
                        {
                            throw new FormatException("Point requires ID, kind, X, Y.");
                        }

                        var x = double.Parse(t[2], CultureInfo.InvariantCulture) * metresPerUnit;
                        var y = double.Parse(t[3], CultureInfo.InvariantCulture) * metresPerUnit;
                        airport.Nodes.Add(t[0], new(t[0], t[1], new(x, y), t.Length > 4 ? t[4] : ""));
                        break;
                    case "SEGMENTS":
                        if (t.Length < 4)
                        {
                            throw new FormatException("Segment requires ID, kind, start, end.");
                        }

                        airport.Segments.Add(new(t[0], t[1], t[2], t[3]));
                        break;
                    case "ROUTES":
                        if (t.Length < 3)
                        {
                            throw new FormatException("Route requires two or more point IDs.");
                        }

                        airport.Routes.Add(t[0], t.Skip(1).TakeWhile(s => !s.Equals("end", StringComparison.OrdinalIgnoreCase)).ToList());
                        break;
                }
            }
            catch (Exception e) when (e is FormatException or ArgumentException or OverflowException) { throw new InvalidDataException($"{Path.GetFileName(path)}:{lineNumber}: {e.Message}", e); }
        }
    }
    public static Airport Import(string manifestPath)
    {
        var manifest = JsonStore.Read<AirportImport>(manifestPath);
        var airport = new Airport { Code = manifest.Code, Name = manifest.Name, Latitude = manifest.Latitude, Longitude = manifest.Longitude, Runways = manifest.Runways };
        ReadMap(Path.Combine(Path.GetDirectoryName(manifestPath)!, manifest.MapFile), airport, manifest.MetresPerUnit);
        foreach (var id in manifest.Gates)
        {
            if (!airport.Nodes.TryGetValue(id, out var n))
            {
                throw new InvalidDataException($"Missing gate point: {id}");
            }

            airport.Nodes[id] = n with
            {
                Kind = "GATE"
            };
        }
        airport.Validate();
        return airport;
    }
}
public sealed class AirportImport
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string MapFile { get; set; } = "";
    public double MetresPerUnit
    {
        get; set;
    }
    public double Latitude
    {
        get; set;
    }
    public double Longitude
    {
        get; set;
    }
    public List<string> Gates { get; set; } = [];
    public List<Runway> Runways { get; set; } = [];
}
