using System.Text.Json;
using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class FlightDynamicsTests
{
    private static string Data(string code)
    {
        var fixture = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA");
        if (fixture is not null && File.Exists(Path.Combine(fixture, code, code + ".TWR")))
        {
            return fixture;
        }
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var data = Path.Combine(folder.FullName, "Data");
            if (File.Exists(Path.Combine(data, code, code + ".TWR")))
            {
                return data;
            }
        }
        throw new InvalidOperationException("Airport data is required for dynamics checks.");
    }

    [Theory]
    [InlineData("CYN", "9", "C182")]
    [InlineData("CYN", "14", "B737")]
    [Trait("Category", "Integration")]
    public void Threshold_crossing_flare_rollout_exit_and_stop_are_continuous(string code, string runway, string type)
    {
        var airport = OriginalData.LoadAirport(Data(code), code);
        var simulation = new Simulation(new Session { Airport = airport, Options = new() { Runway = runway, AircraftPerMinute = 0, WindKnots = 0 } });
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "DYNAMICS", type, useTrafficFlow: false);
        Assert.True(simulation.Execute("DYNAMICS/CL").Success);
        var rwy = airport.GetRunway(runway);
        var threshold = airport.Point(rwy.Threshold);
        var heading = threshold.Bearing(airport.Point(rwy.End));
        var crossed = false;
        var flare = false;
        var roll = false;
        var turn = false;
        var restored = false;
        for (var step = 0; step < 30000 && aircraft.Phase != FlightPhase.ArrivalHold; step++)
        {
            var oldPhase = aircraft.Phase;
            var oldAltitude = aircraft.AltitudeFeet;
            var oldHeading = aircraft.Heading;
            var oldSpeed = aircraft.SpeedKnots;
            var oldPitch = aircraft.PitchDegrees;
            var oldRemaining = RouteMovement.RemainingDistance(aircraft);
            simulation.Advance(.05);
            Assert.InRange(Math.Abs(aircraft.PitchDegrees - oldPitch), 0, .151);
            Assert.InRange(Math.Abs(aircraft.AltitudeFeet - oldAltitude), 0, 4);
            if (aircraft.Phase == FlightPhase.Approach && aircraft.Position.Distance(threshold) < 3)
            {
                crossed = true;
                Assert.True(aircraft.AltitudeFeet > 15, "The aircraft landed on the threshold instead of crossing it above ground.");
            }
            if (aircraft.Phase == FlightPhase.Approach && aircraft.AltitudeFeet < 40)
            {
                flare = true;
                if (!restored)
                {
                    var copy = new Simulation(JsonSerializer.Deserialize<Session>(JsonSerializer.Serialize(simulation.State, JsonStore.Options), JsonStore.Options)!);
                    copy.Advance(.05);
                    simulation.Advance(.05);
                    Assert.Equal(aircraft.Position, copy.State.Aircraft[0].Position);
                    Assert.Equal(aircraft.PitchDegrees, copy.State.Aircraft[0].PitchDegrees);
                    restored = true;
                }
            }
            if (oldPhase == FlightPhase.Approach && aircraft.Phase == FlightPhase.LandingRoll)
            {
                roll = true;
                Assert.InRange(threshold.Distance(aircraft.Position), 100, 301);
            }
            if (oldPhase == FlightPhase.LandingRoll)
            {
                Assert.InRange(Math.Abs(RouteMovement.AngleDifference(oldHeading, aircraft.Heading)), 0, 1.5);
                Assert.True(oldSpeed - aircraft.SpeedKnots <= .5,
                    $"{code}: {oldPhase}->{aircraft.Phase}; speed {oldSpeed}->{aircraft.SpeedKnots}, remaining {oldRemaining}, index {aircraft.PathIndex}.");
                turn |= Math.Abs(RouteMovement.AngleDifference(heading, aircraft.Heading)) > 5;
            }
        }
        Assert.True(crossed && flare && roll && turn && restored);
        Assert.Equal(FlightPhase.ArrivalHold, aircraft.Phase);
        Assert.Equal(0, aircraft.SpeedKnots);
    }

    [Fact]
    public void Light_aircraft_uses_its_profile_and_liftoff_has_no_height_jump()
    {
        var airport = OriginalData.LoadAirport(Data("CYN"), "CYN");
        var simulation = new Simulation(new Session { Airport = airport, Options = new() { Runway = "9", AircraftPerMinute = 0 } });
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "LIGHT", "C182");
        var runway = airport.GetRunway("9");
        aircraft.Position = airport.Point(runway.Threshold);
        aircraft.Heading = airport.Point(runway.Threshold).Bearing(airport.Point(runway.End));
        aircraft.Phase = FlightPhase.LinedUp;
        Assert.Equal(80, FlightDynamics.DepartureSpeed(aircraft));
        Assert.True(simulation.Execute("LIGHT/CT").Success);
        for (var step = 0; step < 10000 && aircraft.Phase == FlightPhase.TakeoffRoll; step++)
        {
            simulation.Advance(.05);
        }
        Assert.Equal(FlightPhase.Departing, aircraft.Phase);
        Assert.InRange(aircraft.AltitudeFeet, 0, 1);
        Assert.True(aircraft.PitchDegrees > 0);
        simulation.Advance(10);
        Assert.InRange(aircraft.SpeedKnots, 70, 80.01);
        Assert.True(aircraft.AltitudeFeet > 0);
    }

    [Fact]
    public void Mesh_pitch_bank_and_ground_contact_use_the_same_pose()
    {
        var mesh = new AircraftMesh("pose", [new(-10, 0, 0), new(10, 0, 0), new(0, 0, -20)], []);
        var neutral = mesh.WorldPoints(new(), 100, 0, 20, 0, 0);
        var rotated = mesh.WorldPoints(new(), 100, 0, 20, 10, 20);
        Assert.True(rotated[0].Altitude > rotated[1].Altitude);
        Assert.True(rotated[2].Altitude > neutral[2].Altitude);
        Assert.All(mesh.WorldPoints(new(), 0, 0, 20, 10, 0), point => Assert.True(point.Altitude >= 0));
    }

    [Fact]
    public void Hold_position_brakes_over_distance_and_then_resumes_without_a_heading_snap()
    {
        var simulation = new Simulation(new Session { Options = new() { AircraftPerMinute = 0 } });
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "HOLD");
        aircraft.Position = new(250, 0);
        aircraft.Heading = 90;
        aircraft.Phase = FlightPhase.TaxiOut;
        aircraft.Path = [new(700, 0)];
        aircraft.SpeedKnots = 15;
        var start = aircraft.Position;
        Assert.True(simulation.Execute("HOLD/HY").Success);
        simulation.Advance(.05);
        Assert.InRange(aircraft.SpeedKnots, 14, 15);
        Assert.True(aircraft.Position.Distance(start) > 0);
        simulation.Advance(15);
        Assert.Equal(0, aircraft.SpeedKnots);
        Assert.InRange(aircraft.Position.Distance(start), 10, 40);
        var stopped = aircraft.Position;
        simulation.Advance(2);
        Assert.Equal(stopped, aircraft.Position);
        Assert.True(simulation.Execute("HOLD/TU").Success);
        simulation.Advance(2);
        Assert.True(aircraft.Position.Distance(stopped) > 0);
    }

    [Fact]
    public void High_speed_commanded_turn_limits_bank_and_changes_attitude_gradually()
    {
        var simulation = new Simulation(new Session { Options = new() { AircraftPerMinute = 0 } });
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "TURN");
        aircraft.SpeedKnots = 400;
        Assert.True(simulation.Execute("TURN/MR1").Success);
        var heading = aircraft.Heading;
        simulation.Advance(1);
        Assert.InRange(RouteMovement.AngleDifference(heading, aircraft.Heading), .5, 1.4);
        Assert.InRange(aircraft.BankDegrees, .1, 12.001);
        simulation.Advance(5);
        Assert.InRange(aircraft.BankDegrees, 20, 25);
    }

    [Fact]
    public void Higher_touchdown_speed_requires_more_rollout_with_the_same_brakes()
    {
        var braking = RunwayPerformance.Acceleration(130, 0, 1200);
        Assert.Equal(1200, RunwayPerformance.BrakingDistance(130, 0, braking), 6);
        Assert.Equal(4800, RunwayPerformance.BrakingDistance(260, 0, braking), 6);
        Assert.True(RunwayPerformance.BrakingDistance(150, 16, braking) > RunwayPerformance.BrakingDistance(130, 16, braking));
    }
}
