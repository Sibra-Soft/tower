namespace Tower.Core;

public enum GeneratedArrivalStage
{
    Inbound, Final, Cancelled
}

public sealed class GeneratedArrivalProgress
{
    public GeneratedArrivalStage Stage { get; set; }
    public OriginalRoutePoint Origin { get; set; } = new("", new());
    public List<OriginalRoutePoint> Points { get; set; } = [];
    public int NextPointIndex { get; set; }
    public int ContactPointIndex { get; set; }
    public OriginalInitialContact Contact { get; set; } = new();
    public double PatternAltitudeFeetAgl { get; set; }
    public double PatternSpeedKnots { get; set; }
    public double ApproachSpeedKnots { get; set; }
    public double? SpeedOverrideKnots { get; set; }

    public bool IsValid(double elapsed) => Enum.IsDefined(Stage) && Origin is not null &&
        !string.IsNullOrWhiteSpace(Origin.Name) && double.IsFinite(Origin.Position.X) && double.IsFinite(Origin.Position.Y) &&
        Points is { Count: >= 2 and <= 3 } && NextPointIndex >= 0 && NextPointIndex <= Points.Count &&
        ContactPointIndex >= 0 && ContactPointIndex < Points.Count - 1 &&
        Points.All(p => p is not null && !string.IsNullOrWhiteSpace(p.Name) && double.IsFinite(p.Position.X) && double.IsFinite(p.Position.Y)) &&
        Contact is not null && Contact.IsValid(elapsed) && Contact.InitialSpeedKnots > 0 &&
        double.IsFinite(PatternAltitudeFeetAgl) && PatternAltitudeFeetAgl >= 0 &&
        double.IsFinite(PatternSpeedKnots) && PatternSpeedKnots > 0 &&
        double.IsFinite(ApproachSpeedKnots) && ApproachSpeedKnots > 0 &&
        (SpeedOverrideKnots is null || double.IsFinite(SpeedOverrideKnots.Value) && SpeedOverrideKnots > 0) &&
        (Stage != GeneratedArrivalStage.Inbound || NextPointIndex < Points.Count) &&
        (Stage != GeneratedArrivalStage.Final || NextPointIndex == Points.Count && Contact.EntryReached);
}

public sealed partial class Simulation
{
    private bool CanInitializeGeneratedArrival(Aircraft aircraft)
    {
        var airport = State.Airport;
        if (!airport.Original.UseGeneratedArrivalRoutes || aircraft.Direction != FlightDirection.Arrival ||
            aircraft.OriginalPlan is not null || aircraft.GeneratedRoute is not { } flow ||
            aircraft.GeneratedPerformance is not { } performance ||
            !airport.Original.Fixes.TryGetValue(flow.Origin, out var fix) ||
            airport.Original.SourceMslToAgl(performance.Altitude2FeetMsl) is not { } patternAltitude)
        {
            return false;
        }
        var origin = fix.ProjectFrom(airport.Latitude, airport.Longitude, airport.Observer);
        var finalDistance = Math.Max(airport.Original.PatternDistance(aircraft.Type, "final") ?? 3 * 1852,
            patternAltitude * Coordinates.MetresPerFoot / Math.Tan(3 * Math.PI / 180));
        return performance.IsValid() && performance.InitialSpeedKnots > 0 &&
            double.IsFinite(origin.X) && double.IsFinite(origin.Y) &&
            finalDistance < OriginalTimingUnits.ContactDistanceMetres - 1852;
    }

    private void InitializeGeneratedArrival(Aircraft aircraft)
    {
        if (!CanInitializeGeneratedArrival(aircraft))
        {
            return;
        }
        var airport = State.Airport;
        var performance = aircraft.GeneratedPerformance!;
        var flow = aircraft.GeneratedRoute!;
        var fix = airport.Original.Fixes[flow.Origin];
        var patternAltitude = airport.Original.SourceMslToAgl(performance.Altitude2FeetMsl)!.Value;
        var runway = airport.GetRunway(aircraft.Runway);
        var threshold = airport.Point(runway.Threshold);
        var heading = threshold.Bearing(airport.Point(runway.End));
        var origin = fix.ProjectFrom(airport.Latitude, airport.Longitude, airport.Observer);
        var contact = threshold.Offset(heading + 180, OriginalTimingUnits.ContactDistanceMetres);
        var finalDistance = Math.Max(airport.Original.PatternDistance(aircraft.Type, "final") ?? 3 * 1852,
            patternAltitude * Coordinates.MetresPerFoot / Math.Tan(3 * Math.PI / 180));
        var final = threshold.Offset(heading + 180, finalDistance);
        var patternSpeed = aircraft.Specification.PatternSpeed ?? aircraft.Specification.ApproachSpeed;
        var maxSpeed = Math.Max(performance.InitialSpeedKnots, patternSpeed);
        var rates = aircraft.Specification.DescentRatesFpm.Values.Where(r => double.IsFinite(r) && r > 0).ToArray();
        var descentRate = rates.Length == 0 ? 1200 : rates.Min();
        var descentDistance = Math.Max(0, aircraft.AltitudeFeet - patternAltitude) / descentRate * 60 * maxSpeed * MetresPerKnotSecond;
        var deceleration = SpeedPerformance.Rate(aircraft.Specification, true, false, 2);
        var brakingDistance = Math.Max(0, maxSpeed * maxSpeed - aircraft.Specification.ApproachSpeed * aircraft.Specification.ApproachSpeed) * MetresPerKnotSecond / (2 * deceleration);
        // Preparation margin is a modern choice, not a recovered spawning formula.
        var lead = Math.Max(0, Math.Max(descentDistance, brakingDistance) + 500 - contact.Distance(final)) +
            Math.Max(0, aircraft.Position.Distance(threshold) - OriginalTimingUnits.ContactDistanceMetres);
        var bearing = origin.Distance(contact) > 1 ? contact.Bearing(origin) : Coordinates.NormalizeHeading(heading + 180);
        var start = contact.Offset(bearing, lead);
        var spaced = false;
        for (var attempt = 0; attempt < 24; attempt++)
        {
            start = contact.Offset(bearing, lead);
            if (State.Aircraft.All(other => !other.IsAirborne || other.Phase == FlightPhase.Completed || other.Position.Distance(start) >= 3 * 1852))
            {
                spaced = true;
                break;
            }
            lead += 4 * 1852;
        }
        if (!spaced)
        {
            throw new InvalidOperationException("No safe arrival route entry is available.");
        }
        List<OriginalRoutePoint> points = [];
        if (lead > origin.Distance(contact) + 1)
        {
            points.Add(new(flow.Origin, origin));
        }
        var contactIndex = points.Count;
        points.Add(new("CONTACT " + runway.Name, contact));
        points.Add(new("FINAL " + runway.Name, final));
        var route = new GeneratedArrivalProgress
        {
            Origin = new(flow.Origin, origin), Points = points, ContactPointIndex = contactIndex,
            PatternAltitudeFeetAgl = patternAltitude, PatternSpeedKnots = patternSpeed,
            ApproachSpeedKnots = aircraft.Specification.ApproachSpeed,
            Contact = new() { CreatedSeconds = State.ElapsedSeconds, EntrySeconds = State.ElapsedSeconds, InitialSpeedKnots = performance.InitialSpeedKnots }
        };
        aircraft.GeneratedArrival = route;
        aircraft.Position = start;
        aircraft.Heading = start.Distance(contact) > 0.01 ? start.Bearing(contact) : heading;
        aircraft.Phase = FlightPhase.Circuit;
        aircraft.CircuitLeg = "arrival";
        aircraft.TargetSpeedKnots = patternSpeed;
        SetPath(aircraft, points.Select(p => p.Position).ToList(), heading);
        route.NextPointIndex = aircraft.PathIndex;
        if (route.NextPointIndex > contactIndex)
        {
            MarkGeneratedArrivalContact(route);
        }
    }

    private void MarkGeneratedArrivalContact(GeneratedArrivalProgress route)
    {
        if (!route.Contact.EntryReached)
        {
            route.Contact.EntryReached = true;
            route.Contact.EntrySeconds = State.ElapsedSeconds;
            route.Contact.RadioDueSeconds = State.ElapsedSeconds + OriginalTimingUnits.RadioDelaySeconds;
        }
    }

    private void UpdateGeneratedArrivalContact(Aircraft aircraft)
    {
        if (aircraft.GeneratedArrival?.Contact is not { Announced: false, RadioDueSeconds: { } due } contact || State.ElapsedSeconds + 1e-7 < due)
        {
            return;
        }
        contact.Announced = true;
        contact.AnnouncedSeconds = State.ElapsedSeconds;
        TransmitInitialContact(aircraft);
        AircraftAdded?.Invoke(aircraft);
    }

    private bool UpdateGeneratedArrival(Aircraft aircraft, double dt)
    {
        if (aircraft.GeneratedArrival is not { Stage: GeneratedArrivalStage.Inbound } route || aircraft.Phase != FlightPhase.Circuit)
        {
            return false;
        }
        var runway = State.Airport.GetRunway(aircraft.Runway);
        var threshold = State.Airport.Point(runway.Threshold);
        var heading = threshold.Bearing(State.Airport.Point(runway.End));
        var remaining = 0d;
        var previous = aircraft.Position;
        foreach (var point in route.Points.Skip(route.NextPointIndex))
        {
            remaining += previous.Distance(point.Position);
            previous = point.Position;
        }
        var deceleration = SpeedPerformance.Rate(aircraft.Specification, true, false, 2);
        var limit = Math.Sqrt(route.ApproachSpeedKnots * route.ApproachSpeedKnots + 2 * deceleration * remaining / MetresPerKnotSecond);
        aircraft.TargetSpeedKnots = route.SpeedOverrideKnots ?? Math.Min(route.PatternSpeedKnots, limit);
        ApproachSpeed(aircraft, aircraft.TargetSpeedKnots, dt, true, 2);
        ApproachAltitude(aircraft, route.PatternAltitudeFeetAgl, dt, 1200);
        // Reach the geographic contact marker exactly; round/intercept only
        // after it, so first radio is not tied to a shortcut around a corner.
        var path = route.Contact.EntryReached ? route.Points : route.Points.Take(route.ContactPointIndex + 1).ToList();
        route.NextPointIndex = AdvanceAirRoute(aircraft, path.Select(p => p.Position).ToArray(), route.NextPointIndex,
            dt, route.Contact.EntryReached ? heading : null);
        aircraft.PathIndex = route.NextPointIndex;
        if (route.NextPointIndex > route.ContactPointIndex)
        {
            MarkGeneratedArrivalContact(route);
        }
        if (route.NextPointIndex == route.Points.Count)
        {
            route.Stage = GeneratedArrivalStage.Final;
            aircraft.Phase = FlightPhase.Approach;
            aircraft.CircuitLeg = "final";
            aircraft.TargetSpeedKnots = route.SpeedOverrideKnots ?? route.ApproachSpeedKnots;
            SetPath(aircraft, [threshold]);
            Transmit(aircraft, "Turning final.", "flight.turning.final");
        }
        return true;
    }
}
