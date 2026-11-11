using System.Text.Json;
using Tower.Core;
using Tower.Infrastructure;
using TowerSpeech.Core;
using Xunit;

namespace Tower.Tests;

public sealed class TrafficDistributionTests
{
    private static Simulation Create(IReadOnlyCollection<string>? allowed = null)
    {
        var airport = Airport.Training();
        airport.Runways.Add(new() { Name = "36", Threshold = "R36", End = "R18", Hold = "H18", Exit = "E18", Rollout = "X18" });
        airport.TrafficPool = [new("SMALL", "C172"), new("LARGE", "B738")];
        airport.AircraftTypes = new()
        {
            ["C172"] = new() { Type = "C172", WingSpanMetres = 11 },
            ["B738"] = new() { Type = "B738", WingSpanMetres = 35 }
        };
        return new(Simulation.CreateFreeSession(airport,
            new() { Runway = "18", ArrivalRunways = ["18", "36"], AircraftPerMinute = 1 }, seed: 42), allowed);
    }

    [Fact]
    public void Both_directions_rotate_over_the_checked_runways_without_opening_other_runways()
    {
        var simulation = Create();
        var last = new Dictionary<FlightDirection, string>();
        var counts = new Dictionary<FlightDirection, int>();
        for (var index = 0; index < 80; index++)
        {
            Assert.True(simulation.TryAddFreeTraffic());
            var aircraft = Assert.Single(simulation.State.Aircraft);
            if (last.TryGetValue(aircraft.Direction, out var previous))
            {
                Assert.NotEqual(previous, aircraft.Runway);
            }
            last[aircraft.Direction] = aircraft.Runway;
            counts[aircraft.Direction] = counts.GetValueOrDefault(aircraft.Direction) + 1;
            Assert.Contains(new TrafficEntry(aircraft.CallSign, aircraft.Type), simulation.State.Airport.TrafficPool);
            Assert.False(aircraft.LandingCleared);
            Assert.NotEqual(FlightPhase.TakeoffRoll, aircraft.Phase);
            simulation.State.Aircraft.Clear();
        }
        Assert.Equal(2, counts.Count);
        Assert.All(counts.Values, count => Assert.True(count > 10));
    }

    [Theory]
    [InlineData(FlightDirection.Arrival)]
    [InlineData(FlightDirection.Departure)]
    public void Unavailable_direction_does_not_waste_the_spawn_when_the_other_direction_is_usable(FlightDirection available)
    {
        var simulation = Create();
        foreach (var runway in simulation.State.Airport.Runways)
        {
            runway.GeneratedArrivals = available == FlightDirection.Arrival;
            runway.GeneratedDepartures = available == FlightDirection.Departure;
        }
        for (var index = 0; index < 24; index++)
        {
            Assert.True(simulation.TryAddFreeTraffic());
            Assert.Equal(available, Assert.Single(simulation.State.Aircraft).Direction);
            simulation.State.Aircraft.Clear();
        }
    }

    [Fact]
    public void Unchecked_runway_is_not_opened_when_checked_runways_cannot_generate_traffic()
    {
        var simulation = Create();
        simulation.State.Options.ArrivalRunways = ["18"];
        simulation.State.Airport.GetRunway("18").GeneratedArrivals = false;
        simulation.State.Airport.GetRunway("18").GeneratedDepartures = false;
        var nextFlight = simulation.State.NextFlight;
        Assert.False(simulation.TryAddFreeTraffic());
        Assert.Empty(simulation.State.Aircraft);
        Assert.Equal(nextFlight, simulation.State.NextFlight);
        Assert.Empty(simulation.State.FreeTraffic!.LastArrivalRunway);
        Assert.Empty(simulation.State.FreeTraffic.LastDepartureRunway);
    }

    [Fact]
    public void Explicit_runway_selection_is_not_overridden_by_tailwind()
    {
        var simulation = Create();
        simulation.State.Options.ArrivalRunways = ["18"];
        simulation.State.Options.WindFromDegrees = 0;
        simulation.State.Options.WindKnots = 20;
        for (var index = 0; index < 20; index++)
        {
            Assert.True(simulation.TryAddFreeTraffic());
            Assert.Equal("18", Assert.Single(simulation.State.Aircraft).Runway);
            simulation.State.Aircraft.Clear();
        }
    }

    [Fact]
    public void Runway_rotation_skips_insufficient_length_for_the_selected_aircraft()
    {
        var simulation = Create();
        var airport = simulation.State.Airport;
        airport.Nodes.Add("SHORT", new("SHORT", "RUNWAY", new(0, 0)));
        airport.GetRunway("36").Threshold = "SHORT";
        foreach (var specification in airport.AircraftTypes.Values)
        {
            specification.TakeoffDistanceFeet = 8000;
        }
        foreach (var runway in airport.Runways)
        {
            runway.GeneratedArrivals = false;
        }
        for (var index = 0; index < 12; index++)
        {
            Assert.True(simulation.TryAddFreeTraffic());
            var aircraft = Assert.Single(simulation.State.Aircraft);
            Assert.Equal(FlightDirection.Departure, aircraft.Direction);
            Assert.Equal("18", aircraft.Runway);
            simulation.State.Aircraft.Clear();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Canyon_free_session_uses_both_selected_runways_and_completes_arrivals_and_departures()
    {
        var data = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA") ?? WorkspacePaths.DefaultRoot;
        var airport = OriginalData.LoadAirport(data, "CYN");
        var speech = new WorkbenchSpeechRuntime(data);
        speech.Preferences.Engine = SpeechEngineKind.BaoOriginal;
        var allowed = speech.GetTrafficCallsigns()!;
        var simulation = new Simulation(Simulation.CreateFreeSession(airport,
            new() { Runway = "9", ArrivalRunways = ["9", "32"], AircraftPerMinute = 0.25, WindKnots = 0 }, seed: 42), allowed);
        var generated = new HashSet<(FlightDirection, string)>();
        simulation.AircraftAdded += aircraft =>
        {
            Assert.Contains(aircraft.CallSign, allowed);
            Assert.Contains(aircraft.Runway, new[] { "9", "32" });
            Assert.False(aircraft.LandingCleared);
            Assert.NotNull(aircraft.GeneratedRoute);
            if (aircraft.Direction == FlightDirection.Arrival)
            {
                Assert.Contains(airport.Original.ArrivalFlow, entry => entry.Fix == aircraft.GeneratedRoute.Origin);
            }
            else
            {
                Assert.Contains(airport.Original.DepartureFlow, entry => entry.Fix == aircraft.GeneratedRoute.Destination);
            }
            generated.Add((aircraft.Direction, aircraft.Runway));
        };
        for (var second = 0; second < 3600; second++)
        {
            foreach (var aircraft in simulation.State.Aircraft.Where(aircraft => aircraft.IsOnFrequency).ToList())
            {
                var occupied = simulation.State.Aircraft.Any(other => other != aircraft && other.IsOnRunway);
                var command = aircraft.Phase switch
                {
                    FlightPhase.AtGate when aircraft.Pushback is { Completed: false } => "PB",
                    FlightPhase.AtGate or FlightPhase.ReadyForTaxi => "T" + aircraft.Runway,
                    FlightPhase.HoldingShort when !occupied => "CT",
                    FlightPhase.Circuit or FlightPhase.Approach when !aircraft.LandingCleared => "CL",
                    FlightPhase.ArrivalHold => "TP",
                    FlightPhase.TaxiIn or FlightPhase.TaxiOut when aircraft.Holding && !occupied => "TU",
                    _ => null
                };
                if (command is not null)
                {
                    _ = simulation.Execute(aircraft.CallSign + "/" + command);
                }
            }
            simulation.Advance(1);
        }
        Assert.Equal(4, generated.Count);
        Assert.True(simulation.State.Performance.Arrivals > 0, "No free arrival reached parking.");
        Assert.True(simulation.State.Performance.Departures > 0, "No free departure completed its route.");
        Assert.Equal(0.25, simulation.State.Options.AircraftPerMinute);
    }

    [Fact]
    public void Bao_filter_and_runway_cursors_survive_save_and_resume_without_reseeding()
    {
        var simulation = Create(["SMALL"]);
        for (var index = 0; index < 6; index++)
        {
            Assert.True(simulation.TryAddFreeTraffic());
            simulation.State.Aircraft.Clear();
        }
        var path = Path.Combine(Path.GetTempPath(), "Tower-Distribution-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            JsonStore.Write(path, simulation.State);
            var restored = new Simulation(JsonStore.LoadSession(path), ["SMALL"]);
            for (var index = 0; index < 20; index++)
            {
                Assert.True(simulation.TryAddFreeTraffic());
                Assert.True(restored.TryAddFreeTraffic());
                Assert.Equal(JsonSerializer.Serialize(simulation.State, JsonStore.Options), JsonSerializer.Serialize(restored.State, JsonStore.Options));
                Assert.Equal("SMALL", Assert.Single(restored.State.Aircraft).CallSign);
                simulation.State.Aircraft.Clear();
                restored.State.Aircraft.Clear();
            }
        }
        finally
        {
            File.Delete(path);
        }
    }
}
