using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class SpeedPerformanceTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("NaN")]
    public void Invalid_original_acceleration_rates_are_rejected(string value)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, $"Type: TEST\nTaxi_Acceleration_Rate: {value}\n");
            Assert.Throws<InvalidDataException>(() => OriginalData.ReadPerformance(path, Airport.Training()));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Rates_convert_units_and_choose_the_correct_mode()
    {
        var specification = new AircraftSpecification
        {
            Acceleration = new()
            {
                AirborneAcceleration = 7,
                AirborneDeceleration = 6,
                TaxiAcceleration = 3,
                TaxiDeceleration = 5
            }
        };

        Assert.Equal(0.3048 / (1852.0 / 3600), SpeedPerformance.KnotsPerSecond(1), 10);
        Assert.Equal(SpeedPerformance.KnotsPerSecond(7), SpeedPerformance.Rate(specification, true, true, 2));
        Assert.Equal(SpeedPerformance.KnotsPerSecond(6), SpeedPerformance.Rate(specification, true, false, 2));
        Assert.Equal(SpeedPerformance.KnotsPerSecond(3), SpeedPerformance.Rate(specification, false, true, 3));
        Assert.Equal(SpeedPerformance.KnotsPerSecond(5), SpeedPerformance.Rate(specification, false, false, 3));
        specification.Acceleration.TaxiDeceleration = null;
        Assert.Equal(8, SpeedPerformance.Rate(specification, false, false, 8));
        specification.Acceleration.TaxiDeceleration = double.NaN;
        Assert.False(specification.Acceleration.IsValid());
        Assert.Equal(8, SpeedPerformance.Rate(specification, false, false, 8));
    }

    [Fact]
    public void Taxi_and_airborne_motion_apply_rates_in_simulation_seconds()
    {
        var simulation = new Simulation(new Session { Options = new() { AircraftPerMinute = 0 } });
        var departure = simulation.AddAircraft(FlightDirection.Departure, "TAXI");
        departure.Specification.Acceleration.TaxiAcceleration = 3;
        Assert.True(simulation.Execute("TAXI/T18").Success);
        simulation.Advance(1);
        Assert.Equal(SpeedPerformance.KnotsPerSecond(3), departure.SpeedKnots, 6);

        departure.Phase = FlightPhase.Departing;
        departure.SpeedKnots = 180;
        departure.Specification.Acceleration.AirborneAcceleration = 7;
        simulation.Advance(1);
        Assert.Equal(180 + SpeedPerformance.KnotsPerSecond(7), departure.SpeedKnots, 6);

        var arrival = simulation.AddAircraft(FlightDirection.Arrival, "SLOW");
        arrival.SpeedKnots = 160;
        arrival.TargetSpeedKnots = 140;
        arrival.Specification.Acceleration.AirborneDeceleration = 6;
        simulation.Advance(1);
        Assert.Equal(160 - SpeedPerformance.KnotsPerSecond(6), arrival.SpeedKnots, 6);
        simulation.Advance(10);
        Assert.Equal(140, arrival.SpeedKnots, 6);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Original_profiles_keep_all_four_rates_when_serialized()
    {
        var directory = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Assert.Skip("Set TOWER_ORIGINAL_DATA to verify original acceleration profiles.");
            return;
        }

        foreach (var code in new[] { "CYN" })
        {
            var airport = OriginalData.LoadAirport(directory, code);
            foreach (var specification in airport.AircraftTypes.Values)
            {
                Assert.Equal(specification.OriginalPerformance["Airborne_Acceleration_Rate"], specification.Acceleration.AirborneAcceleration);
                Assert.Equal(specification.OriginalPerformance["Airborne_Deceleration_Rate"], specification.Acceleration.AirborneDeceleration);
                Assert.Equal(specification.OriginalPerformance["Taxi_Acceleration_Rate"], specification.Acceleration.TaxiAcceleration);
                Assert.Equal(specification.OriginalPerformance["Taxi_Deceleration_Rate"], specification.Acceleration.TaxiDeceleration);
                Assert.True(specification.Acceleration.IsValid());
            }

            var original = airport.AircraftTypes["C182"];
            var json = System.Text.Json.JsonSerializer.Serialize(original, JsonStore.Options);
            var restored = System.Text.Json.JsonSerializer.Deserialize<AircraftSpecification>(json, JsonStore.Options)!;
            Assert.Equal(3, restored.Acceleration.TaxiAcceleration);
            Assert.Equal(5, restored.Acceleration.TaxiDeceleration);
        }
    }
}
