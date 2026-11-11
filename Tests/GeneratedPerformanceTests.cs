using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class GeneratedPerformanceTests
{
    [Theory]
    [InlineData(100, 0, 10)]
    [InlineData(101, 10, 25)]
    [InlineData(150, 10, 25)]
    [InlineData(151, 25, 40)]
    [InlineData(200, 25, 40)]
    [InlineData(201, 40, 50)]
    public void Arrival_height_ranges_follow_rounded_prf_cruise_class(double cruise, int minimum, int maximum)
    {
        var calls = 0;
        var result = GeneratedFlightPerformance.Create(FlightDirection.Arrival, cruise, 1000, (low, high) =>
        {
            calls++;
            Assert.Equal(minimum, low);
            Assert.Equal(maximum, high);
            return high;
        });
        Assert.Equal(1, calls);
        Assert.Equal(78030 + (int)Math.Floor(maximum * 50 * 78.03 + 0.5), result.Altitude1Units);
        Assert.Equal(1000, result.Altitude2FeetMsl, 3);
    }

    [Theory]
    [InlineData(100, 0, 2)]
    [InlineData(101, 2, 5)]
    [InlineData(150, 2, 5)]
    [InlineData(151, 5, 8)]
    [InlineData(200, 5, 8)]
    [InlineData(201, 8, 10)]
    public void Departure_keeps_two_distinct_heights_and_original_rounding_order(double cruise, int minimum, int maximum)
    {
        var calls = 0;
        var result = GeneratedFlightPerformance.Create(FlightDirection.Departure, cruise, 1000, (low, high) =>
        {
            calls++;
            Assert.Equal(calls == 1 ? 4 : minimum, low);
            Assert.Equal(calls == 1 ? 10 : maximum, high);
            return calls == 1 ? 4 : high;
        });
        Assert.Equal(2, calls);
        Assert.Equal(312 * 500, result.Altitude1Units);
        Assert.Equal(result.Altitude1Units + (int)Math.Floor(maximum * 500 * 78.03 + 0.5), result.Altitude2Units);
        Assert.Equal(0, result.InitialSpeedKnots);
    }

    [Theory]
    [InlineData(7500, true)]
    [InlineData(8000, false)]
    public void Arrival_speed_uses_cruise_and_the_absolute_10000_foot_limit(double pattern, bool limited)
    {
        var result = GeneratedFlightPerformance.Create(FlightDirection.Arrival, 400, pattern, (_, high) => high);
        Assert.Equal(OriginalTimingUnits.SpeedKnots(limited ? 0x727 : OriginalTimingUnits.CruiseUnits(400)!.Value), result.InitialSpeedKnots);
    }

    private static Simulation Create(bool enabled = true)
    {
        var session = new Session();
        session.Options.AircraftPerMinute = 0;
        var metadata = session.Airport.Original;
        metadata.UseOriginalGeneratedPerformance = enabled;
        metadata.FieldElevationFeetMsl = 500;
        metadata.PatternCategoryByType["TEST"] = 1;
        metadata.PatternValues["cat1_altit"] = 1000;
        session.Airport.AircraftTypes["TEST"] = new() { Type = "TEST", OriginalPerformance = new() { ["Cruise_Speed"] = 120 } };
        return new(session);
    }

    [Fact]
    public void Initial_arrival_state_uses_msl_conversion_and_preserves_authored_plans_and_legacy_behavior()
    {
        var simulation = Create();
        var arrival = simulation.AddAircraft(FlightDirection.Arrival, "ARR", "TEST");
        Assert.Equal(arrival.GeneratedPerformance!.Altitude1FeetMsl - 500, arrival.AltitudeFeet);
        Assert.Equal(arrival.GeneratedPerformance.InitialSpeedKnots, arrival.SpeedKnots);
        Assert.Null(arrival.OriginalPlan);
        Assert.False(arrival.LandingCleared);
        Assert.Null(Create(false).AddAircraft(FlightDirection.Arrival, "OLD", "TEST").GeneratedPerformance);
        var plan = new OriginalFlightPlan { Altitude1Feet = 4000, AltitudeReference = OriginalAltitudeReference.Msl };
        var authored = simulation.AddAircraft(FlightDirection.Arrival, "SIM", "TEST", originalPlan: plan);
        Assert.Null(authored.GeneratedPerformance);
        Assert.Equal(3500, authored.AltitudeFeet);
    }

    [Fact]
    public void Departure_starts_stationary_and_captures_only_first_climb_height()
    {
        var simulation = Create();
        var departure = simulation.AddAircraft(FlightDirection.Departure, "DEP", "TEST");
        Assert.Equal(0, departure.AltitudeFeet);
        Assert.Equal(0, departure.SpeedKnots);
        Assert.Equal(FlightPhase.AtGate, departure.Phase);
        var target = departure.GeneratedPerformance!.Altitude1FeetMsl - 500;
        departure.Phase = FlightPhase.Departing;
        departure.AltitudeFeet = target - 1;
        departure.SpeedKnots = 180;
        simulation.Advance(1);
        Assert.Equal(target, departure.AltitudeFeet, 6);
        Assert.True(simulation.Execute("DEP/B").Success);
        Assert.False(departure.FollowOriginalDepartureAltitude);
    }

    [Fact]
    public void Saving_preserves_performance_and_next_generated_values()
    {
        var simulation = Create();
        simulation.AddAircraft(FlightDirection.Arrival, "FIRST", "TEST");
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            JsonStore.Write(path, simulation.State);
            var restored = new Simulation(JsonStore.LoadSession(path));
            Assert.Equal(simulation.State.Aircraft[0].GeneratedPerformance, restored.State.Aircraft[0].GeneratedPerformance);
            Assert.Equal(simulation.AddAircraft(FlightDirection.Arrival, "NEXT", "TEST").GeneratedPerformance,
                restored.AddAircraft(FlightDirection.Arrival, "NEXT", "TEST").GeneratedPerformance);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("CYN")]
    [Trait("Category", "Integration")]
    public void Fresh_original_imports_enable_generator_for_real_prf_profiles(string code)
    {
        var directory = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Assert.Skip("Set TOWER_ORIGINAL_DATA to verify original generated performance.");
            return;
        }
        var airport = OriginalData.LoadAirport(directory, code);
        Assert.True(airport.Original.UseOriginalGeneratedPerformance);
        var simulation = new Simulation(new() { Airport = airport, Options = new() { Runway = airport.Runways[0].Name } });
        foreach (var type in airport.AircraftTypes.Keys)
        {
            var arrival = simulation.AddAircraft(FlightDirection.Arrival, "ARR" + simulation.State.Aircraft.Count, type);
            Assert.NotNull(arrival.GeneratedPerformance);
            Assert.True(arrival.GeneratedPerformance.IsValid());
            Assert.True(arrival.SpeedKnots > 0);
        }
    }
}
