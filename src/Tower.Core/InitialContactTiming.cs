namespace Tower.Core;

public sealed partial class Simulation
{
    private void InitializeTimedContact(Aircraft aircraft, OriginalFlightTiming timing)
    {
        var contact = new OriginalInitialContact
        {
            CreatedSeconds = State.ElapsedSeconds,
            EntrySeconds = timing.EntrySeconds,
            InitialSpeedKnots = timing.InitialSpeedKnots
        };
        aircraft.InitialContact = contact;
        if (aircraft.Direction == FlightDirection.Departure)
        {
            return;
        }
        var route = aircraft.OriginalRoute!;
        var points = aircraft.OriginalPlan!.ResolvedRoute!.ToList();
        points.Add(new("CONTACT " + aircraft.Runway, timing.ArrivalContactPoint!.Value));
        var remainingSeconds = Math.Max(0, timing.EntrySeconds - State.ElapsedSeconds);
        var prepared = PrepareTimedRoute(points, remainingSeconds, timing.InitialSpeedKnots);
        contact.EntryPointIndex = prepared.Count - 1;
        prepared.Add(route.Points[^1]);
        route.Points = prepared;
        route.NextPointIndex = 1;
        route.TargetAltitudeFeetAgl = aircraft.AltitudeFeet;
        aircraft.Position = prepared[0].Position;
        aircraft.Heading = aircraft.Position.Bearing(prepared[1].Position);
        aircraft.SpeedKnots = timing.InitialSpeedKnots;
        if (remainingSeconds <= 0)
        {
            MarkInitialEntry(aircraft);
        }
    }

    private List<OriginalRoutePoint> PrepareTimedRoute(List<OriginalRoutePoint> points, double seconds, double airspeed)
    {
        if (seconds <= 0)
        {
            return [points[^1]];
        }
        double GroundSpeed(double course)
        {
            var wind = WindMotion.Track(airspeed, course, State.Options);
            return (wind.CanHoldTrack ? wind.GroundSpeedKnots : airspeed) * MetresPerKnotSecond;
        }
        var remaining = seconds;
        for (int index = points.Count - 1; index > 0; index--)
        {
            var length = points[index].Position.Distance(points[index - 1].Position);
            var speed = GroundSpeed(points[index - 1].Position.Bearing(points[index].Position));
            if (remaining * speed < length)
            {
                var start = points[index].Position.Towards(points[index - 1].Position, remaining * speed);
                return [new("PREENTRY", start), .. points.Skip(index)];
            }
            remaining -= length / speed;
        }
        var first = points[0].Position;
        var next = points.FirstOrDefault(point => point.Position.Distance(first) > 0.01);
        var bearing = next is null ? 0 : next.Position.Bearing(first);
        return [new("PREENTRY", first.Offset(bearing, remaining * GroundSpeed(bearing + 180))), .. points];
    }

    private bool UpdatePreparedArrival(Aircraft aircraft, OriginalRouteProgress route, double dt)
    {
        var contact = aircraft.InitialContact!;
        aircraft.SpeedKnots = contact.InitialSpeedKnots;
        var remainingTime = dt;
        while (route.NextPointIndex <= contact.EntryPointIndex)
        {
            var point = route.Points[route.NextPointIndex].Position;
            var distance = aircraft.Position.Distance(point);
            if (distance > 0.01)
            {
                var course = aircraft.Position.Bearing(point);
                var movement = AirTrackDistance(aircraft, course, remainingTime);
                if (movement + 1e-7 < distance)
                {
                    aircraft.Position = aircraft.Position.Towards(point, movement);
                    return true;
                }
                remainingTime = Math.Max(0, remainingTime - distance / (movement / remainingTime));
            }
            aircraft.Position = point;
            if (route.NextPointIndex == contact.EntryPointIndex)
            {
                if (State.ElapsedSeconds + 1e-7 < contact.EntrySeconds)
                {
                    return true;
                }
                MarkInitialEntry(aircraft);
                route.NextPointIndex++;
                return true;
            }
            route.NextPointIndex++;
        }
        return true;
    }

    private void MarkInitialEntry(Aircraft aircraft)
    {
        var contact = aircraft.InitialContact!;
        if (contact.EntryReached)
        {
            return;
        }
        contact.EntryReached = true;
        contact.RadioDueSeconds = Math.Max(contact.EntrySeconds, State.ElapsedSeconds) + OriginalTimingUnits.RadioDelaySeconds;
        if (aircraft.OriginalRoute is { } route)
        {
            route.TargetAltitudeFeetAgl = State.Airport.Original.PatternAltitudeAgl(aircraft.Type) ?? route.TargetAltitudeFeetAgl;
        }
    }

    private void UpdateInitialContact(Aircraft aircraft)
    {
        if (aircraft.InitialContact is not { Announced: false } contact)
        {
            return;
        }
        if (aircraft.Direction == FlightDirection.Departure && State.ElapsedSeconds + 1e-7 >= contact.EntrySeconds)
        {
            MarkInitialEntry(aircraft);
        }
        if (contact.RadioDueSeconds is not { } due || State.ElapsedSeconds + 1e-7 < due)
        {
            return;
        }
        contact.Announced = true;
        contact.AnnouncedSeconds = State.ElapsedSeconds;
        TransmitInitialContact(aircraft);
        AircraftAdded?.Invoke(aircraft);
    }

    private void TransmitInitialContact(Aircraft aircraft)
    {
        if (aircraft.Pushback is { Completed: false })
        {
            Transmit(aircraft, "Request pushback.", "departure.pushback.request");
            return;
        }
        var distance = aircraft.Position.Distance(State.Airport.Observer) / 1852;
        var miles = OriginalRadioDistance.SpokenMiles(distance);
        var direction = OriginalRadioDirection.FromBearing(State.Airport.Observer.Bearing(aircraft.Position));
        Transmit(aircraft, aircraft.Direction == FlightDirection.Arrival
            ? FormattableString.Invariant($"with you {miles} {(miles == 1 ? "mile" : "miles")} {direction}") : "ready for taxi",
            aircraft.Direction == FlightDirection.Arrival ? "arrival.initial" : "departure.ready",
            aircraft.Direction == FlightDirection.Arrival ? distance : null,
            detail: aircraft.Direction == FlightDirection.Arrival ? direction : "");
    }
}
