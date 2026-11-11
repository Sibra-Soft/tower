using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class OriginalRouteTests
{
    private static Simulation Create()
    {
        var airport = Airport.Training();
        airport.Code = "TEST";
        airport.Original.FieldElevationFeetMsl = 500;
        airport.Original.PatternCategoryByType["PLANE"] = 1;
        airport.Original.PatternValues["cat1_altit"] = 1000;
        airport.AircraftTypes["PLANE"] = new() { Type = "PLANE" };
        return new(new() { Airport = airport, Options = new() { AircraftPerMinute = 0 } });
    }

    private static OriginalFlightPlan Plan() => new()
    {
        AltitudeReference = OriginalAltitudeReference.Msl,
        Altitude1Feet = 2000,
        Altitude2Feet = 3000,
        Squawk = "1200",
        RouteTokens = ["TEST", "FIRST", "SECOND", "Ctr", "@TO", "@LP"],
        ResolvedRoute = [new("FIRST", new(-3000, 4000)), new("SECOND", new(-1000, 3000))]
    };

    [Theory]
    [InlineData("CL", LandingIntent.FullStop)]
    [InlineData("CG", LandingIntent.TouchAndGo)]
    [InlineData("CA", LandingIntent.LowApproach)]
    [InlineData("CO", LandingIntent.Option)]
    public void Arrival_passes_each_fix_then_joins_final_without_changing_clearance(string command, LandingIntent intent)
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "ARR", "PLANE", originalPlan: Plan());
        var route = aircraft.OriginalRoute!;
        Assert.Equal(new Position(-3000, 4000), aircraft.Position);
        Assert.Equal(1, route.NextPointIndex);
        Assert.Equal(1500, aircraft.AltitudeFeet);
        Assert.True(simulation.Execute("ARR/" + command).Success);
        aircraft.Position = route.Points[1].Position;
        simulation.Advance(0.05);
        Assert.Equal(2, route.NextPointIndex);
        Assert.Equal(OriginalRouteStage.FollowingFixes, route.Stage);
        aircraft.Position = route.Points[2].Position;
        simulation.Advance(0.05);
        Assert.Equal(OriginalRouteStage.Finished, route.Stage);
        Assert.Equal(FlightPhase.Approach, aircraft.Phase);
        Assert.True(aircraft.LandingCleared);
        Assert.Equal(intent, aircraft.LandingIntent);
        Assert.False(route.Altitude2Active);
        Assert.Equal(500, route.TargetAltitudeFeetAgl);
    }

    [Theory]
    [InlineData("@LP", true, 2500)]
    [InlineData("@NL", false, 1500)]
    public void Departure_route_starts_after_initial_climb_and_altitude_two_only_applies_to_lp(string action, bool active, double target)
    {
        var simulation = Create();
        var plan = Plan();
        plan.RouteTokens[^1] = action;
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "DEP", "PLANE", originalPlan: plan);
        var route = aircraft.OriginalRoute!;
        Assert.Equal(0, aircraft.AltitudeFeet);
        aircraft.Phase = FlightPhase.Departing;
        aircraft.Position = simulation.State.Airport.Point("R18");
        aircraft.AltitudeFeet = 1500;
        aircraft.SpeedKnots = 150;
        simulation.Advance(0.05);
        Assert.Equal(OriginalRouteStage.InitialClimb, route.Stage);
        Assert.False(route.Altitude2Active);
        aircraft.Position = simulation.State.Airport.Point("R36");
        aircraft.AltitudeFeet = 1400;
        simulation.Advance(0.05);
        Assert.Equal(OriginalRouteStage.InitialClimb, route.Stage);
        aircraft.AltitudeFeet = 1500;
        simulation.Advance(0.05);
        Assert.Equal(OriginalRouteStage.FollowingFixes, route.Stage);
        Assert.Equal(active, route.Altitude2Active);
        Assert.Equal(target, route.TargetAltitudeFeetAgl);
        Assert.Equal(1500, aircraft.AltitudeFeet);
        simulation.Advance(1);
        Assert.True(active ? aircraft.AltitudeFeet > 1500 : aircraft.AltitudeFeet == 1500);
    }

    [Fact]
    public void Routed_departure_finishes_at_last_fix_instead_of_the_old_eight_mile_boundary()
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "DEP", "PLANE", originalPlan: Plan());
        var route = aircraft.OriginalRoute!;
        aircraft.Phase = FlightPhase.Departing;
        route.Stage = OriginalRouteStage.FollowingFixes;
        route.Points = [new("DISTANT", new(0, -50000))];
        aircraft.Position = new(0, -30000);
        simulation.Advance(1);
        Assert.Equal(FlightPhase.Departing, aircraft.Phase);
        aircraft.Position = route.Points[0].Position;
        simulation.Advance(0.05);
        Assert.Equal(FlightPhase.Completed, aircraft.Phase);
        Assert.Equal(1, simulation.State.Performance.Departures);
        simulation.Advance(1);
        Assert.Equal(1, simulation.State.Performance.Departures);
    }

    [Theory]
    [InlineData("B")]
    [InlineData("ART")]
    [InlineData("PF5")]
    [InlineData("URH090")]
    public void Accepted_controller_override_cancels_route(string command)
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "ARR", "PLANE", originalPlan: Plan());
        Assert.True(simulation.Execute("ARR/" + command).Success);
        Assert.Equal(OriginalRouteStage.Cancelled, aircraft.OriginalRoute!.Stage);
    }

    [Fact]
    public void Invalid_override_and_full_orbit_preserve_route_progress()
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "ARR", "PLANE", originalPlan: Plan());
        Assert.False(simulation.Execute("ARR/PF5,99").Success);
        Assert.Equal(OriginalRouteStage.FollowingFixes, aircraft.OriginalRoute!.Stage);
        Assert.True(simulation.Execute("ARR/MR3").Success);
        simulation.Advance(121);
        Assert.Equal(FlightPhase.Circuit, aircraft.Phase);
        Assert.Equal(OriginalRouteStage.FollowingFixes, aircraft.OriginalRoute.Stage);
    }

    [Fact]
    public void Saved_route_resumes_same_fix_and_active_altitude_without_restarting()
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "DEP", "PLANE", originalPlan: Plan());
        aircraft.Phase = FlightPhase.Departing;
        aircraft.OriginalRoute!.Stage = OriginalRouteStage.FollowingFixes;
        aircraft.OriginalRoute.NextPointIndex = 1;
        aircraft.OriginalRoute.Altitude2Active = true;
        aircraft.OriginalRoute.TargetAltitudeFeetAgl = 2500;
        aircraft.AltitudeFeet = 1700;
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            JsonStore.Write(path, simulation.State);
            var resumed = new Simulation(JsonStore.LoadSession(path));
            Assert.Equal(1, resumed.State.Aircraft[0].OriginalRoute!.NextPointIndex);
            Assert.True(resumed.State.Aircraft[0].OriginalRoute!.Altitude2Active);
            simulation.Advance(10);
            resumed.Advance(10);
            Assert.Equal(aircraft.Position, resumed.State.Aircraft[0].Position);
            Assert.Equal(aircraft.AltitudeFeet, resumed.State.Aircraft[0].AltitudeFeet);
            resumed.Paused = true;
            var position = resumed.State.Aircraft[0].Position;
            resumed.Advance(30);
            Assert.Equal(position, resumed.State.Aircraft[0].Position);
            aircraft.OriginalRoute.NextPointIndex = 500;
            JsonStore.Write(path, simulation.State);
            Assert.Throws<InvalidDataException>(() => JsonStore.LoadSession(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("FIRST MID TEST Twr @FS", true)]
    [InlineData("FIRST UNKNOWN TEST Twr @FS", false)]
    public void Import_resolves_ordered_sec_points_and_keeps_unresolved_tokens_for_fallback(string tokens, bool resolved)
    {
        var airport = Create().State.Airport;
        airport.Original.Fixes["FIRST"] = new("FIRST", airport.Latitude + 0.1, airport.Longitude);
        airport.Original.Fixes["MID"] = new("MID", airport.Latitude, airport.Longitude - 0.1);
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".SIM");
        try
        {
            File.WriteAllText(path, $"[SimInfo]\nSector TEST.SEC\nAircraft 1\n[Aircraft]\nARR PLANE VFR -1 a 00:00 200 2000 1000 1200 18\n{tokens}\n");
            var session = OriginalData.ReadScenario(path, airport);
            var plan = session.Schedule[0].OriginalPlan!;
            Assert.Equal(resolved, plan.ResolvedRoute is not null);
            Assert.Equal(tokens.Split(' '), plan.RouteTokens);
            if (resolved)
            {
                var points = plan.ResolvedRoute!;
                Assert.Equal(new[] { "FIRST", "MID" }, points.Select(point => point.Name));
                Assert.True(points[0].Position.Y > airport.Observer.Y);
                Assert.True(points[1].Position.X < airport.Observer.X);
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
    public void Original_scenarios_resolve_the_supplied_sec_routes(string code)
    {
        var directory = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Assert.Skip("Set TOWER_ORIGINAL_DATA to verify original routes.");
            return;
        }
        var airport = OriginalData.LoadAirport(directory, code);
        foreach (var suffix in new[] { "ARR", "DEP", "MIX" })
        {
            var session = OriginalData.ReadScenario(OriginalData.Find(directory, code + "_" + suffix + ".SIM")!, airport);
            Assert.All(session.Schedule, flight =>
            {
                Assert.NotNull(flight.OriginalPlan!.ResolvedRoute);
                var simulation = new Simulation(new() { Airport = airport, Options = new() { AircraftPerMinute = 0 } });
                var aircraft = simulation.AddAircraft(flight.Direction, flight.CallSign, flight.Type, flight.Runway, flight.Gate, flight.OriginalPlan);
                Assert.NotNull(aircraft.OriginalRoute);
                Assert.True(aircraft.OriginalRoute!.IsValid());
            });
        }
    }
}
