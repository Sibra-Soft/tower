namespace Tower.Core;

public sealed partial class Simulation
{
    private bool OccupiesActiveRunway(Aircraft first, Aircraft second)
    {
        if (first.IsAirborne || second.IsAirborne)
        {
            return false;
        }
        // Actual pavement occupancy matters, including taxi traffic whose assigned
        // departure/arrival runway may be a completely different strip.
        return OnRunwayWith(first, second) || OnRunwayWith(second, first);
    }

    private bool OnRunwayWith(Aircraft operation, Aircraft other)
    {
        if (!operation.IsOnRunway)
        {
            return false;
        }
        var runway = State.Airport.GetRunway(operation.Runway);
        if (other.IsOnRunway && !SamePhysicalRunway(runway, State.Airport.GetRunway(other.Runway)))
        {
            // Two operations on intersecting strips conflict locally at the
            // intersection, rather than reserving the entire other runway.
            return false;
        }
        return DistanceToRunway(operation.Position, runway) < RunwayHoldDistance &&
            DistanceToRunway(other.Position, runway) < RunwayHoldDistance;
    }

    private static bool FootprintsOverlap(Aircraft first, Aircraft second, double gap = 0)
    {
        // Separating-axis test on the two oriented model bounds. Test both sets
        // of axes; a world-aligned bounding box would overcount diagonal queues.
        foreach (var heading in new[] { first.Heading, second.Heading })
        {
            var own = Footprint(first).Project(first.Heading - heading);
            var other = Footprint(second).Project(second.Heading - heading);
            var radians = heading * Math.PI / 180;
            var dx = second.Position.X - first.Position.X;
            var dy = second.Position.Y - first.Position.Y;
            var forward = dx * Math.Sin(radians) + dy * Math.Cos(radians);
            var right = dx * Math.Cos(radians) - dy * Math.Sin(radians);
            if (forward + other.MinForward >= own.MaxForward + gap || forward + other.MaxForward <= own.MinForward - gap ||
                right + other.MinRight >= own.MaxRight + gap || right + other.MaxRight <= own.MinRight - gap)
            {
                return false;
            }
        }
        return true;
    }

    private static bool SamePhysicalRunway(Runway first, Runway second) =>
        (!string.IsNullOrWhiteSpace(first.PhysicalId) && first.PhysicalId.Equals(second.PhysicalId, StringComparison.OrdinalIgnoreCase)) ||
        (first.Threshold.Equals(second.Threshold, StringComparison.OrdinalIgnoreCase) && first.End.Equals(second.End, StringComparison.OrdinalIgnoreCase)) ||
        (first.Threshold.Equals(second.End, StringComparison.OrdinalIgnoreCase) && first.End.Equals(second.Threshold, StringComparison.OrdinalIgnoreCase));

    private bool OccupiesRunwayIntersection(Aircraft first, Aircraft second)
    {
        if (!first.IsOnRunway || !second.IsOnRunway)
        {
            return false;
        }
        var firstRunway = State.Airport.GetRunway(first.Runway);
        var secondRunway = State.Airport.GetRunway(second.Runway);
        return !SamePhysicalRunway(firstRunway, secondRunway) && RunwaySegmentsIntersect(firstRunway, secondRunway) &&
            DistanceToRunway(first.Position, secondRunway) < RunwayHoldDistance &&
            DistanceToRunway(second.Position, firstRunway) < RunwayHoldDistance;
    }
}
