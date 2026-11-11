using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class RadioOnceTests
{
    private static Simulation Create() => new(new Session { Options = new() { AircraftPerMinute = 0 } });

    [Theory]
    [InlineData(FlightPhase.AtGate, false)]
    [InlineData(FlightPhase.AtGate, true)]
    [InlineData(FlightPhase.ReadyForTaxi, false)]
    [InlineData(FlightPhase.HoldingShort, false)]
    [InlineData(FlightPhase.LinedUp, false)]
    [InlineData(FlightPhase.ArrivalHold, false)]
    public void Waiting_in_the_same_situation_does_not_repeat_radio(FlightPhase phase, bool pushback)
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "ONE");
        aircraft.Phase = phase;
        if (phase == FlightPhase.ArrivalHold)
        {
            aircraft.Direction = FlightDirection.Arrival;
        }
        if (pushback)
        {
            aircraft.Pushback = new();
        }
        aircraft.WaitingSeconds = 119.99;
        var radio = new List<AircraftRadioMessage>();
        simulation.RadioMessage += radio.Add;
        simulation.Advance(361);
        Assert.Empty(radio);
    }

    [Fact]
    public void A_new_situation_can_report_again_and_SG_deliberately_repeats()
    {
        var simulation = Create();
        var radio = new List<AircraftRadioMessage>();
        simulation.RadioMessage += radio.Add;
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "ONE");
        Assert.Single(radio, message => message.IntentKey == "departure.ready");
        simulation.Advance(361);
        Assert.Single(radio);
        Assert.True(simulation.Execute("ONE/T18").Success);
        Until(simulation, () => aircraft.Phase == FlightPhase.HoldingShort);
        Assert.Single(radio, message => message.IntentKey == "departure.takeoff.ready");
        simulation.Advance(361);
        Assert.Single(radio, message => message.IntentKey == "departure.takeoff.ready");
        Assert.True(simulation.Execute("ONE/SG").Success);
        Assert.Equal(2, radio.Count(message => message.IntentKey == "departure.takeoff.ready"));
        Assert.True(simulation.Execute("ONE/TP").Success);
        Until(simulation, () => aircraft.Phase == FlightPhase.AtGate);
        Assert.Equal(2, radio.Count(message => message.IntentKey == "departure.ready"));
        var count = radio.Count;
        simulation.Advance(361);
        Assert.Equal(count, radio.Count);
    }

    [Fact]
    public void Loading_a_waiting_aircraft_does_not_restart_periodic_requests()
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "ONE");
        aircraft.WaitingSeconds = 119.99;
        var previous = aircraft.LastRadioMessage;
        var path = Path.GetTempFileName();
        try
        {
            JsonStore.Write(path, simulation.State);
            simulation = new Simulation(JsonStore.LoadSession(path));
            var radio = new List<AircraftRadioMessage>();
            simulation.RadioMessage += radio.Add;
            simulation.Advance(361);
            Assert.Empty(radio);
            Assert.True(simulation.Execute("ONE/SG").Success);
            Assert.Equal(previous, Assert.Single(radio));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Say_again_repeats_the_previous_message_even_while_blocked_by_ground_traffic(bool legacy)
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "ONE", assignedGate: "G1");
        var other = simulation.AddAircraft(FlightDirection.Departure, "TWO", assignedGate: "G2");
        Assert.True(simulation.Execute("ONE/T18").Success);
        other.Position = aircraft.Position.Towards(aircraft.Path[aircraft.PathIndex], 20);
        simulation.Advance(1);
        var previous = aircraft.LastRadioMessage;
        if (legacy)
        {
            aircraft.LastRadioMessage = null;
        }
        var radio = new List<AircraftRadioMessage>();
        simulation.RadioMessage += radio.Add;
        Assert.True(simulation.Execute("ONE/SG").Success);
        var repeated = Assert.Single(radio);
        Assert.Equal(previous!.Text, repeated.Text);
        if (!legacy)
        {
            Assert.Equal(previous, repeated);
        }
    }

    private static void Until(Simulation simulation, Func<bool> ready)
    {
        for (var step = 0; step < 1800 && !ready(); step++)
        {
            simulation.Advance(1);
        }
        Assert.True(ready());
    }
}
