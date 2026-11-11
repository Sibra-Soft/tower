using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class RouteMovementTests
{
    [Theory]
    [InlineData("ALT", 87)]
    [InlineData("ART", 93)]
    public void Turnout_commands_start_a_rate_limited_turn_instead_of_changing_heading_immediately(string command, double expected)
    {
        var simulation = new Simulation(new Session { Options = new() { AircraftPerMinute = 0 } });
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "TURN");
        aircraft.Position = new(500, 0);
        aircraft.Phase = FlightPhase.Departing;
        aircraft.AltitudeFeet = 1000;
        aircraft.SpeedKnots = 140;
        aircraft.Heading = 90;
        Assert.True(simulation.Execute("TURN/" + command).Success);
        Assert.Equal(90, aircraft.Heading);
        simulation.Advance(1);
        Assert.Equal(expected, aircraft.Heading, 5);
        Assert.True(aircraft.AltitudeFeet > 1000);
    }

    private static Aircraft Corner(bool airborne) => new()
    {
        Position = new(airborne ? -3000 : -100, 0),
        Heading = 90,
        Phase = airborne ? FlightPhase.Circuit : FlightPhase.TaxiOut,
        SpeedKnots = airborne ? 140 : 10,
        Path = [new(0, 0), new(0, airborne ? 3000 : 100)]
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Route_corners_curve_in_position_and_heading_without_changing_source_waypoints(bool airborne)
    {
        var aircraft = Corner(airborne);
        var original = aircraft.Path.ToArray();
        var rounded = false;
        var completed = false;
        for (var step = 0; step < 10000 && !completed; step++)
        {
            var oldHeading = aircraft.Heading;
            var oldPosition = aircraft.Position;
            completed = RouteMovement.Move(aircraft, airborne ? 2 : 0.2);
            var change = Math.Abs((aircraft.Heading - oldHeading + 540) % 360 - 180);
            Assert.True(change < 2, "The aircraft snapped to a new heading.");
            if (oldPosition.Distance(aircraft.Position) > 0.001)
            {
                var direction = oldPosition.Bearing(aircraft.Position);
                Assert.True(Math.Abs((aircraft.Heading - direction + 540) % 360 - 180) < 2);
            }
            rounded |= aircraft.Position.X < -0.1 && aircraft.Position.Y > 0.1;
        }
        Assert.True(completed);
        Assert.True(rounded);
        Assert.Equal(original, aircraft.Path);
        Assert.Equal(original[^1], aircraft.Position);
        Assert.Equal(0, aircraft.Heading, 6);
    }

    [Fact]
    public void Taxi_slows_for_a_corner_and_does_not_stop_at_the_intermediate_node()
    {
        var simulation = new Simulation(new Session { Options = new() { AircraftPerMinute = 0 } });
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "TAXI");
        aircraft.Position = new(250, 0);
        aircraft.Heading = 90;
        aircraft.Phase = FlightPhase.TaxiOut;
        aircraft.SpeedKnots = 15;
        aircraft.Path = [new(350, 0), new(350, 300)];
        var turned = false;
        for (var step = 0; step < 1000 && !turned; step++)
        {
            simulation.Advance(0.05);
            if (aircraft.Position.X < 349.9 && aircraft.Position.Y > 0.1)
            {
                turned = true;
                Assert.InRange(aircraft.SpeedKnots, 1, 8);
            }
        }
        Assert.True(turned);
        Assert.False(aircraft.Holding);
    }

    [Fact]
    public void A_join_from_an_existing_airborne_heading_is_a_curve_not_sideways_motion()
    {
        var aircraft = Corner(true);
        aircraft.Heading = 0;
        var previous = aircraft.Position;
        RouteMovement.Move(aircraft, 10);
        Assert.True(aircraft.Position.Y > previous.Y);
        Assert.InRange(aircraft.Heading, 0, 10);
        Assert.True(aircraft.Position.X < -2990);
    }

    [Fact]
    public void Save_load_resumes_the_same_corner_and_pause_does_not_move_the_aircraft()
    {
        var simulation = new Simulation(new Session { Options = new() { AircraftPerMinute = 0 } });
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "SAVE");
        var template = Corner(false);
        aircraft.Position = template.Position;
        aircraft.Heading = template.Heading;
        aircraft.Path = template.Path;
        aircraft.Phase = template.Phase;
        for (var step = 0; step < 460; step++)
        {
            RouteMovement.Move(aircraft, 0.2);
        }
        Assert.True(aircraft.Movement!.IsValid());
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            JsonStore.Write(path, simulation.State);
            var restored = JsonStore.LoadSession(path);
            var resumed = new Simulation(restored) { Paused = true };
            var savedPosition = restored.Aircraft[0].Position;
            resumed.Advance(10);
            Assert.Equal(savedPosition, restored.Aircraft[0].Position);
            for (var step = 0; step < 200; step++)
            {
                RouteMovement.Move(aircraft, 0.2);
                RouteMovement.Move(restored.Aircraft[0], 0.2);
                Assert.Equal(aircraft.Position, restored.Aircraft[0].Position);
                Assert.Equal(aircraft.Heading, restored.Aircraft[0].Heading);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }
}
