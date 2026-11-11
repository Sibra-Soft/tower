using System.Text.Json;
using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class ConflictDetectionTests
{
    private static Simulation Create() => new(new() { Options = new() { AircraftPerMinute = 0, WindKnots = 0 } });

    private static Aircraft Ground(Simulation simulation, string callsign, Position position)
    {
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, callsign);
        aircraft.Phase = FlightPhase.LinedUp;
        aircraft.Position = position;
        aircraft.SpeedKnots = 0;
        return aircraft;
    }

    [Fact]
    public void Distant_runway_occupants_count_once_and_survive_resume()
    {
        var simulation = Create();
        Ground(simulation, "FIRST", new(0, 1000));
        var second = Ground(simulation, "SECOND", new(0, -1000));
        simulation.Advance(0.05);
        Assert.Equal(1, simulation.State.Performance.RunwaySeparation);
        Assert.Equal(0, simulation.State.Performance.Crashes);
        var restored = new Simulation(JsonSerializer.Deserialize<Session>(JsonSerializer.Serialize(simulation.State, JsonStore.Options), JsonStore.Options)!);
        restored.Advance(1);
        Assert.Equal(1, restored.State.Performance.RunwaySeparation);
        Assert.Equal(1, restored.State.Performance.Incidents);
        second.Position = new(200, -1000);
        simulation.Advance(0.05);
        second.Position = new(0, -1000);
        simulation.Advance(0.05);
        Assert.Equal(2, simulation.State.Performance.RunwaySeparation);
    }

    [Fact]
    public void Taxi_crossing_counts_even_when_assigned_to_another_runway()
    {
        var simulation = Create();
        simulation.State.Airport.Nodes.Add("R09", new("R09", "RUNWAY", new(-1500, 0)));
        simulation.State.Airport.Nodes.Add("R27", new("R27", "RUNWAY", new(1500, 0)));
        simulation.State.Airport.Runways.Add(new() { Name = "09", PhysicalId = "09/27", Threshold = "R09", End = "R27" });
        Ground(simulation, "ROLLING", new(0, 1000));
        var taxi = Ground(simulation, "TAXI", new(0, -500));
        taxi.Runway = "09";
        taxi.Phase = FlightPhase.TaxiOut;
        taxi.Path = [new(200, -500)];
        simulation.Advance(0.05);
        Assert.Equal(1, simulation.State.Performance.RunwaySeparation);
        taxi.Position = new(100, -500);
        simulation.Advance(0.05);
        Assert.Empty(simulation.State.Performance.ActiveRunwayPairs);
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(50, 0, 0)]
    public void Air_ground_collision_is_independent_of_phase(double altitude, double offset, int crashes)
    {
        var simulation = Create();
        Ground(simulation, "GROUND", new(300, 0));
        var air = Ground(simulation, "AIR", new(300 + offset, 0));
        air.Phase = FlightPhase.Vectoring;
        air.AltitudeFeet = altitude;
        air.SpeedKnots = air.TargetSpeedKnots = 80;
        simulation.Advance(0.01);
        Assert.Equal(crashes, simulation.State.Performance.Crashes);
    }

    [Fact]
    public void Model_overhang_collision_is_detected_beyond_the_old_radius()
    {
        var simulation = Create();
        var first = Ground(simulation, "FIRST", new(300, 0));
        var second = Ground(simulation, "SECOND", new(300, 35));
        first.Specification = second.Specification = new() { GroundFootprint = new(-25, 25, -4, 4), WingSpanMetres = 8 };
        first.Heading = second.Heading = 0;
        first.Phase = FlightPhase.LineUp;
        first.Path = [new(300, -1000)];
        first.PathIndex = 0;
        first.SpeedKnots = 2;
        simulation.Advance(0.01);
        Assert.Equal(1, simulation.State.Performance.Crashes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(90)]
    public void Properly_spaced_small_aircraft_do_not_get_a_fixed_distance_alert(double heading)
    {
        var simulation = Create();
        var first = Ground(simulation, "FIRST", new(300, 0));
        var second = Ground(simulation, "SECOND", first.Position.Offset(heading, 15));
        first.Specification = second.Specification = new() { GroundFootprint = new(-4, 4, -3, 3), WingSpanMetres = 6 };
        first.Heading = second.Heading = heading;
        simulation.Advance(0.05);
        Assert.Equal(0, simulation.State.Performance.Incidents);
    }

    [Fact]
    public void An_existing_air_conflict_can_become_a_runway_error_without_a_second_incident()
    {
        var simulation = Create();
        var first = Ground(simulation, "FIRST", new(0, 1000));
        var second = Ground(simulation, "SECOND", new(0, -1000));
        first.Phase = second.Phase = FlightPhase.Vectoring;
        first.AltitudeFeet = second.AltitudeFeet = 2000;
        first.SpeedKnots = second.SpeedKnots = 100;
        simulation.Advance(0.01);
        Assert.Equal(1, simulation.State.Performance.Incidents);
        first.Phase = second.Phase = FlightPhase.LinedUp;
        first.AltitudeFeet = second.AltitudeFeet = 0;
        first.SpeedKnots = second.SpeedKnots = 0;
        simulation.Advance(0.01);
        Assert.Equal(1, simulation.State.Performance.RunwaySeparation);
        Assert.Equal(1, simulation.State.Performance.Incidents);
    }

    [Fact]
    public void Deleting_a_collided_aircraft_releases_pair_state_for_callsign_reuse()
    {
        var simulation = Create();
        var ground = Ground(simulation, "GROUND", new(300, 0));
        var air = Ground(simulation, "AIR", ground.Position);
        air.Phase = FlightPhase.Vectoring;
        air.SpeedKnots = air.TargetSpeedKnots = 80;
        simulation.Advance(0.01);
        Assert.Equal(1, simulation.State.Performance.Crashes);
        Assert.True(simulation.DeleteAircraft(air));
        Assert.Empty(simulation.State.Performance.ReportedCollisions);
        var replacement = Ground(simulation, "AIR", ground.Position);
        replacement.Phase = FlightPhase.Vectoring;
        replacement.SpeedKnots = replacement.TargetSpeedKnots = 80;
        simulation.Advance(0.01);
        Assert.Equal(2, simulation.State.Performance.Crashes);
    }

    [Theory]
    [InlineData(5555, 999, 1)]
    [InlineData(5556, 999, 0)]
    [InlineData(5000, 1000, 0)]
    public void Existing_airborne_distance_and_height_limits_are_preserved(double distance, double height, int incidents)
    {
        var simulation = Create();
        var first = Ground(simulation, "FIRST", new(10000, 0));
        var second = Ground(simulation, "SECOND", new(10000 + distance, 0));
        first.Phase = second.Phase = FlightPhase.Vectoring;
        first.AltitudeFeet = 3000;
        second.AltitudeFeet = 3000 + height;
        first.SpeedKnots = second.SpeedKnots = first.TargetSpeedKnots = second.TargetSpeedKnots = 100;
        first.Heading = second.Heading = 0;
        simulation.Advance(0.01);
        Assert.Equal(incidents, simulation.State.Performance.Incidents);
        Assert.Equal(0, simulation.State.Performance.Crashes);
    }
}
