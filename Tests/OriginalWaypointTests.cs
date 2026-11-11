using System.Globalization;
using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class OriginalWaypointTests
{
    private static Airport Airport()
    {
        var airport = Tower.Core.Airport.Training();
        airport.Code = "FIELD";
        airport.Latitude = 51;
        airport.Longitude = -96;
        airport.Original.FieldElevationFeetMsl = 500;
        airport.AircraftTypes["PLANE"] = new() { Type = "PLANE" };
        return airport;
    }

    private static string Scenario(string waypoints, string route = "LOCAL FIELD Twr", bool departure = false) => $"""
        [SimInfo]
        Sector FIELD.SEC
        Aircraft 1
        [Waypoints]
        {waypoints}
        [Aircraft]
        TEST PLANE VFR -1 {(departure ? "d" : "a")} 00:00 200 2000 4000 1200 18
          {route} {(departure ? "@TO @LP" : "@FS")}
        """;

    private static Session Read(string text, Airport? airport = null)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".SIM");
        try
        {
            File.WriteAllText(path, text);
            return OriginalData.ReadScenario(path, airport ?? Airport());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("5115.00N/09630.00W", 51.25, -96.5)]
    [InlineData("51d15m00.00sN/096d30m00.00sW", 51.25, -96.5)]
    [InlineData("5115.00s/09630.00e", -51.25, 96.5)]
    [InlineData("51d15m30sS/096d30m00sE", -51.25833333333333, 96.5)]
    [InlineData("9000N/18000E", 90, 180)]
    [InlineData("0000N/00000W", 0, 0)]
    public void Coordinates_follow_confirmed_compact_and_dms_notation(string coordinate, double latitude, double longitude)
    {
        var session = Read(Scenario("LOCAL " + coordinate + " 123"));
        var waypoint = Assert.Single(session.OriginalScenario!.Waypoints);
        Assert.Equal(latitude, waypoint.Latitude, 8);
        Assert.Equal(longitude, waypoint.Longitude, 8);
        Assert.Equal(coordinate, waypoint.CoordinateText);
        Assert.Equal("123", waypoint.AdditionalField);
        Assert.Equal("LOCAL", waypoint.Name);
        Assert.Single(session.Schedule[0].OriginalPlan!.ResolvedRoute!);
    }

    [Theory]
    [InlineData("LOCAL 5160.00N/09600.00W 0")]
    [InlineData("LOCAL 5100.00N/09660.00W 0")]
    [InlineData("LOCAL 9100.00N/09600.00W 0")]
    [InlineData("LOCAL 9000.01N/09600.00W 0")]
    [InlineData("LOCAL 5100.00N/18000.01W 0")]
    [InlineData("LOCAL 51d00m60sN/096d00m00sW 0")]
    [InlineData("LOCAL NaN 0")]
    [InlineData("LOCAL 5115,00N/09600.00W 0")]
    [InlineData("LOCAL 5115.00N/09600.00W")]
    [InlineData("LOCAL 51D15M30S S/096D30M00SE 0")]
    [InlineData("LOCAL 5115.00N/09600.00W 0\nlocal 5116.00N/09600.00W 0")]
    public void Malformed_or_duplicate_waypoints_are_rejected(string definition)
    {
        Assert.Throws<InvalidDataException>(() => Read(Scenario(definition)));
    }

    [Fact]
    public void Custom_fixes_are_scenario_scoped_and_shared_sec_definitions_keep_priority()
    {
        var airport = Airport();
        var existing = new GeographicFix("SEC", 52, -97);
        airport.Original.Fixes["SEC"] = existing;
        var session = Read(Scenario("LOCAL 5115.00N/09630.00W 0\nSEC 5300.00N/09800.00W 0", "LOCAL SEC FIELD Twr"), airport);
        var points = session.Schedule[0].OriginalPlan!.ResolvedRoute!;
        Assert.Equal(2, points.Count);
        Assert.Equal(existing.ProjectFrom(airport.Latitude, airport.Longitude, airport.Observer), points[1].Position);
        Assert.False(airport.Original.Fixes.ContainsKey("LOCAL"));
        Assert.Same(existing, airport.Original.Fixes["SEC"]);
        Assert.Equal(2, session.OriginalScenario!.Waypoints.Count);
        var another = Read(Scenario("", "LOCAL FIELD Twr"), airport);
        Assert.Null(another.Schedule[0].OriginalPlan!.ResolvedRoute);
    }

    [Fact]
    public void Mixed_sec_airway_and_custom_waypoint_routes_resolve_in_source_order()
    {
        var airport = Airport();
        airport.Original.Fixes["SEC"] = new("SEC", 51.5, -96);
        airport.Original.Airways["V1"] = new() { Segments = [["LOCAL", "MID", "SEC"]] };
        var text = Scenario("LOCAL 5115.00N/09630.00W 0\nMID 5120.00N/09615.00W 0", "LOCAL V1 SEC FIELD Twr");
        var session = Read(text, airport);
        Assert.Equal(new[] { "LOCAL", "MID", "SEC" }, session.Schedule[0].OriginalPlan!.ResolvedRoute!.Select(point => point.Name));
        Assert.Contains("LOCAL V1 SEC FIELD Twr", string.Join('\n', session.OriginalScenario!.SourceLines));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Custom_arrival_and_departure_fixes_start_real_route_progress(bool departure)
    {
        var airport = Airport();
        var session = Read(Scenario("LOCAL 5115.00N/09630.00W 0", departure ? "FIELD LOCAL Ctr" : "LOCAL FIELD Twr", departure), airport);
        var plan = session.Schedule[0].OriginalPlan!;
        Assert.NotNull(departure ? plan.DestinationBearingDegrees : plan.OriginBearingDegrees);
        var simulation = new Simulation(session);
        simulation.Advance(0.05);
        var aircraft = Assert.Single(session.Aircraft);
        Assert.NotNull(aircraft.OriginalRoute);
        Assert.Equal("LOCAL", aircraft.OriginalRoute!.Points[0].Name);
        Assert.Equal(departure ? OriginalRouteStage.InitialClimb : OriginalRouteStage.FollowingFixes, aircraft.OriginalRoute.Stage);
        Assert.False(aircraft.LandingCleared);
        if (!departure)
        {
            Assert.True(aircraft.Position.X < airport.Observer.X);
        }
    }

    [Fact]
    public void Save_resume_keeps_source_definitions_and_active_route_without_reimporting()
    {
        var session = Read(Scenario("LOCAL 5115.00N/09630.00W SOME_TAG"));
        var simulation = new Simulation(session);
        simulation.Advance(1);
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            JsonStore.Write(path, session);
            var resumed = new Simulation(JsonStore.LoadSession(path));
            Assert.Equal(session.OriginalScenario!.Waypoints, resumed.State.OriginalScenario!.Waypoints);
            Assert.Equal("SOME_TAG", resumed.State.OriginalScenario.Waypoints[0].AdditionalField);
            simulation.Advance(10);
            resumed.Advance(10);
            Assert.Equal(session.Aircraft[0].Position, resumed.State.Aircraft[0].Position);
            Assert.Equal(session.Aircraft[0].AltitudeFeet, resumed.State.Aircraft[0].AltitudeFeet);
            Assert.Equal(session.Aircraft[0].OriginalRoute!.NextPointIndex, resumed.State.Aircraft[0].OriginalRoute!.NextPointIndex);
            Assert.False(resumed.State.Airport.Original.Fixes.ContainsKey("LOCAL"));
            resumed.State.OriginalScenario.Waypoints.Add(new("LOCAL", "0000N/00000E", "0", 0, 0));
            JsonStore.Write(path, resumed.State);
            Assert.Throws<InvalidDataException>(() => JsonStore.LoadSession(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Coordinate_parsing_is_independent_of_windows_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("nl-NL");
            var session = Read(Scenario("LOCAL 5115.50N/09630.50W 0"));
            Assert.Equal(51 + 15.5 / 60, session.OriginalScenario!.Waypoints[0].Latitude, 8);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Read_schedule_uses_the_same_custom_fix_resolution()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".SIM");
        try
        {
            File.WriteAllText(path, Scenario("LOCAL 5115.00N/09630.00W 0"));
            Assert.Equal("LOCAL", OriginalData.ReadSchedule(path, Airport())[0].OriginalPlan!.ResolvedRoute![0].Name);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Legacy_save_without_waypoint_metadata_does_not_reparse_source_text()
    {
        var session = new Session
        {
            Airport = Airport(),
            OriginalScenario = new() { SourceLines = Scenario("LOCAL 5115.00N/09630.00W 0").Split('\n') }
        };
        var json = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(session, JsonStore.Options))!;
        json["OriginalScenario"]!.AsObject().Remove("Waypoints");
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, json.ToJsonString());
            var loaded = JsonStore.LoadSession(path);
            Assert.Empty(loaded.OriginalScenario!.Waypoints);
            Assert.False(loaded.Airport.Original.Fixes.ContainsKey("LOCAL"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
