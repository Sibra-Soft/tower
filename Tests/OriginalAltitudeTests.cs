using System.Text.Json;
using System.Text.Json.Nodes;
using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class OriginalAltitudeTests
{
    private static Simulation Create(bool verified = true)
    {
        var airport = Airport.Training();
        airport.Original.TowerElevationFeetMsl = 500;
        airport.Original.SectorElevationFeetMsl = 750;
        airport.Original.FieldElevationFeetMsl = verified ? 500 : null;
        airport.Original.PatternCategoryByType["TEST"] = 1;
        airport.Original.PatternValues["cat1_altit"] = 1000;
        airport.AircraftTypes["TEST"] = new() { Type = "TEST", WeightCategory = 1 };
        return new(new() { Airport = airport, Options = new() { AircraftPerMinute = 0 } });
    }

    private static OriginalFlightPlan Plan(OriginalAltitudeReference reference = OriginalAltitudeReference.Msl) => new()
    {
        AltitudeReference = reference,
        Altitude1Feet = 2000,
        Altitude2Feet = 9000,
        Squawk = "1200",
        RouteTokens = ["ORIGIN", "DEST", "Twr", "@TO", "@GG1", "@LP"]
    };

    [Theory]
    [InlineData(500, 6300, 5800)]
    [InlineData(12, 4000, 3988)]
    [InlineData(667, 3000, 2333)]
    [InlineData(-100, 1000, 1100)]
    public void Msl_arrival_initial_height_is_converted_once_using_twr_field_elevation(double elevation, double initial, double expected)
    {
        var simulation = Create();
        simulation.State.Airport.Original.FieldElevationFeetMsl = elevation;
        var plan = Plan();
        plan.Altitude1Feet = initial;
        plan.OriginBearingDegrees = 270;
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "ARR", "TEST", originalPlan: plan);
        Assert.Equal(expected, aircraft.AltitudeFeet);
        Assert.False(aircraft.LandingCleared);
        Assert.Equal(initial, aircraft.OriginalPlan!.Altitude1Feet);
        Assert.Equal(9000, aircraft.OriginalPlan.Altitude2Feet);
    }

    [Theory]
    [InlineData(false, OriginalAltitudeReference.Msl)]
    [InlineData(true, OriginalAltitudeReference.Unresolved)]
    public void Missing_field_or_legacy_plan_retains_existing_initial_height(bool verified, OriginalAltitudeReference reference)
    {
        var simulation = Create(verified);
        var baseline = Create(verified).AddAircraft(FlightDirection.Arrival, "BASE", "TEST");
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "ARR", "TEST", originalPlan: Plan(reference));
        Assert.Equal(baseline.AltitudeFeet, aircraft.AltitudeFeet);
    }

    [Theory]
    [InlineData(true, 500)]
    [InlineData(false, 1000)]
    public void Pattern_source_altitude_is_absolute_for_new_imports_and_legacy_saves_keep_agl(bool verified, double expected)
    {
        var simulation = Create(verified);
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "ARR", "TEST");
        Assert.True(simulation.Execute("ARR/ELD").Success);
        aircraft.AltitudeFeet = 1200;
        aircraft.ExtendDownwind = true;
        aircraft.PathIndex = 1;
        simulation.Advance(60);
        Assert.Equal(expected, aircraft.AltitudeFeet, 6);
        Assert.Equal(1000, simulation.State.Airport.Original.PatternAltitude("TEST"));
    }

    [Fact]
    public void Standard_sim_departure_climbs_to_altitude_one_and_does_not_jump_to_route_altitude_two()
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "DEP", "TEST", originalPlan: Plan());
        Assert.Equal(0, aircraft.AltitudeFeet);
        Assert.Equal(FlightPhase.AtGate, aircraft.Phase);
        Assert.True(aircraft.FollowOriginalDepartureAltitude);
        aircraft.Phase = FlightPhase.Departing;
        aircraft.Position = simulation.State.Airport.Point("R18");
        aircraft.AltitudeFeet = 30;
        simulation.Advance(60);
        Assert.Equal(1500, aircraft.AltitudeFeet, 6);
        simulation.Advance(5);
        Assert.Equal(1500, aircraft.AltitudeFeet, 6);
    }

    [Fact]
    public void Controller_breakout_cancels_automatic_sim_climb_target()
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "DEP", "TEST", originalPlan: Plan());
        aircraft.Phase = FlightPhase.Departing;
        aircraft.AltitudeFeet = 600;
        Assert.True(aircraft.FollowOriginalDepartureAltitude);
        Assert.True(simulation.Execute("DEP/B").Success);
        Assert.False(aircraft.FollowOriginalDepartureAltitude);
    }

    [Theory]
    [InlineData("@LP", true)]
    [InlineData("@NL", true)]
    [InlineData("@TG", false)]
    [InlineData("@LA", false)]
    [InlineData("@FS", false)]
    public void Only_confirmed_post_takeoff_actions_enable_the_climb_target(string nextAction, bool expected)
    {
        var simulation = Create();
        var plan = Plan();
        plan.RouteTokens = ["@TO", "@GG1", nextAction];
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "DEP", "TEST", originalPlan: plan);
        Assert.Equal(expected, aircraft.FollowOriginalDepartureAltitude);
    }

    [Fact]
    public void Below_field_source_height_is_preserved_without_putting_aircraft_below_the_map()
    {
        var simulation = Create();
        var plan = Plan();
        plan.Altitude1Feet = 100;
        Assert.Null(simulation.State.Airport.Original.SourceMslToAgl(plan.Altitude1Feet));
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "ARR", "TEST", originalPlan: plan);
        Assert.True(aircraft.AltitudeFeet > 0);
        Assert.Equal(100, aircraft.OriginalPlan!.Altitude1Feet);
    }

    [Fact]
    public void Save_resume_preserves_reference_and_target_without_reapplying_initial_height()
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "DEP", "TEST", originalPlan: Plan());
        aircraft.Phase = FlightPhase.Departing;
        aircraft.Position = simulation.State.Airport.Point("R18");
        aircraft.AltitudeFeet = 600;
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            JsonStore.Write(path, simulation.State);
            var resumed = new Simulation(JsonStore.LoadSession(path));
            Assert.Equal(600, resumed.State.Aircraft[0].AltitudeFeet);
            Assert.Equal(500, resumed.State.Airport.Original.FieldElevationFeetMsl);
            Assert.Equal(OriginalAltitudeReference.Msl, resumed.State.Aircraft[0].OriginalPlan!.AltitudeReference);
            simulation.Advance(10);
            resumed.Advance(10);
            Assert.Equal(aircraft.AltitudeFeet, resumed.State.Aircraft[0].AltitudeFeet);
            Assert.Equal(aircraft.Position, resumed.State.Aircraft[0].Position);
            Assert.True(resumed.State.Aircraft[0].FollowOriginalDepartureAltitude);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Legacy_json_does_not_activate_msl_conversion_from_previously_preserved_raw_elevation()
    {
        var simulation = Create(false);
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "ARR", "TEST", originalPlan: Plan(OriginalAltitudeReference.Unresolved));
        aircraft.AltitudeFeet = 1200;
        var json = JsonNode.Parse(JsonSerializer.Serialize(simulation.State, JsonStore.Options))!;
        json["Airport"]!["Original"]!.AsObject().Remove("FieldElevationFeetMsl");
        json["Aircraft"]![0]!["OriginalPlan"]!.AsObject().Remove("AltitudeReference");
        json["Aircraft"]![0]!.AsObject().Remove("FollowOriginalDepartureAltitude");
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, json.ToJsonString());
            var restored = JsonStore.LoadSession(path);
            Assert.Null(restored.Airport.Original.FieldElevationFeetMsl);
            Assert.Equal(1000, restored.Airport.Original.PatternAltitudeAgl("TEST"));
            Assert.Equal(OriginalAltitudeReference.Unresolved, restored.Aircraft[0].OriginalPlan!.AltitudeReference);
            Assert.Equal(1200, restored.Aircraft[0].AltitudeFeet);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("CYN", 500, 750)]
    [Trait("Category", "Integration")]
    public void Real_airport_imports_use_twr_elevation_and_mark_sim_values_msl(string code, double twr, double sec)
    {
        var directory = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Assert.Skip("Set TOWER_ORIGINAL_DATA to verify original altitude imports.");
            return;
        }
        var airport = OriginalData.LoadAirport(directory, code);
        Assert.Equal(twr, airport.Original.FieldElevationFeetMsl);
        Assert.Equal(sec, airport.Original.SectorElevationFeetMsl);
        var path = OriginalData.Find(directory, code + "_ARR.SIM")!;
        var session = OriginalData.ReadScenario(path, airport);
        Assert.All(session.Schedule, flight => Assert.Equal(OriginalAltitudeReference.Msl, flight.OriginalPlan!.AltitudeReference));
        var first = session.Schedule[0];
        var aircraft = new Simulation(session).AddAircraft(FlightDirection.Arrival, first.CallSign, first.Type, first.Runway, originalPlan: first.OriginalPlan);
        Assert.Equal(first.OriginalPlan!.Altitude1Feet - twr, aircraft.AltitudeFeet, 6);
    }
}
