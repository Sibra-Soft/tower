using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class OriginalActionTests
{
    private static (Simulation Simulation, Aircraft Aircraft) Create(string tokens, FlightDirection direction = FlightDirection.Arrival)
    {
        var simulation = new Simulation(new() { Options = new() { AircraftPerMinute = 0 } });
        var aircraft = simulation.AddAircraft(direction, "TEST", originalPlan: new()
        {
            Actions = OriginalActionProgress.Parse(tokens.Split(' ')),
            RouteTokens = tokens.Split(' '),
            Squawk = "1200"
        });
        Final(simulation, aircraft);
        return (simulation, aircraft);
    }

    private static void Final(Simulation simulation, Aircraft aircraft)
    {
        aircraft.Position = simulation.State.Airport.Point("R18");
        aircraft.Phase = FlightPhase.Approach;
        aircraft.Path = [aircraft.Position];
        aircraft.PathIndex = 0;
        aircraft.AltitudeFeet = 0;
        // This fixture starts at touchdown to isolate action sequencing; real flare is covered by FlightDynamicsTests.
        aircraft.TouchdownDistanceMetres = 0;
        aircraft.SpeedKnots = 120;
        aircraft.LandingCleared = false;
    }

    private static void CompleteRunwayPass(Simulation simulation, Aircraft aircraft)
    {
        aircraft.Path = [aircraft.Position];
        aircraft.PathIndex = 0;
        simulation.Advance(0.05);
    }

    [Theory]
    [InlineData("@FS", LandingIntent.FullStop)]
    [InlineData("@TG", LandingIntent.TouchAndGo)]
    [InlineData("@LA", LandingIntent.LowApproach)]
    public void Plan_requires_clearance_and_cl_selects_planned_operation(string tokens, LandingIntent intent)
    {
        var (simulation, aircraft) = Create(tokens);
        Assert.False(aircraft.LandingCleared);
        simulation.Advance(0.05);
        Assert.Equal(FlightPhase.GoAround, aircraft.Phase);
        Assert.Equal(0, aircraft.OriginalActions!.Index);
        Final(simulation, aircraft);
        Assert.True(simulation.Execute("TEST/CL").Success);
        Assert.Equal(intent, aircraft.LandingIntent);
    }

    [Theory]
    [InlineData("@TG @FS", FlightPhase.TakeoffRoll, FlightPhase.Circuit, 1, OriginalFlightAction.FullStop)]
    [InlineData("@LA @TG", FlightPhase.LowApproach, FlightPhase.Circuit, 1, OriginalFlightAction.TouchAndGo)]
    [InlineData("@LA @LP", FlightPhase.LowApproach, FlightPhase.Departing, 1, OriginalFlightAction.LeavePattern)]
    [InlineData("@TG @NL", FlightPhase.TakeoffRoll, FlightPhase.Departing, 1, OriginalFlightAction.NoLanding)]
    public void Completed_operation_advances_once_and_consumes_clearance(string tokens, FlightPhase runwayPhase, FlightPhase nextPhase, int index, OriginalFlightAction action)
    {
        var (simulation, aircraft) = Create(tokens);
        Assert.True(simulation.Execute("TEST/CL").Success);
        simulation.Advance(0.05);
        Assert.Equal(runwayPhase, aircraft.Phase);
        Assert.Equal(0, aircraft.OriginalActions!.Index);
        CompleteRunwayPass(simulation, aircraft);
        Assert.Equal(nextPhase, aircraft.Phase);
        Assert.Equal(index, aircraft.OriginalActions.Index);
        Assert.Equal(action, aircraft.OriginalActions.Current());
        Assert.False(aircraft.LandingCleared);
        simulation.Advance(0.05);
        Assert.Equal(index, aircraft.OriginalActions.Index);
    }

    [Fact]
    public void Third_action_repeats_without_creating_extra_slots()
    {
        var (simulation, aircraft) = Create("@TG @TG @TG");
        for (int pass = 0; pass < 4; pass++)
        {
            Final(simulation, aircraft);
            Assert.True(simulation.Execute("TEST/CL").Success);
            simulation.Advance(0.05);
            CompleteRunwayPass(simulation, aircraft);
            Assert.Equal(Math.Min(2, pass + 1), aircraft.OriginalActions!.Index);
            Assert.Equal(FlightPhase.Circuit, aircraft.Phase);
            Assert.False(aircraft.LandingCleared);
        }
    }

    [Theory]
    [InlineData("@TO @FS", FlightPhase.Circuit)]
    [InlineData("@TO @LP", FlightPhase.Departing)]
    public void Normal_takeoff_advances_to_the_second_action(string tokens, FlightPhase phase)
    {
        var (simulation, aircraft) = Create(tokens, FlightDirection.Departure);
        var log = new List<string>();
        simulation.Message += log.Add;
        var lastRadio = aircraft.LastTransmission;
        aircraft.Phase = FlightPhase.TakeoffRoll;
        CompleteRunwayPass(simulation, aircraft);
        Assert.Equal(1, aircraft.OriginalActions!.Index);
        Assert.Equal(phase, aircraft.Phase);
        Assert.False(aircraft.LandingCleared);
        if (phase == FlightPhase.Departing)
        {
            Assert.Contains(log, message => message.Contains("airborne", StringComparison.Ordinal));
            Assert.Equal(lastRadio, aircraft.LastTransmission);
        }
    }

    [Fact]
    public void Third_action_lp_activates_altitude_two_after_a_touch_and_go()
    {
        var (simulation, aircraft) = Create("@TG @TG @LP");
        simulation.State.Airport.Original.FieldElevationFeetMsl = 500;
        aircraft.OriginalActions!.Index = 1;
        aircraft.OriginalPlan!.AltitudeReference = OriginalAltitudeReference.Msl;
        aircraft.OriginalPlan.Altitude1Feet = 2000;
        aircraft.OriginalPlan.Altitude2Feet = 4000;
        aircraft.OriginalPlan.ResolvedRoute = [new("OUT", new(20000, -20000))];
        Assert.True(simulation.Execute("TEST/CL").Success);
        simulation.Advance(0.05);
        CompleteRunwayPass(simulation, aircraft);
        Assert.Equal(2, aircraft.OriginalActions.Index);
        var route = aircraft.OriginalRoute!;
        Assert.Equal(OriginalRouteStage.InitialClimb, route.Stage);
        Assert.Equal(1500, route.TargetAltitudeFeetAgl);
        aircraft.Position = simulation.State.Airport.Point("R36");
        aircraft.AltitudeFeet = 1500;
        simulation.Advance(0.05);
        Assert.True(route.Altitude2Active);
        Assert.Equal(3500, route.TargetAltitudeFeetAgl);
    }

    [Theory]
    [InlineData("@TG", "CA", LandingIntent.LowApproach)]
    [InlineData("@LA", "CG", LandingIntent.TouchAndGo)]
    [InlineData("@TG", "CO", LandingIntent.TouchAndGo)]
    [InlineData("@LA", "CO", LandingIntent.LowApproach)]
    [InlineData("@FS", "CO", LandingIntent.FullStop)]
    public void Explicit_operation_takes_priority_and_option_uses_plan(string tokens, string command, LandingIntent intent)
    {
        var (simulation, aircraft) = Create(tokens);
        Assert.True(simulation.Execute("TEST/" + command).Success);
        Assert.Equal(intent, aircraft.LandingIntent);
        Assert.Equal(0, aircraft.OriginalActions!.Index);
    }

    [Theory]
    [InlineData(FlightDirection.Arrival)]
    [InlineData(FlightDirection.Departure)]
    public void Full_stop_completes_at_gate_including_a_training_departure(FlightDirection direction)
    {
        var (simulation, aircraft) = Create("@FS", direction);
        Assert.True(simulation.Execute("TEST/CL").Success);
        simulation.Advance(0.05);
        Assert.Equal(FlightPhase.LandingRoll, aircraft.Phase);
        Assert.True(aircraft.OriginalActions!.FullStopLanded);
        Assert.False(aircraft.OriginalActions.Completed);
        aircraft.Phase = FlightPhase.TaxiIn;
        aircraft.Position = simulation.State.Airport.Point(aircraft.Gate);
        CompleteRunwayPass(simulation, aircraft);
        Assert.Equal(FlightPhase.Completed, aircraft.Phase);
        Assert.True(aircraft.OriginalActions.Completed);
        Assert.Equal(1, simulation.State.Performance.Arrivals);
        simulation.Advance(1);
        Assert.Equal(1, simulation.State.Performance.Arrivals);
    }

    [Fact]
    public void Parser_preserves_three_original_slots_and_ignores_non_action_tags()
    {
        var actions = OriginalActionProgress.Parse(["ORIGIN", "@TO", "@GG1", "@UNKNOWN", "@tg", "@LA", "@FS"]);
        Assert.Equal(new[] { OriginalFlightAction.Takeoff, OriginalFlightAction.TouchAndGo, OriginalFlightAction.LowApproach }, actions);
        Assert.Equal(new[] { OriginalFlightAction.FullStop, OriginalFlightAction.NoLanding, OriginalFlightAction.NoLanding }, OriginalActionProgress.Parse(["@FS"]));
    }

    [Fact]
    public void Save_after_touchdown_resumes_without_advancing_twice_and_bad_index_is_rejected()
    {
        var (simulation, aircraft) = Create("@TG @FS");
        Assert.True(simulation.Execute("TEST/CL").Success);
        simulation.Advance(0.05);
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            JsonStore.Write(path, simulation.State);
            var resumed = new Simulation(JsonStore.LoadSession(path));
            CompleteRunwayPass(resumed, resumed.State.Aircraft[0]);
            Assert.Equal(1, resumed.State.Aircraft[0].OriginalActions!.Index);
            JsonStore.Write(path, resumed.State);
            resumed = new Simulation(JsonStore.LoadSession(path));
            resumed.Advance(0.05);
            Assert.Equal(1, resumed.State.Aircraft[0].OriginalActions!.Index);
            resumed.State.Aircraft[0].OriginalActions!.Index = 3;
            JsonStore.Write(path, resumed.State);
            Assert.Throws<InvalidDataException>(() => JsonStore.LoadSession(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
