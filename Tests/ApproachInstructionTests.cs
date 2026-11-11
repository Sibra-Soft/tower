using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class ApproachInstructionTests
{
    private static (Simulation Simulation, Aircraft Aircraft) Create()
    {
        var airport = Airport.Training();
        airport.Runways.Add(new() { Name = "36", Threshold = "R36", End = "R18", Hold = "H18", Exit = "E18", Rollout = "X18" });
        airport.AircraftTypes["TEST"] = new() { Type = "TEST", ApproachSpeed = 100, PatternSpeed = 120 };
        var simulation = new Simulation(new() { Airport = airport, Options = new() { AircraftPerMinute = 0, WindKnots = 0 } });
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "TEST", "TEST", useTrafficFlow: false);
        return (simulation, aircraft);
    }

    private static void Command(Simulation simulation, string command)
    {
        var result = simulation.Execute(command);
        Assert.True(result.Success, command + ": " + result.Message);
    }

    private static void Until(Simulation simulation, Func<bool> condition, int seconds = 1800)
    {
        for (var second = 0; second < seconds && !condition(); second++)
        {
            simulation.Advance(1);
        }
        Assert.True(condition());
    }

    private static Simulation Resume(Simulation simulation)
    {
        var path = Path.GetTempFileName();
        try
        {
            JsonStore.Write(path, simulation.State);
            return new(JsonStore.LoadSession(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Circuit_speed_instruction_survives_save_resume_and_the_transition_to_final()
    {
        var (simulation, aircraft) = Create();
        Command(simulation, "TEST/PLD18/speed 90/CL");
        simulation.Advance(5);
        simulation = Resume(simulation);
        aircraft = Assert.Single(simulation.State.Aircraft);
        Assert.Equal(90, aircraft.ApproachSpeedOverrideKnots);
        Until(simulation, () => aircraft.Phase == FlightPhase.Approach);
        Assert.Equal(90, aircraft.TargetSpeedKnots);
        Assert.InRange(aircraft.SpeedKnots, 89.9, 90.1);
        Assert.True(aircraft.LandingCleared);
    }

    [Fact]
    public void Orbit_after_a_permanent_heading_stays_vectored_and_obeys_a_speed_change()
    {
        var (simulation, aircraft) = Create();
        Command(simulation, "TEST/CL/URH090/speed 90");
        simulation.Advance(30);
        Assert.InRange(aircraft.SpeedKnots, 89.9, 90.1);
        Assert.False(aircraft.LandingCleared);
        Command(simulation, "TEST/ML3");
        Assert.Equal(FlightPhase.Vectoring, aircraft.ResumePhase);
        simulation = Resume(simulation);
        aircraft = Assert.Single(simulation.State.Aircraft);
        Until(simulation, () => !aircraft.ResumeAfterTurn, 180);
        Assert.Equal(FlightPhase.Vectoring, aircraft.Phase);
        Assert.False(aircraft.LandingCleared);
        var position = aircraft.Position;
        simulation.Advance(5);
        Assert.Equal(FlightPhase.Vectoring, aircraft.Phase);
        Assert.True(position.Distance(aircraft.Position) > 100);
        Command(simulation, "TEST/EF36");
        Assert.Equal("36", aircraft.Runway);
        Assert.Null(aircraft.ApproachSpeedOverrideKnots);
        Assert.Equal(0, aircraft.TurnRemaining);
        Assert.False(aircraft.ResumeAfterTurn);
        Assert.Equal("36", aircraft.LastRadioMessage!.Runway);
    }

    [Theory]
    [InlineData("CL", LandingIntent.FullStop)]
    [InlineData("CG", LandingIntent.TouchAndGo)]
    [InlineData("CA", LandingIntent.LowApproach)]
    public void Short_approach_and_orbit_keep_clearance_and_explicit_speed(string clearance, LandingIntent intent)
    {
        var (simulation, aircraft) = Create();
        Command(simulation, "TEST/ELB18/speed 90/" + clearance + "/MS/ML3");
        Until(simulation, () => !aircraft.ResumeAfterTurn, 180);
        Assert.True(aircraft.LandingCleared);
        Assert.Equal(intent, aircraft.LandingIntent);
        Assert.Equal(90, aircraft.ApproachSpeedOverrideKnots);
        Until(simulation, () => aircraft.Phase == FlightPhase.Approach);
        Assert.Equal(90, aircraft.TargetSpeedKnots);
    }

    [Fact]
    public void New_procedure_cancels_old_clearance_reports_extension_and_orbit_progress()
    {
        var (simulation, aircraft) = Create();
        Command(simulation, "TEST/PLD18/X/speed 90/CL/ML3");
        Assert.True(aircraft.ResumeAfterTurn);
        Command(simulation, "TEST/PF4,36");
        Assert.Equal("36", aircraft.Runway);
        Assert.Equal("final", aircraft.CircuitLeg);
        Assert.False(aircraft.LandingCleared);
        Assert.False(aircraft.ReportLeg);
        Assert.False(aircraft.ExtendDownwind);
        Assert.False(aircraft.ResumeAfterTurn);
        Assert.Equal(0, aircraft.TurnRemaining);
        Assert.Equal(4, aircraft.ReportFinalNm);
        Assert.Null(aircraft.ApproachSpeedOverrideKnots);
        Assert.Equal("36", aircraft.LastRadioMessage!.Runway);
    }

    [Theory]
    [InlineData("EF99")]
    [InlineData("PF4,99")]
    [InlineData("PRB99")]
    public void Invalid_runway_instruction_preserves_active_approach(string command)
    {
        var (simulation, aircraft) = Create();
        Command(simulation, "TEST/PLD18/X/speed 90/CG");
        var path = aircraft.Path.ToList();
        Assert.False(simulation.Execute("TEST/" + command).Success);
        Assert.Equal("18", aircraft.Runway);
        Assert.Equal(path, aircraft.Path);
        Assert.True(aircraft.LandingCleared);
        Assert.True(aircraft.ReportLeg);
        Assert.True(aircraft.ExtendDownwind);
        Assert.Equal(LandingIntent.TouchAndGo, aircraft.LandingIntent);
        Assert.Equal(90, aircraft.ApproachSpeedOverrideKnots);
    }

    [Fact]
    public void Go_around_replaces_old_reports_and_accepts_a_new_speed_instruction()
    {
        var (simulation, aircraft) = Create();
        Command(simulation, "TEST/PLD18/X/speed 90/CL/G");
        Assert.Equal(FlightPhase.GoAround, aircraft.Phase);
        Assert.False(aircraft.LandingCleared);
        Assert.False(aircraft.ReportLeg);
        Assert.False(aircraft.ExtendDownwind);
        Assert.Empty(aircraft.CircuitLeg);
        Assert.Null(aircraft.ApproachSpeedOverrideKnots);
        Command(simulation, "TEST/speed 90");
        simulation.Advance(30);
        Assert.InRange(aircraft.SpeedKnots, 89.9, 90.1);
    }

    [Fact]
    public void Corrupt_saved_speed_instruction_is_rejected()
    {
        var (simulation, aircraft) = Create();
        aircraft.ApproachSpeedOverrideKnots = -1;
        Assert.Throws<InvalidDataException>(() => Resume(simulation));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Canyon_changes_runway_goes_around_and_completes_a_new_approach_with_matching_radio()
    {
        var data = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA") ?? throw new InvalidOperationException("Set TOWER_ORIGINAL_DATA.");
        var airport = OriginalData.LoadAirport(data, "CYN");
        var simulation = new Simulation(new() { Airport = airport, Options = new() { Runway = "9", AircraftPerMinute = 0, WindKnots = 0 } });
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "AAL1535", "C182", "9");
        Until(simulation, () => aircraft.IsOnFrequency);
        Command(simulation, "AAL1535/CL/ERD32");
        Assert.Equal("32", aircraft.Runway);
        Assert.False(aircraft.LandingCleared);
        Assert.Equal(GeneratedArrivalStage.Cancelled, aircraft.GeneratedArrival!.Stage);
        Command(simulation, "AAL1535/speed 90/CL");
        Until(simulation, () => aircraft.Phase == FlightPhase.Approach);
        Command(simulation, "AAL1535/G");
        Until(simulation, () => aircraft.GoAroundApproachRequests == 1);
        Assert.Equal("flight.approach.request", aircraft.LastRadioMessage!.IntentKey);
        Assert.Equal("32", aircraft.LastRadioMessage.Runway);
        simulation = Resume(simulation);
        aircraft = Assert.Single(simulation.State.Aircraft);
        Assert.Equal(1, aircraft.GoAroundApproachRequests);
        Command(simulation, "AAL1535/PF3,9/speed 90/CL");
        Assert.Equal("9", aircraft.Runway);
        Assert.Equal("9", aircraft.LastRadioMessage!.Runway);
        Assert.Equal(0, aircraft.GoAroundApproachRequests);
        Until(simulation, () => aircraft.Phase is FlightPhase.ArrivalHold or FlightPhase.GoAround);
        Assert.Equal(FlightPhase.ArrivalHold, aircraft.Phase);
        Command(simulation, "AAL1535/TP");
        Until(simulation, () => aircraft.Phase == FlightPhase.Completed);
        Assert.Equal(1, simulation.State.Performance.Arrivals);
    }
}
