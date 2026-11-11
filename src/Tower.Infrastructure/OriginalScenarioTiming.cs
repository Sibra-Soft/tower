using Tower.Core;

namespace Tower.Infrastructure;

public static partial class OriginalData
{
    private static OriginalFlightTiming? ReadOriginalTiming(ScheduledFlight flight, Airport airport)
    {
        var plan = flight.OriginalPlan!;
        if (flight.Direction == FlightDirection.Departure)
        {
            return plan.Actions?[0] == OriginalFlightAction.Takeoff
                ? new() { EntrySeconds = flight.TimeSeconds } : null;
        }
        if (plan.Actions?[0] == OriginalFlightAction.Takeoff || plan.ResolvedRoute is not { Count: > 0 } || airport.Original.SourceMslToAgl(plan.Altitude1Feet) is null ||
            !airport.AircraftTypes[flight.Type].OriginalPerformance.TryGetValue("Cruise_Speed", out var cruiseSpeed) ||
            OriginalTimingUnits.CruiseUnits(cruiseSpeed) is not { } cruiseUnits)
        {
            return null;
        }
        var runway = airport.GetRunway(flight.Runway);
        var threshold = airport.Point(runway.Threshold);
        var heading = threshold.Bearing(airport.Point(runway.End));
        var lead = OriginalTimingUnits.LeadSeconds(cruiseUnits);
        return new()
        {
            EntrySeconds = flight.TimeSeconds,
            SpawnSeconds = Math.Max(0, flight.TimeSeconds - lead),
            LeadSeconds = lead,
            InitialSpeedKnots = OriginalTimingUnits.SpeedKnots(plan.Altitude1Feet <= 10000 ? Math.Min(cruiseUnits, 0x727) : cruiseUnits),
            ArrivalContactPoint = threshold.Offset(heading + 180, OriginalTimingUnits.ContactDistanceMetres)
        };
    }
}
