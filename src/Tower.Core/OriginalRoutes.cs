namespace Tower.Core;

public sealed record OriginalRoutePoint(string Name, Position Position);

public enum OriginalRouteStage
{
    InitialClimb, FollowingFixes, Finished, Cancelled
}

public sealed class OriginalRouteProgress
{
    public OriginalRouteStage Stage { get; set; }
    public List<OriginalRoutePoint> Points { get; set; } = [];
    public int NextPointIndex { get; set; }
    public double TargetAltitudeFeetAgl { get; set; }
    public bool Altitude2Active { get; set; }
    public bool? UseAltitude2 { get; set; }

    public bool IsValid() => Enum.IsDefined(Stage) && Points is { Count: > 0 } &&
        NextPointIndex >= 0 && NextPointIndex <= Points.Count &&
        double.IsFinite(TargetAltitudeFeetAgl) && TargetAltitudeFeetAgl >= 0 &&
        Points.All(point => point is not null && !string.IsNullOrWhiteSpace(point.Name) &&
            double.IsFinite(point.Position.X) && double.IsFinite(point.Position.Y)) &&
        (Stage != OriginalRouteStage.FollowingFixes || NextPointIndex < Points.Count);
}

public sealed partial class Simulation
{
    private void InitializeOriginalRoute(Aircraft aircraft)
    {
        if (aircraft.OriginalPlan is not { ResolvedRoute.Count: > 0 } plan ||
            plan.AltitudeReference != OriginalAltitudeReference.Msl ||
            State.Airport.Original.SourceMslToAgl(plan.Altitude1Feet) is not { } altitude)
        {
            return;
        }
        var points = plan.ResolvedRoute.ToList();
        var progress = new OriginalRouteProgress { Points = points, TargetAltitudeFeetAgl = altitude };
        if (aircraft.Direction == FlightDirection.Arrival)
        {
            var runway = State.Airport.GetRunway(aircraft.Runway);
            var threshold = State.Airport.Point(runway.Threshold);
            var heading = threshold.Bearing(State.Airport.Point(runway.End));
            var range = State.Airport.Original.PatternDistance(aircraft.Type, "final") ?? 3 * 1852;
            points.Add(new("FINAL " + runway.Name, threshold.Offset(heading + 180, range)));
            aircraft.Position = points[0].Position;
            aircraft.Heading = aircraft.Position.Bearing(points[1].Position);
            aircraft.Phase = FlightPhase.Circuit;
            aircraft.CircuitLeg = "route";
            progress.NextPointIndex = 1;
            progress.Stage = OriginalRouteStage.FollowingFixes;
            progress.TargetAltitudeFeetAgl = State.Airport.Original.PatternAltitudeAgl(aircraft.Type) ?? altitude;
        }
        else
        {
            if (!aircraft.FollowOriginalDepartureAltitude)
            {
                return;
            }
            progress.Stage = OriginalRouteStage.InitialClimb;
        }
        aircraft.OriginalRoute = progress;
    }

    private static void CancelOriginalRoute(Aircraft aircraft)
    {
        if (aircraft.GeneratedArrival is { } arrival)
        {
            arrival.Stage = GeneratedArrivalStage.Cancelled;
        }
        if (aircraft.GeneratedDeparture is { } departure && departure.Stage != GeneratedDepartureStage.Finished)
        {
            departure.Stage = GeneratedDepartureStage.Cancelled;
            aircraft.FollowOriginalDepartureRoute = false;
            aircraft.FollowOriginalDepartureAltitude = false;
        }
        if (aircraft.FreeTraffic is { } generated)
        {
            generated.AwaitingEntry = false;
        }
        if (aircraft.OriginalRoute is { } progress)
        {
            progress.Stage = OriginalRouteStage.Cancelled;
        }
    }

    private bool UpdateOriginalRoute(Aircraft aircraft, double dt)
    {
        if (aircraft.OriginalRoute is not { } route || route.Stage is OriginalRouteStage.Finished or OriginalRouteStage.Cancelled ||
            aircraft.Phase is not (FlightPhase.Circuit or FlightPhase.Departing))
        {
            return false;
        }
        var airport = State.Airport;
        var runway = airport.GetRunway(aircraft.Runway);
        if (aircraft.Direction == FlightDirection.Arrival && aircraft.InitialContact is { EntryReached: false })
        {
            return UpdatePreparedArrival(aircraft, route, dt);
        }
        if (route.Stage == OriginalRouteStage.InitialClimb)
        {
            var threshold = airport.Point(runway.Threshold);
            var end = airport.Point(runway.End);
            var heading = threshold.Bearing(end);
            var radians = heading * Math.PI / 180;
            var along = (aircraft.Position.X - threshold.X) * Math.Sin(radians) + (aircraft.Position.Y - threshold.Y) * Math.Cos(radians);
            FlyTrack(aircraft, heading, dt);
            ApproachSpeed(aircraft, FlightDynamics.DepartureSpeed(aircraft), dt, true, 3);
            ApproachAltitude(aircraft, route.TargetAltitudeFeetAgl, dt, 1800);
            if (along >= threshold.Distance(end) && Math.Abs(aircraft.AltitudeFeet - route.TargetAltitudeFeetAgl) < 0.01)
            {
                route.Stage = OriginalRouteStage.FollowingFixes;
                aircraft.OriginalDepartureRouteStarted = true;
                var nextAction = aircraft.OriginalPlan!.RouteTokens.Select(token => token.ToUpperInvariant())
                    .Where(token => token is "@TO" or "@LP" or "@NL" or "@FS" or "@TG" or "@LA").Skip(1).FirstOrDefault();
                if ((route.UseAltitude2 ?? nextAction == "@LP") && airport.Original.SourceMslToAgl(aircraft.OriginalPlan.Altitude2Feet) is { } target)
                {
                    route.TargetAltitudeFeetAgl = target;
                    route.Altitude2Active = true;
                }
            }
            return true;
        }

        ApproachSpeed(aircraft, aircraft.Direction == FlightDirection.Departure ? FlightDynamics.DepartureSpeed(aircraft) : aircraft.TargetSpeedKnots, dt, true, 3);
        ApproachAltitude(aircraft, route.TargetAltitudeFeetAgl, dt, 1200);
        double? exitHeading = aircraft.Direction == FlightDirection.Arrival
            ? airport.Point(runway.Threshold).Bearing(airport.Point(runway.End)) : null;
        route.NextPointIndex = AdvanceAirRoute(aircraft, route.Points.Select(point => point.Position).ToArray(), route.NextPointIndex, dt, exitHeading);
        if (route.NextPointIndex == route.Points.Count)
        {
            route.Stage = OriginalRouteStage.Finished;
            if (aircraft.Direction == FlightDirection.Arrival)
            {
                aircraft.Phase = FlightPhase.Approach;
                aircraft.CircuitLeg = "final";
                aircraft.TargetSpeedKnots = aircraft.Specification.ApproachSpeed;
                SetPath(aircraft, [airport.Point(runway.Threshold)]);
            }
            else
            {
                CompleteDeparture(aircraft);
            }
        }
        return true;
    }
}
