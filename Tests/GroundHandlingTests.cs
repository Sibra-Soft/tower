using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class GroundHandlingTests
{
    private static Simulation Create()
    {
        var airport = Airport.Training();
        airport.AircraftTypes["C172"] = new() { Type = "C172", WingSpanMetres = 11, GroundFootprint = new(-8, 8, -6, 6) };
        airport.TrafficPool = [new("FREE1", "C172"), new("FREE2", "C172"), new("FREE3", "C172")];
        return new(Simulation.CreateFreeSession(airport, new() { AircraftPerMinute = 0 }, seed: 42));
    }

    [Fact]
    public void Automatically_placed_manual_departure_avoids_an_inbound_reserved_stand()
    {
        var simulation = Create();
        simulation.AddAircraft(FlightDirection.Arrival, "INBOUND", "C172", assignedGate: "G1", useTrafficFlow: false);
        var departure = simulation.AddAircraft(FlightDirection.Departure, "DEPART", "C172");
        Assert.NotEqual("G1", departure.Gate);
    }

    [Fact]
    public void Airborne_gate_reservation_survives_save_and_is_released_when_the_arrival_is_deleted()
    {
        var simulation = Create();
        simulation.AddAircraft(FlightDirection.Arrival, "INBOUND", "C172", assignedGate: "G1", useTrafficFlow: false);
        simulation.AddAircraft(FlightDirection.Departure, "STAND2", "C172", assignedGate: "G2");
        simulation.AddAircraft(FlightDirection.Departure, "STAND3", "C172", assignedGate: "G3");
        Assert.False(simulation.TryAddFreeTraffic());
        var path = Path.GetTempFileName();
        try
        {
            JsonStore.Write(path, simulation.State);
            simulation = new(JsonStore.LoadSession(path));
            Assert.False(simulation.TryAddFreeTraffic());
            Assert.True(simulation.DeleteAircraft(simulation.State.Aircraft.Single(aircraft => aircraft.CallSign == "INBOUND")));
            Assert.True(simulation.TryAddFreeTraffic());
            Assert.Equal("G1", simulation.State.Aircraft.Single(aircraft => aircraft.FreeTraffic is not null).Gate);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Parking_request_reassigns_a_blocked_gate_reserves_the_new_gate_and_releases_it_after_completion()
    {
        var simulation = Create();
        var arrival = simulation.AddAircraft(FlightDirection.Arrival, "PARK", "C172", assignedGate: "G1", useTrafficFlow: false);
        arrival.Phase = FlightPhase.ArrivalHold;
        arrival.Position = simulation.State.Airport.Point("E18");
        arrival.Path = [];
        arrival.PathIndex = 0;
        var blocker = simulation.AddAircraft(FlightDirection.Departure, "BLOCK", "C172", assignedGate: "G1");
        simulation.AddAircraft(FlightDirection.Arrival, "RESERVED", "C172", assignedGate: "G2", useTrafficFlow: false);
        var log = new List<string>();
        simulation.Message += log.Add;
        Assert.False(simulation.Execute("PARK/taxi to gate G1").Success);
        Assert.Equal("G1", arrival.Gate);
        Assert.True(simulation.Execute("PARK/TP").Success);
        Assert.Equal("G3", arrival.Gate);
        Assert.Equal(simulation.State.Airport.Point("G3"), arrival.Path[^1]);
        Assert.True(simulation.DeleteAircraft(blocker));
        Assert.True(simulation.TryAddFreeTraffic());
        Assert.Equal("G1", simulation.State.Aircraft.Single(aircraft => aircraft.FreeTraffic is not null).Gate);
        for (var second = 0; second < 1200 && arrival.Phase != FlightPhase.Completed; second++)
        {
            simulation.Advance(1);
        }
        Assert.Equal(FlightPhase.Completed, arrival.Phase);
        Assert.Equal(1, simulation.State.Performance.Arrivals);
        Assert.Contains(log, text => text.Contains("parked at G3", StringComparison.Ordinal));
        Assert.True(simulation.TryAddFreeTraffic());
        Assert.Contains(simulation.State.Aircraft, aircraft => aircraft.FreeTraffic is not null && aircraft.Gate == "G3");
    }

    [Fact]
    public void Model_overhang_blocks_a_stand_even_when_the_origin_is_more_than_forty_metres_away()
    {
        var simulation = Create();
        var blocker = simulation.AddAircraft(FlightDirection.Departure, "LONG", "C172", assignedGate: "G1");
        blocker.Specification = new() { WingSpanMetres = 20, GroundFootprint = new(-20, 80, -10, 10) };
        blocker.Position = blocker.Position.Offset(180, 60);
        blocker.Heading = 0;
        simulation.AddAircraft(FlightDirection.Departure, "STAND2", "C172", assignedGate: "G2");
        simulation.AddAircraft(FlightDirection.Departure, "STAND3", "C172", assignedGate: "G3");
        Assert.False(simulation.TryAddFreeTraffic());
        blocker.Position = blocker.Position.Offset(180, 150);
        Assert.True(simulation.TryAddFreeTraffic());
        Assert.Equal("G1", simulation.State.Aircraft.Single(aircraft => aircraft.FreeTraffic is not null).Gate);
    }

    [Fact]
    public void Taxi_follows_the_bend_without_stopping_for_an_aircraft_beyond_the_straight_extension()
    {
        var simulation = Create();
        var moving = simulation.AddAircraft(FlightDirection.Departure, "MOVING", "C172", assignedGate: "G1");
        var other = simulation.AddAircraft(FlightDirection.Departure, "OTHER", "C172", assignedGate: "G2");
        moving.Specification = new() { WingSpanMetres = 8, GroundFootprint = new(-10, 40, -4, 4) };
        moving.Phase = FlightPhase.TaxiOut;
        moving.Position = new(5000, 5000);
        moving.Heading = 0;
        moving.Path = [new(5000, 5100), new(5200, 5100)];
        moving.PathIndex = 0;
        other.Position = new(5000, 5150);
        other.Specification = new() { WingSpanMetres = 10, GroundFootprint = new(-20, 10, -5, 5) };
        other.Heading = 0;
        other.Phase = FlightPhase.HoldingShort;
        for (var second = 0; second < 120 && moving.Phase != FlightPhase.HoldingShort; second++)
        {
            simulation.Advance(1);
        }
        Assert.Equal(FlightPhase.HoldingShort, moving.Phase);
        Assert.InRange(moving.Position.Distance(new(5200, 5100)), 0, 0.01);
    }

    [Fact]
    public void Queue_behind_a_bend_stays_silent_and_resumes_after_save_and_release()
    {
        var simulation = Create();
        var following = simulation.AddAircraft(FlightDirection.Departure, "FOLLOW", "C172", assignedGate: "G1");
        var leading = simulation.AddAircraft(FlightDirection.Departure, "LEAD", "C172", assignedGate: "G2");
        following.Specification = new() { WingSpanMetres = 8, GroundFootprint = new(-10, 40, -4, 4) };
        following.Phase = FlightPhase.TaxiOut;
        following.Position = new(5000, 5000);
        following.Heading = 0;
        following.Path = [new(5000, 5100), new(5200, 5100)];
        following.PathIndex = 0;
        leading.Position = new(5020, 5100);
        leading.Specification = new() { WingSpanMetres = 16, GroundFootprint = new(-30, 30, -8, 8) };
        leading.Heading = 90;
        leading.Phase = FlightPhase.HoldingShort;
        var messages = new List<AircraftRadioMessage>();
        simulation.RadioMessage += messages.Add;
        simulation.Advance(40);
        Assert.Equal(0, following.SpeedKnots);
        Assert.True(following.Position.Y < 5090);
        Assert.True(Math.Abs(RouteMovement.AngleDifference(following.Heading, leading.Heading)) >= 60);
        Assert.True(simulation.Execute("FOLLOW/SS").Success);
        Assert.DoesNotContain(messages, message => message.IntentKey == "ground.blocked");
        var path = Path.GetTempFileName();
        try
        {
            JsonStore.Write(path, simulation.State);
            simulation = new(JsonStore.LoadSession(path));
            following = simulation.State.Aircraft.Single(aircraft => aircraft.CallSign == "FOLLOW");
            var stopped = following.Position;
            simulation.Advance(5);
            Assert.Equal(stopped, following.Position);
            Assert.True(simulation.DeleteAircraft(simulation.State.Aircraft.Single(aircraft => aircraft.CallSign == "LEAD")));
            for (var second = 0; second < 120 && following.Phase != FlightPhase.HoldingShort; second++)
            {
                simulation.Advance(1);
            }
            Assert.Equal(FlightPhase.HoldingShort, following.Phase);
            Assert.InRange(following.Position.Distance(new(5200, 5100)), 0, 0.01);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
