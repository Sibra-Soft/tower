namespace Tower.Core;

public enum GeneratedDepartureStage
{
    InitialClimb, ToDestination, Finished, Cancelled
}

// A snapshot of generated traffic, never a synthetic authored SIM plan.
public sealed class GeneratedDepartureProgress
{
    public GeneratedDepartureStage Stage { get; set; }
    public OriginalRoutePoint Destination { get; set; } = new("", new());
    public double FirstAltitudeFeetAgl { get; set; }
    public double? SecondAltitudeFeetAgl { get; set; }
    public bool Altitude2Active { get; set; }
    public int NextPointIndex { get; set; }

    public bool IsValid() => Enum.IsDefined(Stage) && Destination is not null &&
        !string.IsNullOrWhiteSpace(Destination.Name) && double.IsFinite(Destination.Position.X) && double.IsFinite(Destination.Position.Y) &&
        double.IsFinite(FirstAltitudeFeetAgl) && FirstAltitudeFeetAgl > 0 &&
        (SecondAltitudeFeetAgl is null || double.IsFinite(SecondAltitudeFeetAgl.Value) && SecondAltitudeFeetAgl.Value >= 0) &&
        (!Altitude2Active || SecondAltitudeFeetAgl is not null && Stage != GeneratedDepartureStage.InitialClimb) &&
        (Stage != GeneratedDepartureStage.ToDestination || Altitude2Active == (SecondAltitudeFeetAgl is not null)) &&
        NextPointIndex is >= 0 and <= 1 &&
        (Stage is not (GeneratedDepartureStage.InitialClimb or GeneratedDepartureStage.ToDestination) || NextPointIndex == 0) &&
        (Stage != GeneratedDepartureStage.Finished || NextPointIndex == 1);
}

public sealed partial class Simulation
{
    private void InitializeGeneratedDeparture(Aircraft aircraft)
    {
        var airport = State.Airport;
        if (!airport.Original.UseGeneratedDepartureRoutes || aircraft.Direction != FlightDirection.Departure ||
            aircraft.OriginalPlan is not null || aircraft.GeneratedRoute is not { } flow ||
            aircraft.GeneratedPerformance is not { } performance || !aircraft.FollowOriginalDepartureAltitude ||
            !airport.Original.Fixes.TryGetValue(flow.Destination, out var fix) ||
            airport.Original.SourceMslToAgl(performance.Altitude1FeetMsl) is not > 0)
        {
            return;
        }
        var position = fix.ProjectFrom(airport.Latitude, airport.Longitude, airport.Observer);
        if (!double.IsFinite(position.X) || !double.IsFinite(position.Y))
        {
            return;
        }
        aircraft.GeneratedDeparture = new()
        {
            Destination = new(flow.Destination, position),
            FirstAltitudeFeetAgl = airport.Original.SourceMslToAgl(performance.Altitude1FeetMsl)!.Value,
            SecondAltitudeFeetAgl = airport.Original.SourceMslToAgl(performance.Altitude2FeetMsl)
        };
    }

    private bool UpdateGeneratedDeparture(Aircraft aircraft, double dt)
    {
        if (aircraft.GeneratedDeparture is not { } route || route.Stage is GeneratedDepartureStage.Finished or GeneratedDepartureStage.Cancelled ||
            aircraft.Phase != FlightPhase.Departing)
        {
            return false;
        }
        if (!aircraft.FollowOriginalDepartureRoute || !aircraft.FollowOriginalDepartureAltitude)
        {
            route.Stage = GeneratedDepartureStage.Cancelled;
            return false;
        }
        // Preserve the existing departure speed/PRF acceleration behavior.
        ApproachSpeed(aircraft, FlightDynamics.DepartureSpeed(aircraft), dt, true, 3);
        if (route.Stage == GeneratedDepartureStage.InitialClimb)
        {
            var runway = State.Airport.GetRunway(aircraft.Runway);
            var threshold = State.Airport.Point(runway.Threshold);
            var end = State.Airport.Point(runway.End);
            var heading = threshold.Bearing(end);
            FlyTrack(aircraft, heading, dt);
            ApproachAltitude(aircraft, route.FirstAltitudeFeetAgl, dt, 1800);
            var radians = heading * Math.PI / 180;
            var along = (aircraft.Position.X - threshold.X) * Math.Sin(radians) + (aircraft.Position.Y - threshold.Y) * Math.Cos(radians);
            // Modern transition shared with authored departures: clear the runway
            // and capture height 1 before the LP route/height-2 instruction.
            if (along >= threshold.Distance(end) && Math.Abs(aircraft.AltitudeFeet - route.FirstAltitudeFeetAgl) < 0.01)
            {
                route.Stage = GeneratedDepartureStage.ToDestination;
                route.Altitude2Active = route.SecondAltitudeFeetAgl is not null;
                aircraft.OriginalDepartureRouteStarted = true;
                aircraft.Movement = null;
            }
            return true;
        }
        ApproachAltitude(aircraft, route.Altitude2Active ? route.SecondAltitudeFeetAgl!.Value : route.FirstAltitudeFeetAgl, dt, 1200);
        route.NextPointIndex = AdvanceAirRoute(aircraft, [route.Destination.Position], route.NextPointIndex, dt);
        if (route.NextPointIndex == 1)
        {
            CompleteDeparture(aircraft);
        }
        return true;
    }
}
