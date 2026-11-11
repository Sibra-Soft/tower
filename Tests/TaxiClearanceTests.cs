using System.Text.Json.Nodes;
using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class TaxiClearanceTests
{
    private static Simulation New() => new(new Session { Options = new() { AircraftPerMinute = 0 } });

    private static Aircraft Crossing(Simulation simulation)
    {
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "TAXI");
        aircraft.Phase = FlightPhase.TaxiOut;
        aircraft.Position = new(-200, 0);
        aircraft.Path = [new(200, 0)];
        aircraft.PathIndex = 0;
        return aircraft;
    }

    private static void Until(Simulation simulation, Func<bool> condition)
    {
        for (var second = 0; second < 1200 && !condition(); second++)
        {
            simulation.Advance(1);
        }
        Assert.True(condition(), "Aircraft did not reach the expected state.");
    }

    private static void BrakeToStop(Simulation simulation, Aircraft aircraft)
    {
        var start = aircraft.Position;
        var speed = aircraft.SpeedKnots;
        Assert.True(speed > 0);
        for (var step = 0; step < 200 && aircraft.SpeedKnots > 0; step++)
        {
            simulation.Advance(.05);
            Assert.InRange(aircraft.SpeedKnots, 0, speed);
            speed = aircraft.SpeedKnots;
        }
        Assert.Equal(0, aircraft.SpeedKnots);
        Assert.InRange(aircraft.Position.Distance(start), .001, 30);
    }

    [Fact]
    public void Hold_short_brakes_at_runway_boundary_and_preserves_the_remaining_route()
    {
        var simulation = New();
        var aircraft = Crossing(simulation);
        aircraft.Path = [new(-150, 200), new(200, 200)];
        var route = aircraft.Path.ToArray();
        Assert.True(simulation.Execute("TAXI/HS18").Success);
        Until(simulation, () => aircraft.Holding);
        Assert.Equal(-35.01, aircraft.Position.X, 3);
        Assert.Equal(200, aircraft.Position.Y, 3);
        Assert.True(aircraft.HoldShortReached == true);
        Assert.Equal(route, aircraft.Path);
        var stopped = aircraft.Position;
        simulation.Advance(10);
        Assert.Equal(stopped, aircraft.Position);
        Assert.True(simulation.Execute("TAXI/TU").Success);
        Assert.Null(aircraft.HoldShortRunway);
        Assert.False(aircraft.IsOnRunway);
        simulation.Advance(50);
        Assert.True(aircraft.Position.X > 35);
    }

    [Theory]
    [InlineData(FlightPhase.TaxiIn)]
    [InlineData(FlightPhase.TaxiOut)]
    public void Taxi_crossings_are_automatic_without_an_explicit_HS(FlightPhase phase)
    {
        var simulation = New();
        var aircraft = Crossing(simulation);
        aircraft.Phase = phase;
        simulation.AddAircraft(FlightDirection.Arrival, "ARRIVAL");
        Assert.True(simulation.Execute("ARRIVAL/CL").Success);
        simulation.Advance(60);
        Assert.True(aircraft.Position.X > 35);
        Assert.False(aircraft.Holding);
    }

    [Fact]
    public void Taxi_redirection_releases_the_hold_only_after_the_new_route_is_validated()
    {
        var simulation = New();
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "DEPARTURE");
        Assert.True(simulation.Execute("DEPARTURE/T18/HS18/HY").Success);
        var route = aircraft.Path.ToArray();
        var position = aircraft.Position;
        Assert.False(simulation.Execute("DEPARTURE/T99").Success);
        Assert.True(aircraft.Holding);
        Assert.Equal("18", aircraft.HoldShortRunway);
        Assert.Equal(route, aircraft.Path);
        Assert.True(simulation.Execute("DEPARTURE/TP").Success);
        Assert.False(aircraft.Holding);
        Assert.Null(aircraft.HoldShortRunway);
        Assert.True(aircraft.HoldShortReached == false);
        Assert.Equal(position, aircraft.Position);
    }

    [Fact]
    public void Continue_after_HY_preserves_a_future_HS_and_continue_after_HS_releases_it()
    {
        var simulation = New();
        var aircraft = Crossing(simulation);
        Assert.True(simulation.Execute("TAXI/HS18").Success);
        simulation.Advance(2);
        Assert.True(simulation.Execute("TAXI/HY").Success);
        BrakeToStop(simulation, aircraft);
        var position = aircraft.Position;
        simulation.Advance(5);
        Assert.Equal(position, aircraft.Position);
        Assert.True(simulation.Execute("TAXI/TU").Success);
        Assert.Equal("18", aircraft.HoldShortRunway);
        Until(simulation, () => aircraft.Holding);
        Assert.True(aircraft.HoldShortReached == true);
        Assert.True(simulation.Execute("TAXI/TU").Success);
        Assert.Null(aircraft.HoldShortRunway);
    }

    [Fact]
    public void HS_targets_the_requested_crossing_after_other_runways()
    {
        var simulation = New();
        var airport = simulation.State.Airport;
        airport.Nodes.Add("R09", new("R09", "RUNWAY", new(200, 1500)));
        airport.Nodes.Add("R27", new("R27", "RUNWAY", new(200, -1500)));
        airport.Runways.Add(new()
        {
            Name = "09",
            PhysicalId = "09/27",
            Threshold = "R09",
            End = "R27"
        });
        var aircraft = Crossing(simulation);
        aircraft.Path = [new(400, 0)];
        Assert.True(simulation.Execute("TAXI/HS09").Success);
        Until(simulation, () => aircraft.Holding);
        Assert.Equal(164.99, aircraft.Position.X, 3);
        Assert.Equal("09", aircraft.HoldShortRunway);
    }

    [Fact]
    public void Reciprocal_runway_name_holds_at_the_same_physical_runway()
    {
        var simulation = New();
        simulation.State.Airport.Runways.Add(new()
        {
            Name = "36",
            Threshold = "R36",
            End = "R18"
        });
        var aircraft = Crossing(simulation);
        Assert.True(simulation.Execute("TAXI/HS36").Success);
        Until(simulation, () => aircraft.Holding);
        Assert.Equal(-35.01, aircraft.Position.X, 3);
        Assert.Equal("36", aircraft.HoldShortRunway);
    }

    [Fact]
    public void HS_rejects_a_late_instruction_without_discarding_a_previous_hold()
    {
        var simulation = New();
        var aircraft = Crossing(simulation);
        aircraft.Position = new(0, 0);
        aircraft.Holding = true;
        var position = aircraft.Position;
        var route = aircraft.Path.ToArray();
        Assert.False(simulation.Execute("TAXI/HS18").Success);
        Assert.True(aircraft.Holding);
        Assert.Null(aircraft.HoldShortRunway);
        Assert.Equal(position, aircraft.Position);
        Assert.Equal(route, aircraft.Path);
    }

    [Fact]
    public void HS_does_not_stop_at_the_infinite_extension_of_a_runway()
    {
        var simulation = New();
        var aircraft = Crossing(simulation);
        aircraft.Position = new(-200, 1700);
        aircraft.Path = [new(200, 1700)];
        Assert.True(simulation.Execute("TAXI/HS18").Success);
        simulation.Advance(100);
        Assert.False(aircraft.Holding);
        Assert.True(aircraft.Position.X > 35);
    }

    [Fact]
    public void Lineup_can_be_paused_resumed_and_cleared_for_takeoff_without_snapping()
    {
        var simulation = New();
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "DEPARTURE");
        Assert.True(simulation.Execute("DEPARTURE/T18/HS18").Success);
        Until(simulation, () => aircraft.Phase == FlightPhase.HoldingShort);
        Assert.False(simulation.Execute("DEPARTURE/TU").Success);
        Assert.True(simulation.Execute("DEPARTURE/TH").Success);
        Assert.Null(aircraft.HoldShortRunway);
        simulation.Advance(2);
        Assert.True(simulation.Execute("DEPARTURE/HY").Success);
        BrakeToStop(simulation, aircraft);
        var position = aircraft.Position;
        simulation.Advance(5);
        Assert.Equal(position, aircraft.Position);
        Assert.True(simulation.Execute("DEPARTURE/TU").Success);
        Assert.Equal(FlightPhase.LineUp, aircraft.Phase);
        Assert.True(simulation.Execute("DEPARTURE/CT").Success);
        Assert.Equal(position, aircraft.Position);
        simulation.Advance(0.05);
        Assert.True(aircraft.Position.Distance(position) < 2);
        Assert.True(aircraft.SpeedKnots < 15);
    }

    [Fact]
    public void Cancellation_during_threshold_access_retains_the_route_and_only_CT_resumes()
    {
        var simulation = New();
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "DEPARTURE");
        Assert.True(simulation.Execute("DEPARTURE/T18").Success);
        Until(simulation, () => aircraft.Phase == FlightPhase.HoldingShort);
        Assert.True(simulation.Execute("DEPARTURE/CT").Success);
        simulation.Advance(2);
        var route = aircraft.Path.ToArray();
        Assert.True(simulation.Execute("DEPARTURE/CC").Success);
        Assert.False(simulation.Execute("DEPARTURE/CT").Success);
        Until(simulation, () => aircraft.SpeedKnots == 0);
        var position = aircraft.Position;
        foreach (var command in new[] { "TH", "HY", "TU", "TP", "HS18", "SG", "SS" })
        {
            Assert.False(simulation.Execute("DEPARTURE/" + command).Success);
            Assert.Equal(FlightPhase.AbortedTakeoff, aircraft.Phase);
            Assert.Equal(position, aircraft.Position);
            Assert.Equal(route, aircraft.Path);
        }
        Assert.True(simulation.Execute("DEPARTURE/CT").Success);
        Assert.Equal(position, aircraft.Position);
        simulation.Advance(0.05);
        Assert.True(aircraft.Position.Distance(position) < 2);
    }

    [Fact]
    public void Terminal_HS_with_an_inside_hold_node_accepts_TH_without_requiring_a_crossing_TU()
    {
        var simulation = New();
        var airport = simulation.State.Airport;
        airport.Nodes["H18"] = airport.Nodes["H18"] with
        {
            Position = new(20, 1450)
        };
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "DEPARTURE");
        Assert.True(simulation.Execute("DEPARTURE/T18/HS18").Success);
        Until(simulation, () => aircraft.Holding);
        Assert.Equal(FlightPhase.HoldingShort, aircraft.Phase);
        Assert.True(aircraft.HoldShortReached == true);
        Assert.True(aircraft.Position.X > 35);
        var restored = RoundTrip(simulation.State);
        Assert.False(restored.Execute("DEPARTURE/TU").Success);
        Assert.True(restored.Execute("DEPARTURE/TH").Success);
        Assert.Null(restored.State.Aircraft[0].HoldShortRunway);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Original_airports_allow_HS_lineup_and_takeoff_clearance_on_every_runway()
    {
        var data = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA");
        if (string.IsNullOrWhiteSpace(data))
        {
            Assert.Skip("Original game data not supplied.");
        }
        foreach (var code in new[] { "CYN" })
        {
            var airport = OriginalData.LoadAirport(data, code);
            var type = airport.AircraftTypes.Values.OrderBy(spec => spec.TakeoffDistanceFeet ?? double.MaxValue).First().Type;
            foreach (var runway in airport.Runways)
            {
                var simulation = new Simulation(new Session { Airport = airport, Options = new() { Runway = runway.Name, AircraftPerMinute = 0 } });
                var aircraft = simulation.AddAircraft(FlightDirection.Departure, "VERIFY", type);
                if (aircraft.Pushback is not null)
                {
                    Assert.True(simulation.Execute("VERIFY/PB").Success);
                    Until(simulation, () => aircraft.Phase == FlightPhase.ReadyForTaxi);
                }
                Assert.True(simulation.Execute("VERIFY/T" + runway.Name + "/HS" + runway.Name).Success, code + " " + runway.Name);
                Until(simulation, () => aircraft.Holding || aircraft.Phase == FlightPhase.HoldingShort);
                if (aircraft.Phase == FlightPhase.TaxiOut)
                {
                    Assert.True(simulation.Execute("VERIFY/TU").Success);
                    Until(simulation, () => aircraft.Phase == FlightPhase.HoldingShort);
                }
                Assert.True(simulation.Execute("VERIFY/TH").Success, code + " " + runway.Name);
                var position = aircraft.Position;
                Assert.True(simulation.Execute("VERIFY/CT").Success, code + " " + runway.Name);
                Assert.Equal(position, aircraft.Position);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Saved_session_distinguishes_a_future_HS_from_a_reached_HS(bool reached)
    {
        var simulation = New();
        var aircraft = Crossing(simulation);
        Assert.True(simulation.Execute("TAXI/HS18").Success);
        if (reached)
        {
            Until(simulation, () => aircraft.Holding);
        }
        else
        {
            Assert.True(simulation.Execute("TAXI/HY").Success);
        }
        var restored = RoundTrip(simulation.State);
        Assert.True(restored.Execute("TAXI/TU").Success);
        Assert.Equal(reached ? null : "18", restored.State.Aircraft[0].HoldShortRunway);
    }

    [Fact]
    public void Legacy_saved_HS_stop_can_still_be_released_with_TU()
    {
        var simulation = New();
        var aircraft = Crossing(simulation);
        Assert.True(simulation.Execute("TAXI/HS18").Success);
        Until(simulation, () => aircraft.Holding);
        var restored = RoundTrip(simulation.State, removeHoldReason: true);
        Assert.Null(restored.State.Aircraft[0].HoldShortReached);
        Assert.True(restored.Execute("TAXI/TU").Success);
        Assert.Null(restored.State.Aircraft[0].HoldShortRunway);
    }

    [Theory]
    [InlineData(FlightPhase.TaxiOut, false, "18")]
    [InlineData(FlightPhase.TaxiOut, true, null)]
    [InlineData(FlightPhase.LinedUp, true, "18")]
    public void Saved_sessions_reject_inconsistent_reached_hold_flags(FlightPhase phase, bool holding, string? runway)
    {
        var simulation = New();
        var aircraft = Crossing(simulation);
        aircraft.Phase = phase;
        aircraft.Holding = holding;
        aircraft.HoldShortRunway = runway;
        aircraft.HoldShortReached = true;
        Assert.Throws<InvalidDataException>(() => RoundTrip(simulation.State));
    }

    [Theory]
    [InlineData("MS")]
    [InlineData("ML3")]
    [InlineData("SA/SH/SR/SP/SS/SG")]
    public void Reports_short_approach_and_full_orbit_retain_current_clearance(string command)
    {
        var simulation = New();
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "ARRIVAL");
        Assert.True(simulation.Execute("ARRIVAL/CG").Success);
        Assert.True(simulation.Execute("ARRIVAL/" + command).Success);
        Assert.True(aircraft.LandingCleared);
        Assert.Equal(LandingIntent.TouchAndGo, aircraft.LandingIntent);
    }

    [Theory]
    [InlineData("G")]
    [InlineData("B")]
    [InlineData("ULH90")]
    [InlineData("ML1")]
    [InlineData("EF18")]
    [InlineData("PF")]
    [InlineData("PF5,18")]
    public void Leaving_the_approach_or_reentering_the_circuit_discards_old_landing_intent(string command)
    {
        var simulation = New();
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "ARRIVAL");
        Assert.True(simulation.Execute("ARRIVAL/CG").Success);
        Assert.True(simulation.Execute("ARRIVAL/" + command).Success);
        Assert.False(aircraft.LandingCleared);
        Assert.Equal(LandingIntent.FullStop, aircraft.LandingIntent);
    }

    [Fact]
    public void Runway_change_cancels_clearance_and_rejected_commands_preserve_it()
    {
        var simulation = New();
        simulation.State.Airport.Runways.Add(new()
        {
            Name = "36",
            Threshold = "R36",
            End = "R18"
        });
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "ARRIVAL");
        Assert.True(simulation.Execute("ARRIVAL/CG").Success);
        Assert.False(simulation.Execute("ARRIVAL/EF99").Success);
        Assert.False(simulation.Execute("ARRIVAL/HS18").Success);
        Assert.True(aircraft.LandingCleared);
        Assert.Equal(LandingIntent.TouchAndGo, aircraft.LandingIntent);
        Assert.True(simulation.Execute("ARRIVAL/PF5,36").Success);
        Assert.False(aircraft.LandingCleared);
        Assert.Equal(LandingIntent.FullStop, aircraft.LandingIntent);
    }

    private static Simulation RoundTrip(Session session, bool removeHoldReason = false)
    {
        var directory = Path.Combine(Path.GetTempPath(), "TowerTaxiTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "session.json");
            JsonStore.Write(path, session);
            if (removeHoldReason)
            {
                var document = JsonNode.Parse(File.ReadAllText(path))!;
                document["Aircraft"]![0]!.AsObject().Remove("HoldShortReached");
                File.WriteAllText(path, document.ToJsonString());
            }
            return new Simulation(JsonStore.LoadSession(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
