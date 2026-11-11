using System.Globalization;
namespace Tower.Core;

// Single-writer engine. UI, clock and audio are deliberately absent from this assembly.
public sealed partial class Simulation
{
    public const double MetresPerKnotSecond = 1852.0 / 3600;
    public Session State
    {
        get;
    }
    public bool Paused
    {
        get; set;
    }
    public event Action<string>? Message;
    public event Action<string, string>? RadioTransmission;
    public event Action<AircraftRadioMessage>? RadioMessage;
    public event Action<Aircraft>? AircraftAdded;
    private readonly HashSet<string> activeConflicts = [];
    private readonly HashSet<string>? allowedCallsigns;
    public bool DeleteAircraft(Aircraft aircraft)
    {
        if (aircraft.Phase == FlightPhase.Completed || !State.Aircraft.Remove(aircraft))
        {
            return false;
        }

        aircraft.Phase = FlightPhase.Completed;
        State.RunwayDepartures.RemoveAll(departure => departure.Id == aircraft.RunwayDepartureId);
        activeConflicts.RemoveWhere(key => key.Split('/').Contains(aircraft.CallSign, StringComparer.Ordinal));
        State.Performance.ActiveRunwayPairs.RemoveWhere(key => key.Split('/').Contains(aircraft.CallSign, StringComparer.Ordinal));
        State.Performance.ReportedCollisions.RemoveWhere(key => key.Split('/').Contains(aircraft.CallSign, StringComparer.Ordinal));
        Message?.Invoke($"{aircraft.CallSign}: deleted from simulation.");
        return true;
    }

    public Simulation(Session state, IReadOnlyCollection<string>? allowedCallsigns = null)
    {
        state.Airport.Validate();
        this.allowedCallsigns = allowedCallsigns is null ? null : new HashSet<string>(
            allowedCallsigns.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim().ToUpperInvariant()),
            StringComparer.OrdinalIgnoreCase);
        if (this.allowedCallsigns is { Count: 0 })
        {
            throw new InvalidDataException("Original BAO speech requires approved full callsign recordings. Approve callsigns in the Workbench or select Windows speech in Speech Setup.");
        }
        if (this.allowedCallsigns is not null)
        {
            var unsupported = state.Aircraft.Where(a => a.Phase != FlightPhase.Completed).Select(a => a.CallSign)
                .Concat(state.Schedule.Where(f => !f.Spawned).Select(f => f.CallSign))
                .Where(callsign => !this.allowedCallsigns.Contains(callsign)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (unsupported.Length > 0)
            {
                throw new InvalidDataException("This scenario contains callsigns without approved BAO recordings: "
                    + string.Join(", ", unsupported) + ". Select Windows speech or use a scenario with recorded callsigns.");
            }
        }
        State = state;
        activeConflicts = state.Performance.ActiveSeparationPairs;
        RestoreFreeTrafficFlights();
    }
    public Aircraft AddAircraft(FlightDirection direction, string? callSign = null, string? type = null, string? assignedRunway = null, string? assignedGate = null, OriginalFlightPlan? originalPlan = null,
        OriginalFlightTiming? timing = null, bool useTrafficFlow = true, FreeTrafficFlight? freeTrafficFlight = null)
    {
        if (timing is not null && (!timing.IsValid(direction) || direction == FlightDirection.Arrival &&
            (originalPlan?.ResolvedRoute is not { Count: > 0 } || originalPlan.AltitudeReference != OriginalAltitudeReference.Msl ||
             State.Airport.Original.SourceMslToAgl(originalPlan.Altitude1Feet) is null)))
        {
            throw new InvalidDataException("Invalid original flight timing.");
        }
        var arrivals = State.Options.ArrivalRunways;
        var runwayName = direction == FlightDirection.Arrival && arrivals.Count > 0
            ? arrivals[State.Aircraft.Count(a => a.Direction == FlightDirection.Arrival) % arrivals.Count] : State.Options.Runway;
        var runway = assignedRunway is not null ? State.Airport.GetRunway(assignedRunway)
            : State.Airport.GeneratedRunways(direction, [runwayName]).FirstOrDefault()
                ?? throw new InvalidOperationException("No runway is available for generated traffic in this direction.");
        var threshold = State.Airport.Point(runway.Threshold);
        var heading = threshold.Bearing(State.Airport.Point(runway.End));
        if (allowedCallsigns is not null && callSign is null)
        {
            bool Available(string value) => allowedCallsigns.Contains(value)
                && State.Aircraft.All(a => a.Phase == FlightPhase.Completed || !a.CallSign.Equals(value, StringComparison.OrdinalIgnoreCase))
                && State.Schedule.All(f => f.Spawned || !f.CallSign.Equals(value, StringComparison.OrdinalIgnoreCase));
            var entry = State.Airport.TrafficPool.FirstOrDefault(p => Available(p.CallSign));
            callSign = entry?.CallSign ?? allowedCallsigns.Order(StringComparer.Ordinal).FirstOrDefault(Available)
                ?? throw new InvalidOperationException("All recorded BAO callsigns are in use or reserved for scheduled flights.");
            type ??= entry?.Type;
            State.NextFlight++;
        }
        else if (callSign is null && State.Airport.TrafficPool.Count > 0)
        {
            var entry = State.Airport.TrafficPool.FirstOrDefault(p => State.Aircraft.All(a => a.CallSign != p.CallSign));
            if (entry is not null)
            {
                callSign = entry.CallSign;
                type = entry.Type;
                State.NextFlight++;
            }
        }
        callSign ??= $"TWR{State.NextFlight++}";
        callSign = callSign.Trim().ToUpperInvariant();
        if (allowedCallsigns is not null && !allowedCallsigns.Contains(callSign))
        {
            throw new InvalidOperationException("No approved BAO callsign recording for " + callSign + ".");
        }
        if (callSign.Length == 0 || State.Aircraft.Any(a => a.CallSign.Equals(callSign, StringComparison.OrdinalIgnoreCase)
            && (allowedCallsigns is null || a.Phase != FlightPhase.Completed)))
        {
            throw new InvalidOperationException("Callsign must be unique and non-empty.");
        }

        type ??= State.Airport.AircraftTypes.Keys.FirstOrDefault() ?? "B738";
        var groundSpecification = State.Airport.AircraftTypes.GetValueOrDefault(type) ?? new AircraftSpecification();
        var gates = State.Airport.Nodes.Values.Where(n => n.Kind.Equals("GATE", StringComparison.OrdinalIgnoreCase)).ToList();
        var gate = direction == FlightDirection.Departure
            ? gates.FirstOrDefault(g => GateAvailable(g, groundSpecification))
            : gates[State.NextFlight % gates.Count];
        if (assignedGate is not null)
        {
            gate = gates.FirstOrDefault(g => g.Id == assignedGate);
            if (gate is not null && direction == FlightDirection.Departure && !GateAvailable(gate, groundSpecification, includeReservations: false))
            {
                throw new InvalidOperationException("Assigned gate occupied.");
            }
        }
        if (gate is null)
        {
            throw new InvalidOperationException("All gates are occupied.");
        }

        var ac = new Aircraft
        {
            CallSign = callSign,
            Runway = runway.Name,
            Direction = direction,
            Gate = gate.Id,
            Heading = heading,
            HoldShortReached = false,
            OriginalPlan = originalPlan,
            FreeTraffic = freeTrafficFlight,
            FollowOriginalDepartureRoute = direction == FlightDirection.Departure && originalPlan?.DestinationBearingDegrees is not null
        };
        var originalAltitudeAgl = originalPlan?.AltitudeReference == OriginalAltitudeReference.Msl
            ? State.Airport.Original.SourceMslToAgl(originalPlan.Altitude1Feet) : null;
        ac.FollowOriginalDepartureAltitude = direction == FlightDirection.Departure && originalAltitudeAgl is > 0 &&
            originalPlan!.HasDepartureClimbTarget();
        ac.Type = type;
        if (State.Airport.AircraftTypes.TryGetValue(type, out var specification))
        {
            ac.Specification = specification;
        }

        ac.TargetSpeedKnots = ac.Specification.ApproachSpeed;
        if (originalPlan is null && useTrafficFlow)
        {
            ac.GeneratedRoute = SelectTrafficFlow(direction, runway);
            ac.FollowOriginalDepartureRoute = direction == FlightDirection.Departure && ac.GeneratedRoute is not null;
        }
        if (direction == FlightDirection.Arrival)
        {
            var occupiedApproach = State.Aircraft.Where(a => a.Phase is FlightPhase.Approach or FlightPhase.Circuit &&
                a.Direction == FlightDirection.Arrival && a.Runway == runway.Name).Select(a => a.Position.Distance(threshold)).DefaultIfEmpty(0).Max();
            var distance = Math.Max(8 * 1852, occupiedApproach + 4 * 1852);
            ac.Position = threshold.Offset(heading + 180, distance);
            ac.AltitudeFeet = distance * Math.Tan(3 * Math.PI / 180) / 0.3048;
            ac.SpeedKnots = ac.TargetSpeedKnots;
            ac.Phase = FlightPhase.Approach;
            ac.Path = [threshold];
            if ((originalPlan?.OriginBearingDegrees ?? ac.GeneratedRoute?.BearingDegrees) is { } originBearing)
            {
                // SEC supplies the origin direction. Entry range and final join remain
                // explicit modern choices until the original trajectory is recovered.
                ac.Position = threshold.Offset(originBearing, distance);
                BeginCircuit(ac, "final", false, false, runway.Name);
                ac.Heading = ac.Position.Bearing(ac.Path[0]);
            }
            if (originalAltitudeAgl is { } initialAltitude)
            {
                ac.AltitudeFeet = initialAltitude;
            }
        }
        else
        {
            ac.Position = gate.Position;
            ac.Phase = FlightPhase.AtGate;
        }
        if (allowedCallsigns is not null)
        {
            State.Aircraft.RemoveAll(a => a.Phase == FlightPhase.Completed && a.CallSign.Equals(callSign, StringComparison.OrdinalIgnoreCase));
        }
        if (originalPlan?.Actions is { } actions)
        {
            ac.OriginalActions = new() { Actions = actions.ToList() };
        }
        if (originalPlan is null && useTrafficFlow)
        {
            InitializeGeneratedPerformance(ac);
        }
        if (freeTrafficFlight is not null)
        {
            ConfigureFreeTrafficFlight(ac);
        }
        InitializeOriginalRoute(ac);
        InitializeGeneratedDeparture(ac);
        InitializeGeneratedArrival(ac);
        InitializePushback(ac);
        if (timing is not null)
        {
            InitializeTimedContact(ac, timing);
        }
        State.Aircraft.Add(ac);
        if (ac.IsOnFrequency)
        {
            TransmitInitialContact(ac);
            AircraftAdded?.Invoke(ac);
        }

        return ac;
    }
    private CommandResult Apply(Aircraft ac, string command)
    {
        if (command == "pushback" || command.StartsWith("pushback ", StringComparison.Ordinal))
        {
            return BeginPushback(ac, command.Length > 8 ? command[9..] : "");
        }
        if (ac.Phase == FlightPhase.Pushback)
        {
            if (command is "hold" or "hold position")
            {
                ac.Holding = true;
                return new(true, "Holding position.");
            }
            if (command is "continue" or "continue taxi")
            {
                if (!ac.Holding)
                {
                    return new(false, "Aircraft is not holding.");
                }
                ac.Holding = false;
                return new(true, "Continuing pushback.");
            }
            if (!command.StartsWith("say ", StringComparison.Ordinal))
            {
                return new(false, "Complete pushback before issuing taxi or runway commands.");
            }
        }
        if (ac.Phase == FlightPhase.AtGate && ac.Pushback is { Completed: false } &&
            command.StartsWith("taxi ", StringComparison.Ordinal))
        {
            return new(false, "Pushback is required before taxi. Use PB [N|E|S|W].");
        }
        var extra = ApplyOriginal(ac, command);
        if (extra is not null)
        {
            return extra;
        }

        var airport = State.Airport;
        var runway = airport.GetRunway(ac.Runway);
        bool Is(params FlightPhase[] phases) => phases.Contains(ac.Phase);
        CommandResult Ok(string text) => new(true, text);
        CommandResult No(string text) => new(false, text);
        if (command is "cleared to land" or "cleared for landing" or "land")
        {
            if (!Is(FlightPhase.Approach, FlightPhase.Circuit))
            {
                return No("Not on approach or in the circuit.");
            }

            // A controller may issue an unsafe landing clearance. Evaluate the
            // resulting runway movements/conflicts instead of preventing the error.
            var landingIntent = PlannedLandingIntent(ac);
            if (landingIntent == LandingIntent.FullStop && RunwayPerformance.Metres(ac.Specification.LandingDistanceFeet) is { } requiredLanding &&
                RunwayPerformance.LandingExit(airport, runway, requiredLanding) is null)
            {
                return No("No runway exit beyond the required landing distance.");
            }
            if (landingIntent == LandingIntent.TouchAndGo && RunwayPerformance.Metres(ac.Specification.TakeoffDistanceFeet) is { } takeoffDistance &&
                takeoffDistance > airport.Point(runway.Threshold).Distance(airport.Point(runway.End)))
            {
                return No("Insufficient runway for touch and go.");
            }

            ac.LandingIntent = PlannedLandingIntent(ac);
            ac.LandingCleared = true;

            return Ok($"Cleared to land runway {ac.Runway}.");
        }
        if (command is "go around" or "ga")
        {
            if (!ac.IsAirborne || Is(FlightPhase.Departing))
            {
                return No("Go-around requires arriving airborne traffic.");
            }

            GoAround(ac, "instructed");
            return Ok("Going around.");
        }
        if (command is "hold position" or "hold")
        {
            if (!Is(FlightPhase.TaxiIn, FlightPhase.TaxiOut, FlightPhase.LineUp))
            {
                return No("Hold position requires a taxiing or lining-up aircraft.");
            }

            ac.Holding = true;
            return Ok("Holding position.");
        }
        if (command is "continue taxi" or "continue")
        {
            if (!Is(FlightPhase.TaxiIn, FlightPhase.TaxiOut, FlightPhase.LineUp))
            {
                return No("TU only resumes a stopped taxi route; use TH or CT to enter the departure runway.");
            }
            if (!ac.Holding && GroundObstruction(ac) is null)
            {
                return No("Aircraft is not holding.");
            }

            ac.Holding = false;
            if (ac.HoldShortReached != false)
            {
                ac.HoldShortRunway = null;
            }
            ac.HoldShortReached = false;
            return Ok("Continuing taxi.");
        }
        if (command.StartsWith("taxi to runway ", StringComparison.Ordinal))
        {
            if (!Is(FlightPhase.AtGate, FlightPhase.ReadyForTaxi, FlightPhase.TaxiOut, FlightPhase.TaxiIn, FlightPhase.HoldingShort))
            {
                return No("Aircraft must be at a gate, taxiing or holding short.");
            }

            var requested = airport.GetRunway(command[15..].ToUpperInvariant());
            var path = TaxiPathFromCurrent(ac, requested.Hold);
            ac.HoldShortRunway = null;
            ac.HoldShortReached = false;
            ac.Runway = requested.Name;
            SetPath(ac, path);
            ac.Phase = FlightPhase.TaxiOut;

            return Ok($"Taxi to runway {ac.Runway}");
        }
        if (command == "taxi to parking" || command.StartsWith("taxi to gate ", StringComparison.Ordinal))
        {
            if (!Is(FlightPhase.ArrivalHold, FlightPhase.TaxiIn, FlightPhase.TaxiOut, FlightPhase.HoldingShort))
            {
                return No("Aircraft must be off the runway.");
            }

            var gateId = command == "taxi to parking" ? ac.Gate : command[13..].ToUpperInvariant();
            if (!airport.Nodes.TryGetValue(gateId, out var gate) || !gate.Kind.Equals("GATE", StringComparison.OrdinalIgnoreCase))
            {
                return No("Unknown gate.");
            }

            List<Position>? path = null;
            if (GateAvailable(gate, ac.Specification, ac))
            {
                try
                {
                    path = TaxiPathFromCurrent(ac, gateId);
                }
                catch (InvalidDataException)
                {
                    // A generic parking request may select another connected stand.
                }
            }
            if (path is null && command != "taxi to parking")
            {
                return No("Gate occupied, reserved or unreachable.");
            }
            if (path is null)
            {
                var bestLength = double.PositiveInfinity;
                foreach (var alternative in airport.Nodes.Values.Where(node => node.Kind == "GATE" && GateAvailable(node, ac.Specification, ac)))
                {
                    List<Position> candidate;
                    try
                    {
                        candidate = TaxiPathFromCurrent(ac, alternative.Id);
                    }
                    catch (InvalidDataException)
                    {
                        continue;
                    }
                    var length = candidate.Zip(candidate.Skip(1), (start, end) => start.Distance(end)).Sum();
                    if (length >= bestLength)
                    {
                        continue;
                    }
                    bestLength = length;
                    gateId = alternative.Id;
                    path = candidate;
                }
            }
            if (path is null)
            {
                return No("No available gate has a connected taxi route.");
            }
            ac.HoldShortRunway = null;
            ac.HoldShortReached = false;
            ac.Gate = gateId;
            SetPath(ac, path);
            ac.Phase = FlightPhase.TaxiIn;
            return Ok($"Taxi to gate {gateId}.");
        }
        if (command is "line up and wait" or "line up")
        {
            if (!Is(FlightPhase.HoldingShort))
            {
                return No("Aircraft must be holding short.");
            }

            // The controller can clear an occupied runway; assess conflicts during movement.
            SetPath(ac, [ac.Position, airport.Point(runway.Threshold)], airport.Point(runway.Threshold).Bearing(airport.Point(runway.End)));
            ac.HoldShortRunway = null;
            ac.HoldShortReached = false;
            ac.Phase = FlightPhase.LineUp;
            return Ok("Line up and wait.");
        }
        if (command is "cleared for takeoff" or "takeoff")
        {
            if (!Is(FlightPhase.LinedUp, FlightPhase.LineUp, FlightPhase.HoldingShort, FlightPhase.AbortedTakeoff))
            {
                return No("Aircraft must be holding short, lining up or lined up.");
            }

            if (Is(FlightPhase.AbortedTakeoff) && ac.SpeedKnots > 0.1)
            {
                return No("Wait until the aircraft has stopped.");
            }

            if (ac.RunwayOverrunReported)
            {
                return No("Aircraft has overrun the runway and cannot resume takeoff.");
            }
            Position? liftoff = null;
            var needsThresholdAccess = Is(FlightPhase.HoldingShort, FlightPhase.LineUp) ||
                Is(FlightPhase.AbortedTakeoff) && ac.Path.Count == 3 && ac.PathIndex == 1;
            if (RunwayPerformance.Metres(ac.Specification.TakeoffDistanceFeet) is { } takeoffDistance)
            {
                var runwayHeading = airport.Point(runway.Threshold).Bearing(airport.Point(runway.End));
                takeoffDistance = WindMotion.TakeoffDistance(ac, takeoffDistance, runwayHeading, State.Options);
                var start = needsThresholdAccess ? airport.Point(runway.Threshold) : ac.Position;
                var available = start.Distance(airport.Point(runway.End));
                if (takeoffDistance > available)
                {
                    return No("Insufficient runway for the required takeoff distance.");
                }
                liftoff = start.Towards(airport.Point(runway.End), takeoffDistance);
                var initialSpeed = needsThresholdAccess ? 12 : ac.SpeedKnots;
                ac.RunwayAccelerationMetresPerSecondSquared = RunwayPerformance.Acceleration(initialSpeed,
                    WindMotion.TakeoffGroundSpeed(ac, runwayHeading, State.Options), takeoffDistance);
            }
            // Hold-short clearance first follows the access path at taxi speed; acceleration starts at threshold.
            if (needsThresholdAccess)
            {
                SetPath(ac, [ac.Position, airport.Point(runway.Threshold), liftoff ?? airport.Point(runway.End)]);
            }
            else if (!Is(FlightPhase.AbortedTakeoff))
            {
                SetPath(ac, [ac.Position, liftoff ?? airport.Point(runway.End)]);
            }
            else if (liftoff is { } resumedLiftoff)
            {
                SetPath(ac, [ac.Position, resumedLiftoff]);
            }

            ac.Phase = FlightPhase.TakeoffRoll;
            ac.RunwayDepartureId = null;
            ac.HoldShortRunway = null;
            ac.HoldShortReached = false;
            return Ok($"Cleared for takeoff runway {ac.Runway}.");
        }
        if (command.StartsWith("speed ", StringComparison.Ordinal))
        {
            if (!Is(FlightPhase.Approach, FlightPhase.Circuit, FlightPhase.Vectoring, FlightPhase.GoAround) ||
                !double.TryParse(command[6..], NumberStyles.Float, CultureInfo.InvariantCulture, out var speed) || !double.IsFinite(speed) || speed < ac.Specification.ApproachSpeed * 0.8 || speed > ac.Specification.ApproachSpeed * 1.3)
            {
                return No($"Approach speed must be {ac.Specification.ApproachSpeed * 0.8:0}–{ac.Specification.ApproachSpeed * 1.3:0} knots for {ac.Type}.");
            }

            ac.TargetSpeedKnots = speed;
            ac.ApproachSpeedOverrideKnots = speed;
            if (ac.GeneratedArrival is { Stage: not GeneratedArrivalStage.Cancelled } arrival)
            {
                arrival.SpeedOverrideKnots = speed;
            }
            return Ok($"Speed {speed:0} knots.");
        }
        if (command is "contact departure" or "handoff")
        {
            if (!Is(FlightPhase.Departing, FlightPhase.Breakout))
            {
                return No("Aircraft must be departing or broken out of traffic.");
            }

            CompleteDeparture(ac);
            return Ok("Contacting departure. Goodbye.");
        }
        return No("Unknown command. Use Help > Commands.");
    }
    private static void SetPath(Aircraft ac, List<Position> path, double? exitHeading = null)
    {
        ac.Movement = null;
        ac.PathExitHeading = exitHeading;
        ac.Path = path;
        ac.PathIndex = path.Count > 0 && ac.Position.Distance(path[0]) < 1 ? 1 : 0;
        ac.Holding = false;
    }
    private List<Position> TaxiPathFromCurrent(Aircraft ac, string destination)
    {
        var airport = State.Airport;
        // Finish the current directed edge before choosing a new route. Never snap to a gate.
        var join = ac.PathIndex < ac.Path.Count ? ac.Path[ac.PathIndex] : ac.Position;
        var node = airport.Nodes.Values.FirstOrDefault(n => n.Position.Distance(join) < 0.01)
            ?? throw new InvalidDataException("Current taxi route does not join the airport graph.");
        var path = Routing.FindTaxiPath(airport, node.Id, destination);
        if (ac.Position.Distance(join) > 0.01)
        {
            path.Insert(0, ac.Position);
        }

        return path;
    }
    public void Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0 || seconds > 3600)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }

        if (Paused)
        {
            return;
        }
        // Fixed maximum substep keeps acceleration, separation and runway events stable under UI delays.
        while (seconds > 0.0000001)
        {
            var step = Math.Min(0.05, seconds);
            Tick(step);
            seconds -= step;
        }
    }
    private void Tick(double dt)
    {
        State.ElapsedSeconds += dt;
        foreach (var flight in State.Schedule.Where(f => !f.Spawned && (f.Timing?.SpawnSeconds ?? f.TimeSeconds) <= State.ElapsedSeconds).ToArray())
        {
            try
            {
                AddAircraft(flight.Direction, flight.CallSign, flight.Type, flight.Runway, flight.Gate, flight.OriginalPlan, flight.Timing, useTrafficFlow: false);
                flight.Spawned = true;
            }
            catch (InvalidOperationException) { /* Retry an occupied gate at the next simulation step. */ }
        }
        if (State.FreeTraffic is not null)
        {
            UpdateFreeTraffic();
        }
        else if (State.Options.AircraftPerMinute > 0)
        {
            State.SpawnAccumulator += dt;
            var interval = 60 / State.Options.AircraftPerMinute;
            if (State.SpawnAccumulator >= interval)
            {
                State.SpawnAccumulator -= interval;
                if (State.Aircraft.Count(a => a.Phase != FlightPhase.Completed) < 30)
                {
                    try
                    {
                        AddAircraft(State.NextFlight % 2 == 0 ? FlightDirection.Arrival : FlightDirection.Departure);
                    }
                    catch (InvalidOperationException) { }
                }
            }
        }
        foreach (var ac in State.Aircraft.Where(a => a.Phase != FlightPhase.Completed).ToArray())
        {
            var altitude = ac.AltitudeFeet;
            var previousHeading = ac.Heading;
            var previousPosition = ac.Position;
            Update(ac, dt);
            ac.GroundSpeedKnots = previousPosition.Distance(ac.Position) / dt / MetresPerKnotSecond;
            if (!ac.IsAirborne)
            {
                ac.GroundTrackDegrees = ac.Heading;
            }
            FlightDynamics.UpdateAttitude(ac, previousHeading, altitude, dt, State.Options);
            UpdateInitialContact(ac);
            UpdateGeneratedArrivalContact(ac);
            UpdateRunwayDeparture(ac);
            ac.VerticalSpeedFpm = (ac.AltitudeFeet - altitude) / dt * 60;
            AircraftAnimation.Update(ac, dt);
        }
        DetectConflicts();
    }
    private void Update(Aircraft ac, double dt)
    {
        if (ac.InitialContact is { Announced: false } contact && State.ElapsedSeconds <= contact.CreatedSeconds + 1e-7)
        {
            return;
        }
        if (UpdatePushback(ac, dt))
        {
            return;
        }
        var airport = State.Airport;
        var rw = airport.GetRunway(ac.Runway);
        var threshold = airport.Point(rw.Threshold);
        var end = airport.Point(rw.End);
        var heading = threshold.Bearing(end);
        if (UpdateGeneratedArrival(ac, dt) || UpdateGeneratedDeparture(ac, dt) || UpdateOriginalRoute(ac, dt) || UpdateOriginal(ac, dt))
        {
            return;
        }

        if (ac.Phase == FlightPhase.Approach)
        {
            if (ac.LandingIntent != LandingIntent.LowApproach)
            {
                ac.TouchdownDistanceMetres ??= Math.Min(300, threshold.Distance(end) * .12);
                threshold = threshold.Offset(heading, ac.TouchdownDistanceMetres.Value);
            }
            var landingSpeed = ac.LandingIntent != LandingIntent.LowApproach && ac.AltitudeFeet < 50
                ? Math.Min(ac.TargetSpeedKnots, FlightDynamics.LandingSpeed(ac)) : ac.TargetSpeedKnots;
            ApproachSpeed(ac, landingSpeed, dt, true, 2);
            var course = ac.Position.Bearing(threshold);
            var approachMovement = AirTrackDistance(ac, course, dt);
            var speed = approachMovement / dt;
            var distance = ac.Position.Distance(threshold);
            if (ac.ReportFinalNm > 0 && distance <= ac.ReportFinalNm * 1852)
            {
                Transmit(ac, FormattableString.Invariant($"{ac.ReportFinalNm:0.#} mile final."), "flight.final.distance", value: ac.ReportFinalNm);
                ac.ReportFinalNm = 0;
            }
            var tooHigh = ac.Specification.DescentRatesFpm.Count > 0 && ac.AltitudeFeet > distance * Math.Tan(3 * Math.PI / 180) / Coordinates.MetresPerFoot + 150;
            if (distance < Math.Max(300, speed * dt) && (!ac.LandingCleared || tooHigh))
            {
                GoAround(ac, tooHigh ? "too high on final" : "no landing clearance");
                return;
            }
            ac.Position = ac.Position.Towards(threshold, approachMovement);
            var glideAltitude = Math.Max(ac.LandingIntent == LandingIntent.LowApproach ? 50 : 0, ac.Position.Distance(threshold) * Math.Tan(3 * Math.PI / 180) / Coordinates.MetresPerFoot);
            if (ac.LandingIntent != LandingIntent.LowApproach && ac.Position.Distance(threshold) < 250)
            {
                var fraction = ac.Position.Distance(threshold) / 250;
                glideAltitude *= 2 * fraction - fraction * fraction;
            }
            if (ac.GeneratedArrival is { Stage: GeneratedArrivalStage.Final })
            {
                glideAltitude = Math.Min(ac.AltitudeFeet, glideAltitude);
            }
            if (ac.Specification.DescentRatesFpm.Count > 0)
            {
                ApproachAltitude(ac, glideAltitude, dt, 1200);
            }
            else
            {
                ac.AltitudeFeet = glideAltitude;
            }

            if (ac.Position.Distance(threshold) < 0.01)
            {
                if (ac.Specification.DescentRatesFpm.Count > 0 && ac.AltitudeFeet > (ac.LandingIntent == LandingIntent.LowApproach ? 55 : 5))
                {
                    GoAround(ac, "unable to reach landing profile");
                    return;
                }
                ac.LandingCleared = false;
                if (ac.LandingIntent == LandingIntent.LowApproach)
                {
                    ac.Phase = FlightPhase.LowApproach;
                    SetPath(ac, [threshold, end]);
                    Message?.Invoke($"{ac.CallSign}: low approach over the runway.");
                    return;
                }
                ac.AltitudeFeet = 0;
                ac.SpeedKnots = WindMotion.GroundVelocity(ac.SpeedKnots, ac.Heading, State.Options).Distance(default);
                ac.Heading = heading;
                ac.GroundTrackDegrees = heading;
                if (ac.LandingIntent == LandingIntent.TouchAndGo)
                {
                    var liftPoint = end;
                    if (RunwayPerformance.Metres(ac.Specification.TakeoffDistanceFeet) is { } touchAndGoDistance)
                    {
                        touchAndGoDistance = WindMotion.TakeoffDistance(ac, touchAndGoDistance, heading, State.Options);
                        if (touchAndGoDistance > threshold.Distance(end))
                        {
                            GoAround(ac, "insufficient runway for touch and go");
                            return;
                        }
                        liftPoint = threshold.Towards(end, touchAndGoDistance);
                        ac.RunwayAccelerationMetresPerSecondSquared = RunwayPerformance.Acceleration(ac.SpeedKnots,
                            WindMotion.TakeoffGroundSpeed(ac, heading, State.Options), touchAndGoDistance);
                    }
                    State.Performance.RecordMovement(State.ElapsedSeconds);
                    ac.Phase = FlightPhase.TakeoffRoll;
                    CheckRunwayCategorySeparation(ac, landing: true);
                    ac.RunwayDepartureId = null;
                    RecordTakeoffRoll(ac);
                    SetPath(ac, [threshold, liftPoint]);
                    Message?.Invoke($"{ac.CallSign}: touchdown, taking off again.");
                    return;
                }
                ac.Phase = FlightPhase.LandingRoll;
                var landingExit = (Rollout: rw.Rollout, Exit: rw.Exit);
                if (RunwayPerformance.Metres(ac.Specification.LandingDistanceFeet) is { } landingDistance)
                {
                    var braking = RunwayPerformance.Acceleration(ac.Specification.ApproachSpeed, ac.Specification.TaxiSpeed,
                        Math.Max(100, landingDistance - (ac.TouchdownDistanceMetres ?? 0)));
                    var required = (ac.TouchdownDistanceMetres ?? 0) + RunwayPerformance.BrakingDistance(ac.SpeedKnots, ac.Specification.TaxiSpeed, braking);
                    var windRatio = WindMotion.Track(ac.Specification.ApproachSpeed, heading, State.Options).GroundSpeedKnots /
                        Math.Max(1, ac.Specification.ApproachSpeed);
                    var windLandingDistance = (ac.TouchdownDistanceMetres ?? 0) +
                        Math.Max(0, landingDistance - (ac.TouchdownDistanceMetres ?? 0)) * windRatio * windRatio;
                    var chosenExit = RunwayPerformance.LandingExit(airport, rw, Math.Max(windLandingDistance, required));
                    if (chosenExit is null)
                    {
                        GoAround(ac, "no suitable runway exit");
                        return;
                    }
                    landingExit = chosenExit.Value;
                    ac.RunwayDecelerationMetresPerSecondSquared = braking;
                }
                SetPath(ac, [threshold, airport.Point(landingExit.Rollout), airport.Point(landingExit.Exit)]);
                State.Performance.RecordMovement(State.ElapsedSeconds);
                if (ac.OriginalActions is { } landedActions)
                {
                    landedActions.FullStopLanded = true;
                }
                CheckRunwayCategorySeparation(ac, landing: true);
                Message?.Invoke($"{ac.CallSign}: landed; vacating runway {rw.Name}.");
            }
            return;
        }
        if (ac.Phase == FlightPhase.GoAround)
        {
            ApproachSpeed(ac, ac.ApproachSpeedOverrideKnots ?? Math.Min(FlightDynamics.DepartureSpeed(ac), ac.Specification.PatternSpeed ?? ac.Specification.ApproachSpeed), dt, true, 3);
            ApproachAltitude(ac, 3000, dt, 1500);
            var completed = Move(ac, ac.SpeedKnots * MetresPerKnotSecond * dt);
            if (ac.GoAroundApproachRequests == 0 && ac.PathIndex >= 1)
            {
                RequestApproachAfterGoAround(ac);
            }
            if (ac.GoAroundApproachRequests == 1 && ac.PathIndex >= 2)
            {
                RequestApproachAfterGoAround(ac);
            }
            if (completed)
            {
                if (ac.GoAroundApproachRequests < 2)
                {
                    RequestApproachAfterGoAround(ac);
                }
                // Continue flying; only an explicit pattern entry rebuilds final.
                SetPath(ac, [ac.Position.Offset(ac.Heading, 10 * 1852)]);
            }
            return;
        }
        if (ac.Phase == FlightPhase.Departing)
        {
            if (ac.DepartureTurnHeading is { } commandedHeading)
            {
                var turn = (commandedHeading - ac.Heading + 540) % 360 - 180;
                ac.Heading = Coordinates.NormalizeHeading(ac.Heading + Math.Clamp(turn, -FlightDynamics.TurnRate(ac) * dt, FlightDynamics.TurnRate(ac) * dt));
                if (Math.Abs(turn) <= FlightDynamics.TurnRate(ac) * dt)
                {
                    ac.DepartureTurnHeading = null;
                }
            }
            var guided = ac.FollowOriginalDepartureRoute && (ac.OriginalPlan?.DestinationBearingDegrees ?? ac.GeneratedRoute?.BearingDegrees) is not null;
            if (guided && (ac.OriginalPlan?.DestinationBearingDegrees ?? ac.GeneratedRoute?.BearingDegrees) is { } destinationBearing)
            {
                var radians = heading * Math.PI / 180;
                var along = (ac.Position.X - threshold.X) * Math.Sin(radians) + (ac.Position.Y - threshold.Y) * Math.Cos(radians);
                if (along >= threshold.Distance(end))
                {
                    ac.OriginalDepartureRouteStarted = true;
                }
                if (ac.OriginalDepartureRouteStarted)
                {
                    var course = ac.GroundTrackDegrees ?? ac.Heading;
                    var difference = (destinationBearing - course + 540) % 360 - 180;
                    ac.GroundTrackDegrees = Coordinates.NormalizeHeading(course + Math.Clamp(difference, -FlightDynamics.TurnRate(ac) * dt, FlightDynamics.TurnRate(ac) * dt));
                }
            }
            if (guided)
            {
                FlyTrack(ac, ac.OriginalDepartureRouteStarted ? ac.GroundTrackDegrees ?? heading : heading, dt);
            }
            else
            {
                FlyHeading(ac, ac.Heading, dt);
            }
            ApproachSpeed(ac, FlightDynamics.DepartureSpeed(ac), dt, true, 3);
            double? departureAltitude = null;
            if (ac.FollowOriginalDepartureAltitude)
            {
                if (ac.OriginalPlan?.AltitudeReference == OriginalAltitudeReference.Msl)
                {
                    departureAltitude = airport.Original.SourceMslToAgl(ac.OriginalPlan.Altitude1Feet);
                }
                else if (ac.GeneratedPerformance is { } generated)
                {
                    departureAltitude = airport.Original.SourceMslToAgl(generated.Altitude1FeetMsl);
                }
            }
            if (departureAltitude is { } targetAltitude)
            {
                ApproachAltitude(ac, targetAltitude, dt, 1800);
            }
            else
            {
                ac.AltitudeFeet += VerticalPerformance.Rate(ac.Specification, ac.AltitudeFeet, true, 1800) * dt / 60;
            }
            if (ac.Position.Distance(end) > 8 * 1852)
            {
                CompleteDeparture(ac);
            }

            return;
        }
        if (ac.Phase is FlightPhase.AtGate or FlightPhase.ReadyForTaxi or FlightPhase.ArrivalHold or FlightPhase.HoldingShort or FlightPhase.LinedUp)
        {
            ac.SpeedKnots = 0;
            return;
        }
        if (ac.Holding)
        {
            var previous = ac.SpeedKnots;
            ApproachSpeed(ac, 0, dt, false, 3);
            var clearance = GroundTrafficAhead(ac).Distance;
            if (ac.HoldShortRunway is { } holdingRunway)
            {
                clearance = Math.Min(clearance, Math.Max(0, (TaxiHoldDistance(ac, airport.GetRunway(holdingRunway)) ?? double.PositiveInfinity) - .01));
            }
            var distance = Math.Min((previous + ac.SpeedKnots) / 2 * MetresPerKnotSecond * dt, clearance);
            Move(ac, Math.Max(0, distance));
            if (clearance <= distance + .001)
            {
                ac.SpeedKnots = 0;
            }
            return;
        }
        var runwayAccess = ac.Phase == FlightPhase.TakeoffRoll && ac.Path.Count == 3 && ac.PathIndex < 2;
        var remaining = RemainingDistance(ac);
        if (ac.Phase is FlightPhase.TaxiIn or FlightPhase.TaxiOut)
        {
            remaining = Math.Min(remaining, GroundTrafficAhead(ac).Distance);
        }
        var holdDistance = ac.Phase is FlightPhase.TaxiIn or FlightPhase.TaxiOut && ac.HoldShortRunway is { } hold
            ? TaxiHoldDistance(ac, airport.GetRunway(hold)) : null;
        var distanceBeforeHold = holdDistance is { } entry ? Math.Max(0, entry - 0.01) : double.PositiveInfinity;
        double target = ac.Phase switch
        {
            FlightPhase.LandingRoll => ac.RunwayDecelerationMetresPerSecondSquared is not null ? ac.Specification.TaxiSpeed : ac.PathIndex >= 2 ? 12 : 35,
            FlightPhase.TakeoffRoll => ac.PathIndex == 1 && ac.Path.Count == 3 ? 12 : WindMotion.TakeoffGroundSpeed(ac, heading, State.Options),
            FlightPhase.LineUp => 10,
            _ => ac.Specification.TaxiSpeed
        };
        // Stop at route end, never at each intermediate taxi waypoint.
        if (runwayAccess || ac.Phase is FlightPhase.TaxiIn or FlightPhase.TaxiOut or FlightPhase.LineUp or FlightPhase.LandingRoll)
        {
            var braking = ac.Specification.Acceleration.TaxiDeceleration is { } deceleration && double.IsFinite(deceleration) && deceleration > 0
                ? deceleration * Coordinates.MetresPerFoot
                : 1.5;
            if (ac.Phase == FlightPhase.LandingRoll && ac.RunwayDecelerationMetresPerSecondSquared is { } rolloutBraking)
            {
                braking = Math.Min(braking, rolloutBraking);
            }
            // Leave integration margin: the next step travels before the lower speed is established.
            var planningBraking = braking * .8;
            target = Math.Min(target, Math.Sqrt(2 * planningBraking * Math.Min(remaining, distanceBeforeHold)) / MetresPerKnotSecond);
            target = Math.Min(target, RouteMovement.TaxiSpeedLimit(ac, planningBraking));
        }

        var previousSpeed = ac.SpeedKnots;
        var taxiingToThreshold = ac.Phase == FlightPhase.TakeoffRoll && ac.PathIndex == 1 && ac.Path.Count == 3;
        if (ac.Phase == FlightPhase.TakeoffRoll && !taxiingToThreshold && ac.RunwayDepartureId is null)
        {
            RecordTakeoffRoll(ac);
        }
        if (ac.Phase == FlightPhase.TakeoffRoll && !taxiingToThreshold && ac.RunwayAccelerationMetresPerSecondSquared is { } acceleration)
        {
            ac.SpeedKnots = Approach(ac.SpeedKnots, target, acceleration / MetresPerKnotSecond * dt);
        }
        else if (ac.Phase == FlightPhase.LandingRoll && ac.PathIndex < 2 && ac.RunwayDecelerationMetresPerSecondSquared is { } braking)
        {
            ac.SpeedKnots = Approach(ac.SpeedKnots, target, braking / MetresPerKnotSecond * dt);
        }
        else if (ac.Phase != FlightPhase.TakeoffRoll || taxiingToThreshold)
        {
            ApproachSpeed(ac, target, dt, false, ac.Phase == FlightPhase.LandingRoll ? 4 : 3);
        }
        else
        {
            ac.SpeedKnots = Approach(ac.SpeedKnots, target, dt * 3);
        }
        var trafficDistance = double.PositiveInfinity;
        if (ac.Phase is FlightPhase.TaxiIn or FlightPhase.TaxiOut && ac.PathIndex < ac.Path.Count)
        {
            trafficDistance = GroundTrafficAhead(ac).Distance;
            if (trafficDistance < 0.001)
            {
                ac.SpeedKnots = 0;
                return;
            }
            ac.TrafficObstructionReported = false;
        }
        var speedForDistance = ac.Phase is FlightPhase.TakeoffRoll or FlightPhase.LandingRoll
            && (ac.RunwayAccelerationMetresPerSecondSquared is not null || ac.RunwayDecelerationMetresPerSecondSquared is not null)
            ? (previousSpeed + ac.SpeedKnots) / 2
            : ac.SpeedKnots;
        var movement = Math.Max(0, speedForDistance * MetresPerKnotSecond) * dt;
        var stoppedByTraffic = trafficDistance < movement;
        movement = Math.Min(movement, trafficDistance);
        var reachedHold = movement >= distanceBeforeHold;
        var pathComplete = Move(ac, Math.Min(movement, distanceBeforeHold));
        if (stoppedByTraffic)
        {
            ac.SpeedKnots = 0;
        }
        if (reachedHold)
        {
            ac.Holding = true;
            ac.HoldShortReached = true;
            ac.SpeedKnots = 0;
            // Some original terminal hold nodes lie inside the synthetic envelope.
            // A stop on their final access edge is the departure hold, not an
            // intermediate crossing that must be released with TU first.
            if (ac.Phase == FlightPhase.TaxiOut && ac.PathIndex == ac.Path.Count - 1 &&
                ac.Path[^1].Distance(airport.Point(rw.Hold)) < 0.01 &&
                airport.GetRunway(ac.HoldShortRunway!).PhysicalId == rw.PhysicalId)
            {
                ac.Phase = FlightPhase.HoldingShort;
            }
            if (ac.Phase == FlightPhase.HoldingShort)
            {
                TransmitReadyForTakeoff(ac);
            }
            else
            {
                Transmit(ac, $"Holding short runway {ac.HoldShortRunway}.", "ground.holding.short", spokenRunway: ac.HoldShortRunway);
            }
            return;
        }
        if (!pathComplete)
        {
            return;
        }

        switch (ac.Phase)
        {
            case FlightPhase.LandingRoll:
                ac.Phase = FlightPhase.ArrivalHold;
                Transmit(ac, "up your frequency request taxi to parking.", "ground.parking.request");
                break;

            case FlightPhase.TaxiIn:
                ac.Phase = FlightPhase.AtGate;
                if (ac.Direction == FlightDirection.Arrival || ac.OriginalActions?.FullStopLanded == true)
                {
                    ac.Phase = FlightPhase.Completed;
                    if (ac.OriginalActions is { } actions)
                    {
                        actions.Completed = true;
                    }
                    State.Performance.Arrivals++;
                    Award(10);
                }
                Message?.Invoke($"{ac.CallSign}: parked at {ac.Gate}.");
                if (ac.Phase == FlightPhase.AtGate)
                {
                    InitializePushback(ac);
                    TransmitInitialContact(ac);
                }
                break;

            case FlightPhase.TaxiOut:
                ac.Phase = FlightPhase.HoldingShort;
                TransmitReadyForTakeoff(ac);
                break;

            case FlightPhase.LineUp:
                ac.Phase = FlightPhase.LinedUp;
                ac.Heading = heading;
                break;

            case FlightPhase.TakeoffRoll:
                var runwayPass = ac.LandingIntent == LandingIntent.TouchAndGo;
                ac.SpeedKnots = WindMotion.RunwayAirspeed(ac.SpeedKnots, heading, State.Options);
                ac.GroundTrackDegrees = heading;
                MarkRunwayDepartureAirborne(ac);
                if (ContinueOriginalActionsAfterAirborne(ac))
                {
                    ac.AltitudeFeet = 0;
                    if (ac.Phase == FlightPhase.Departing)
                    {
                        Message?.Invoke($"{ac.CallSign}: airborne, ready for departure handoff.");
                    }
                    TransmitOnTheGo(ac, runwayPass);
                    break;
                }
                if (ac.LandingIntent == LandingIntent.TouchAndGo)
                {
                    BeginCircuit(ac, "downwind", ac.RightTraffic, false, ac.Runway);
                    ac.AltitudeFeet = 0;
                    TransmitOnTheGo(ac, runwayPass);
                    break;
                }

                ac.Phase = FlightPhase.Departing;
                ac.AltitudeFeet = 0;
                ac.Heading = heading;

                Message?.Invoke($"{ac.CallSign}: airborne, ready for departure handoff.");
                break;
        }
        if (!ac.IsAirborne)
        {
            ac.SpeedKnots = 0;
        }
    }
    private static double Approach(double value, double target, double amount) => value < target ? Math.Min(target, value + amount) : Math.Max(target, value - amount);

    private static void ApproachSpeed(Aircraft ac, double target, double dt, bool airborne, double fallbackKnotsPerSecond)
    {
        if (airborne && ac.Movement is { Airborne: true })
        {
            var braking = SpeedPerformance.Rate(ac.Specification, true, false, fallbackKnotsPerSecond) * MetresPerKnotSecond;
            var minimum = FlightDynamics.Profile(ac, "Stall_Speed", ac.Specification.ApproachSpeed * .75) * 1.05;
            target = Math.Min(target, Math.Max(minimum, RouteMovement.TaxiSpeedLimit(ac, braking)));
        }
        var rate = SpeedPerformance.Rate(ac.Specification, airborne, target > ac.SpeedKnots, fallbackKnotsPerSecond);
        ac.SpeedKnots = Approach(ac.SpeedKnots, target, rate * dt);
    }
    private static void ApproachAltitude(Aircraft ac, double target, double dt, double fallbackFpm)
    {
        var rate = VerticalPerformance.Rate(ac.Specification, ac.AltitudeFeet, target > ac.AltitudeFeet, fallbackFpm);
        ac.AltitudeFeet = Approach(ac.AltitudeFeet, target, rate * dt / 60);
    }
    private static double RemainingDistance(Aircraft ac) => RouteMovement.RemainingDistance(ac);
    private bool Move(Aircraft ac, double distance)
    {
        if (!ac.IsAirborne || ac.SpeedKnots <= 0.000001)
        {
            return RouteMovement.Move(ac, distance);
        }
        ac.PathIndex = AdvanceAirRoute(ac, ac.Path, ac.PathIndex, distance / ac.SpeedKnots / MetresPerKnotSecond, ac.PathExitHeading);
        return ac.PathIndex >= ac.Path.Count;
    }
    private void GoAround(Aircraft ac, string reason)
    {
        CancelOriginalRoute(ac);
        ResetAirborneInstruction(ac);
        var rw = State.Airport.GetRunway(ac.Runway);
        var t = State.Airport.Point(rw.Threshold);
        var e = State.Airport.Point(rw.End);
        var h = t.Bearing(e);
        ac.LandingCleared = false;
        ac.LandingIntent = LandingIntent.FullStop;
        ac.TouchdownDistanceMetres = null;
        ac.ReportFinalNm = 0;
        ac.Phase = FlightPhase.GoAround;
        ac.GoAroundApproachRequests = 0;
        // GAMENT 10026B30: runway midpoint + PAT upwind, then offsets
        // 0x486200/256 and 0x6C9300/256 metres with request events at the first two points.
        var midpoint = new Position((t.X + e.X) / 2, (t.Y + e.Y) / 2);
        var firstRequest = midpoint.Offset(h, State.Airport.Original.PatternDistance(ac.Type, "upwind") ?? 1852);
        var secondRequest = firstRequest.Offset(h, 18530);
        SetPath(ac, [firstRequest, secondRequest, secondRequest.Offset(h, 27795)], h);
        State.Performance.GoArounds++;
        if (reason != "instructed")
        {
            Message?.Invoke($"{ac.CallSign}: go-around reason: {reason}.");
            Transmit(ac, "Executing missed approach.", "flight.goaround");
        }
    }
    private void RequestApproachAfterGoAround(Aircraft aircraft)
    {
        aircraft.GoAroundApproachRequests++;
        Transmit(aircraft, $"Request approach to runway {aircraft.Runway}.", "flight.approach.request");
    }
    private void CompleteDeparture(Aircraft ac)
    {
        if (ac.Phase == FlightPhase.Completed)
        {
            return;
        }
        if (ac.GeneratedDeparture is { Stage: not GeneratedDepartureStage.Cancelled } generatedRoute)
        {
            generatedRoute.Stage = GeneratedDepartureStage.Finished;
            generatedRoute.NextPointIndex = 1;
        }
        if (ac.FreeTraffic is { } local)
        {
            local.AwaitingEntry = false;
        }
        if (ac.OriginalActions is { } actions)
        {
            actions.Completed = true;
        }
        ac.Phase = FlightPhase.Completed;
        if (ac.Direction == FlightDirection.Departure)
        {
            State.Performance.Departures++;
            Award(10);
        }
    }
    private void Award(int points)
    {
        State.Performance.Score += points;
    }
    private void DetectConflicts()
    {
        var current = new HashSet<string>();
        var currentRunways = new HashSet<string>();
        var aircraft = State.Aircraft.Where(a => a.IsOnFrequency && a.Phase != FlightPhase.Completed).ToList();
        for (var i = 0; i < aircraft.Count; i++)
        {
            for (var j = i + 1; j < aircraft.Count; j++)
            {
                var a = aircraft[i];
                var b = aircraft[j];
                var d = a.Position.Distance(b.Position);
                var intersectionConflict = OccupiesRunwayIntersection(a, b);
                var runwayConflict = intersectionConflict || OccupiesActiveRunway(a, b);
                var verticalMetres = Math.Abs(a.AltitudeFeet - b.AltitudeFeet) * Coordinates.MetresPerFoot;
                var radius = Math.Max(5, (a.Specification.WingSpanMetres + b.Specification.WingSpanMetres) / 4);
                var collision = verticalMetres < 5 && (a.SpeedKnots > 1 || b.SpeedKnots > 1) &&
                    (!a.IsAirborne && !b.IsAirborne ? FootprintsOverlap(a, b) : d < radius);
                var conflict = runwayConflict || collision || (a.IsAirborne && b.IsAirborne
                    ? d < 3 * 1852 && Math.Abs(a.AltitudeFeet - b.AltitudeFeet) < 1000
                    : !a.IsAirborne && !b.IsAirborne && FootprintsOverlap(a, b, 2));
                if (!conflict)
                {
                    continue;
                }

                var key = string.CompareOrdinal(a.CallSign, b.CallSign) < 0 ? a.CallSign + "/" + b.CallSign : b.CallSign + "/" + a.CallSign;
                current.Add(key);
                var newIncident = activeConflicts.Add(key);
                if (newIncident)
                {
                    State.Performance.Incidents++;
                    Award(-20);
                    Message?.Invoke($"{(intersectionConflict ? "Runway intersection" : runwayConflict ? "Runway separation" : "Separation")} alert: {key}.");
                }
                if (runwayConflict)
                {
                    currentRunways.Add(key);
                    if (State.Performance.ActiveRunwayPairs.Add(key))
                    {
                        State.Performance.RunwaySeparation++;
                        if (!newIncident)
                        {
                            Message?.Invoke($"Runway separation alert: {key}.");
                        }
                    }
                }
                // Collision detection is independent of the aircraft's airborne phase.
                if (collision && State.Performance.ReportedCollisions.Add(key))
                {
                    State.Performance.Crashes++;
                    Message?.Invoke($"Collision: {key}.");
                }
            }
        }

        activeConflicts.IntersectWith(current);
        State.Performance.ActiveRunwayPairs.IntersectWith(currentRunways);
    }
}
