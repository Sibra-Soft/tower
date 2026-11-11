namespace Tower.Core;

public sealed partial class Simulation
{
    private bool GateAvailable(MapNode gate, AircraftSpecification specification, Aircraft? except = null, bool includeReservations = true)
    {
        var heading = State.Airport.PushbackGates.TryGetValue(gate.Id, out var definition)
            ? State.Airport.Point(definition.ConnectionNode).Bearing(gate.Position) : except?.Heading ?? 0;
        var footprint = specification.GroundFootprint is { } model && model.IsValid() ? model
            : new AircraftFootprint(-specification.WingSpanMetres, specification.WingSpanMetres,
                -specification.WingSpanMetres / 2, specification.WingSpanMetres / 2);
        var own = footprint.Project(heading);
        return State.Aircraft.All(other =>
        {
            if (other == except || other.Phase == FlightPhase.Completed)
            {
                return true;
            }
            if (includeReservations && (other.Direction == FlightDirection.Arrival || other.Phase == FlightPhase.TaxiIn) &&
                other.Gate.Equals(gate.Id, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (other.IsAirborne)
            {
                return true;
            }
            var dx = other.Position.X - gate.Position.X;
            var dy = other.Position.Y - gate.Position.Y;
            var bounds = Footprint(other).Project(other.Heading);
            return other.Position.Distance(gate.Position) >= 40 &&
                (dx + bounds.MaxRight + 5 <= own.MinRight || dx + bounds.MinRight - 5 >= own.MaxRight ||
                 dy + bounds.MaxForward + 5 <= own.MinForward || dy + bounds.MinForward - 5 >= own.MaxForward);
        });
    }

    private Aircraft? GroundObstruction(Aircraft aircraft)
    {
        var traffic = GroundTrafficAhead(aircraft);
        return traffic.Distance <= 0.5 ? traffic.Aircraft : null;
    }

    private static AircraftFootprint Footprint(Aircraft aircraft)
    {
        if (aircraft.Specification.GroundFootprint is { } footprint && footprint.IsValid())
        {
            return footprint;
        }
        // Legacy saves and missing models have no measured length/origin. Use a
        // conservative span-based envelope rather than the former fixed 55 metres.
        var span = Math.Max(1, aircraft.Specification.WingSpanMetres);
        return new(-span, span, -span / 2, span / 2);
    }

    private (Aircraft? Aircraft, double Distance) GroundTrafficAhead(Aircraft aircraft)
    {
        var pushing = aircraft.Phase == FlightPhase.Pushback && aircraft.Pushback is { Completed: false };
        if (!pushing && !(aircraft.Phase is FlightPhase.TaxiIn or FlightPhase.TaxiOut && aircraft.PathIndex < aircraft.Path.Count))
        {
            return (null, double.PositiveInfinity);
        }
        var others = State.Aircraft.Where(other => other != aircraft && !other.IsAirborne && other.Phase != FlightPhase.Completed).ToList();
        if (others.Count == 0)
        {
            return (null, double.PositiveInfinity);
        }
        var path = pushing
            ? aircraft.Pushback!.Samples.Skip(aircraft.Pushback.SampleIndex).Select(sample => sample.Position)
            : RouteMovement.RemainingPath(aircraft);
        var footprint = Footprint(aircraft);
        var speed = aircraft.SpeedKnots * MetresPerKnotSecond;
        var lookAhead = Math.Max(120, speed * speed + Math.Max(Math.Abs(footprint.MinForward), Math.Abs(footprint.MaxForward)) * 2 +
            others.Select(other => Math.Max(Math.Abs(Footprint(other).MinForward), Math.Abs(Footprint(other).MaxForward))).DefaultIfEmpty(0).Max() * 2);
        Aircraft? nearest = null;
        var stopDistance = double.PositiveInfinity;
        var previous = aircraft.Position;
        var travelled = 0d;
        foreach (var point in path)
        {
            var length = previous.Distance(point);
            if (length < 0.000001)
            {
                continue;
            }
            var dx = (point.X - previous.X) / length;
            var dy = (point.Y - previous.Y) / length;
            var direction = previous.Bearing(point);
            var own = footprint.Project(pushing ? 180 : 0);
            foreach (var other in others)
            {
                var x = other.Position.X - previous.X;
                var y = other.Position.Y - previous.Y;
                var along = x * dx + y * dy;
                var across = x * dy - y * dx;
                var bounds = Footprint(other).Project(other.Heading - direction);
                if (along <= 0 || across + bounds.MinRight > own.MaxRight + 1 || across + bounds.MaxRight < own.MinRight - 1)
                {
                    continue;
                }
                var entry = along + bounds.MinForward - own.MaxForward - 5;
                if (entry > length || along + bounds.MaxForward < own.MinForward)
                {
                    continue;
                }
                var available = travelled + Math.Max(0, entry);
                if (available < stopDistance)
                {
                    nearest = other;
                    stopDistance = available;
                }
            }
            travelled += length;
            previous = point;
            if (travelled >= lookAhead || travelled >= stopDistance)
            {
                break;
            }
        }
        return (nearest, stopDistance);
    }

    private bool WaitingInDepartureQueue(Aircraft aircraft, Aircraft other)
    {
        if (!(aircraft.Phase == FlightPhase.TaxiOut && aircraft.Direction == FlightDirection.Departure &&
            other.Direction == FlightDirection.Departure &&
            other.Phase is FlightPhase.TaxiOut or FlightPhase.HoldingShort or FlightPhase.LineUp or FlightPhase.LinedUp or FlightPhase.TakeoffRoll &&
            State.Airport.GetRunway(aircraft.Runway).PhysicalId == State.Airport.GetRunway(other.Runway).PhysicalId))
        {
            return false;
        }
        if (Math.Abs(RouteMovement.AngleDifference(aircraft.Heading, other.Heading)) < 60)
        {
            return true;
        }
        // A leader already beyond the bend still belongs to the same queue.
        var previous = aircraft.Position;
        foreach (var point in RouteMovement.RemainingPath(aircraft))
        {
            var dx = point.X - previous.X;
            var dy = point.Y - previous.Y;
            var squared = dx * dx + dy * dy;
            if (squared > 0.000001)
            {
                var fraction = Math.Clamp(((other.Position.X - previous.X) * dx + (other.Position.Y - previous.Y) * dy) / squared, 0, 1);
                var closest = new Position(previous.X + fraction * dx, previous.Y + fraction * dy);
                if (closest.Distance(other.Position) < (aircraft.Specification.WingSpanMetres + other.Specification.WingSpanMetres) / 2 + 5 &&
                    Math.Abs(RouteMovement.AngleDifference(previous.Bearing(point), other.Heading)) < 60)
                {
                    return true;
                }
            }
            previous = point;
        }
        return false;
    }
}
