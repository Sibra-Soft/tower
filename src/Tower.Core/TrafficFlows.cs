namespace Tower.Core;

public sealed record TrafficFlowEntry(string Fix, int Percent);

// Generated traffic is kept separate from authored SIM flight plans.
public sealed record GeneratedTrafficRoute(string Origin, string Destination, double BearingDegrees);

public static class TrafficFlows
{
    public static TrafficFlowEntry SelectDeparture(IReadOnlyList<TrafficFlowEntry> entries, int roll)
    {
        if (entries.Count == 0 || roll is < 0 or > 99)
        {
            throw new ArgumentOutOfRangeException(nameof(roll));
        }
        long cumulative = 0;
        foreach (var entry in entries)
        {
            cumulative += entry.Percent;
            if (cumulative > roll)
            {
                return entry;
            }
        }
        // GAMENT.DLL uses the first entry if the percentages do not cover the roll.
        return entries[0];
    }
}

public sealed partial class Simulation
{
    private int DrawTrafficRandom(int minimum, int maximum)
    {
        var state = State.TrafficFlowRandomState;
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        State.TrafficFlowRandomState = state;
        return minimum + (int)(state % (uint)(maximum - minimum + 1));
    }

    private GeneratedTrafficRoute? SelectTrafficFlow(FlightDirection direction, Runway runway)
    {
        var airport = State.Airport;
        var entries = direction == FlightDirection.Arrival ? airport.Original.ArrivalFlow : airport.Original.DepartureFlow;
        if (entries.Count == 0)
        {
            return null;
        }
        var threshold = airport.Point(runway.Threshold);
        Position Point(TrafficFlowEntry entry) => airport.Original.Fixes[entry.Fix]
            .ProjectFrom(airport.Latitude, airport.Longitude, airport.Observer);
        TrafficFlowEntry selected;
        if (direction == FlightDirection.Arrival)
        {
            var heading = threshold.Bearing(airport.Point(runway.End));
            selected = entries.MinBy(entry => Math.Abs((Point(entry).Bearing(threshold) - heading + 540) % 360 - 180))!;
        }
        else
        {
            // Persist the modern PRNG independently of callsign allocation and SIM seeds.
            selected = TrafficFlows.SelectDeparture(entries, DrawTrafficRandom(0, 99));
        }
        return direction == FlightDirection.Arrival
            ? new(selected.Fix, airport.Code, threshold.Bearing(Point(selected)))
            : new(airport.Code, selected.Fix, threshold.Bearing(Point(selected)));
    }
}
