namespace Tower.Core;

// World axes: X east, Y north. Distances in metres, headings clockwise from north.
public readonly record struct Position(double X, double Y)
{
    public double Distance(Position other) => Math.Sqrt(Math.Pow(other.X - X, 2) + Math.Pow(other.Y - Y, 2));
    public Position Towards(Position other, double distance)
    {
        var length = Distance(other);
        return length < 0.000001 || distance >= length ? other : new(X + (other.X - X) * distance / length, Y + (other.Y - Y) * distance / length);
    }
    public Position Offset(double heading, double metres) => new(X + Math.Sin(heading * Math.PI / 180) * metres, Y + Math.Cos(heading * Math.PI / 180) * metres);
    public double Bearing(Position other) => (Math.Atan2(other.X - X, other.Y - Y) * 180 / Math.PI + 360) % 360;
}
public enum FlightPhase
{
    Approach, GoAround, LandingRoll, ArrivalHold, TaxiIn, AtGate, TaxiOut, HoldingShort, LineUp, LinedUp, TakeoffRoll, Departing, Completed, Circuit, Vectoring, Breakout, LowApproach, AbortedTakeoff, Pushback, ReadyForTaxi
}
public enum FlightDirection
{
    Arrival, Departure
}
public enum LandingIntent
{
    FullStop, TouchAndGo, LowApproach, Option
}
public sealed class Aircraft
{
    public double? GroundTrackDegrees { get; set; }
    public double GroundSpeedKnots { get; set; }
    public double PitchDegrees { get; set; }
    public double BankDegrees { get; set; }
    public double? TouchdownDistanceMetres { get; set; }
    public GeneratedArrivalProgress? GeneratedArrival { get; set; }
    public GeneratedDepartureProgress? GeneratedDeparture { get; set; }
    public PushbackProgress? Pushback { get; set; }
    public FreeTrafficFlight? FreeTraffic { get; set; }
    public GeneratedFlightPerformance? GeneratedPerformance { get; set; }
    public GeneratedTrafficRoute? GeneratedRoute { get; set; }
    public OriginalInitialContact? InitialContact { get; set; }
    public bool IsOnFrequency => (InitialContact is null || InitialContact.Announced) &&
        (GeneratedArrival is null || GeneratedArrival.Contact.Announced);
    public OriginalRouteProgress? OriginalRoute { get; set; }
    public OriginalActionProgress? OriginalActions { get; set; }
    public string? RunwayDepartureId { get; set; }
    public AircraftRadioMessage? LastRadioMessage
    {
        get; set;
    }
    public OriginalFlightPlan? OriginalPlan
    {
        get; set;
    }
    public bool FollowOriginalDepartureRoute
    {
        get; set;
    }
    public bool FollowOriginalDepartureAltitude { get; set; }
    public bool OriginalDepartureRouteStarted
    {
        get; set;
    }
    public double? RunwayAccelerationMetresPerSecondSquared
    {
        get; set;
    }
    public double? RunwayDecelerationMetresPerSecondSquared
    {
        get; set;
    }
    public bool RunwayOverrunReported
    {
        get; set;
    }
    public AircraftSpecification Specification { get; set; } = new();
    public AircraftAnimationState? Animation { get; set; }
    public string? HoldShortRunway
    {
        get; set;
    }
    // Null identifies older saved sessions, which did not distinguish HY from an HS stop.
    public bool? HoldShortReached
    {
        get; set;
    }
    public string CallSign { get; set; } = "";
    public string Type { get; set; } = "B738";
    public string Runway { get; set; } = "18";
    public FlightDirection Direction
    {
        get; set;
    }
    public FlightPhase Phase
    {
        get; set;
    }
    public Position Position
    {
        get; set;
    }
    public double Heading
    {
        get; set;
    }
    public double AltitudeFeet
    {
        get; set;
    }
    public double SpeedKnots
    {
        get; set;
    }
    public double TargetSpeedKnots { get; set; } = 140;
    public double? ApproachSpeedOverrideKnots { get; set; }
    public LandingIntent LandingIntent
    {
        get; set;
    }
    public string LastTransmission { get; set; } = "";
    public double VerticalSpeedFpm
    {
        get; set;
    }
    public string CircuitLeg { get; set; } = "";
    public bool RightTraffic
    {
        get; set;
    }
    public bool ReportLeg
    {
        get; set;
    }
    public bool ExtendDownwind
    {
        get; set;
    }
    public double ReportFinalNm
    {
        get; set;
    }
    public double TurnRemaining
    {
        get; set;
    }
    public int TurnDirection { get; set; } = 1;
    public bool ResumeAfterTurn
    {
        get; set;
    }
    public FlightPhase ResumePhase
    {
        get; set;
    }
    // Retained for legacy saves; automatic waiting reminders are no longer emitted.
    public double WaitingSeconds
    {
        get; set;
    }
    public bool TrafficObstructionReported { get; set; }
    public bool LandingCleared
    {
        get; set;
    }
    public bool Holding
    {
        get; set;
    }
    public List<Position> Path { get; set; } = [];
    public RouteMotion? Movement { get; set; }
    public double? PathExitHeading { get; set; }
    public double? DepartureTurnHeading { get; set; }
    public int PathIndex
    {
        get; set;
    }
    public string Gate { get; set; } = "G1";
    public double LabelX { get; set; } = 12;
    public double LabelY { get; set; } = -30;
    public double BriteLabelBearing { get; set; } = 45;
    public double AsdeLabelBearing { get; set; } = 45;
    public int GoAroundApproachRequests { get; set; }
    public bool IsAirborne => Phase is FlightPhase.Approach or FlightPhase.GoAround or FlightPhase.Departing or FlightPhase.Circuit or FlightPhase.Vectoring or FlightPhase.Breakout or FlightPhase.LowApproach;
    public bool IsOnRunway => Phase is FlightPhase.LandingRoll or FlightPhase.LineUp or FlightPhase.LinedUp or FlightPhase.TakeoffRoll or FlightPhase.LowApproach or FlightPhase.AbortedTakeoff;
}
public sealed record MapNode(string Id, string Kind, Position Position, string Label = "");
public sealed record MapSegment(string Id, string Kind, string From, string To)
{
    public bool OneWay
    {
        get; init;
    }
}
public sealed class Runway
{
    // Modern airport authoring metadata; old airports/saves allow both directions of traffic.
    public bool GeneratedArrivals { get; set; } = true;
    public bool GeneratedDepartures { get; set; } = true;
    public string Name { get; set; } = "18";
    public string PhysicalId { get; set; } = "18/36";
    public string Threshold { get; set; } = "R18";
    public string End { get; set; } = "R36";
    public string Hold { get; set; } = "H18";
    public string Exit { get; set; } = "E18";
    public string Rollout { get; set; } = "X18";
}
public sealed class AircraftSpecification
{
    public AircraftFootprint? GroundFootprint { get; set; }
    public string? OriginalTypeGroup { get; set; }
    public bool? OriginalFixedGear { get; set; }
    public double? TakeoffDistanceFeet
    {
        get; set;
    }
    public double? LandingDistanceFeet
    {
        get; set;
    }
    public double? StoppingDistanceFeet
    {
        get; set;
    }
    public AccelerationProfile Acceleration { get; set; } = new();
    public Dictionary<AltitudeBand, double> ClimbRatesFpm { get; set; } = [];
    public Dictionary<AltitudeBand, double> DescentRatesFpm { get; set; } = [];
    public int? WeightCategory
    {
        get; set;
    }
    public Dictionary<string, double> OriginalPerformance { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public double? PatternSpeed
    {
        get; set;
    }
    public double? LandingSpeed
    {
        get; set;
    }
    public string Name { get; set; } = "";
    public int VoiceOffset
    {
        get; set;
    }
    public string Type { get; set; } = "B738";
    public double ApproachSpeed { get; set; } = 140;
    public double TakeoffSpeed { get; set; } = 160;
    public double TaxiSpeed { get; set; } = 15;
    public double WingSpanMetres { get; set; } = 35;
    public double TurnRate { get; set; } = 3;
}
public sealed record TrafficEntry(string CallSign, string Type);
public sealed class ScheduledFlight
{
    public OriginalFlightTiming? Timing { get; set; }
    public OriginalFlightPlan? OriginalPlan
    {
        get; set;
    }
    public string CallSign { get; set; } = "";
    public string Type { get; set; } = "B738";
    public FlightDirection Direction
    {
        get; set;
    }
    public string Runway { get; set; } = "18";
    public string? Gate
    {
        get; set;
    }
    public double TimeSeconds
    {
        get; set;
    }
    public bool Spawned
    {
        get; set;
    }
}
public sealed class Airport
{
    public RadarMap RadarMap { get; set; } = new();
    public Dictionary<string, GatePushbackDefinition> PushbackGates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public OriginalMetadata Original { get; set; } = new();
    public override string ToString() => Name;
    public Position Observer { get; set; } = new(1000, 0);
    public Dictionary<string, AircraftSpecification> AircraftTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<Position>> Outlines { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<TrafficEntry> TrafficPool { get; set; } = [];
    public string Code { get; set; } = "TRAINING";
    public string Name { get; set; } = "Tower Training Airport";
    public int Difficulty { get; set; } = 1;
    public double Latitude { get; set; } = 51.45;
    public double Longitude { get; set; } = 5.374;
    public Dictionary<string, MapNode> Nodes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<MapSegment> Segments { get; set; } = [];
    public Dictionary<string, string> ParkingRampCodes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> OneWayRoutes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> Routes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<Runway> Runways { get; set; } = [];
    public Position Point(string id) => Nodes.TryGetValue(id, out var node) ? node.Position : throw new InvalidDataException($"Unknown map point: {id}");
    public Runway GetRunway(string name) => Runways.FirstOrDefault(r => r.Name.TrimStart('0').Equals(name.TrimStart('0'), StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidDataException($"Unknown runway: {name}");
    public List<Runway> GeneratedRunways(FlightDirection direction, IEnumerable<string> selected, bool allowUnselectedFallback = true)
    {
        var configured = selected.Select(GetRunway).Distinct().ToList();
        bool Allowed(Runway runway) => direction == FlightDirection.Arrival ? runway.GeneratedArrivals : runway.GeneratedDepartures;
        var permitted = configured.Where(Allowed).ToList();
        if (permitted.Count > 0 || configured.Count == 0 || !allowUnselectedFallback)
        {
            return permitted;
        }
        // A selected arrival-only runway must not spawn departures in its prohibited direction.
        var preferred = configured[0];
        var heading = Point(preferred.Threshold).Bearing(Point(preferred.End));
        return Runways.Where(Allowed).OrderBy(runway => Math.Abs(RouteMovement.AngleDifference(heading,
            Point(runway.Threshold).Bearing(Point(runway.End))))).Take(1).ToList();
    }
    public void Validate()
    {
        if (AircraftTypes.Values.Any(specification => specification.GroundFootprint is { } footprint && !footprint.IsValid()))
        {
            throw new InvalidDataException("Invalid aircraft ground footprint.");
        }
        if (RadarMap is null)
        {
            throw new InvalidDataException("Missing radar map geometry.");
        }
        RadarMap.Validate();
        if (PushbackGates is null || PushbackGates.Any(pair => pair.Value is null ||
            !Nodes.TryGetValue(pair.Key, out var gate) || gate.Kind != "GATE" ||
            !Nodes.ContainsKey(pair.Value.ConnectionNode) || pair.Value.Side is not ("Left" or "Right" or "Center") ||
            pair.Value.Connections is null || pair.Value.Connections.Any(string.IsNullOrWhiteSpace) ||
            pair.Value.AlleyConnectionNode is null || pair.Value.AlleyConnectionNode.Length > 0 && !Nodes.ContainsKey(pair.Value.AlleyConnectionNode) ||
            !double.IsFinite(pair.Value.DistanceA) || pair.Value.DistanceA < 0 ||
            !double.IsFinite(pair.Value.DistanceB) || pair.Value.DistanceB < 0 ||
            !double.IsFinite(pair.Value.Angle) || pair.Value.Angle is < 0 or > 180 ||
            !Segments.Any(s => s.Id == pair.Value.Ramp && s.Kind == "RAMP")))
        {
            throw new InvalidDataException("Invalid pushback gate definition.");
        }
        foreach (var flow in new[] { Original.ArrivalFlow, Original.DepartureFlow })
        {
            if (flow is null || flow.Count > 20 || flow.Any(entry => entry is null || entry.Percent < 0 ||
                string.IsNullOrWhiteSpace(entry.Fix) || !Original.Fixes.ContainsKey(entry.Fix)))
            {
                throw new InvalidDataException("Invalid sector traffic flow.");
            }
        }
        if (Original.Airways is null || Original.Airways.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null || !pair.Value.IsValid()))
        {
            throw new InvalidDataException("Invalid original airway definition.");
        }
        if (Original.FieldElevationFeetMsl is { } elevation && !double.IsFinite(elevation))
        {
            throw new InvalidDataException("Invalid field elevation.");
        }
        if (Nodes.Count < 2 || Runways.Count == 0)
        {
            throw new InvalidDataException("Airport requires map points and configured runway geometry.");
        }

        foreach (var node in Nodes.Values)
        {
            if (!double.IsFinite(node.Position.X) || !double.IsFinite(node.Position.Y))
            {
                throw new InvalidDataException("Non-finite map coordinate.");
            }
        }

        foreach (var s in Segments)
        {
            _ = Point(s.From);
            _ = Point(s.To);
        }
        foreach (var route in Routes.Values)
        {
            foreach (var id in route)
            {
                _ = Point(id);
            }
        }

        if (!Nodes.Values.Any(n => n.Kind.Equals("GATE", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("Airport requires at least one GATE.");
        }

        foreach (var r in Runways)
        {
            if (string.IsNullOrWhiteSpace(r.PhysicalId) || Point(r.Threshold).Distance(Point(r.End)) < 300)
            {
                throw new InvalidDataException("Invalid runway geometry.");
            }

            _ = Point(r.Hold);
            _ = Point(r.Exit);
            _ = Point(r.Rollout);
        }
        if (Runways.Select(r => r.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Runways.Count)
        {
            throw new InvalidDataException("Duplicate runway name.");
        }
    }
    public static Airport Training()
    {
        var airport = new Airport();
        foreach (var n in new[] {
            new MapNode("R18", "RUNWAY", new(0, 1500), "18"), new MapNode("R36", "RUNWAY", new(0,-1500), "36"),
            new MapNode("X18", "RUNWAY", new(0,-900)), new MapNode("E18", "TAXIWAY", new(180,-900), "A"),
            new MapNode("H18", "HOLD", new(180,1450), "H18"), new MapNode("A1", "TAXIWAY", new(350,1450), "A"),
            new MapNode("A2", "TAXIWAY", new(350,-900), "A"), new MapNode("G1", "GATE", new(700,-500), "G1"),
            new MapNode("G2", "GATE", new(700,0), "G2"), new MapNode("G3", "GATE", new(700,500), "G3") })
        {
            airport.Nodes.Add(n.Id, n);
        }

        airport.Segments = [new("RW", "RUNWAY", "R18","R36"), new("A", "TAXIWAY", "A1","A2"),
            new("B", "TAXIWAY", "E18","A2"), new("C", "TAXIWAY", "H18","A1"),
            new("D", "TAXIWAY", "G1","A2"), new("E", "TAXIWAY", "G2","A2"), new("F", "TAXIWAY", "G3","A1")];
        airport.Runways = [new()];
        airport.Routes.Add("A", ["H18", "A1", "A2", "E18"]);
        airport.Routes.Add("B", ["A2", "G1"]);
        return airport;
    }
}
public sealed class ScenarioOptions
{
    public List<string> ArrivalRunways { get; set; } = [];
    public bool Practice { get; set; } = true;
    public double AircraftPerMinute { get; set; } = 0.5;
    public int TrafficAircraftCount { get; set; } = 1;
    public int TrafficIntervalMinutes { get; set; } = 2;
    public double WindFromDegrees { get; set; } = 180;
    public double WindKnots { get; set; } = 8;
    public double VisibilityNm { get; set; } = 10;
    public int StartMinutes { get; set; } = 12 * 60;
    public string Runway { get; set; } = "18";
}
public sealed partial class Performance
{
    public int CommandsIssued
    {
        get; set;
    }
    public int CommandErrors
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
    public int GoArounds
    {
        get; set;
    }
    public int Incidents
    {
        get; set;
    }
    public int Score
    {
        get; set;
    }
}
public sealed class Session
{
    public FreeTrafficState? FreeTraffic { get; set; }
    public uint TrafficFlowRandomState { get; set; } = 0x6D2B79F5;
    public OriginalScenario? OriginalScenario
    {
        get; set;
    }
    public List<ScheduledFlight> Schedule { get; set; } = [];
    public int Version { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public Airport Airport { get; set; } = Airport.Training();
    public ScenarioOptions Options { get; set; } = new();
    public List<Aircraft> Aircraft { get; set; } = [];
    public List<RunwayDeparture> RunwayDepartures { get; set; } = [];
    public Performance Performance { get; set; } = new();
    public double ElapsedSeconds
    {
        get; set;
    }
    public double SpawnAccumulator
    {
        get; set;
    }
    public int NextFlight { get; set; } = 100;
    public string ControllerId { get; set; } = "";
    public bool ResultsRecorded
    {
        get; set;
    }
}
public sealed record CommandResult(bool Success, string Message);
