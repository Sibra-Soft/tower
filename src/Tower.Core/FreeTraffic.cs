using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace Tower.Core;

public enum FreeTrafficTask
{
    Arrival, Departure, VfrFullStop, VfrCircuit, VfrDeparture
}

// Session state, deliberately separate from user settings and authored SIM plans.
public sealed class FreeTrafficState
{
    public uint InitialSeed { get; set; }
    public bool Started { get; set; }
    public double NextSpawnSeconds { get; set; }
    public string LastCallsign { get; set; } = "";
    public string LastType { get; set; } = "";
    public string LastGate { get; set; } = "";
    public string LastArrivalRunway { get; set; } = "";
    public string LastDepartureRunway { get; set; } = "";
}

public sealed class FreeTrafficFlight
{
    public FreeTrafficTask Task { get; set; }
    public bool AwaitingEntry { get; set; }
    public Position OrbitCentre { get; set; }
    public double OrbitRadiusMetres { get; set; }
    public bool Clockwise { get; set; }
    [JsonIgnore]
    public bool IsVfr => Task is FreeTrafficTask.VfrFullStop or FreeTrafficTask.VfrCircuit or FreeTrafficTask.VfrDeparture;

    [JsonIgnore]
    public string Caption => Task switch
    {
        FreeTrafficTask.VfrCircuit => AwaitingEntry ? "VFR T/G entry" : "VFR circuit",
        FreeTrafficTask.VfrFullStop => AwaitingEntry ? "VFR entry" : "VFR arrival",
        FreeTrafficTask.VfrDeparture => "VFR departure",
        _ => ""
    };

    public bool IsValid() => Enum.IsDefined(Task) && double.IsFinite(OrbitRadiusMetres) && OrbitRadiusMetres >= 0 &&
        double.IsFinite(OrbitCentre.X) && double.IsFinite(OrbitCentre.Y) &&
        (!AwaitingEntry || IsVfr && Task != FreeTrafficTask.VfrDeparture && OrbitRadiusMetres is >= 500 and <= 5000);
}

public sealed partial class Simulation
{
    private readonly Dictionary<(string Runway, FlightDirection Direction, string Endpoint), List<MapNode>> freeTrafficGateConnections = [];

    private List<T> ShuffleFreeTraffic<T>(IEnumerable<T> source)
    {
        var values = source.ToList();
        for (var index = values.Count - 1; index > 0; index--)
        {
            var other = DrawTrafficRandom(0, index);
            (values[index], values[other]) = (values[other], values[index]);
        }
        return values;
    }

    public static Session CreateFreeSession(Airport airport, ScenarioOptions options, string controllerId = "", uint? seed = null)
    {
        var initial = seed ?? BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(sizeof(uint)));
        if (initial == 0)
        {
            initial = 1;
        }
        return new()
        {
            Airport = airport,
            Options = options,
            ControllerId = controllerId,
            TrafficFlowRandomState = initial,
            FreeTraffic = new() { InitialSeed = initial }
        };
    }

    public void StartFreeTraffic()
    {
        if (State.FreeTraffic is not { Started: false } traffic)
        {
            return;
        }
        traffic.Started = true;
        if (State.Options.AircraftPerMinute > 0)
        {
            var initialCount = DrawTrafficRandom(1, 3);
            for (var index = 0; index < initialCount; index++)
            {
                if (!TryAddFreeTraffic())
                {
                    break;
                }
            }
            traffic.NextSpawnSeconds = State.ElapsedSeconds + NextFreeTrafficInterval();
        }
    }

    private double NextFreeTrafficInterval() => 60 / State.Options.AircraftPerMinute * DrawTrafficRandom(500, 1500) / 1000d;

    private void UpdateFreeTraffic()
    {
        StartFreeTraffic();
        var traffic = State.FreeTraffic!;
        if (State.Options.AircraftPerMinute <= 0 || State.ElapsedSeconds < traffic.NextSpawnSeconds)
        {
            return;
        }
        var added = TryAddFreeTraffic();
        traffic.NextSpawnSeconds = State.ElapsedSeconds + (added ? NextFreeTrafficInterval()
            : Math.Clamp(6 / State.Options.AircraftPerMinute, 5, 30));
    }

    public bool TryAddFreeTraffic()
    {
        if (State.FreeTraffic is not { } traffic || State.Aircraft.Count(a => a.Phase != FlightPhase.Completed) >= 30)
        {
            return false;
        }
        var airport = State.Airport;
        var candidates = airport.TrafficPool.Where(entry => airport.AircraftTypes.ContainsKey(entry.Type) &&
            (allowedCallsigns is null || allowedCallsigns.Contains(entry.CallSign)) &&
            State.Aircraft.All(a => a.Phase == FlightPhase.Completed || !a.CallSign.Equals(entry.CallSign, StringComparison.OrdinalIgnoreCase)) &&
            State.Schedule.All(f => f.Spawned || !f.CallSign.Equals(entry.CallSign, StringComparison.OrdinalIgnoreCase))).ToList();
        if (candidates.Count == 0)
        {
            return false;
        }
        var arrivals = State.Aircraft.Count(a => a.Phase != FlightPhase.Completed && a.Direction == FlightDirection.Arrival);
        var departures = State.Aircraft.Count(a => a.Phase != FlightPhase.Completed && a.Direction == FlightDirection.Departure);
        var arrivalChance = Math.Clamp(50 + (departures - arrivals) * 5, 20, 80);
        var preferredDirection = DrawTrafficRandom(0, 99) < arrivalChance ? FlightDirection.Arrival : FlightDirection.Departure;
        // Prefer variety without excluding the only runway-compatible model.
        candidates = ShuffleFreeTraffic(candidates).OrderBy(entry => entry.Type == traffic.LastType)
            .ThenBy(entry => entry.CallSign == traffic.LastCallsign).ToList();
        // Scenario Setup has one shared checked runway list, not an arrival-only list.
        var names = State.Options.ArrivalRunways.Count > 0
            ? State.Options.ArrivalRunways : [State.Options.Runway];
        foreach (var direction in new[] { preferredDirection, preferredDirection == FlightDirection.Arrival ? FlightDirection.Departure : FlightDirection.Arrival })
        {
            if (TryAddFreeTraffic(direction, candidates, airport.GeneratedRunways(direction, names, allowUnselectedFallback: false), traffic))
            {
                return true;
            }
        }
        return false;
    }

    private bool TryAddFreeTraffic(FlightDirection direction, List<TrafficEntry> candidates, List<Runway> permittedRunways, FreeTrafficState traffic)
    {
        var lastRunway = direction == FlightDirection.Arrival ? traffic.LastArrivalRunway : traffic.LastDepartureRunway;
        var lastIndex = permittedRunways.FindIndex(runway => runway.Name.Equals(lastRunway, StringComparison.OrdinalIgnoreCase));
        var orderedRunways = Enumerable.Range(1, permittedRunways.Count)
            .Select(offset => permittedRunways[(lastIndex + offset) % permittedRunways.Count]).ToList();
        var entryAttempts = 0;
        foreach (var entry in candidates)
        {
            foreach (var runway in orderedRunways)
            {
                var specification = State.Airport.AircraftTypes[entry.Type];
                var gates = FreeTrafficGates(runway, direction, specification);
                if (gates.Count == 0)
                {
                    continue;
                }
                var alternate = gates.Where(gate => gate.Id != traffic.LastGate).ToList();
                if (alternate.Count > 0)
                {
                    gates = alternate;
                }
                var gate = gates[DrawTrafficRandom(0, gates.Count - 1)];
                if (++entryAttempts > 24)
                {
                    return false;
                }
                try
                {
                    // Completed records must not permanently exhaust the pool in Windows speech mode.
                    State.Aircraft.RemoveAll(a => a.Phase == FlightPhase.Completed && a.CallSign.Equals(entry.CallSign, StringComparison.OrdinalIgnoreCase));
                    AddAircraft(direction, entry.CallSign, entry.Type, runway.Name, gate.Id, freeTrafficFlight: new()
                    {
                        Task = direction == FlightDirection.Arrival ? FreeTrafficTask.Arrival : FreeTrafficTask.Departure
                    });
                    State.NextFlight++;
                    traffic.LastCallsign = entry.CallSign;
                    traffic.LastType = entry.Type;
                    traffic.LastGate = gate.Id;
                    if (direction == FlightDirection.Arrival)
                    {
                        traffic.LastArrivalRunway = runway.Name;
                    }
                    else
                    {
                        traffic.LastDepartureRunway = runway.Name;
                    }
                    return true;
                }
                catch (InvalidOperationException)
                {
                    // No safe entry or gate: retry without granting any clearance or emitting radio.
                }
            }
        }
        return false;
    }

    private List<MapNode> FreeTrafficGates(Runway runway, FlightDirection direction, AircraftSpecification specification)
    {
        var airport = State.Airport;
        var length = airport.Point(runway.Threshold).Distance(airport.Point(runway.End));
        var required = RunwayPerformance.Metres(direction == FlightDirection.Departure ? specification.TakeoffDistanceFeet : specification.LandingDistanceFeet);
        if (required > length)
        {
            return [];
        }
        var endpoint = direction == FlightDirection.Departure ? runway.Hold : runway.Exit;
        if (direction == FlightDirection.Arrival && required is { } landing)
        {
            if (RunwayPerformance.LandingExit(airport, runway, landing) is not { } exit)
            {
                return [];
            }
            endpoint = exit.Exit;
        }
        var key = (runway.Name, direction, endpoint);
        if (!freeTrafficGateConnections.TryGetValue(key, out var connected))
        {
            connected = airport.Nodes.Values.Where(node => node.Kind == "GATE" &&
                HasTaxiConnection(direction == FlightDirection.Departure ? node.Id : endpoint,
                    direction == FlightDirection.Departure ? endpoint : node.Id)).ToList();
            freeTrafficGateConnections[key] = connected;
        }
        return connected.Where(node => GateAvailable(node, specification)).ToList();
    }

    private bool HasTaxiConnection(string from, string to)
    {
        try
        {
            _ = Routing.FindTaxiPath(State.Airport, from, to);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private void ConfigureFreeTrafficFlight(Aircraft aircraft)
    {
        var flight = aircraft.FreeTraffic!;
        if (aircraft.Direction == FlightDirection.Departure)
        {
            flight.Task = FreeTrafficTask.Departure;
            flight.AwaitingEntry = false;
            return;
        }
        var airport = State.Airport;
        var runway = airport.GetRunway(aircraft.Runway);
        var threshold = airport.Point(runway.Threshold);
        var heading = threshold.Bearing(airport.Point(runway.End));
        flight.Task = FreeTrafficTask.Arrival;
        flight.AwaitingEntry = false;
        if (CanInitializeGeneratedArrival(aircraft))
        {
            // Preserve the existing random variation; the route planner spaces it along the incoming route.
            aircraft.Position = threshold.Offset(aircraft.GeneratedRoute!.BearingDegrees, DrawTrafficRandom(7 * 1852, 13 * 1852));
            return;
        }
        for (var attempt = 0; attempt < 24; attempt++)
        {
            var bearing = aircraft.GeneratedRoute?.BearingDegrees ?? Coordinates.NormalizeHeading(heading + 180);
            aircraft.Position = threshold.Offset(bearing, DrawTrafficRandom(7 * 1852, 13 * 1852));
            var target = aircraft.Path.FirstOrDefault(threshold);
            aircraft.Heading = aircraft.Position.Bearing(target);
            if (State.Aircraft.Where(other => other.IsAirborne && other.Phase != FlightPhase.Completed)
                .All(other => aircraft.Position.Distance(other.Position) >= 3 * 1852))
            {
                return;
            }
        }
        throw new InvalidOperationException("No safe traffic entry is available.");
    }

    // Compatibility for saves made before the VOTER/VFR misunderstanding was corrected.
    private void RestoreFreeTrafficFlights()
    {
        foreach (var aircraft in State.Aircraft.Where(a => a.FreeTraffic is { IsVfr: true }))
        {
            var flight = aircraft.FreeTraffic!;
            var pending = flight.AwaitingEntry;
            flight.Task = aircraft.Direction == FlightDirection.Arrival ? FreeTrafficTask.Arrival : FreeTrafficTask.Departure;
            flight.AwaitingEntry = false;
            if (pending && aircraft.IsAirborne && aircraft.Phase != FlightPhase.Completed)
            {
                BeginCircuit(aircraft, "final", false, false, aircraft.Runway);
            }
        }
    }
}
