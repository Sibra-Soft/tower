using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class RunwayDefaultTests
{
    [Fact]
    public void Active_runways_use_source_order_canonical_names_and_remove_duplicates()
    {
        var airport = Airport.Training();
        airport.Code = "TEST";
        airport.Runways = [new() { Name = "14" }, new() { Name = "09" }, new() { Name = "27R" }];
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ini");
        try
        {
            File.WriteAllText(path, "[Scenario]\nTESTActiveRunways=9,27r,09,UNKNOWN\n");
            Assert.Equal(new[] { "09", "27R" }, OriginalData.ReadDefaultRunways(path, airport));
            File.WriteAllText(path, "[Scenario]\nTESTActiveRunways=UNKNOWN\n");
            Assert.Equal(new[] { "14" }, OriginalData.ReadDefaultRunways(path, airport));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Missing_settings_keep_the_existing_first_runway_fallback()
    {
        var airport = Airport.Training();
        Assert.Equal(new[] { "18" }, OriginalData.ReadDefaultRunways(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ini"), airport));
    }

    [Theory]
    [InlineData("CYN", "9")]
    [Trait("Category", "Integration")]
    public void Original_configuration_selects_the_declared_airport_runways(string code, string expected)
    {
        var data = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA");
        if (string.IsNullOrWhiteSpace(data))
        {
            Assert.Skip("Set TOWER_ORIGINAL_DATA to verify the supplied runway defaults.");
            return;
        }
        var airport = OriginalData.LoadAirport(data, code);
        var settingsPath = Path.Combine(Path.GetDirectoryName(data)!, "settings.ini");
        Assert.Equal(expected.Split(','), OriginalData.ReadDefaultRunways(settingsPath, airport));
    }
}
