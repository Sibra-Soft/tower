using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class OriginalAirwayTests
{
    [Theory]
    [InlineData("A", "D", "A B C D")]
    [InlineData("D", "A", "D C B A")]
    [InlineData("B", "C", "B C")]
    public void Traversal_uses_only_the_contiguous_segment_between_endpoints(string entry, string exit, string expected)
    {
        var airway = new OriginalAirway { Segments = [["A", "B", "C", "D"]] };
        Assert.Equal(expected.Split(' '), airway.Between(entry, exit));
    }

    [Theory]
    [InlineData("A", "D")]
    [InlineData("A", "A")]
    [InlineData("A", "UNKNOWN")]
    public void Disconnected_or_invalid_endpoints_are_not_guessed(string entry, string exit)
    {
        var airway = new OriginalAirway { Segments = [["A", "B"], ["C", "D"]] };
        Assert.Null(airway.Between(entry, exit));
    }

    [Fact]
    public void Ambiguous_segments_or_repeated_endpoints_are_rejected()
    {
        Assert.Null(new OriginalAirway { Segments = [["A", "B", "C"], ["A", "D", "C"]] }.Between("A", "C"));
        Assert.Null(new OriginalAirway { Segments = [["A", "B", "A", "C"]] }.Between("A", "C"));
    }

    [Theory]
    [InlineData("A V1 C FIELD Twr", "A B C")]
    [InlineData("C V1 A FIELD Twr", "C B A")]
    [InlineData("A V1 C V2 D FIELD Twr", "A B C D")]
    [InlineData("A MISSING C FIELD Twr", null)]
    [InlineData("A V3 C FIELD Twr", null)]
    public void Scenario_expands_airways_without_losing_original_tokens(string route, string? expected)
    {
        var airport = Airport.Training();
        airport.Code = "FIELD";
        airport.AircraftTypes["PLANE"] = new();
        foreach (var (name, offset) in new[] { ("A", 0.1), ("B", 0.2), ("C", 0.3), ("D", 0.4) })
        {
            airport.Original.Fixes[name] = new(name, airport.Latitude + offset, airport.Longitude);
        }
        airport.Original.Airways["V1"] = new() { Segments = [["A", "B", "C"]] };
        airport.Original.Airways["V2"] = new() { Segments = [["C", "D"]] };
        airport.Original.Airways["V3"] = new() { Segments = [["A", "MISSING_FIX", "C"]] };
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".SIM");
        try
        {
            File.WriteAllText(path, $"[SimInfo]\nSector FIELD.SEC\nAircraft 1\n[Aircraft]\nTEST PLANE VFR -1 a 00:00 200 2000 1000 1200 18\n{route} @FS\n");
            var flight = OriginalData.ReadScenario(path, airport).Schedule[0].OriginalPlan!;
            Assert.Equal((route + " @FS").Split(' '), flight.RouteTokens);
            if (expected is null)
            {
                Assert.Null(flight.ResolvedRoute);
            }
            else
            {
                Assert.Equal(expected.Split(' '), flight.ResolvedRoute!.Select(point => point.Name));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("CYN")]
    [Trait("Category", "Integration")]
    public void Original_sec_airways_are_imported_and_saved(string code)
    {
        var directory = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Assert.Skip("Set TOWER_ORIGINAL_DATA to verify SEC airways.");
            return;
        }
        var airport = OriginalData.LoadAirport(directory, code);
        Assert.NotEmpty(airport.Original.Airways);
        Assert.All(airport.Original.Airways.Values, airway => Assert.NotEmpty(airway.Segments));
        if (code == "CYN")
        {
            Assert.Equal(new[] { "VAN", "ANSON", "BOSCO", "YCK" }, airport.Original.Airways["V1"].Between("VAN", "YCK"));
        }
        var json = System.Text.Json.JsonSerializer.Serialize(airport, JsonStore.Options);
        var restored = System.Text.Json.JsonSerializer.Deserialize<Airport>(json, JsonStore.Options)!;
        Assert.Equal(airport.Original.Airways.Count, restored.Original.Airways.Count);
    }
}
