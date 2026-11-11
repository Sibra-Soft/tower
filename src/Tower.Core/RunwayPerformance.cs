namespace Tower.Core;

public static class RunwayPerformance
{
    public static double? Metres(double? feet) => feet is { } value && double.IsFinite(value) && value > 0
        ? value * Coordinates.MetresPerFoot
        : null;

    public static double Acceleration(double initialKnots, double finalKnots, double metres)
    {
        var initial = initialKnots * Simulation.MetresPerKnotSecond;
        var final = finalKnots * Simulation.MetresPerKnotSecond;
        return Math.Abs(final * final - initial * initial) / (2 * metres);
    }

    public static double BrakingDistance(double initialKnots, double finalKnots, double acceleration)
    {
        var initial = initialKnots * Simulation.MetresPerKnotSecond;
        var final = finalKnots * Simulation.MetresPerKnotSecond;
        return Math.Max(0, initial * initial - final * final) / (2 * acceleration);
    }

    public static (string Rollout, string Exit)? LandingExit(Airport airport, Runway runway, double minimumMetres)
    {
        var threshold = airport.Point(runway.Threshold);
        var end = airport.Point(runway.End);
        var dx = end.X - threshold.X;
        var dy = end.Y - threshold.Y;
        var length = threshold.Distance(end);
        // Some original intersections reference the short runway-access segment
        // rather than the full Runway segment (Canyon P27 references S85).
        var candidates = airport.Nodes.Values.Where(node => node.Kind.Equals("RWYINT", StringComparison.OrdinalIgnoreCase)
            && (node.Label.Equals(runway.PhysicalId, StringComparison.OrdinalIgnoreCase) ||
                airport.Segments.Any(segment => segment.Id.Equals(node.Label, StringComparison.OrdinalIgnoreCase) &&
                    (segment.From == node.Id && (segment.To == runway.Threshold || segment.To == runway.End) ||
                     segment.To == node.Id && (segment.From == runway.Threshold || segment.From == runway.End)))))
            .Where(node => Math.Abs((node.Position.X - threshold.X) * dy - (node.Position.Y - threshold.Y) * dx) / length <= 5)
            .Select(node => (Node: node, Along: ((node.Position.X - threshold.X) * dx + (node.Position.Y - threshold.Y) * dy) / length))
            .Where(candidate => candidate.Along >= minimumMetres && candidate.Along < length)
            .OrderBy(candidate => candidate.Along);

        foreach (var candidate in candidates)
        {
            var exit = airport.Segments.Where(segment => segment.Kind is not ("RUNWAY" or "VROUTE") &&
                    (segment.From == candidate.Node.Id || !segment.OneWay && segment.To == candidate.Node.Id))
                .Select(segment => segment.From == candidate.Node.Id ? segment.To : segment.From)
                .FirstOrDefault(id => airport.Nodes[id].Kind is not ("RWYINT" or "RUNWAY"));
            if (exit is not null)
            {
                return (candidate.Node.Id, exit);
            }
        }

        // Authored maps need not have original RwyInt metadata.
        return threshold.Distance(airport.Point(runway.Rollout)) >= minimumMetres
            ? (runway.Rollout, runway.Exit)
            : null;
    }
}
