namespace Tower.Core;

public sealed partial class Simulation
{
    // Synthetic clearance envelope; the original map does not specify pavement widths.
    private const double RunwayHoldDistance = 35;

    private double DistanceToRunway(Position point, Runway runway)
    {
        var start = State.Airport.Point(runway.Threshold);
        var end = State.Airport.Point(runway.End);
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var fraction = Math.Clamp(((point.X - start.X) * dx + (point.Y - start.Y) * dy) / (dx * dx + dy * dy), 0, 1);
        return point.Distance(new(start.X + fraction * dx, start.Y + fraction * dy));
    }

    private double? TaxiHoldDistance(Aircraft aircraft, Runway runway)
    {
        var start = aircraft.Position;
        var travelled = 0.0;
        foreach (var end in RouteMovement.RemainingPath(aircraft))
        {
            var length = start.Distance(end);
            if (length > 0.000001 && DistanceToRunway(start, runway) >= RunwayHoldDistance - 0.000001)
            {
                var fraction = RunwayEntryFraction(start, end, runway);
                if (fraction is { } entry)
                {
                    return travelled + length * entry;
                }
            }
            travelled += length;
            start = end;
        }
        return null;
    }

    private double? RunwayEntryFraction(Position start, Position end, Runway runway)
    {
        var threshold = State.Airport.Point(runway.Threshold);
        var farEnd = State.Airport.Point(runway.End);
        var length = threshold.Distance(farEnd);
        var ux = (farEnd.X - threshold.X) / length;
        var uy = (farEnd.Y - threshold.Y) / length;
        var x = (start.X - threshold.X) * ux + (start.Y - threshold.Y) * uy;
        var y = -(start.X - threshold.X) * uy + (start.Y - threshold.Y) * ux;
        var dx = (end.X - start.X) * ux + (end.Y - start.Y) * uy;
        var dy = -(end.X - start.X) * uy + (end.Y - start.Y) * ux;
        var enter = 0.0;
        var exit = 1.0;
        var first = double.PositiveInfinity;
        if (ClipAxis(x, dx, 0, length, ref enter, ref exit) &&
            ClipAxis(y, dy, -RunwayHoldDistance, RunwayHoldDistance, ref enter, ref exit))
        {
            first = enter;
        }
        first = Math.Min(first, CircleEntry(x, y, dx, dy));
        first = Math.Min(first, CircleEntry(x - length, y, dx, dy));
        return double.IsFinite(first) ? first : null;
    }

    private static bool ClipAxis(double start, double delta, double minimum, double maximum, ref double enter, ref double exit)
    {
        if (Math.Abs(delta) < 0.000000001)
        {
            return start >= minimum && start <= maximum;
        }
        var first = (minimum - start) / delta;
        var last = (maximum - start) / delta;
        enter = Math.Max(enter, Math.Min(first, last));
        exit = Math.Min(exit, Math.Max(first, last));
        return enter <= exit;
    }

    private static double CircleEntry(double x, double y, double dx, double dy)
    {
        var a = dx * dx + dy * dy;
        var b = 2 * (x * dx + y * dy);
        var c = x * x + y * y - RunwayHoldDistance * RunwayHoldDistance;
        var discriminant = b * b - 4 * a * c;
        if (a < 0.000000001 || discriminant < 0)
        {
            return double.PositiveInfinity;
        }
        var entry = (-b - Math.Sqrt(discriminant)) / (2 * a);
        return entry >= 0 && entry <= 1 ? entry : double.PositiveInfinity;
    }
}
