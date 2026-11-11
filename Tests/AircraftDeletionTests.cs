using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class AircraftDeletionTests
{
    private static Simulation Create() => new(new() { Options = new() { AircraftPerMinute = 0 } });

    [Fact]
    public void Deleting_a_departure_frees_its_gate_without_scoring_or_respawning_after_save_load()
    {
        var simulation = Create();
        var departure = simulation.AddAircraft(FlightDirection.Departure, "BLOCK", assignedGate: "G1");
        var arrival = simulation.AddAircraft(FlightDirection.Arrival, "ARRIVE", assignedGate: "G1");
        arrival.Phase = FlightPhase.ArrivalHold;
        arrival.AltitudeFeet = 0;
        arrival.Position = simulation.State.Airport.Point(simulation.State.Airport.GetRunway(arrival.Runway).Exit);
        arrival.Path.Clear();
        arrival.PathIndex = 0;
        simulation.State.Schedule.Add(new() { CallSign = "BLOCK", Runway = "18", Gate = "G1", Spawned = true });
        // Generic TP may choose another free gate. Request this exact stand to
        // verify that deletion releases G1 rather than testing gate reassignment.
        Assert.False(simulation.Execute("ARRIVE/taxi to gate G1").Success);
        var score = simulation.State.Performance.Score;
        var messages = new List<string>();
        var radio = new List<AircraftRadioMessage>();
        simulation.Message += messages.Add;
        simulation.RadioMessage += radio.Add;

        Assert.True(simulation.DeleteAircraft(departure));
        Assert.False(simulation.DeleteAircraft(departure));
        Assert.DoesNotContain(departure, simulation.State.Aircraft);
        Assert.Equal(score, simulation.State.Performance.Score);
        Assert.Equal(0, simulation.State.Performance.Arrivals);
        Assert.Equal(0, simulation.State.Performance.Departures);
        Assert.Equal("BLOCK: deleted from simulation.", Assert.Single(messages));
        Assert.Empty(radio);
        var parking = simulation.Execute("ARRIVE/taxi to gate G1");
        Assert.True(parking.Success, parking.Message);
        Assert.Equal("G1", arrival.Gate);
        Assert.Equal(simulation.State.Airport.Point("G1"), arrival.Path[^1]);

        var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            JsonStore.Write(file, simulation.State);
            var resumed = new Simulation(JsonStore.LoadSession(file));
            resumed.Advance(1);
            Assert.DoesNotContain(resumed.State.Aircraft, aircraft => aircraft.CallSign == "BLOCK");
            Assert.True(Assert.Single(resumed.State.Schedule).Spawned);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Deleting_a_rolling_departure_removes_its_current_runway_history()
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "ROLLING");
        aircraft.Phase = FlightPhase.LinedUp;
        aircraft.Position = simulation.State.Airport.Point("R18");
        Assert.True(simulation.Execute("ROLLING/CT").Success);
        simulation.Advance(0.05);
        Assert.NotEmpty(simulation.State.RunwayDepartures);
        Assert.True(simulation.DeleteAircraft(aircraft));
        Assert.Empty(simulation.State.RunwayDepartures);
    }

    [Fact]
    public void A_foreign_aircraft_with_the_same_callsign_cannot_delete_the_selected_instance()
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "KEEP");
        Assert.False(simulation.DeleteAircraft(new() { CallSign = "KEEP" }));
        Assert.Same(aircraft, Assert.Single(simulation.State.Aircraft));
    }

    [Fact]
    public void Reusing_a_deleted_callsign_does_not_suppress_new_conflict_alerts()
    {
        var simulation = Create();
        var first = simulation.AddAircraft(FlightDirection.Arrival, "FIRST");
        var second = simulation.AddAircraft(FlightDirection.Arrival, "SECOND");
        second.Position = first.Position;
        second.AltitudeFeet = first.AltitudeFeet;
        simulation.Advance(0.05);
        Assert.Equal(1, simulation.State.Performance.Incidents);
        Assert.True(simulation.DeleteAircraft(first));
        var replacement = simulation.AddAircraft(FlightDirection.Arrival, "FIRST");
        replacement.Position = second.Position;
        replacement.AltitudeFeet = second.AltitudeFeet;
        simulation.Advance(0.05);
        Assert.Equal(2, simulation.State.Performance.Incidents);
    }
}
