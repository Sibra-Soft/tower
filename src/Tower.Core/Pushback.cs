namespace Tower.Core;

// Source values are retained verbatim. Distances A/B are not converted into
// metres without proof of their units; declared ramp/alley geometry defines the trajectory.
public sealed record GatePushbackDefinition(string Side, double DistanceA, double Angle, double DistanceB,
    string Ramp, string ConnectionNode, string Source)
{
    public List<string> Connections { get; init; } = [];
    public string AlleyConnectionNode { get; init; } = "";
}

public sealed record PushbackPose(Position Position, double Heading);

public sealed class PushbackProgress
{
    public string ConnectionNode { get; set; } = "";
    public double InitialHeading { get; set; }
    public double FinalHeading { get; set; }
    public bool Completed { get; set; }
    public List<PushbackPose> Samples { get; set; } = [];
    public int SampleIndex { get; set; } = 1;

    public bool IsValid() => !string.IsNullOrWhiteSpace(ConnectionNode) &&
        double.IsFinite(InitialHeading) && InitialHeading is >= 0 and < 360 &&
        double.IsFinite(FinalHeading) && FinalHeading is >= 0 and < 360 &&
        Samples is { Count: <= 513 } && Samples.Count != 1 && SampleIndex >= 1 && SampleIndex <= Math.Max(1, Samples.Count) &&
        (!Completed || Samples.Count > 1 && SampleIndex == Samples.Count) &&
        Samples.All(s => s is not null && double.IsFinite(s.Position.X) && double.IsFinite(s.Position.Y) &&
            double.IsFinite(s.Heading) && s.Heading is >= 0 and < 360);
}

public sealed partial class Simulation
{
    private void InitializePushback(Aircraft aircraft)
    {
        if (aircraft.Direction != FlightDirection.Departure || aircraft.Phase != FlightPhase.AtGate)
        {
            return;
        }
        aircraft.Pushback = null;
        if (!State.Airport.PushbackGates.TryGetValue(aircraft.Gate, out var definition))
        {
            return;
        }
        var join = State.Airport.Point(definition.ConnectionNode);
        if (aircraft.Position.Distance(join) < 5)
        {
            return;
        }
        // Park nose-in; motion will initially be in the opposite direction.
        aircraft.Heading = join.Bearing(aircraft.Position);
        aircraft.Pushback = new() { ConnectionNode = definition.ConnectionNode, InitialHeading = aircraft.Heading };
    }

    private CommandResult BeginPushback(Aircraft aircraft, string direction)
    {
        if (aircraft.Phase != FlightPhase.AtGate || aircraft.Direction != FlightDirection.Departure ||
            aircraft.Pushback is not { Completed: false, Samples.Count: 0 } progress)
        {
            return new(false, "Pushback requires a waiting departure at a pushback stand.");
        }
        var definition = State.Airport.PushbackGates[aircraft.Gate];
        var connectionNode = definition.ConnectionNode;
        var desired = direction switch
        {
            "n" or "north" => 0d,
            "e" or "east" => 90d,
            "s" or "south" => 180d,
            "w" or "west" => 270d,
            "" => Coordinates.NormalizeHeading(Math.Round((progress.InitialHeading +
                (definition.Side == "Left" ? -90 : definition.Side == "Right" ? 90 : 0)) / 90) * 90),
            _ => double.NaN
        };
        if (!double.IsFinite(desired))
        {
            return new(false, "Use PB [N|E|S|W], or pushback north/east/south/west.");
        }
        var bestCost = double.PositiveInfinity;
        foreach (var candidate in new[] { definition.ConnectionNode, definition.AlleyConnectionNode }.Where(node => node.Length > 0).Distinct())
        {
            List<Position> path;
            try
            {
                path = Routing.FindTaxiPath(State.Airport, candidate, State.Airport.GetRunway(aircraft.Runway).Hold);
            }
            catch (InvalidDataException)
            {
                continue; // Keep stand-only/legacy pushback available without a usable route.
            }
            var next = path.Skip(1).FirstOrDefault(point => point.Distance(path[0]) > 0.01, path[0]);
            var taxiHeading = path[0].Bearing(next);
            var headings = direction.Length == 0 ? new[] { 0d, 90d, 180d, 270d } : new[] { desired };
            foreach (var heading in headings.Where(heading => Math.Abs(RouteMovement.AngleDifference(progress.InitialHeading, heading)) <= 135))
            {
                var cost = aircraft.Position.Distance(path[0]);
                for (var index = 1; index < path.Count; index++)
                {
                    cost += path[index - 1].Distance(path[index]);
                }
                // Use the same aircraft-dependent radius as the taxi movement.
                cost += Math.Abs(RouteMovement.AngleDifference(heading, taxiHeading)) * Math.PI / 180 * FlightDynamics.GroundRadius(aircraft);
                if (cost >= bestCost)
                {
                    continue;
                }
                bestCost = cost;
                desired = heading;
                connectionNode = candidate;
            }
        }
        if (Math.Abs(RouteMovement.AngleDifference(progress.InitialHeading, desired)) > 135)
        {
            return new(false, "That facing direction requires a U-turn outside this stand's pushback geometry.");
        }
        var end = State.Airport.Point(connectionNode);
        if (State.Aircraft.Any(other => other != aircraft && other.Phase == FlightPhase.Pushback &&
            other.Pushback?.ConnectionNode == connectionNode))
        {
            return new(false, "The pushback connection is already in use.");
        }
        var start = aircraft.Position;
        var handle = start.Distance(end) * 0.5;
        var first = start.Offset(progress.InitialHeading + 180, handle);
        var second = end.Offset(desired, handle);
        var samples = new List<PushbackPose>();
        for (var i = 0; i <= 128; i++)
        {
            var t = i / 128d;
            var u = 1 - t;
            var position = new Position(u * u * u * start.X + 3 * u * u * t * first.X + 3 * u * t * t * second.X + t * t * t * end.X,
                u * u * u * start.Y + 3 * u * u * t * first.Y + 3 * u * t * t * second.Y + t * t * t * end.Y);
            var tangent = new Position(3 * u * u * (first.X - start.X) + 6 * u * t * (second.X - first.X) + 3 * t * t * (end.X - second.X),
                3 * u * u * (first.Y - start.Y) + 6 * u * t * (second.Y - first.Y) + 3 * t * t * (end.Y - second.Y));
            samples.Add(new(position, Coordinates.NormalizeHeading(new Position(0, 0).Bearing(tangent) + 180)));
        }
        progress.FinalHeading = desired;
        progress.ConnectionNode = connectionNode;
        progress.Samples = samples;
        progress.SampleIndex = 1;
        aircraft.Phase = FlightPhase.Pushback;
        aircraft.Holding = false;
        aircraft.SpeedKnots = 0;
        return new(true, $"Pushback approved, face {PushbackDirection(desired)}.");
    }

    private static string PushbackDirection(double heading) => new[] { "north", "east", "south", "west" }[(int)Math.Round(heading / 90) % 4];

    private bool UpdatePushback(Aircraft aircraft, double dt)
    {
        if (aircraft.Phase != FlightPhase.Pushback || aircraft.Pushback is not { } progress)
        {
            return false;
        }
        if (aircraft.Holding)
        {
            aircraft.SpeedKnots = 0;
            return true;
        }
        if (GroundObstruction(aircraft) is not null)
        {
            aircraft.SpeedKnots = 0;
            return true;
        }
        aircraft.TrafficObstructionReported = false;
        var routeRemaining = aircraft.Position.Distance(progress.Samples[progress.SampleIndex].Position);
        for (var i = progress.SampleIndex + 1; i < progress.Samples.Count; i++)
        {
            routeRemaining += progress.Samples[i - 1].Position.Distance(progress.Samples[i].Position);
        }
        var target = Math.Min(3, Math.Sqrt(2 * 0.4 * routeRemaining) / MetresPerKnotSecond);
        aircraft.SpeedKnots = Approach(aircraft.SpeedKnots, target, dt);
        var distance = aircraft.SpeedKnots * MetresPerKnotSecond * dt;
        var requestedDistance = distance;
        var turnAllowance = 8 * dt;
        while (progress.SampleIndex < progress.Samples.Count)
        {
            var sample = progress.Samples[progress.SampleIndex];
            var remaining = aircraft.Position.Distance(sample.Position);
            var turn = Math.Abs(RouteMovement.AngleDifference(aircraft.Heading, sample.Heading));
            if (remaining > 0.000001 && (distance < remaining || turn > turnAllowance))
            {
                var moved = Math.Min(distance, turn > 0.000001 ? remaining * turnAllowance / turn : remaining);
                aircraft.Position = aircraft.Position.Towards(sample.Position, moved);
                aircraft.Heading = Coordinates.NormalizeHeading(aircraft.Heading +
                    RouteMovement.AngleDifference(aircraft.Heading, sample.Heading) * moved / remaining);
                distance -= moved;
                break;
            }
            aircraft.Position = sample.Position;
            aircraft.Heading = sample.Heading;
            distance -= remaining;
            turnAllowance = Math.Max(0, turnAllowance - turn);
            progress.SampleIndex++;
        }
        aircraft.SpeedKnots = (requestedDistance - distance) / dt / MetresPerKnotSecond;
        if (progress.SampleIndex == progress.Samples.Count)
        {
            aircraft.SpeedKnots = 0;
            aircraft.Heading = progress.FinalHeading;
            progress.Completed = true;
            aircraft.Phase = FlightPhase.ReadyForTaxi;
            Transmit(aircraft, "Ready for taxi.", "departure.ready");
        }
        return true;
    }
}
