using Tower.Core;
using Xunit;

namespace Tower.Tests;

public sealed class TrafficCallsignTests
{
    private static Session Session() => new() { Options = new() { AircraftPerMinute = 0 } };

    [Fact]
    public void Bao_traffic_filters_airport_pool_preserves_type_and_never_invents_a_callsign()
    {
        var state = Session();
        state.Airport.TrafficPool = [new("UNKNOWN99", "B738"), new("AAL1535", "C172")];
        var simulation = new Simulation(state, ["aal1535", "UAL412"]);
        var first = simulation.AddAircraft(FlightDirection.Arrival);
        Assert.Equal("AAL1535", first.CallSign);
        Assert.Equal("C172", first.Type);
        Assert.Equal("UAL412", simulation.AddAircraft(FlightDirection.Arrival).CallSign);
        Assert.Throws<InvalidOperationException>(() => simulation.AddAircraft(FlightDirection.Arrival));
        Assert.Throws<InvalidOperationException>(() => simulation.AddAircraft(FlightDirection.Arrival, "TWR999"));
        Assert.Equal(2, state.Aircraft.Count);
    }

    [Fact]
    public void Bao_automatic_traffic_waits_when_all_recorded_callsigns_are_in_use()
    {
        var state = Session();
        state.Options.AircraftPerMinute = 60;
        var simulation = new Simulation(state, ["AAL1535"]);
        simulation.AddAircraft(FlightDirection.Arrival);
        simulation.Advance(3);
        Assert.Single(state.Aircraft);
        Assert.Equal("AAL1535", state.Aircraft[0].CallSign);
    }

    [Fact]
    public void Bao_callsign_is_reused_only_after_completion()
    {
        var state = Session();
        var simulation = new Simulation(state, ["AAL1535"]);
        var first = simulation.AddAircraft(FlightDirection.Arrival);
        Assert.Throws<InvalidOperationException>(() => simulation.AddAircraft(FlightDirection.Arrival));
        first.Phase = FlightPhase.Completed;
        var next = simulation.AddAircraft(FlightDirection.Arrival);
        Assert.NotSame(first, next);
        Assert.Equal("AAL1535", next.CallSign);
        Assert.Same(next, Assert.Single(state.Aircraft));
    }

    [Fact]
    public void Bao_generated_traffic_reserves_scheduled_callsigns()
    {
        var state = Session();
        state.Schedule.Add(new()
        {
            CallSign = "AAL1535",
            TimeSeconds = 2,
            Direction = FlightDirection.Arrival
        });
        var simulation = new Simulation(state, ["AAL1535", "UAL412"]);
        Assert.Equal("UAL412", simulation.AddAircraft(FlightDirection.Arrival).CallSign);
        simulation.Advance(3);
        Assert.True(state.Schedule[0].Spawned);
        Assert.Contains(state.Aircraft, a => a.CallSign == "AAL1535");
    }

    [Fact]
    public void Bao_rejects_unsupported_saved_and_scheduled_callsigns_before_running()
    {
        var state = Session();
        new Simulation(state).AddAircraft(FlightDirection.Arrival, "CUSTOM1");
        Assert.Contains("CUSTOM1", Assert.Throws<InvalidDataException>(() => new Simulation(state, ["AAL1535"])).Message);
        state.Aircraft.Clear();
        state.Schedule.Add(new()
        {
            CallSign = "CUSTOM2"
        });
        Assert.Contains("CUSTOM2", Assert.Throws<InvalidDataException>(() => new Simulation(state, ["AAL1535"])).Message);
        Assert.Throws<InvalidDataException>(() => new Simulation(Session(), []));
    }

    [Fact]
    public void Windows_traffic_retains_airport_pool_and_generated_callsigns()
    {
        var state = Session();
        state.Airport.TrafficPool.Add(new("CUSTOM99", "C172"));
        var simulation = new Simulation(state);
        Assert.Equal("CUSTOM99", simulation.AddAircraft(FlightDirection.Arrival).CallSign);
        Assert.StartsWith("TWR", simulation.AddAircraft(FlightDirection.Arrival).CallSign);
        Assert.Equal("CUSTOM55", simulation.AddAircraft(FlightDirection.Arrival, "CUSTOM55").CallSign);
    }
}
