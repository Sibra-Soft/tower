using System.Text.Json;
using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class PerformanceReviewTests
{
    [Theory]
    [InlineData(0, 50, 0)]
    [InlineData(1, 50, 375)]
    [InlineData(5, 50, 1875)]
    [InlineData(1, 70, 525)]
    [InlineData(1, 98, 735)]
    public void First_five_movements_use_original_difficulty_scaling(int movements, int difficulty, int expected)
    {
        Assert.Equal(expected, new Performance { Movements = movements }.Rating(difficulty, 1));
    }

    [Fact]
    public void Scheduled_scenarios_use_their_declared_traffic_density_without_enabling_generated_traffic()
    {
        var session = new Session
        {
            Airport = new() { Difficulty = 50 },
            OriginalScenario = new() { DeclaredMinutes = 10 },
            Options = new() { AircraftPerMinute = 0 },
            Schedule = Enumerable.Range(0, 10).Select(_ => new ScheduledFlight()).ToList(),
            Performance = new() { Movements = 6, LastMovementSeconds = 600 }
        };
        Assert.Equal(1350, session.Performance.Rating(session));
        Assert.Equal(0, session.Options.AircraftPerMinute);
    }

    [Fact]
    public void Throughput_factor_penalties_and_zero_floor_follow_original_formula()
    {
        var performance = new Performance { Movements = 6, LastMovementSeconds = 600 };
        Assert.Equal(1350, performance.Rating(50, 1));
        performance.RunwaySeparation = 1;
        Assert.Equal(1100, performance.Rating(50, 1));
        performance.Crashes = 1;
        Assert.Equal(100, performance.Rating(50, 1));
        performance.Crashes = 2;
        Assert.Equal(0, performance.Rating(50, 1));
        Assert.Equal(0, performance.Rating(50, 0));
    }

    [Theory]
    [InlineData(109, 0, -1)]
    [InlineData(110, 49, 1)]
    [InlineData(119, 50, 0)]
    [InlineData(120, 49, 3)]
    [InlineData(129, 50, 2)]
    [InlineData(130, 0, 4)]
    [InlineData(139, 99, 4)]
    [InlineData(140, 50, 5)]
    [InlineData(149, 51, 6)]
    [InlineData(150, 50, 7)]
    [InlineData(159, 51, 8)]
    [InlineData(160, 0, 9)]
    public void Original_reward_bands_include_random_branch_boundaries(int average, int random, int kind)
    {
        Assert.Equal(kind, PerformanceRewards.Choose(100, average, 0, random)?.Kind ?? -1);
        Assert.Null(PerformanceRewards.Choose(100, average, 1, random));
        Assert.Null(PerformanceRewards.Choose(0, average, 0, random));
    }

    [Fact]
    public void Actual_compact_commands_populate_categories_including_rejected_recognized_commands()
    {
        var simulation = new Simulation(new());
        simulation.AddAircraft(FlightDirection.Departure, "TEST");
        simulation.Execute("TEST/T18");
        simulation.Execute("TEST/CT");
        simulation.Execute("TEST/FD");
        simulation.Execute("TEST/HY");
        simulation.Execute("TEST/PF3");
        simulation.Execute("TEST/SA");
        simulation.Execute("TEST/B");
        simulation.Execute("TEST/ELD18");
        simulation.Execute("TEST/EF18");
        var performance = simulation.State.Performance;
        Assert.Equal(1, performance.TaxiOperations);
        Assert.Equal(1, performance.TakeoffLandingCommands);
        Assert.Equal(1, performance.Handoffs);
        Assert.Equal(1, performance.Holds);
        Assert.Equal(1, performance.Reports);
        Assert.Equal(1, performance.Requests);
        Assert.Equal(1, performance.Breakouts);
        Assert.Equal(1, performance.Patterns);
        Assert.Equal(1, performance.StraightIns);
        simulation.Execute("TEST/nonsense");
        Assert.Equal(1, performance.Requests);
    }

    [Fact]
    public void Practice_and_restored_recorded_sessions_do_not_duplicate_statistics_or_rewards()
    {
        var profile = new ControllerProfile();
        var session = new Session { Airport = new() { Code = "CYN", Difficulty = 50 } };
        session.Performance.RecordMovement(60);
        ControllerPerformance.Record(profile, session, 0);
        Assert.Empty(profile.RecordedSessions);
        session.Options.Practice = false;
        Assert.Null(ControllerPerformance.Record(profile, session, 0));
        Assert.Equal(1, profile.Sessions);
        Assert.Equal(375, profile.AverageRating);
        Assert.Null(ControllerPerformance.Record(profile, session, 0));
        Assert.Equal(1, profile.Sessions);
        var second = new Session { Airport = session.Airport, Options = new() { Practice = false } };
        second.Performance.Movements = 2;
        var reward = ControllerPerformance.Record(profile, second, 0);
        Assert.Equal(5, reward?.Kind); // Average 562 versus 375: 49% improvement.
        Assert.Equal(1, profile.RewardCounts[5]);
        var restored = JsonSerializer.Deserialize<ControllerProfile>(JsonSerializer.Serialize(profile, JsonStore.Options), JsonStore.Options)!;
        Assert.Null(ControllerPerformance.Record(restored, second, 0));
        Assert.Equal(2, restored.Sessions);
        Assert.Equal(1, restored.RewardCounts[5]);
    }

    [Fact]
    public void Operational_errors_prevent_rewards_even_when_the_average_improves()
    {
        var profile = new ControllerProfile { Sessions = 1, AirportRatings = new() { ["TRAINING"] = [100] } };
        var session = new Session { Airport = new() { Difficulty = 50 }, Options = new() { Practice = false } };
        session.Performance.Movements = 5;
        session.Performance.CategorySeparationArrival = 1;
        Assert.Null(ControllerPerformance.Record(profile, session, 0));
        Assert.True(profile.AverageRating > 100);
        Assert.Empty(profile.RewardCounts);
    }

    [Theory]
    [InlineData("arrival")]
    [InlineData("departure")]
    [InlineData("runway")]
    [InlineData("crash")]
    [InlineData("overrun")]
    [InlineData("incident")]
    public void Every_operational_error_blocks_rewards_independently(string kind)
    {
        var profile = new ControllerProfile { Sessions = 1, AirportRatings = new() { ["TRAINING"] = [100] } };
        var session = new Session { Airport = new() { Difficulty = 50 }, Options = new() { Practice = false }, Performance = new() { Movements = 5 } };
        switch (kind)
        {
            case "arrival":
                session.Performance.CategorySeparationArrival = 1;
                break;
            case "departure":
                session.Performance.CategorySeparationDeparture = 1;
                break;
            case "runway":
                session.Performance.RunwaySeparation = 1;
                break;
            case "crash":
                session.Performance.Crashes = 1;
                break;
            case "overrun":
                session.Performance.RunwayOverruns = 1;
                break;
            case "incident":
                session.Performance.Incidents = 1;
                break;
        }
        Assert.True(session.Performance.HasOperationalErrors);
        Assert.Null(ControllerPerformance.Record(profile, session, 0));
        Assert.Empty(profile.RewardCounts);
        Assert.Equal(2, profile.Sessions);
        Assert.True(profile.AverageRating > 100);
    }

    [Fact]
    public void Pushback_is_a_taxi_operation_and_rejected_input_is_not_an_operational_incident()
    {
        var simulation = new Simulation(new());
        simulation.AddAircraft(FlightDirection.Departure, "TEST");
        Assert.False(simulation.Execute("TEST/PB").Success);
        Assert.False(simulation.Execute("TEST/PB north").Success);
        Assert.Equal(2, simulation.State.Performance.TaxiOperations);
        Assert.False(simulation.State.Performance.HasOperationalErrors);
    }

    [Fact]
    public void Historical_rating_without_a_previous_rated_session_cannot_earn_a_first_session_reward()
    {
        var profile = new ControllerProfile { AirportRatings = new() { ["TRAINING"] = [100] } };
        var session = new Session { Airport = new() { Difficulty = 50 }, Options = new() { Practice = false }, Performance = new() { Movements = 5 } };
        Assert.Null(ControllerPerformance.Record(profile, session, 0));
        Assert.Equal(1, profile.Sessions);
        Assert.True(profile.AverageRating > 100);
        Assert.Empty(profile.RewardCounts);
    }

    [Fact]
    public void Rating_history_keeps_ten_results_per_airport()
    {
        var profile = new ControllerProfile();
        for (var index = 1; index <= 12; index++)
        {
            var session = new Session { Options = new() { Practice = false } };
            session.Performance.Movements = 1;
            ControllerPerformance.Record(profile, session, 0);
        }
        Assert.Equal(10, profile.AirportRatings["TRAINING"].Count);
        Assert.Equal(12, profile.RecordedSessions.Count);
        Assert.Equal(12, profile.Sessions);
    }

    [Fact]
    public void Collision_and_active_separation_counters_survive_session_serialization()
    {
        var simulation = new Simulation(new() { Options = new() { AircraftPerMinute = 0 } });
        foreach (var callsign in new[] { "FIRST", "SECOND" })
        {
            var aircraft = simulation.AddAircraft(FlightDirection.Departure, callsign);
            aircraft.Phase = FlightPhase.TaxiOut;
            aircraft.Position = new(0, 0);
            aircraft.Path = [new(1000, 0)];
            aircraft.PathIndex = 0;
            aircraft.SpeedKnots = 2;
        }
        simulation.Advance(0.01);
        Assert.Equal(1, simulation.State.Performance.Crashes);
        var incidents = simulation.State.Performance.Incidents;
        var restored = JsonSerializer.Deserialize<Session>(JsonSerializer.Serialize(simulation.State, JsonStore.Options), JsonStore.Options)!;
        new Simulation(restored).Advance(0.01);
        Assert.Equal(1, restored.Performance.Crashes);
        Assert.Equal(incidents, restored.Performance.Incidents);
    }
}
