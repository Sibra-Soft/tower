namespace Tower.Core;

// Values created by the original traffic generator, not authored SIM fields.
public sealed record GeneratedFlightPerformance(double CruiseSpeedKnots, int Altitude1Units, int Altitude2Units,
    double InitialSpeedKnots)
{
    public const double AltitudeUnitsPerFoot = 78.03;
    public double Altitude1FeetMsl => Altitude1Units / AltitudeUnitsPerFoot;
    public double Altitude2FeetMsl => Altitude2Units / AltitudeUnitsPerFoot;

    public bool IsValid() => OriginalTimingUnits.CruiseUnits(CruiseSpeedKnots) is not null &&
        Altitude1Units >= 0 && Altitude2Units >= 0 && double.IsFinite(InitialSpeedKnots) && InitialSpeedKnots >= 0;

    public static GeneratedFlightPerformance Create(FlightDirection direction, double cruiseKnots, double patternFeetMsl,
        Func<int, int, int> draw)
    {
        var cruise = OriginalTimingUnits.CruiseUnits(cruiseKnots)
            ?? throw new ArgumentOutOfRangeException(nameof(cruiseKnots));
        var speedClass = (int)Math.Floor(cruise * 18d * 0.007587208 + 0.5);
        int Units(double feet) => checked((int)Math.Floor(feet * AltitudeUnitsPerFoot + 0.5));
        var first = 0;
        var second = 0;
        if (direction == FlightDirection.Arrival)
        {
            var range = speedClass <= 100 ? (0, 10) : speedClass <= 150 ? (10, 25)
                : speedClass <= 200 ? (25, 40) : (40, 50);
            second = Units(patternFeetMsl);
            first = checked(second + Units(draw(range.Item1, range.Item2) * 50));
        }
        else
        {
            // The historical code rounds BEFORE multiplying the first height by 500.
            first = checked(Units(draw(4, 10)) * 500);
            var range = speedClass <= 100 ? (0, 2) : speedClass <= 150 ? (2, 5)
                : speedClass <= 200 ? (5, 8) : (8, 10);
            second = checked(first + Units(draw(range.Item1, range.Item2) * 500));
        }
        var initialSpeed = direction == FlightDirection.Arrival
            ? OriginalTimingUnits.SpeedKnots(first <= 0xBE80C ? Math.Min(cruise, 0x727) : cruise) : 0;
        return new(cruiseKnots, first, second, initialSpeed);
    }
}

public sealed partial class Simulation
{
    private void InitializeGeneratedPerformance(Aircraft aircraft)
    {
        var metadata = State.Airport.Original;
        if (!metadata.UseOriginalGeneratedPerformance || metadata.FieldElevationFeetMsl is null ||
            !aircraft.Specification.OriginalPerformance.TryGetValue("Cruise_Speed", out var cruise) ||
            OriginalTimingUnits.CruiseUnits(cruise) is null)
        {
            return;
        }
        var pattern = metadata.PatternAltitude(aircraft.Type);
        if (aircraft.Direction == FlightDirection.Arrival && (pattern is null || metadata.SourceMslToAgl(pattern.Value) is null))
        {
            return;
        }
        var performance = GeneratedFlightPerformance.Create(aircraft.Direction, cruise, pattern ?? 0, DrawTrafficRandom);
        aircraft.GeneratedPerformance = performance;
        if (metadata.SourceMslToAgl(performance.Altitude1FeetMsl) is not { } altitude)
        {
            return;
        }
        if (aircraft.Direction == FlightDirection.Arrival)
        {
            aircraft.AltitudeFeet = altitude;
            aircraft.SpeedKnots = performance.InitialSpeedKnots;
        }
        else
        {
            aircraft.FollowOriginalDepartureAltitude = altitude > 0;
        }
    }
}
