using System.Text.Json;
using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class RadarMapTests
{
    private static Airport Airport() => new() { Latitude = 51.25, Longitude = -96, Observer = new(123, 456) };

    [Fact]
    public void Map_uses_sector_projection_and_retains_world_distance_dashes_and_wrapping_arc()
    {
        var airport = Airport();
        var map = OriginalData.ReadRadarMap("""
            Tower V1.0 Map File Version 3.8
            [Maps]
            LINE Green 1
            dash_double 1 2 3
            5115.00N/09600.00W
            5116.00N/09600.00W
            end
            ARC Green 1
            center 5115.00N/09600.00W
            radius 30.1
            start_angle 348
            end_angle 011
            degrees_on 1
            degrees_off 1
            """.Split('\n'), airport);
        var line = Assert.Single(map.Lines);
        Assert.Equal(airport.Observer, line.Points[0]);
        Assert.Equal(new GeographicFix("", 51.25 + 1.0 / 60, -96).ProjectFrom(51.25, -96, airport.Observer), line.Points[1]);
        Assert.Equal(new double[] { 1852, 5556, 3704, 5556 }, line.DashMetres);
        var arc = Assert.Single(map.Arcs);
        Assert.Equal(30.1 * 1852, arc.RadiusMetres);
        Assert.Equal(348, arc.StartDegrees);
        Assert.Equal(11, arc.EndDegrees);
    }

    [Theory]
    [InlineData("solid")]
    [InlineData("shoreline")]
    [InlineData("border")]
    [InlineData("dash 0.5 1")]
    public void Source_line_styles_are_supported(string style)
    {
        var map = OriginalData.ReadRadarMap(new[] { "[Maps]", "LINE Green 1", style,
            "5115.00N/09600.00W", "5116.00N/09600.00W", "end" }, Airport());
        Assert.Single(map.Lines);
    }

    [Theory]
    [InlineData("dash 0 1", "5115.00N/09600.00W")]
    [InlineData("solid", "5161.00N/09600.00W")]
    [InlineData("solid", "not-a-coordinate")]
    public void Invalid_geometry_is_rejected(string style, string coordinate)
    {
        Assert.Throws<InvalidDataException>(() => OriginalData.ReadRadarMap(new[] { "[Maps]", "LINE Green 1", style,
            coordinate, "5116.00N/09600.00W", "end" }, Airport()));
    }

    [Fact]
    public void Map_and_symbol_source_types_survive_session_reload_and_old_saves_default_empty()
    {
        var session = new Session { Airport = Tower.Core.Airport.Training() };
        session.Airport.RadarMap = OriginalData.ReadRadarMap(new[] { "[Maps]", "LINE Green 1", "solid",
            "5115.00N/09600.00W", "5116.00N/09600.00W", "end" }, Airport());
        session.Airport.Original.Fixes["FAF"] = new("FAF", 51.25, -96) { SourceKind = "p" };
        var path = Path.GetTempFileName();
        try
        {
            JsonStore.Write(path, session);
            var restored = JsonStore.LoadSession(path);
            Assert.Equal(session.Airport.RadarMap.Lines.Single().Points, restored.Airport.RadarMap.Lines.Single().Points);
            Assert.Equal("p", restored.Airport.Original.Fixes["FAF"].SourceKind);
            var legacy = JsonSerializer.Deserialize<Tower.Core.Airport>("{}");
            Assert.NotNull(legacy);
            Assert.Empty(legacy.RadarMap.Lines);
            Assert.Empty(legacy.RadarMap.Arcs);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
