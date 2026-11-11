using System.Text.Json;
using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class RunwayUseTests
{
    private static Airport Create()
    {
        var airport = Airport.Training();
        airport.Runways.Add(new() { Name = "36", Threshold = "R36", End = "R18", Hold = "H18", Exit = "E18", Rollout = "X18" });
        return airport;
    }

    [Fact]
    public void Old_airports_and_saves_keep_both_traffic_directions_enabled()
    {
        var runway = JsonSerializer.Deserialize<Runway>("{\"Name\":\"18\"}", JsonStore.Options)!;
        Assert.True(runway.GeneratedArrivals && runway.GeneratedDepartures);
        var airport = Create();
        Assert.Equal(new[] { "18", "36" }, airport.GeneratedRunways(FlightDirection.Arrival, ["18", "36"]).Select(r => r.Name));
        Assert.Equal(new[] { "18", "36" }, airport.GeneratedRunways(FlightDirection.Departure, ["18", "36"]).Select(r => r.Name));
    }

    [Theory]
    [InlineData(FlightDirection.Arrival)]
    [InlineData(FlightDirection.Departure)]
    public void Generator_falls_back_to_a_permitted_runway_and_preserves_flags_in_a_save(FlightDirection direction)
    {
        var airport = Create();
        airport.GetRunway("18").GeneratedArrivals = direction != FlightDirection.Arrival;
        airport.GetRunway("18").GeneratedDepartures = direction != FlightDirection.Departure;
        var restored = JsonSerializer.Deserialize<Airport>(JsonSerializer.Serialize(airport, JsonStore.Options), JsonStore.Options)!;
        Assert.Equal("36", Assert.Single(restored.GeneratedRunways(direction, ["18"])).Name);
        var simulation = new Simulation(new Session { Airport = restored, Options = new() { Runway = "18", AircraftPerMinute = 0 } });
        Assert.Equal("36", simulation.AddAircraft(direction, "AUTO").Runway);
        Assert.Equal("18", simulation.AddAircraft(direction, "EXPLICIT", assignedRunway: "18").Runway);
    }

    [Fact]
    public void No_eligible_runway_never_silently_spawns_on_a_prohibited_direction()
    {
        var airport = Create();
        foreach (var runway in airport.Runways)
        {
            runway.GeneratedDepartures = false;
        }
        Assert.Empty(airport.GeneratedRunways(FlightDirection.Departure, ["18"]));
        var simulation = new Simulation(new Session { Airport = airport, Options = new() { AircraftPerMinute = 0 } });
        Assert.Throws<InvalidOperationException>(() => simulation.AddAircraft(FlightDirection.Departure));
        Assert.Empty(simulation.State.Aircraft);
    }
}
