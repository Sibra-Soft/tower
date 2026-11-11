namespace Tower.Core;

public sealed class OriginalFlightTiming
{
    public double SpawnSeconds { get; set; }
    public double EntrySeconds { get; set; }
    public double InitialSpeedKnots { get; set; }
    public Position? ArrivalContactPoint { get; set; }
    public int LeadSeconds { get; set; }

    public bool IsValid(FlightDirection direction) => double.IsFinite(SpawnSeconds) && SpawnSeconds >= 0 &&
        double.IsFinite(EntrySeconds) && EntrySeconds >= SpawnSeconds && LeadSeconds >= 0 &&
        double.IsFinite(InitialSpeedKnots) && InitialSpeedKnots >= 0 &&
        (direction == FlightDirection.Departure ? ArrivalContactPoint is null :
            InitialSpeedKnots > 0 && ArrivalContactPoint is { } point && double.IsFinite(point.X) && double.IsFinite(point.Y));
}

public static class OriginalTimingUnits
{
    public const double CruiseUnitsPerKnot = 7.32227777777778;
    public const double ContactDistanceMetres = 18530;
    public const double RadioDelaySeconds = 1;

    public static int? CruiseUnits(double knots)
    {
        var units = Math.Floor(knots * CruiseUnitsPerKnot + 0.5);
        return double.IsFinite(units) && units >= 1 && units <= int.MaxValue / 18 ? (int)units : null;
    }

    public static int LeadSeconds(int cruiseUnits) => 0x5A7A800 / checked(18 * cruiseUnits);
    public static double SpeedKnots(int units) => units * 18d / 256 / Simulation.MetresPerKnotSecond;
}

public sealed class OriginalInitialContact
{
    public double CreatedSeconds { get; set; }
    public double EntrySeconds { get; set; }
    public double InitialSpeedKnots { get; set; }
    public int? EntryPointIndex { get; set; }
    public bool EntryReached { get; set; }
    public double? RadioDueSeconds { get; set; }
    public bool Announced { get; set; }
    public double? AnnouncedSeconds { get; set; }

    public bool IsValid(double elapsedSeconds) => double.IsFinite(CreatedSeconds) && CreatedSeconds >= 0 && CreatedSeconds <= elapsedSeconds &&
        double.IsFinite(EntrySeconds) && EntrySeconds >= 0 && double.IsFinite(InitialSpeedKnots) && InitialSpeedKnots >= 0 &&
        (EntryPointIndex is null || EntryPointIndex >= 0) &&
        (RadioDueSeconds is null || double.IsFinite(RadioDueSeconds.Value) && RadioDueSeconds >= EntrySeconds && RadioDueSeconds >= CreatedSeconds) &&
        (AnnouncedSeconds is null || double.IsFinite(AnnouncedSeconds.Value) && AnnouncedSeconds <= elapsedSeconds) &&
        EntryReached == (RadioDueSeconds is not null) && Announced == (AnnouncedSeconds is not null) &&
        (!Announced || EntryReached && AnnouncedSeconds + 1e-7 >= RadioDueSeconds);
}
