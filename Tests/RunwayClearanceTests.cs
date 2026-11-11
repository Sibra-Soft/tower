using System.Text.Json;
using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class RunwayClearanceTests
{
    private static Simulation CrossingRunways()
    {
        var airport = Airport.Training();
        airport.Nodes.Add("R09", new("R09", "RUNWAY", new(-1500, 0)));
        airport.Nodes.Add("R27", new("R27", "RUNWAY", new(1500, 0)));
        airport.Runways.Add(new()
        {
            Name = "09",
            PhysicalId = "09/27",
            Threshold = "R09",
            End = "R27"
        });
        airport.Runways.Add(new()
        {
            Name = "36",
            PhysicalId = "18/36",
            Threshold = "R36",
            End = "R18"
        });
        return new(new Session { Airport = airport, Options = new() { Runway = "18", AircraftPerMinute = 0 } });
    }

    [Theory]
    [InlineData("CL")]
    [InlineData("CG")]
    [InlineData("CA")]
    [InlineData("CO")]
    [InlineData("TH")]
    [InlineData("CT")]
    public void Clearance_on_a_crossing_runway_does_not_reserve_the_entire_other_runway(string command)
    {
        var simulation = CrossingRunways();
        var other = simulation.AddAircraft(FlightDirection.Arrival, "OTHER", assignedRunway: "09");
        Assert.True(simulation.Execute("OTHER/CL").Success);
        bool departure = command is "TH" or "CT";
        var aircraft = simulation.AddAircraft(departure ? FlightDirection.Departure : FlightDirection.Arrival, "TEST");
        if (departure)
        {
            aircraft.Phase = FlightPhase.HoldingShort;
            aircraft.Position = simulation.State.Airport.Point("H18");
        }
        Assert.True(simulation.Execute("TEST/" + command).Success);
        Assert.True(other.LandingCleared);
    }

    [Theory]
    [InlineData(FlightPhase.LandingRoll)]
    [InlineData(FlightPhase.TakeoffRoll)]
    [InlineData(FlightPhase.LineUp)]
    [InlineData(FlightPhase.LinedUp)]
    [InlineData(FlightPhase.AbortedTakeoff)]
    [InlineData(FlightPhase.TaxiIn)]
    [InlineData(FlightPhase.TaxiOut)]
    public void Actual_crossing_traffic_does_not_reject_a_landing_clearance(FlightPhase phase)
    {
        var simulation = CrossingRunways();
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "TEST");
        var other = simulation.AddAircraft(FlightDirection.Departure, "OTHER", assignedRunway: "09");
        other.Phase = phase;
        other.Position = new(0, 0);
        Assert.True(simulation.Execute("TEST/CL").Success);
        Assert.True(aircraft.LandingCleared);
        other.Position = new(1000, 0);
        Assert.True(simulation.Execute("TEST/CL").Success);
        Assert.True(aircraft.LandingCleared);
    }

    [Theory]
    [InlineData("TH")]
    [InlineData("CT")]
    public void Actual_crossing_traffic_does_not_block_lineup_and_takeoff(string command)
    {
        var simulation = CrossingRunways();
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "TEST");
        aircraft.Phase = FlightPhase.HoldingShort;
        aircraft.Position = simulation.State.Airport.Point("H18");
        var other = simulation.AddAircraft(FlightDirection.Arrival, "OTHER", assignedRunway: "09");
        other.Phase = FlightPhase.LandingRoll;
        other.Position = new(0, 0);
        var before = aircraft.Position;
        Assert.True(simulation.Execute("TEST/" + command).Success);
        Assert.Equal(command == "TH" ? FlightPhase.LineUp : FlightPhase.TakeoffRoll, aircraft.Phase);
        Assert.Equal(before, aircraft.Position);
        Assert.Equal(0, simulation.State.Performance.CommandErrors);
    }

    [Theory]
    [InlineData("18", "CL")]
    [InlineData("18", "CT")]
    [InlineData("18", "TH")]
    [InlineData("36", "CL")]
    [InlineData("36", "CT")]
    [InlineData("36", "TH")]
    [InlineData("09", "CL")]
    [InlineData("09", "CT")]
    [InlineData("09", "TH")]
    public void All_three_clearances_accept_same_opposite_and_crossing_runway_occupancy(string occupiedRunway, string command)
    {
        var simulation = CrossingRunways();
        var aircraft = simulation.AddAircraft(command == "CL" ? FlightDirection.Arrival : FlightDirection.Departure, "TEST");
        if (command != "CL")
        {
            aircraft.Phase = FlightPhase.HoldingShort;
            aircraft.Position = simulation.State.Airport.Point("H18");
        }
        var other = simulation.AddAircraft(FlightDirection.Departure, "OTHER", assignedRunway: occupiedRunway);
        other.Phase = FlightPhase.LinedUp;
        other.Position = new(0, 0);
        var radio = new List<AircraftRadioMessage>();
        simulation.RadioMessage += radio.Add;
        Assert.True(simulation.Execute("TEST/" + command).Success);
        Assert.Equal(0, simulation.State.Performance.CommandErrors);
        Assert.DoesNotContain(radio, message => message.IntentKey == "command.rejected");
        if (command == "CL")
        {
            Assert.True(aircraft.LandingCleared);
        }
        else
        {
            Assert.Equal(command == "TH" ? FlightPhase.LineUp : FlightPhase.TakeoffRoll, aircraft.Phase);
        }
    }

    [Fact]
    public void Unsafe_takeoff_is_accepted_but_actual_collision_is_still_counted()
    {
        var simulation = CrossingRunways();
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "TEST");
        var other = simulation.AddAircraft(FlightDirection.Departure, "OTHER");
        aircraft.Phase = other.Phase = FlightPhase.LinedUp;
        aircraft.Position = other.Position = simulation.State.Airport.Point("R18");
        Assert.True(simulation.Execute("TEST/CT").Success);
        simulation.Advance(1);
        Assert.Equal(1, simulation.State.Performance.Crashes);
        Assert.Equal(1, simulation.State.Performance.RunwaySeparation);
        Assert.Equal(0, simulation.State.Performance.CommandErrors);
        simulation.Advance(1);
        Assert.Equal(1, simulation.State.Performance.Crashes);
    }

    [Theory]
    [InlineData("18")]
    [InlineData("36")]
    public void Conflicting_landings_can_be_cleared_on_the_same_physical_runway(string runway)
    {
        var simulation = CrossingRunways();
        var other = simulation.AddAircraft(FlightDirection.Arrival, "OTHER", assignedRunway: runway);
        Assert.True(simulation.Execute("OTHER/CL").Success);
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "TEST");
        Assert.True(simulation.Execute("TEST/CL").Success);
        Assert.True(other.LandingCleared);
        Assert.True(aircraft.LandingCleared);
    }

    [Theory]
    [InlineData("CL", FlightPhase.LandingRoll)]
    [InlineData("CG", FlightPhase.TakeoffRoll)]
    [InlineData("CA", FlightPhase.LowApproach)]
    [InlineData("CO", FlightPhase.LandingRoll)]
    public void Occupancy_does_not_veto_a_clearance_or_force_a_missed_approach(string command, FlightPhase expected)
    {
        var simulation = CrossingRunways();
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "TEST");
        var other = simulation.AddAircraft(FlightDirection.Departure, "OTHER");
        other.Phase = FlightPhase.LinedUp;
        other.Position = simulation.State.Airport.Point("R18");
        aircraft.Position = other.Position;
        aircraft.AltitudeFeet = 0;
        // Start at touchdown to isolate clearance/occupancy rules from the flare.
        aircraft.TouchdownDistanceMetres = 0;
        var messages = new List<AircraftRadioMessage>();
        simulation.RadioMessage += messages.Add;
        var errors = simulation.State.Performance.CommandErrors;
        Assert.True(simulation.Execute("TEST/" + command).Success);
        Assert.True(aircraft.LandingCleared);
        Assert.Equal(errors, simulation.State.Performance.CommandErrors);
        Assert.DoesNotContain(messages, message => message.IntentKey == "command.rejected");
        simulation.Advance(0.05);
        Assert.Equal(expected, aircraft.Phase);
        Assert.DoesNotContain(messages, message => message.IntentKey == "flight.goaround");
        if (command == "CL")
        {
            Assert.Equal(1, simulation.State.Performance.RunwaySeparation);
            Assert.Equal(1, simulation.State.Performance.Crashes);
        }
    }

    [Fact]
    public void Without_landing_clearance_an_occupied_runway_still_results_in_a_missed_approach()
    {
        var simulation = CrossingRunways();
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "TEST");
        var other = simulation.AddAircraft(FlightDirection.Departure, "OTHER");
        other.Phase = FlightPhase.LinedUp;
        other.Position = aircraft.Position = simulation.State.Airport.Point("R18");
        aircraft.AltitudeFeet = 0;
        aircraft.TouchdownDistanceMetres = 0;
        simulation.Advance(0.05);
        Assert.Equal(FlightPhase.GoAround, aircraft.Phase);
        Assert.Equal("flight.goaround", aircraft.LastRadioMessage!.IntentKey);
    }

    [Fact]
    public void Completed_aircraft_cannot_leave_a_stale_runway_reservation()
    {
        var simulation = CrossingRunways();
        var completed = simulation.AddAircraft(FlightDirection.Arrival, "OLD");
        completed.Phase = FlightPhase.Completed;
        completed.LandingCleared = true;
        completed.Position = new(0, 0);
        simulation.AddAircraft(FlightDirection.Arrival, "TEST");
        Assert.True(simulation.Execute("TEST/CL").Success);
    }

    [Fact]
    public void Replacement_clearance_updates_the_intent_even_during_runway_occupancy()
    {
        var simulation = CrossingRunways();
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "TEST");
        Assert.True(simulation.Execute("TEST/CG").Success);
        var other = simulation.AddAircraft(FlightDirection.Departure, "OTHER", assignedRunway: "09");
        other.Phase = FlightPhase.LandingRoll;
        other.Position = new(0, 0);
        Assert.True(simulation.Execute("TEST/CL").Success);
        Assert.True(aircraft.LandingCleared);
        Assert.Equal(LandingIntent.FullStop, aircraft.LandingIntent);
    }

    [Theory]
    [InlineData("PF")]
    [InlineData("PF5")]
    [InlineData("PF5,18")]
    [InlineData("PF5,36")]
    public void Final_report_rebuilds_final_and_cancels_prior_clearance_including_same_runway(string command)
    {
        var simulation = CrossingRunways();
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "TEST");
        Assert.True(simulation.Execute("TEST/CG").Success);
        Assert.True(simulation.Execute("TEST/" + command).Success);
        Assert.False(aircraft.LandingCleared);
        Assert.Equal(LandingIntent.FullStop, aircraft.LandingIntent);
        Assert.Equal("final", aircraft.CircuitLeg);
        var restored = JsonSerializer.Deserialize<Session>(JsonSerializer.Serialize(simulation.State, JsonStore.Options), JsonStore.Options)!;
        Assert.False(restored.Aircraft.Single().LandingCleared);
        Assert.True(new Simulation(restored).Execute("TEST/CL").Success);
    }

    [Fact]
    public void Invalid_final_report_preserves_the_previous_clearance_and_route()
    {
        var simulation = CrossingRunways();
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "TEST");
        Assert.True(simulation.Execute("TEST/CA").Success);
        var path = aircraft.Path.ToArray();
        foreach (var command in new[] { "PFNaN", "PF5,99", "PF5,18,36" })
        {
            Assert.False(simulation.Execute("TEST/" + command).Success);
            Assert.True(aircraft.LandingCleared);
            Assert.Equal(LandingIntent.LowApproach, aircraft.LandingIntent);
            Assert.Equal(path, aircraft.Path.ToArray());
        }
    }

    [Fact]
    public void Occupied_intersection_is_reported_once_until_the_conflict_clears()
    {
        var simulation = CrossingRunways();
        var first = simulation.AddAircraft(FlightDirection.Departure, "FIRST");
        var second = simulation.AddAircraft(FlightDirection.Departure, "SECOND", assignedRunway: "09");
        first.Phase = second.Phase = FlightPhase.TakeoffRoll;
        first.Holding = second.Holding = true;
        first.Position = new(0, 30);
        second.Position = new(30, 0);
        var log = new List<string>();
        simulation.Message += log.Add;
        simulation.Advance(2);
        Assert.Equal(1, simulation.State.Performance.Incidents);
        Assert.Single(log, m => m.StartsWith("Runway intersection alert:"));
        second.Position = new(1000, 0);
        simulation.Advance(1);
        second.Position = new(30, 0);
        simulation.Advance(1);
        Assert.Equal(2, simulation.State.Performance.Incidents);
    }
}
