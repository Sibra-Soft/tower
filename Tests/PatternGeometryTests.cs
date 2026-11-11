using System.Text.Json;
using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class PatternGeometryTests
{
    private static OriginalMetadata Pattern() => new()
    {
        PatternMetresPerUnit = OriginalPatternUnits.MetresPerSourceUnit,
        PatternUsesFixedPointRounding = true,
        PatternValues = new()
        {
            ["cat1_upwind"] = 5000,
            ["cat1_base"] = 9000,
            ["cat1_final"] = 9000,
            ["cat2_upwind"] = 6000,
            ["cat2_base"] = 12000,
            ["cat2_final"] = 18000,
            ["cat3_upwind"] = 7000,
            ["cat3_base"] = 12000,
            ["cat3_final"] = 18000
        }
    };

    [Theory]
    [InlineData(1, 0.3046875)]
    [InlineData(5000, 1524.0234375)]
    [InlineData(6000, 1828.828125)]
    [InlineData(7000, 2133.6328125)]
    [InlineData(8000, 2438.4375)]
    [InlineData(9000, 2743.2421875)]
    [InlineData(12000, 3657.65625)]
    [InlineData(14000, 4267.265625)]
    [InlineData(18000, 5486.484375)]
    public void Original_fixed_point_distances_are_reproduced(double source, double expectedMetres)
    {
        var metadata = Pattern();
        metadata.PatternCategoryByType["TEST"] = 1;
        metadata.PatternValues["cat1_final"] = source;
        Assert.Equal(expectedMetres, metadata.PatternDistance("TEST", "final"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(37)]
    [InlineData(180)]
    public void Left_and_right_circuits_use_category_distances_in_the_runway_frame(double heading)
    {
        for (int category = 1; category <= 3; category++)
        {
            var (simulation, aircraft, threshold) = Circuit(heading, category);
            var upwind = category switch
            {
                1 => 1524.0234375,
                2 => 1828.828125,
                _ => 2133.6328125
            };
            var width = category == 1 ? 2743.2421875 : 3657.65625;
            var final = category == 1 ? 2743.2421875 : 5486.484375;
            var initialPosition = aircraft.Position;
            foreach (var (command, side) in new[] { ("ELD18", -1), ("ERD18", 1) })
            {
                Assert.True(simulation.Execute("PAT/" + command).Success);
                Assert.Equal(initialPosition, aircraft.Position);
                Assert.Equal(3, aircraft.Path.Count);
                InRunwayFrame(aircraft.Path[0], threshold, heading, upwind, side * width);
                InRunwayFrame(aircraft.Path[1], threshold, heading, -final, side * width);
                InRunwayFrame(aircraft.Path[2], threshold, heading, -final, 0);
            }
            Assert.True(simulation.Execute("PAT/ELB18").Success);
            Assert.Equal(2, aircraft.Path.Count);
            InRunwayFrame(aircraft.Path[0], threshold, heading, -final, -width);
            Assert.True(simulation.Execute("PAT/EF18").Success);
            InRunwayFrame(Assert.Single(aircraft.Path), threshold, heading, -final, 0);
        }
    }

    [Fact]
    public void Explicit_final_and_short_approach_keep_their_commanded_distances()
    {
        var (simulation, aircraft, threshold) = Circuit(0, 3);
        Assert.True(simulation.Execute("PAT/EF18").Success);
        Assert.True(simulation.Execute("PAT/PF5,18").Success);
        Assert.Equal(5 * 1852, threshold.Distance(Assert.Single(aircraft.Path)), 8);
        Assert.True(simulation.Execute("PAT/MS").Success);
        Assert.Equal(1852, threshold.Distance(Assert.Single(aircraft.Path)), 8);
    }

    [Fact]
    public void Geometry_and_conversion_survive_save_and_resume()
    {
        var (simulation, aircraft, _) = Circuit(37, 2);
        Assert.True(simulation.Execute("PAT/ERD18").Success);
        var restored = JsonSerializer.Deserialize<Session>(JsonSerializer.Serialize(simulation.State, JsonStore.Options), JsonStore.Options)!;
        Assert.True(restored.Airport.Original.PatternUsesFixedPointRounding);
        Assert.Equal(5486.484375, restored.Airport.Original.PatternDistance(aircraft.Type, "final"));
        Assert.Equal(aircraft.Path.ToArray(), restored.Aircraft.Single().Path.ToArray());
    }

    [Fact]
    public void Old_sessions_and_explicit_linear_conversions_are_not_reinterpreted()
    {
        var legacy = JsonSerializer.Deserialize<OriginalMetadata>("""{"PatternValues":{"cat1_final":9000},"PatternCategoryByType":{"TEST":1}}""", JsonStore.Options)!;
        Assert.Null(legacy.PatternDistance("TEST", "final"));
        Assert.False(legacy.PatternUsesFixedPointRounding);
        legacy.PatternMetresPerUnit = Coordinates.MetresPerFoot;
        Assert.Equal(2743.2, legacy.PatternDistance("TEST", "final")!.Value, 8);
    }

    [Fact]
    public void Missing_or_unsafe_distances_keep_existing_route_fallbacks()
    {
        var (simulation, aircraft, threshold) = Circuit(0, 1);
        var metadata = simulation.State.Airport.Original;
        metadata.PatternValues["cat1_final"] = double.MaxValue;
        metadata.PatternValues.Remove("cat1_base");
        Assert.True(simulation.Execute("PAT/ERD18").Success);
        InRunwayFrame(aircraft.Path[1], threshold, 0, -3 * 1852, 2 * 1852);
        metadata.PatternCategoryByType.Clear();
        Assert.True(simulation.Execute("PAT/ELD18").Success);
        InRunwayFrame(aircraft.Path[0], threshold, 0, 1852, -2 * 1852);
    }

    private static (Simulation Simulation, Aircraft Aircraft, Position Threshold) Circuit(double heading, int category)
    {
        var airport = Airport.Training();
        var runway = airport.GetRunway("18");
        var threshold = new Position(120, -340);
        airport.Nodes[runway.Threshold] = airport.Nodes[runway.Threshold] with
        {
            Position = threshold
        };
        airport.Nodes[runway.End] = airport.Nodes[runway.End] with
        {
            Position = threshold.Offset(heading, 3000)
        };
        airport.Original = Pattern();
        var simulation = new Simulation(new Session { Airport = airport, Options = new() { AircraftPerMinute = 0, Runway = "18" } });
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "PAT");
        airport.Original.PatternCategoryByType[aircraft.Type] = category;
        return (simulation, aircraft, threshold);
    }

    private static void InRunwayFrame(Position point, Position origin, double heading, double along, double right)
    {
        var angle = heading * Math.PI / 180;
        var dx = point.X - origin.X;
        var dy = point.Y - origin.Y;
        Assert.Equal(along, dx * Math.Sin(angle) + dy * Math.Cos(angle), 7);
        Assert.Equal(right, dx * Math.Cos(angle) - dy * Math.Sin(angle), 7);
    }
}
