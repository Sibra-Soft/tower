using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class GeneratedDepartureTests
{
    private static string Data => Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA")
        ?? throw new InvalidOperationException("Set TOWER_ORIGINAL_DATA.");

    private static Simulation Create(bool enabled = true)
    {
        var airport = Airport.Training();
        airport.Original.UseOriginalGeneratedPerformance = true;
        airport.Original.UseGeneratedDepartureRoutes = enabled;
        airport.Original.FieldElevationFeetMsl = 500;
        airport.Original.Fixes.Add("EXIT", new("EXIT", airport.Latitude + 1, airport.Longitude + 1));
        airport.Original.DepartureFlow = [new("EXIT", 100)];
        airport.AircraftTypes["TEST"] = new() { Type = "TEST", OriginalPerformance = new() { ["Cruise_Speed"] = 200 } };
        return new(new Session { Airport = airport, Options = new() { AircraftPerMinute = 0 } });
    }

    private static void Until(Simulation simulation, Func<bool> done, int seconds = 2400)
    {
        for (var i = 0; i < seconds && !done(); i++)
        {
            simulation.Advance(1);
        }
        Assert.True(done(), "Generated departure did not reach the expected stage.");
    }

    private static Aircraft EnterRoute(Simulation simulation)
    {
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "DEP", "TEST");
        var progress = aircraft.GeneratedDeparture!;
        aircraft.Phase = FlightPhase.Departing;
        aircraft.AltitudeFeet = progress.FirstAltitudeFeetAgl;
        aircraft.SpeedKnots = 180;
        var runway = simulation.State.Airport.GetRunway(aircraft.Runway);
        var start = simulation.State.Airport.Point(runway.Threshold);
        var end = simulation.State.Airport.Point(runway.End);
        aircraft.Position = end.Offset(start.Bearing(end), 100);
        simulation.Advance(0.05);
        Assert.Equal(GeneratedDepartureStage.ToDestination, progress.Stage);
        return aircraft;
    }

    [Fact]
    public void Both_runway_end_and_first_height_precede_route_and_second_height()
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "DEP", "TEST");
        var route = aircraft.GeneratedDeparture!;
        Assert.Null(aircraft.OriginalPlan);
        Assert.Equal(0, aircraft.AltitudeFeet);
        Assert.Equal(0, aircraft.SpeedKnots);
        Assert.False(aircraft.LandingCleared);
        Assert.Equal(aircraft.GeneratedPerformance!.Altitude1FeetMsl - 500, route.FirstAltitudeFeetAgl, 6);
        Assert.Equal(aircraft.GeneratedPerformance.Altitude2FeetMsl - 500, route.SecondAltitudeFeetAgl!.Value, 6);
        aircraft.Phase = FlightPhase.Departing;
        aircraft.SpeedKnots = 180;
        aircraft.AltitudeFeet = route.FirstAltitudeFeetAgl;
        aircraft.Position = simulation.State.Airport.Point("R18");
        simulation.Advance(1);
        Assert.Equal(GeneratedDepartureStage.InitialClimb, route.Stage);
        Assert.False(route.Altitude2Active);
        Assert.Equal(180, aircraft.Heading);
        aircraft.Position = simulation.State.Airport.Point("R36").Offset(180, 100);
        aircraft.AltitudeFeet = route.FirstAltitudeFeetAgl - 100;
        simulation.Advance(1);
        Assert.Equal(GeneratedDepartureStage.InitialClimb, route.Stage);
        Until(simulation, () => route.Stage == GeneratedDepartureStage.ToDestination);
        Assert.InRange(Math.Abs(aircraft.AltitudeFeet - route.FirstAltitudeFeetAgl), 0, 30);
        Assert.True(route.Altitude2Active);
        Until(simulation, () => Math.Abs(aircraft.AltitudeFeet - route.SecondAltitudeFeetAgl!.Value) < 0.01);
        var target = route.SecondAltitudeFeetAgl.Value;
        simulation.Advance(5);
        Assert.Equal(target, aircraft.AltitudeFeet, 6);
    }

    [Theory]
    [InlineData("CYN", "9", "G1")]
    [Trait("Category", "Integration")]
    public void Full_generated_departure_from_pushback_to_destination(string code, string runway, string gate)
    {
        var airport = OriginalData.LoadAirport(Data, code);
        var simulation = new Simulation(new Session { Airport = airport, Options = new() { Runway = runway, AircraftPerMinute = 0 } });
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "AAL1535", "C130", runway, gate);
        var progress = Assert.IsType<GeneratedDepartureProgress>(aircraft.GeneratedDeparture);
        Assert.Equal(airport.Original.Fixes[aircraft.GeneratedRoute!.Destination].ProjectFrom(airport.Latitude, airport.Longitude, airport.Observer), progress.Destination.Position);
        Assert.True(simulation.Execute("AAL1535/PB").Success);
        Until(simulation, () => aircraft.Phase == FlightPhase.ReadyForTaxi);
        Assert.True(simulation.Execute("AAL1535/T" + runway).Success);
        Until(simulation, () => aircraft.Phase == FlightPhase.HoldingShort);
        Assert.Equal(GeneratedDepartureStage.InitialClimb, progress.Stage);
        Assert.True(simulation.Execute("AAL1535/CT").Success);
        Until(simulation, () => progress.Stage == GeneratedDepartureStage.ToDestination);
        Assert.True(progress.Altitude2Active);
        Assert.True(aircraft.OriginalDepartureRouteStarted);
        Assert.Null(aircraft.OriginalPlan);
        Until(simulation, () => aircraft.Phase == FlightPhase.Completed);
        Assert.InRange(aircraft.Position.Distance(progress.Destination.Position), 0, 0.001);
        Assert.Equal(GeneratedDepartureStage.Finished, progress.Stage);
        Assert.Equal(1, simulation.State.Performance.Departures);
        simulation.Advance(10);
        Assert.Equal(1, simulation.State.Performance.Departures);
    }

    [Fact]
    public void Beyond_eight_miles_does_not_complete_a_route_to_a_far_fix()
    {
        var simulation = Create();
        var aircraft = EnterRoute(simulation);
        simulation.Advance(240);
        Assert.True(aircraft.Position.Distance(simulation.State.Airport.Point("R36")) > 8 * 1852);
        Assert.Equal(FlightPhase.Departing, aircraft.Phase);
        Assert.Equal(0, simulation.State.Performance.Departures);
        Assert.True(simulation.Execute("DEP/FD").Success);
        Assert.Equal(GeneratedDepartureStage.Finished, aircraft.GeneratedDeparture!.Stage);
        Assert.Equal(1, simulation.State.Performance.Departures);
    }

    [Theory]
    [InlineData("B")]
    [InlineData("AT")]
    [InlineData("ALT")]
    [InlineData("ART")]
    [InlineData("URH090")]
    public void Controller_override_cancels_generated_route_and_automatic_height(string command)
    {
        var simulation = Create();
        var aircraft = EnterRoute(simulation);
        Assert.True(simulation.Execute("DEP/" + command).Success);
        Assert.Equal(GeneratedDepartureStage.Cancelled, aircraft.GeneratedDeparture!.Stage);
        Assert.False(aircraft.FollowOriginalDepartureRoute);
        Assert.False(aircraft.FollowOriginalDepartureAltitude);
        Until(simulation, () => aircraft.Phase != FlightPhase.Vectoring, 150);
        Assert.True(simulation.Execute("DEP/FD").Success);
        Assert.Equal(1, simulation.State.Performance.Departures);
    }

    [Fact]
    public void Full_orbit_temporarily_suspends_then_resumes_the_route()
    {
        var simulation = Create();
        var aircraft = EnterRoute(simulation);
        var progress = aircraft.GeneratedDeparture!;
        Assert.True(simulation.Execute("DEP/ML3").Success);
        Assert.Equal(FlightPhase.Vectoring, aircraft.Phase);
        Assert.Equal(GeneratedDepartureStage.ToDestination, progress.Stage);
        Until(simulation, () => aircraft.Phase == FlightPhase.Departing, 150);
        simulation.Advance(1);
        Assert.Equal(GeneratedDepartureStage.ToDestination, progress.Stage);
        Assert.True(aircraft.FollowOriginalDepartureRoute);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Save_resume_preserves_route_snapshot_stage_height_and_next_random_values(bool following)
    {
        var simulation = Create();
        var aircraft = following ? EnterRoute(simulation) : simulation.AddAircraft(FlightDirection.Departure, "DEP", "TEST");
        var route = aircraft.GeneratedDeparture!;
        var path = Path.Combine(Path.GetTempPath(), "Departure-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            JsonStore.Write(path, simulation.State);
            var loaded = JsonStore.LoadSession(path);
            loaded.Airport.Original.Fixes["EXIT"] = new("EXIT", 0, 0);
            var resumed = new Simulation(loaded);
            var restored = Assert.Single(resumed.State.Aircraft);
            Assert.Equal(route.Destination, restored.GeneratedDeparture!.Destination);
            Assert.Equal(route.Stage, restored.GeneratedDeparture.Stage);
            Assert.Equal(route.Altitude2Active, restored.GeneratedDeparture.Altitude2Active);
            Assert.Equal(simulation.State.TrafficFlowRandomState, resumed.State.TrafficFlowRandomState);
            var position = restored.Position;
            resumed.Paused = true;
            resumed.Advance(10);
            Assert.Equal(position, restored.Position);
            resumed.Paused = false;
            simulation.Advance(5);
            resumed.Advance(5);
            Assert.Equal(aircraft.Position, restored.Position);
            Assert.Equal(aircraft.AltitudeFeet, restored.AltitudeFeet);
            restored.GeneratedDeparture.NextPointIndex = 2;
            JsonStore.Write(path, resumed.State);
            Assert.Throws<InvalidDataException>(() => JsonStore.LoadSession(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Legacy_and_authored_plans_are_not_reinterpreted()
    {
        var legacy = Create(false).AddAircraft(FlightDirection.Departure, "OLD", "TEST");
        Assert.NotNull(legacy.GeneratedPerformance);
        Assert.Null(legacy.GeneratedDeparture);
        var simulation = Create();
        var plan = new OriginalFlightPlan { AltitudeReference = OriginalAltitudeReference.Msl, Altitude1Feet = 3000, Altitude2Feet = 6000 };
        var authored = simulation.AddAircraft(FlightDirection.Departure, "SIM", "TEST", originalPlan: plan);
        Assert.Same(plan, authored.OriginalPlan);
        Assert.Null(authored.GeneratedDeparture);
        Assert.Null(authored.GeneratedPerformance);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cancelled_or_handed_off_departure_stays_terminal_after_loading(bool cancelled)
    {
        var simulation = Create();
        var aircraft = EnterRoute(simulation);
        Assert.True(simulation.Execute(cancelled ? "DEP/AT" : "DEP/FD").Success);
        var path = Path.Combine(Path.GetTempPath(), "DepartureTerminal-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            JsonStore.Write(path, simulation.State);
            var restored = new Simulation(JsonStore.LoadSession(path));
            var departure = Assert.Single(restored.State.Aircraft);
            restored.Advance(1);
            Assert.Equal(cancelled ? GeneratedDepartureStage.Cancelled : GeneratedDepartureStage.Finished,
                departure.GeneratedDeparture!.Stage);
            Assert.Equal(cancelled ? 0 : 1, restored.State.Performance.Departures);
            if (cancelled)
            {
                Assert.False(departure.FollowOriginalDepartureRoute);
                Assert.False(departure.FollowOriginalDepartureAltitude);
            }
            else
            {
                Assert.Equal(FlightPhase.Completed, departure.Phase);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }
}
