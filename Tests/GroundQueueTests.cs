using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class GroundQueueTests
{
    private static (Simulation Simulation, Aircraft Following, Aircraft Leading) Queue(AircraftFootprint following, AircraftFootprint leading, double leadingHeading = 0)
    {
        var airport = Airport.Training();
        airport.AircraftTypes["FOLLOW"] = new() { GroundFootprint = following, WingSpanMetres = following.MaxRight - following.MinRight };
        airport.AircraftTypes["LEAD"] = new() { GroundFootprint = leading, WingSpanMetres = leading.MaxRight - leading.MinRight };
        var simulation = new Simulation(new Session { Airport = airport, Options = new() { AircraftPerMinute = 0 } });
        var first = simulation.AddAircraft(FlightDirection.Departure, "FOLLOW", "FOLLOW", assignedGate: "G1", useTrafficFlow: false);
        var second = simulation.AddAircraft(FlightDirection.Departure, "LEAD", "LEAD", assignedGate: "G2", useTrafficFlow: false);
        first.Position = new(5000, 5000);
        first.Heading = 0;
        first.Phase = FlightPhase.TaxiOut;
        first.Path = Enumerable.Range(1, 200).Select(index => first.Position.Offset(0, index * 5)).ToList();
        first.PathIndex = 0;
        first.Movement = null;
        second.Position = first.Position.Offset(0, 200);
        second.Heading = leadingHeading;
        second.Phase = FlightPhase.HoldingShort;
        return (simulation, first, second);
    }

    [Theory]
    [InlineData(5, 4, 40, 35, 0)]
    [InlineData(45, 30, 50, 35, 0)]
    [InlineData(50, 35, 5, 4, 0)]
    [InlineData(10, 4, 50, 25, 180)]
    public void Model_nose_tail_and_orientation_set_spacing_even_with_short_route_segments(double front, double rear, double otherFront, double otherRear, double heading)
    {
        var own = new AircraftFootprint(-rear, front, -8, 8);
        var other = new AircraftFootprint(-otherRear, otherFront, -15, 15);
        var (simulation, following, leading) = Queue(own, other, heading);
        var messages = new List<AircraftRadioMessage>();
        simulation.RadioMessage += messages.Add;
        for (var step = 0; step < 450; step++)
        {
            simulation.Advance(0.1);
            var gap = leading.Position.Y + other.Project(heading).MinForward - following.Position.Y - own.MaxForward;
            Assert.True(gap >= 4.99, "The following model crossed the leading model's clearance boundary.");
        }
        Assert.Equal(0, following.SpeedKnots);
        Assert.InRange(leading.Position.Y + other.Project(heading).MinForward - following.Position.Y - front, 4.99, 5.01);
        Assert.DoesNotContain(messages, message => message.IntentKey == "ground.blocked");
        Assert.True(simulation.Execute("FOLLOW/SS").Success);
        Assert.Equal(heading != 0, messages[^1].IntentKey == "ground.blocked");
    }

    [Fact]
    public void Three_aircraft_keep_the_nearest_queue_spacing_regardless_of_list_order()
    {
        var own = new AircraftFootprint(-10, 20, -10, 10);
        var other = new AircraftFootprint(-35, 48, -30, 30);
        var (simulation, following, leading) = Queue(own, other);
        leading.Position = new(5000, 5400);
        var middle = new Aircraft
        {
            CallSign = "MIDDLE", Type = "LEAD", Specification = leading.Specification, Runway = leading.Runway,
            Direction = FlightDirection.Departure, Phase = FlightPhase.TaxiOut, Heading = 0, Position = new(5000, 5200),
            Path = Enumerable.Range(1, 200).Select(index => new Position(5000, 5200 + index * 5)).ToList()
        };
        simulation.State.Aircraft.Add(middle);
        for (var step = 0; step < 800; step++)
        {
            simulation.Advance(0.1);
            Assert.True(middle.Position.Y + other.MinForward - following.Position.Y - own.MaxForward >= 4.99);
            Assert.True(leading.Position.Y + other.MinForward - middle.Position.Y - other.MaxForward >= 4.99);
        }
        Assert.Equal(0, following.SpeedKnots);
        Assert.Equal(0, middle.SpeedKnots);
    }

    [Fact]
    public void Saved_queue_keeps_model_bounds_and_moves_again_when_the_leader_leaves()
    {
        var bounds = new AircraftFootprint(-35, 48, -30, 30);
        var (simulation, following, leading) = Queue(bounds, bounds);
        simulation.Advance(45);
        var stopped = following.Position;
        var path = Path.GetTempFileName();
        try
        {
            JsonStore.Write(path, simulation.State);
            var restored = new Simulation(JsonStore.LoadSession(path));
            following = restored.State.Aircraft.Single(aircraft => aircraft.CallSign == "FOLLOW");
            leading = restored.State.Aircraft.Single(aircraft => aircraft.CallSign == "LEAD");
            Assert.Equal(bounds, following.Specification.GroundFootprint);
            restored.Advance(5);
            Assert.Equal(stopped, following.Position);
            Assert.True(restored.DeleteAircraft(leading));
            restored.Advance(10);
            Assert.True(following.Position.Y > stopped.Y + 10);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_parallel_route_outside_model_width_does_not_block_taxi()
    {
        var bounds = new AircraftFootprint(-35, 48, -30, 30);
        var (simulation, following, leading) = Queue(bounds, bounds);
        leading.Position = new(5200, 5100);
        simulation.Advance(40);
        Assert.True(following.Position.Y > leading.Position.Y + 100);
        Assert.True(following.SpeedKnots > 0);
    }

    [Fact]
    public void Footprint_uses_the_renderers_scale_and_asymmetric_origin()
    {
        var mesh = new AircraftMesh("offset", [new(-5, 0, -40), new(15, 0, 20), new(0, 5, 0)], []);
        var bounds = mesh.GroundFootprint(40);
        Assert.Equal(new(-40, 80, -10, 30), bounds);
        var projected = bounds.Project(90);
        var world = mesh.Vertices.Select(point => mesh.WorldPoint(point, default, 0, 90, 40).Position).ToArray();
        Assert.Equal(world.Min(point => point.X), projected.MinRight, 8);
        Assert.Equal(world.Max(point => point.X), projected.MaxRight, 8);
        Assert.Equal(world.Min(point => point.Y), projected.MinForward, 8);
        Assert.Equal(world.Max(point => point.Y), projected.MaxForward, 8);
    }

    [Theory]
    [InlineData("CYN")]
    [Trait("Category", "Integration")]
    public void Original_aircraft_profiles_preserve_the_actual_rendered_model_extents(string code)
    {
        var data = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA") ?? throw new InvalidOperationException("Set TOWER_ORIGINAL_DATA.");
        var airport = OriginalData.LoadAirport(data, code);
        var catalog = new AircraftModelCatalog(data);
        var expectedTypes = airport.AircraftTypes.Values
            .Where(specification => catalog.Types.Contains(specification.Type, StringComparer.OrdinalIgnoreCase)).ToArray();
        Assert.NotEmpty(expectedTypes);
        var checkedTypes = 0;
        foreach (var specification in airport.AircraftTypes.Values)
        {
            if (catalog.Get(specification.Type) is { } model)
            {
                var expected = model.GroundFootprint(specification.WingSpanMetres);
                var actual = Assert.IsType<AircraftFootprint>(specification.GroundFootprint);
                Assert.Equal(expected.MinForward, actual.MinForward, 8);
                Assert.Equal(expected.MaxForward, actual.MaxForward, 8);
                Assert.Equal(expected.MinRight, actual.MinRight, 8);
                Assert.Equal(expected.MaxRight, actual.MaxRight, 8);
                checkedTypes++;
            }
        }
        Assert.Equal(expectedTypes.Length, checkedTypes);
    }
}
