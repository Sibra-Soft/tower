using System.Text.Json;
using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class FreeTrafficTests
{
    private static Airport Airport()
    {
        var airport = Tower.Core.Airport.Training();
        airport.AircraftTypes = new()
        {
            ["C172"] = new() { Type = "C172", OriginalTypeGroup = "Single", PatternSpeed = 90, WingSpanMetres = 11 },
            ["BE58"] = new() { Type = "BE58", OriginalTypeGroup = "Twin", PatternSpeed = 120, WingSpanMetres = 12 },
            ["B738"] = new() { Type = "B738", OriginalTypeGroup = "Airline", WingSpanMetres = 35 }
        };
        airport.TrafficPool = [new("N100", "C172"), new("N200", "C172"), new("N300", "BE58"), new("JET1", "B738"), new("JET2", "B738")];
        return airport;
    }

    private static Simulation Create(uint seed = 123456, IReadOnlyCollection<string>? allowed = null) => new(
        Simulation.CreateFreeSession(Airport(), new() { Runway = "18", ArrivalRunways = ["18"], AircraftPerMinute = 1 }, seed: seed), allowed);

    [Fact]
    public void New_games_get_fresh_seeds_but_explicit_seed_replays_the_same_opening()
    {
        var seeds = Enumerable.Range(0, 8).Select(_ => Simulation.CreateFreeSession(Airport(), new()).TrafficFlowRandomState).ToHashSet();
        Assert.True(seeds.Count > 1);
        Assert.DoesNotContain(0u, seeds);
        var first = Create();
        var second = Create();
        first.StartFreeTraffic();
        second.StartFreeTraffic();
        Assert.Equal(Describe(first.State), Describe(second.State));
        Assert.InRange(first.State.Aircraft.Count, 1, 3);
        Assert.Equal(first.State.FreeTraffic!.NextSpawnSeconds, second.State.FreeTraffic!.NextSpawnSeconds);
        var count = first.State.Aircraft.Count;
        first.StartFreeTraffic();
        Assert.Equal(count, first.State.Aircraft.Count);
    }

    [Fact]
    public void Timing_varies_within_approved_band_and_does_not_catch_up_after_capacity_is_released()
    {
        var simulation = Create();
        simulation.StartFreeTraffic();
        var intervals = new List<double>();
        for (var index = 0; index < 12; index++)
        {
            simulation.State.Aircraft.Clear();
            var next = simulation.State.FreeTraffic!.NextSpawnSeconds;
            intervals.Add(next - simulation.State.ElapsedSeconds);
            Assert.InRange(intervals[^1], 29.999, 90.001);
            simulation.Advance(next - simulation.State.ElapsedSeconds + 0.001);
            Assert.Single(simulation.State.Aircraft);
        }
        Assert.True(intervals.Distinct().Count() > 1);
        simulation.State.Aircraft.Clear();
        simulation.State.Airport.TrafficPool.Clear();
        simulation.Advance(600);
        Assert.Empty(simulation.State.Aircraft);
        var due = simulation.State.FreeTraffic!.NextSpawnSeconds;
        simulation.State.Airport.TrafficPool.Add(new("JET1", "B738"));
        simulation.Advance(due - simulation.State.ElapsedSeconds + 0.001);
        Assert.Single(simulation.State.Aircraft);
        Assert.InRange(simulation.State.FreeTraffic.NextSpawnSeconds - simulation.State.ElapsedSeconds, 29.999, 90.001);
    }

    [Fact]
    public void Random_pool_preserves_type_and_variety_without_automatic_vfr()
    {
        var simulation = Create();
        var tasks = new Dictionary<FreeTrafficTask, int>();
        var gates = new HashSet<string>();
        var types = new HashSet<string>();
        string? previousType = null;
        for (var index = 0; index < 500; index++)
        {
            Assert.True(simulation.TryAddFreeTraffic());
            var aircraft = Assert.Single(simulation.State.Aircraft);
            Assert.Contains(new TrafficEntry(aircraft.CallSign, aircraft.Type), simulation.State.Airport.TrafficPool);
            Assert.NotEqual(previousType, aircraft.Type);
            previousType = aircraft.Type;
            tasks[aircraft.FreeTraffic!.Task] = tasks.GetValueOrDefault(aircraft.FreeTraffic.Task) + 1;
            gates.Add(aircraft.Gate);
            types.Add(aircraft.Type);
            if (aircraft.FreeTraffic.IsVfr)
            {
                Assert.Contains(aircraft.Specification.OriginalTypeGroup, new[] { "Single", "Twin" });
            }
            simulation.State.Aircraft.Clear();
        }
        Assert.Equal(2, tasks.Count);
        Assert.DoesNotContain(tasks.Keys, task => task >= FreeTrafficTask.VfrFullStop);
        Assert.Equal(3, gates.Count);
        Assert.Equal(3, types.Count);
    }

    [Fact]
    public void Bao_uses_only_recorded_pool_pairs_and_falls_back_when_no_local_type_is_available()
    {
        var simulation = Create(allowed: ["JET1", "UNMAPPED"]);
        for (var index = 0; index < 30; index++)
        {
            Assert.True(simulation.TryAddFreeTraffic());
            var aircraft = Assert.Single(simulation.State.Aircraft);
            Assert.Equal("JET1", aircraft.CallSign);
            Assert.Equal("B738", aircraft.Type);
            Assert.False(aircraft.FreeTraffic!.IsVfr);
            Assert.False(simulation.TryAddFreeTraffic());
            aircraft.Phase = FlightPhase.Completed;
        }
        var unsupported = Create(allowed: ["UNMAPPED"]);
        Assert.False(unsupported.TryAddFreeTraffic());
        Assert.Empty(unsupported.State.Aircraft);
    }

    [Fact]
    public void Variety_never_excludes_the_only_runway_compatible_aircraft()
    {
        var simulation = Create();
        simulation.State.Airport.TrafficPool = [new("N100", "C172"), new("JET1", "B738")];
        var unsuitable = simulation.State.Airport.AircraftTypes["C172"];
        unsuitable.LandingDistanceFeet = 100000;
        unsuitable.TakeoffDistanceFeet = 100000;
        simulation.State.FreeTraffic!.LastType = "B738";
        for (var index = 0; index < 20; index++)
        {
            Assert.True(simulation.TryAddFreeTraffic());
            Assert.Equal("JET1", Assert.Single(simulation.State.Aircraft).CallSign);
            simulation.State.Aircraft.Clear();
        }
    }

    [Fact]
    public void Occupied_and_reserved_gates_postpone_generation_without_a_radio_or_phantom_aircraft()
    {
        var simulation = Create();
        foreach (var gate in new[] { "G1", "G2", "G3" })
        {
            var holder = simulation.AddAircraft(FlightDirection.Departure, "HOLD" + gate, "C172", assignedGate: gate);
            if (gate == "G1")
            {
                holder.Phase = FlightPhase.TaxiIn;
                holder.Position = new(-1000, 0);
            }
        }
        var messages = 0;
        simulation.RadioMessage += _ => messages++;
        Assert.False(simulation.TryAddFreeTraffic());
        Assert.Equal(3, simulation.State.Aircraft.Count);
        Assert.Equal(0, messages);
    }

    [Fact]
    public void Incoming_taxi_reserves_its_gate_even_while_still_far_away()
    {
        var simulation = Create();
        var incoming = simulation.AddAircraft(FlightDirection.Arrival, "PARKING", "C172", assignedGate: "G1");
        incoming.Phase = FlightPhase.TaxiIn;
        incoming.Position = new(-1000, 0);
        for (var index = 0; index < 30; index++)
        {
            Assert.True(simulation.TryAddFreeTraffic());
            var aircraft = simulation.State.Aircraft.Single(a => a != incoming);
            Assert.NotEqual("G1", aircraft.Gate);
            simulation.State.Aircraft.Remove(aircraft);
        }
    }

    [Fact]
    public void Older_pending_vfr_saves_resume_without_an_orbit_or_a_landing_clearance()
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "N100", "C172");
        aircraft.Phase = FlightPhase.Vectoring;
        aircraft.FreeTraffic = new() { Task = FreeTrafficTask.VfrFullStop, AwaitingEntry = true, OrbitRadiusMetres = 1500 };
        var position = aircraft.Position;
        var restored = new Simulation(simulation.State);
        Assert.Equal(position, aircraft.Position);
        Assert.False(aircraft.FreeTraffic.IsVfr);
        Assert.False(aircraft.FreeTraffic.AwaitingEntry);
        Assert.False(aircraft.LandingCleared);
        Assert.Equal(FlightPhase.Circuit, aircraft.Phase);
        Assert.Equal(FreeTrafficTask.Arrival, aircraft.FreeTraffic.Task);
    }
    [Fact]
    public void Saved_generator_restores_the_next_spawn_and_random_choices_without_reseeding()
    {
        var simulation = Create();
        simulation.StartFreeTraffic();
        simulation.Advance(8);
        var file = Path.Combine(Path.GetTempPath(), "Tower-FreeTraffic-" + Guid.NewGuid() + ".json");
        try
        {
            JsonStore.Write(file, simulation.State);
            var restored = new Simulation(JsonStore.LoadSession(file));
            restored.StartFreeTraffic();
            Assert.Equal(Describe(simulation.State), Describe(restored.State));
            for (var index = 0; index < 4; index++)
            {
                simulation.State.Aircraft.Clear();
                restored.State.Aircraft.Clear();
                var step = simulation.State.FreeTraffic!.NextSpawnSeconds - simulation.State.ElapsedSeconds + 0.001;
                simulation.Advance(step);
                restored.Advance(step);
                Assert.Equal(Describe(simulation.State), Describe(restored.State));
                Assert.Equal(simulation.State.TrafficFlowRandomState, restored.State.TrafficFlowRandomState);
                Assert.Equal(simulation.State.FreeTraffic.NextSpawnSeconds, restored.State.FreeTraffic!.NextSpawnSeconds);
            }
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData("CYN", "9")]
    [Trait("Category", "Integration")]
    public void Real_airports_generate_only_known_aircraft_with_connected_free_gates(string code, string runway)
    {
        var data = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA");
        Assert.False(string.IsNullOrWhiteSpace(data), "Set TOWER_ORIGINAL_DATA for real-airport traffic validation.");
        var airport = OriginalData.LoadAirport(data!, code);
        var simulation = new Simulation(Simulation.CreateFreeSession(airport,
            new() { Runway = runway, ArrivalRunways = [runway], AircraftPerMinute = 1 }, seed: 42));
        var directions = new HashSet<FlightDirection>();
        for (var index = 0; index < 30; index++)
        {
            Assert.True(simulation.TryAddFreeTraffic(), code + " has no valid generated traffic.");
            var aircraft = Assert.Single(simulation.State.Aircraft);
            Assert.Contains(new TrafficEntry(aircraft.CallSign, aircraft.Type), airport.TrafficPool);
            Assert.True(aircraft.FreeTraffic!.IsValid());
            Assert.False(aircraft.FreeTraffic.IsVfr);
            directions.Add(aircraft.Direction);
            var assigned = airport.GetRunway(aircraft.Runway);
            var exit = RunwayPerformance.Metres(aircraft.Specification.LandingDistanceFeet) is { } landing
                ? RunwayPerformance.LandingExit(airport, assigned, landing)?.Exit ?? assigned.Exit : assigned.Exit;
            _ = Routing.FindTaxiPath(airport, aircraft.Direction == FlightDirection.Departure ? aircraft.Gate : exit,
                aircraft.Direction == FlightDirection.Departure ? assigned.Hold : aircraft.Gate);
            simulation.State.Aircraft.Clear();
        }
        Assert.Equal(2, directions.Count);
    }

    private static string Describe(Session state) => JsonSerializer.Serialize(state.Aircraft, JsonStore.Options);
}
