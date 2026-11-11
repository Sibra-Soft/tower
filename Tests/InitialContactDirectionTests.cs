using System.Text.Json;
using Tower.Core;
using Tower.Infrastructure;
using TowerSpeech.Core;
using Xunit;

namespace Tower.Tests;

public sealed class InitialContactDirectionTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Installed_original_voice_bank_renders_every_initial_direction()
    {
        var data = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA")
            ?? throw new InvalidOperationException("Set TOWER_ORIGINAL_DATA.");
        var runtime = new WorkbenchSpeechRuntime(data);
        runtime.Preferences.Engine = SpeechEngineKind.BaoOriginal;
        foreach (var bearing in Enumerable.Range(0, 8).Select(sector => sector * 45))
        {
            var direction = OriginalRadioDirection.FromBearing(bearing);
            var message = new AircraftRadioMessage("VOTER51", "CYN", "ignored", "14", "arrival.initial",
                OriginalRadioDistance.NauticalMiles(11), Detail: direction);
            var result = runtime.Render(message);
            Assert.True(result.Diagnostic is null, direction + ": " + result.Diagnostic);
            Assert.True(result.Wave is { Length: > 44 });
        }
    }

    [Theory]
    [InlineData(0, "north")]
    [InlineData(22.49, "north")]
    [InlineData(22.5, "northeast")]
    [InlineData(45, "northeast")]
    [InlineData(67.5, "east")]
    [InlineData(90, "east")]
    [InlineData(135, "southeast")]
    [InlineData(180, "south")]
    [InlineData(225, "southwest")]
    [InlineData(270, "west")]
    [InlineData(315, "northwest")]
    [InlineData(337.5, "north")]
    [InlineData(360, "north")]
    [InlineData(-90, "west")]
    public void Original_compass_rounding_uses_eight_position_sectors(double bearing, string expected)
    {
        Assert.Equal(expected, OriginalRadioDirection.FromBearing(bearing));
    }

    [Fact]
    public void Initial_radio_logs_the_aircraft_position_direction_and_preserves_it_for_repeat_and_save()
    {
        var airport = Airport.Training();
        airport.Observer = new(0, 0);
        var simulation = new Simulation(new Session { Airport = airport, Options = new() { AircraftPerMinute = 0 } });
        var radio = new List<AircraftRadioMessage>();
        var log = new List<string>();
        simulation.RadioMessage += radio.Add;
        simulation.Message += log.Add;
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "WEST1",
            originalPlan: new OriginalFlightPlan { OriginBearingDegrees = 270 });
        var initial = Assert.Single(radio);
        Assert.Equal("arrival.initial", initial.IntentKey);
        Assert.Equal("west", initial.Detail);
        Assert.EndsWith(" miles west", initial.Text);
        Assert.NotEqual("west", OriginalRadioDirection.FromBearing(aircraft.Heading));
        Assert.Equal(aircraft.Position.Distance(airport.Observer) / 1852, initial.DistanceNm);
        Assert.Equal("WEST1: " + initial.Text, Assert.Single(log));
        Assert.Equal("west", JsonSerializer.Deserialize<AircraftRadioMessage>(JsonSerializer.Serialize(initial))!.Detail);
        Assert.True(simulation.Execute("WEST1/SG").Success);
        Assert.Equal(2, radio.Count);
        Assert.Equal(initial.Detail, radio[1].Detail);
        Assert.Equal(initial.Text, radio[1].Text);
    }
}
