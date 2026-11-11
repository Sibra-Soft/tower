using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class SimulationCycleTests
{
    [Fact]
    public void Canyon_base_cycle_is_also_checked_in_ci()
    {
        Arrival_and_departure_complete_with_real_data_and_resume_during_taxi("CYN", "9");
    }
    [Theory]
    [InlineData("CYN", "9")]
    [Trait("Category", "Integration")]
    public void Arrival_and_departure_complete_with_real_data_and_resume_during_taxi(string code, string runway)
    {
        var data = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA");
        if (string.IsNullOrWhiteSpace(data))
        {
            Assert.Skip("Set TOWER_ORIGINAL_DATA to verify complete original-airport cycles.");
            return;
        }
        var simulation = new Simulation(new Session
        {
            Airport = OriginalData.LoadAirport(data, code),
            Options = new() { Runway = runway, AircraftPerMinute = 0 }
        });
        var radio = new List<AircraftRadioMessage>();
        var log = new List<string>();
        simulation.RadioMessage += radio.Add;
        simulation.Message += log.Add;
        var arrival = simulation.AddAircraft(FlightDirection.Arrival, "ARRCYCLE", "C182", useTrafficFlow: false);
        Command(simulation, "ARRCYCLE/CL");
        Until(simulation, arrival, () => arrival.Phase == FlightPhase.ArrivalHold);
        Assert.Contains(radio, message => message.IntentKey == "ground.parking.request");
        Command(simulation, "ARRCYCLE/TP");
        simulation.Advance(10);
        simulation = Resume(simulation, radio, log);
        arrival = simulation.State.Aircraft.Single();
        Until(simulation, arrival, () => arrival.Phase == FlightPhase.Completed, releaseCrossings: true);
        Assert.Equal(1, simulation.State.Performance.Arrivals);
        Assert.True(arrival.Position.Distance(simulation.State.Airport.Point(arrival.Gate)) < 0.01);
        Assert.Contains(log, message => message.Contains("parked at", StringComparison.Ordinal));
        Assert.Single(radio, message => message.IntentKey == "ground.parking.request");
        Assert.DoesNotContain(radio, message => message.Text.Contains("parked at", StringComparison.Ordinal));

        var departure = simulation.AddAircraft(FlightDirection.Departure, "DEPCYCLE", "C182", useTrafficFlow: false);
        if (departure.Pushback is not null)
        {
            Command(simulation, "DEPCYCLE/PB");
            Until(simulation, departure, () => departure.Phase == FlightPhase.ReadyForTaxi);
        }
        Command(simulation, "DEPCYCLE/T" + runway);
        simulation.Advance(10);
        simulation = Resume(simulation, radio, log);
        departure = simulation.State.Aircraft.Single(aircraft => aircraft.CallSign == "DEPCYCLE");
        Until(simulation, departure, () => departure.Phase == FlightPhase.HoldingShort, releaseCrossings: true);
        Command(simulation, "DEPCYCLE/TH");
        Until(simulation, departure, () => departure.Phase == FlightPhase.LinedUp);
        Command(simulation, "DEPCYCLE/CT");
        Until(simulation, departure, () => departure.Phase == FlightPhase.Departing);
        Assert.True(departure.AltitudeFeet > 0);
        simulation = Resume(simulation, radio, log);
        departure = simulation.State.Aircraft.Single(aircraft => aircraft.CallSign == "DEPCYCLE");
        Until(simulation, departure, () => departure.Phase == FlightPhase.Completed);
        Assert.Equal(1, simulation.State.Performance.Departures);
        Assert.Equal(1, simulation.State.Performance.Arrivals);
        Assert.DoesNotContain(radio, message => message.Text.Contains("going around", StringComparison.OrdinalIgnoreCase));
    }

    private static Simulation Resume(Simulation simulation, List<AircraftRadioMessage> radio, List<string> log)
    {
        var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            JsonStore.Write(file, simulation.State);
            var resumed = new Simulation(JsonStore.LoadSession(file));
            resumed.RadioMessage += radio.Add;
            resumed.Message += log.Add;
            return resumed;
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static void Command(Simulation simulation, string command)
    {
        var result = simulation.Execute(command);
        Assert.True(result.Success, command + ": " + result.Message);
    }

    private static void Until(Simulation simulation, Aircraft aircraft, Func<bool> condition, bool releaseCrossings = false)
    {
        for (var second = 0; second < 1800 && !condition(); second++)
        {
            if (releaseCrossings && aircraft.Holding && aircraft.Phase is FlightPhase.TaxiIn or FlightPhase.TaxiOut)
            {
                Command(simulation, aircraft.CallSign + "/TU");
            }
            simulation.Advance(1);
        }
        Assert.True(condition(), $"{simulation.State.Airport.Code}/{aircraft.Runway} {aircraft.CallSign}: {aircraft.Phase}, position {aircraft.Position}, speed {aircraft.SpeedKnots}, path {aircraft.PathIndex}/{aircraft.Path.Count}, holding {aircraft.Holding}.");
    }
}
