using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class ProjectionAndRunwayTests
{
    [Fact]
    public void Runway_polygons_taper_with_depth_and_clip_without_disappearing()
    {
        var camera = new TowerProjection(new(0, 0), 0, 90, 1000, 600);
        var polygon = camera.GroundPolygon([new(-22.5, 100), new(22.5, 100), new(22.5, 1000), new(-22.5, 1000)]);
        Assert.Equal(4, polygon.Length);
        var nearWidth = polygon[1].X - polygon[0].X;
        var farWidth = polygon[2].X - polygon[3].X;
        Assert.Equal(10, nearWidth / farWidth, 6);
        var crossing = camera.GroundPolygon([new(-2000, -100), new(2000, -100), new(2000, 1000), new(-2000, 1000)]);
        Assert.True(crossing.Length >= 3);
        Assert.All(crossing, point =>
        {
            Assert.InRange(point.X, 0, 1000);
            Assert.InRange(point.Y, 0, 600);
            Assert.True(double.IsFinite(point.X) && double.IsFinite(point.Y));
        });
        Assert.Empty(camera.GroundPolygon([new(-10, -1000), new(10, -1000), new(10, -100)]));
    }
    [Fact]
    public void Ground_segments_crossing_the_view_remain_visible_when_endpoints_are_outside()
    {
        var camera = new TowerProjection(new(0, 0), 0, 90, 1000, 600);
        Assert.Null(camera.Point(new(-2000, 1000)));
        Assert.Null(camera.Point(new(2000, 1000)));
        var segment = camera.GroundSegment(new(-2000, 1000), new(2000, 1000));
        Assert.NotNull(segment);
        Assert.Equal(0, segment.Value.From.X, 6);
        Assert.Equal(1000, segment.Value.To.X, 6);
        Assert.Null(camera.GroundSegment(new(-100, -100), new(100, -100)));
        Assert.NotNull(camera.GroundSegment(new(0, -100), new(0, 1000)));
    }

    [Fact]
    public void Aircraft_and_map_share_forward_depth_perspective()
    {
        var camera = new TowerProjection(new(0, 0), 0, 90, 1000, 600);
        var center = camera.Point(new(0, 1000))!.Value;
        var edge = camera.Point(new(500, 1000))!.Value;
        Assert.Equal(center.Y, edge.Y, 6);
        Assert.Equal(750, edge.X, 6);
        var turned = new TowerProjection(new(0, 0), 90, 90, 1000, 600);
        Assert.Equal(500, turned.Point(new(1000, 0))!.Value.X, 6);
        Assert.Null(new TowerProjection(new(0, 0), 0, 90, 0, 0).GroundSegment(new(0, 100), new(0, 1000)));
    }

    [Fact]
    public void Takeoff_uses_required_distance_and_rejects_a_short_runway()
    {
        var simulation = new Simulation(new Session { Options = new() { AircraftPerMinute = 0, WindKnots = 0 } });
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "DIST");
        var runway = simulation.State.Airport.GetRunway(aircraft.Runway);
        aircraft.Phase = FlightPhase.LinedUp;
        aircraft.Position = simulation.State.Airport.Point(runway.Threshold);
        aircraft.Specification.TakeoffDistanceFeet = 1000;
        Assert.True(simulation.Execute("DIST/CT").Success);
        for (var i = 0; i < 2000 && aircraft.Phase == FlightPhase.TakeoffRoll; i++)
        {
            simulation.Advance(0.05);
        }

        Assert.Equal(FlightPhase.Departing, aircraft.Phase);
        Assert.Equal(304.8, aircraft.Position.Distance(simulation.State.Airport.Point(runway.Threshold)), 1);
        Assert.Equal(aircraft.Specification.TakeoffSpeed, aircraft.SpeedKnots, 1);

        aircraft.Phase = FlightPhase.LinedUp;
        aircraft.Position = simulation.State.Airport.Point(runway.Threshold);
        aircraft.Specification.TakeoffDistanceFeet = 20000;
        Assert.False(simulation.Execute("DIST/CT").Success);
        Assert.Equal(FlightPhase.LinedUp, aircraft.Phase);
    }

    [Fact]
    public void Cancelled_takeoff_uses_stopping_distance_and_reports_overrun_once()
    {
        var simulation = new Simulation(new Session { Options = new() { AircraftPerMinute = 0 } });
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "STOP");
        var airport = simulation.State.Airport;
        var runway = airport.GetRunway(aircraft.Runway);
        aircraft.Phase = FlightPhase.AbortedTakeoff;
        aircraft.Position = airport.Point(runway.Threshold);
        aircraft.SpeedKnots = aircraft.Specification.TakeoffSpeed;
        aircraft.Specification.StoppingDistanceFeet = 1000;
        simulation.Advance(30);
        Assert.Equal(0, aircraft.SpeedKnots);
        Assert.Equal(304.8, aircraft.Position.Distance(airport.Point(runway.Threshold)), 1);
        Assert.False(aircraft.RunwayOverrunReported);

        var reports = 0;
        simulation.Message += message => { if (message.Contains("runway overrun", StringComparison.OrdinalIgnoreCase)) { reports++; } };
        aircraft.Position = airport.Point(runway.End).Offset(0, 10);
        aircraft.SpeedKnots = aircraft.Specification.TakeoffSpeed;
        simulation.Advance(30);
        Assert.True(aircraft.RunwayOverrunReported);
        Assert.Equal(1, reports);
        Assert.Equal(1, simulation.State.Performance.RunwayOverruns);
        Assert.True(simulation.State.Performance.HasOperationalErrors);
        var restored = System.Text.Json.JsonSerializer.Deserialize<Session>(
            System.Text.Json.JsonSerializer.Serialize(simulation.State, JsonStore.Options), JsonStore.Options)!;
        new Simulation(restored).Advance(1);
        Assert.Equal(1, restored.Performance.RunwayOverruns);
        Assert.Equal(1, restored.Performance.Incidents);
        Assert.False(simulation.Execute("STOP/CT").Success);
    }

    [Fact]
    public void Landing_exit_must_be_beyond_required_distance()
    {
        var airport = Airport.Training();
        var runway = airport.Runways[0];
        airport.Nodes["EARLY"] = new("EARLY", "RWYINT", new(0, 1200), runway.PhysicalId);
        airport.Nodes["LATE"] = new("LATE", "RWYINT", new(0, 0), runway.PhysicalId);
        airport.Segments.Add(new("EARLYEXIT", "TAXIWAY", "EARLY", "A1"));
        airport.Segments.Add(new("LATEEXIT", "TAXIWAY", "LATE", "A2"));
        Assert.Equal("LATE", RunwayPerformance.LandingExit(airport, runway, 1000)!.Value.Rollout);
        Assert.Null(RunwayPerformance.LandingExit(airport, runway, 4000));
    }

    [Fact]
    public void Runway_access_segment_reference_is_a_valid_exit_only_when_aligned()
    {
        var airport = Airport.Training();
        var runway = airport.Runways[0];
        airport.Nodes["ACCESS"] = new("ACCESS", "RWYINT", new(1, -1200), "SHORT");
        airport.Segments.Add(new("SHORT", "TAXIWAY", "ACCESS", runway.End));
        airport.Segments.Add(new("LEAVE", "TAXIWAY", "ACCESS", "A2"));
        Assert.Equal("ACCESS", RunwayPerformance.LandingExit(airport, runway, 2500)!.Value.Rollout);
        airport.Nodes["ACCESS"] = airport.Nodes["ACCESS"] with
        {
            Position = new(500, -1200)
        };
        Assert.Null(RunwayPerformance.LandingExit(airport, runway, 2500));
    }

    [Fact]
    public void Landing_roll_uses_the_chosen_exit_and_saves_its_performance_state()
    {
        var simulation = new Simulation(new Session { Options = new() { AircraftPerMinute = 0, WindKnots = 0 } });
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "LAND");
        aircraft.Specification.LandingDistanceFeet = 1000;
        Assert.True(simulation.Execute("LAND/CL").Success);
        for (var i = 0; i < 1000 && aircraft.Phase != FlightPhase.LandingRoll; i++)
        {
            simulation.Advance(1);
        }

        Assert.Equal(FlightPhase.LandingRoll, aircraft.Phase);
        Assert.True(aircraft.RunwayDecelerationMetresPerSecondSquared > 0);

        var json = System.Text.Json.JsonSerializer.Serialize(simulation.State, JsonStore.Options);
        var restored = new Simulation(System.Text.Json.JsonSerializer.Deserialize<Session>(json, JsonStore.Options)!);
        for (var i = 0; i < 1000 && aircraft.Phase != FlightPhase.ArrivalHold; i++)
        {
            simulation.Advance(1);
            restored.Advance(1);
        }
        Assert.Equal(FlightPhase.ArrivalHold, aircraft.Phase);
        Assert.Equal(aircraft.Position, restored.State.Aircraft[0].Position);
        Assert.True(simulation.Execute("LAND/TP").Success);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Original_distances_import_without_losing_source_values()
    {
        var directory = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Assert.Skip("Set TOWER_ORIGINAL_DATA to verify original runway distances.");
            return;
        }
        foreach (var code in new[] { "CYN" })
        {
            var airport = OriginalData.LoadAirport(directory, code);
            foreach (var specification in airport.AircraftTypes.Values)
            {
                Assert.Equal(specification.OriginalPerformance["Takeoff_Distance"], specification.TakeoffDistanceFeet);
                Assert.Equal(specification.OriginalPerformance["Landing_Distance"], specification.LandingDistanceFeet);
                Assert.Equal(specification.OriginalPerformance["Stopping_Distance"], specification.StoppingDistanceFeet);
            }
        }
    }
}
