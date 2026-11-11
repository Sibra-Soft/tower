namespace Tower.Core;

public sealed record RouteSample(Position Position, double Heading, int NextWaypoint, double TurnRadius = 0);

public sealed class RouteMotion
{
    private double[]? distancesToEnd;
    public List<Position> Source { get; set; } = [];
    public List<RouteSample> Samples { get; set; } = [];
    public int SampleIndex { get; set; } = 1;
    public int WaypointIndex { get; set; }
    public Position LastPosition { get; set; }
    public bool Airborne { get; set; }
    public double? ExitHeading { get; set; }

    public double RemainingDistance(Position position)
    {
        if (SampleIndex >= Samples.Count)
        {
            return 0;
        }
        if (distancesToEnd is null)
        {
            distancesToEnd = new double[Samples.Count];
            for (var index = Samples.Count - 2; index >= 0; index--)
            {
                distancesToEnd[index] = distancesToEnd[index + 1] + Samples[index].Position.Distance(Samples[index + 1].Position);
            }
        }
        return position.Distance(Samples[SampleIndex].Position) + distancesToEnd[SampleIndex];
    }

    public bool IsValid() => Source is { Count: > 0 } && Samples is { Count: >= 2 and <= 100000 } &&
        SampleIndex >= 1 && SampleIndex <= Samples.Count && WaypointIndex >= 0 && WaypointIndex <= Source.Count &&
        Samples[^1] is { } last && last.NextWaypoint == Source.Count &&
        Samples[SampleIndex - 1] is { } previous && previous.NextWaypoint == WaypointIndex &&
        double.IsFinite(LastPosition.X) && double.IsFinite(LastPosition.Y) &&
        (ExitHeading is null || double.IsFinite(ExitHeading.Value) && ExitHeading >= 0 && ExitHeading < 360) &&
        Source.All(point => double.IsFinite(point.X) && double.IsFinite(point.Y)) &&
        Samples.All(sample => sample is not null && double.IsFinite(sample.Position.X) && double.IsFinite(sample.Position.Y) &&
            double.IsFinite(sample.Heading) && sample.Heading >= 0 && sample.Heading < 360 &&
            double.IsFinite(sample.TurnRadius) && sample.TurnRadius >= 0 && sample.NextWaypoint >= 0 && sample.NextWaypoint <= Source.Count);
}

public static class RouteMovement
{
    public static double AngleDifference(double from, double to) => Difference(from, to);
    private static bool Smooth(Aircraft aircraft) => aircraft.Phase is FlightPhase.TaxiIn or FlightPhase.TaxiOut or FlightPhase.LineUp or
        FlightPhase.Circuit or FlightPhase.GoAround or FlightPhase.Breakout or FlightPhase.LowApproach or FlightPhase.LandingRoll ||
        aircraft.Phase == FlightPhase.TakeoffRoll && aircraft.Path.Count == 3;

    private static double Difference(double from, double to) => (to - from + 540) % 360 - 180;

    private static RouteMotion Prepare(Aircraft aircraft, IReadOnlyList<Position> path, int waypoint, double? exitHeading)
    {
        if (aircraft.Movement is { } cached && cached.WaypointIndex == waypoint && cached.Airborne == aircraft.IsAirborne &&
            cached.LastPosition.Distance(aircraft.Position) < 0.000001 && cached.ExitHeading == exitHeading && cached.Source.SequenceEqual(path))
        {
            return cached;
        }
        var motion = new RouteMotion { Source = path.ToList(), WaypointIndex = waypoint, LastPosition = aircraft.Position, Airborne = aircraft.IsAirborne, ExitHeading = exitHeading };
        var speed = Math.Max(aircraft.SpeedKnots, aircraft.Specification.ApproachSpeed) * Simulation.MetresPerKnotSecond;
        var radius = aircraft.IsAirborne ? speed / (FlightDynamics.TurnRate(aircraft) * Math.PI / 180) : FlightDynamics.GroundRadius(aircraft);
        motion.Samples.Add(new(aircraft.Position, aircraft.Heading, waypoint));
        void AddLegEnd(Position end, double heading, int nextWaypoint)
        {
            var start = motion.Samples[^1];
            var length = start.Position.Distance(end);
            if (motion.Samples.Count == 1 && length > 1 && Math.Abs(Difference(start.Heading, heading)) > 1)
            {
                // Join the first leg from the current flight direction, rather than
                // moving sideways while only rotating the displayed nose.
                var handle = Math.Min(radius, length * 0.35);
                var controls = new List<Position> { start.Position, start.Position.Offset(start.Heading, handle) };
                if (Math.Abs(Difference(start.Heading, heading)) > 150)
                {
                    controls.Add(start.Position.Offset(start.Heading, handle * 2).Offset(start.Heading + 90, handle));
                    controls.Add(end.Offset(heading + 180, handle * 2).Offset(heading + 90, handle));
                }
                controls.Add(end.Offset(heading + 180, handle));
                controls.Add(end);
                Position Evaluate(double fraction)
                {
                    var points = controls.ToArray();
                    for (var count = points.Length - 1; count > 0; count--)
                    {
                        for (var index = 0; index < count; index++)
                        {
                            points[index] = new(points[index].X + (points[index + 1].X - points[index].X) * fraction,
                                points[index].Y + (points[index + 1].Y - points[index].Y) * fraction);
                        }
                    }
                    return points[0];
                }
                var steps = Math.Clamp((int)Math.Ceiling(length / 10), 64, 512);
                for (var step = 1; step < steps; step++)
                {
                    var fraction = step / (double)steps;
                    var before = Evaluate(Math.Max(0, fraction - 0.0001));
                    var after = Evaluate(Math.Min(1, fraction + 0.0001));
                    motion.Samples.Add(new(Evaluate(fraction), before.Bearing(after), waypoint));
                }
            }
            motion.Samples.Add(new(end, heading, nextWaypoint));
        }
        var previous = aircraft.Position;
        for (var index = waypoint; index < path.Count; index++)
        {
            var corner = path[index];
            var incomingLength = previous.Distance(corner);
            var incoming = incomingLength > 0.001 ? previous.Bearing(corner) : aircraft.Heading;
            Position? nextPoint = index + 1 < path.Count ? path[index + 1]
                : aircraft.Phase == FlightPhase.Breakout ? path[0]
                : exitHeading is { } exit ? corner.Offset(exit, Math.Max(1, radius * 3)) : null;
            if (nextPoint is { } next && incomingLength > 0.01)
            {
                var outgoingLength = corner.Distance(next);
                var turn = Difference(incoming, corner.Bearing(next));
                if (Math.Abs(turn) is > 1 and < 175 && outgoingLength > 0.01)
                {
                    var tangent = Math.Tan(Math.Abs(turn) * Math.PI / 360);
                    var trim = Math.Min(radius * tangent, Math.Min(incomingLength, outgoingLength) * 0.4);
                    var actualRadius = trim / tangent;
                    var entry = corner.Offset(incoming + 180, trim);
                    var center = entry.Offset(incoming + Math.Sign(turn) * 90, actualRadius);
                    var radial = center.Bearing(entry);
                    AddLegEnd(entry, incoming, index);
                    var steps = (int)Math.Ceiling(Math.Abs(turn));
                    for (var step = 1; step <= steps; step++)
                    {
                        var angle = turn * step / steps;
                        motion.Samples.Add(new(center.Offset(radial + angle, actualRadius),
                            Coordinates.NormalizeHeading(incoming + angle), step == steps ? index + 1 : index, actualRadius));
                    }
                    previous = motion.Samples[^1].Position;
                    continue;
                }
            }
            AddLegEnd(corner, Coordinates.NormalizeHeading(incoming), index + 1);
            previous = corner;
        }
        aircraft.Movement = motion;
        return motion;
    }

    public static IEnumerable<Position> RemainingPath(Aircraft aircraft)
    {
        if (!Smooth(aircraft) || aircraft.PathIndex >= aircraft.Path.Count)
        {
            return aircraft.Path.Skip(aircraft.PathIndex);
        }
        var motion = Prepare(aircraft, aircraft.Path, aircraft.PathIndex, aircraft.PathExitHeading);
        return motion.Samples.Skip(motion.SampleIndex).Select(sample => sample.Position);
    }

    public static double RemainingDistance(Aircraft aircraft)
    {
        if (Smooth(aircraft) && aircraft.PathIndex < aircraft.Path.Count)
        {
            return Prepare(aircraft, aircraft.Path, aircraft.PathIndex, aircraft.PathExitHeading).RemainingDistance(aircraft.Position);
        }
        var result = 0d;
        var previous = aircraft.Position;
        foreach (var point in aircraft.Path.Skip(aircraft.PathIndex))
        {
            result += previous.Distance(point);
            previous = point;
        }
        return result;
    }

    public static double TaxiSpeedLimit(Aircraft aircraft, double braking)
    {
        if (aircraft.Movement is not { } motion)
        {
            return double.PositiveInfinity;
        }
        var distance = 0d;
        var previous = aircraft.Position;
        var limit = double.PositiveInfinity;
        for (var index = motion.SampleIndex; index < motion.Samples.Count; index++)
        {
            var sample = motion.Samples[index];
            if (sample.TurnRadius > 0)
            {
                // Limit lateral acceleration in the turn and brake before entering it.
                var cornerSpeedSquared = aircraft.IsAirborne
                    ? Math.Min(9.80665 * Math.Tan(25 * Math.PI / 180) * sample.TurnRadius,
                        Math.Pow(aircraft.Specification.TurnRate * Math.PI / 180 * sample.TurnRadius, 2))
                    : .65 * sample.TurnRadius;
                limit = Math.Min(limit, Math.Sqrt(cornerSpeedSquared + 2 * braking * distance) / Simulation.MetresPerKnotSecond);
            }
            distance += previous.Distance(sample.Position);
            previous = sample.Position;
        }
        return limit;
    }

    public static int Advance(Aircraft aircraft, IReadOnlyList<Position> path, int waypoint, double distance, double? exitHeading = null)
    {
        if (waypoint >= path.Count)
        {
            aircraft.Movement = null;
            return waypoint;
        }
        var motion = Prepare(aircraft, path, waypoint, exitHeading);
        if (aircraft.IsAirborne && aircraft.SpeedKnots > .01)
        {
            var braking = SpeedPerformance.Rate(aircraft.Specification, true, false, 2) * Simulation.MetresPerKnotSecond;
            var minimum = FlightDynamics.Profile(aircraft, "Stall_Speed", aircraft.Specification.ApproachSpeed * .75) * 1.05;
            var limit = Math.Max(minimum, TaxiSpeedLimit(aircraft, braking));
            var oldSpeed = aircraft.SpeedKnots;
            var dt = distance / (oldSpeed * Simulation.MetresPerKnotSecond);
            aircraft.SpeedKnots = Math.Min(oldSpeed, Math.Max(limit, oldSpeed - braking / Simulation.MetresPerKnotSecond * dt));
            distance *= aircraft.SpeedKnots / oldSpeed;
        }
        while (motion.SampleIndex < motion.Samples.Count)
        {
            var sample = motion.Samples[motion.SampleIndex];
            var length = aircraft.Position.Distance(sample.Position);
            if (length > 0.000001 && distance < length)
            {
                var start = motion.Samples[motion.SampleIndex - 1];
                var fullLength = start.Position.Distance(sample.Position);
                aircraft.Position = aircraft.Position.Towards(sample.Position, distance);
                var fraction = fullLength <= 0.000001 ? 1 : Math.Clamp(aircraft.Position.Distance(start.Position) / fullLength, 0, 1);
                aircraft.Heading = Coordinates.NormalizeHeading(start.Heading + Difference(start.Heading, sample.Heading) * fraction);
                break;
            }
            if (length > 0.000001)
            {
                aircraft.Heading = sample.Heading;
            }
            aircraft.Position = sample.Position;
            distance = Math.Max(0, distance - length);
            motion.WaypointIndex = sample.NextWaypoint;
            motion.SampleIndex++;
        }
        motion.LastPosition = aircraft.Position;
        return motion.WaypointIndex;
    }

    public static bool Move(Aircraft aircraft, double distance)
    {
        if (!Smooth(aircraft))
        {
            while (aircraft.PathIndex < aircraft.Path.Count)
            {
                var point = aircraft.Path[aircraft.PathIndex];
                var length = aircraft.Position.Distance(point);
                if (length > 0.001)
                {
                    aircraft.Heading = aircraft.Position.Bearing(point);
                }
                if (distance < length)
                {
                    aircraft.Position = aircraft.Position.Towards(point, distance);
                    return false;
                }
                aircraft.Position = point;
                distance -= length;
                aircraft.PathIndex++;
            }
        }
        else
        {
            aircraft.PathIndex = Advance(aircraft, aircraft.Path, aircraft.PathIndex, distance, aircraft.PathExitHeading);
        }
        return aircraft.PathIndex >= aircraft.Path.Count;
    }
}
