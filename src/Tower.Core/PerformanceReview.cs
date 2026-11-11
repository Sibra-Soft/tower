using System.Text.Json.Serialization;

namespace Tower.Core;

public sealed partial class Performance
{
    public int TaxiOperations { get; set; }
    public int TakeoffLandingCommands { get; set; }
    public int Handoffs { get; set; }
    public int Holds { get; set; }
    public int Reports { get; set; }
    public int Requests { get; set; }
    public int Breakouts { get; set; }
    public int Patterns { get; set; }
    public int StraightIns { get; set; }
    public int CategorySeparationArrival { get; set; }
    public int RunwaySeparation { get; set; }
    public int CategorySeparationDeparture { get; set; }
    public int Crashes { get; set; }
    public int RunwayOverruns { get; set; }
    [JsonIgnore]
    public bool HasOperationalErrors => Incidents > 0 || Crashes > 0 || RunwayOverruns > 0 ||
        CategorySeparationArrival > 0 || CategorySeparationDeparture > 0 || RunwaySeparation > 0;
    public int Movements { get; set; }
    public double LastMovementSeconds { get; set; }
    public HashSet<string> ReportedCollisions { get; set; } = [];
    public HashSet<string> ActiveSeparationPairs { get; set; } = [];
    public HashSet<string> ActiveRunwayPairs { get; set; } = [];

    public void RecordMovement(double seconds)
    {
        Movements++;
        LastMovementSeconds = seconds;
    }

    // GAMENT 100040E0: 750 per movement, throughput factor after five movements,
    // penalties 500 per runway separation and 2000 per crash, scaled by difficulty/100.
    public int Rating(int difficulty, double aircraftPerMinute)
    {
        var factor = Movements <= 5 ? 1 : LastMovementSeconds > 0 && aircraftPerMinute > 0
            ? Movements / LastMovementSeconds / (aircraftPerMinute / 60) : 0;
        var result = (750d * Movements * factor - 500d * RunwaySeparation - 2000d * Crashes) * difficulty / 100;
        return double.IsFinite(result) ? (int)Math.Clamp(Math.Truncate(result), 0, int.MaxValue) : 0;
    }

    public int Rating(Session session)
    {
        var rate = session.Options.AircraftPerMinute;
        if (rate <= 0 && session.OriginalScenario?.DeclaredMinutes is > 0 and var minutes)
        {
            rate = (double)session.Schedule.Count / minutes;
        }
        return Rating(session.Airport.Difficulty, rate);
    }

    public void RecordCommand(string command)
    {
        if (command.StartsWith("taxi ", StringComparison.Ordinal) || command == "pushback" || command.StartsWith("pushback ", StringComparison.Ordinal) ||
            command is "continue" or "continue taxi" or "line up" or "line up and wait")
        {
            TaxiOperations++;
        }
        else if (command is "land" or "cleared to land" or "cleared for landing" or "takeoff" or "cleared for takeoff" or
            "touch and go" or "low approach" or "option" or "go around" or "ga" or "cancel takeoff")
        {
            TakeoffLandingCommands++;
        }
        else if (command is "handoff" or "contact departure" || command.StartsWith("contact ", StringComparison.Ordinal))
        {
            Handoffs++;
        }
        else if (command is "hold" or "hold position" || command.StartsWith("hold short ", StringComparison.Ordinal))
        {
            Holds++;
        }
        else if (command.StartsWith("report ", StringComparison.Ordinal))
        {
            Reports++;
        }
        else if (command.StartsWith("say ", StringComparison.Ordinal))
        {
            Requests++;
        }
        else if (command == "break out")
        {
            Breakouts++;
        }
        else if (command.StartsWith("pattern EF", StringComparison.Ordinal))
        {
            StraightIns++;
        }
        else if (command.StartsWith("pattern ", StringComparison.Ordinal) || command is "extend downwind" or "short approach" or
            "turn out" or "left turn out" or "right turn out" || command.StartsWith("turn ", StringComparison.Ordinal))
        {
            Patterns++;
        }
    }
}

public sealed record PerformanceReward(int Kind, string Title, string Message);

public static class PerformanceRewards
{
    public static IReadOnlyList<PerformanceReward> Catalogue { get; } = Array.AsReadOnly(new[]
    {
        new PerformanceReward(0, "Letter of Appreciation", "The Tower chief thinks you've done a great job."),
        new PerformanceReward(1, "Free Meal Pass", "So go enjoy a delicious airport restaurant meal - Free!"),
        new PerformanceReward(2, "Controller of the Month", ""),
        new PerformanceReward(3, "Free Rental Car", "Go on a vacation and rent the car free!"),
        new PerformanceReward(4, "Recognition at Civic Function", "You are recognized for your excellent work."),
        new PerformanceReward(5, "Familiarization Ride (Free Tickets)", "Ride the airways you control, free!"),
        new PerformanceReward(6, "Controller of the Year", ""),
        new PerformanceReward(7, "Time Off with Pay", "Take a vacation on the house."),
        new PerformanceReward(8, "Bonus Check", "Go out and party, you've earned it!"),
        new PerformanceReward(9, "Aircraft Save Award", "The airlines, passengers all send heartfelt thanks.")
    });

    // EXE 0041BB74: percentage improvement over the controller's previous average.
    public static PerformanceReward? Choose(int previousAverage, int average, int errors, int randomPercent)
    {
        if (previousAverage <= 0 || errors > 0)
        {
            return null;
        }
        var improvement = ((long)average - previousAverage) * 100 / previousAverage;
        var kind = improvement switch
        {
            < 10 => -1,
            < 20 => randomPercent < 50 ? 1 : 0,
            < 30 => randomPercent < 50 ? 3 : 2,
            < 40 => 4,
            < 50 => randomPercent > 50 ? 6 : 5,
            < 60 => randomPercent > 50 ? 8 : 7,
            _ => 9
        };
        return kind < 0 ? null : Catalogue[kind];
    }
}
