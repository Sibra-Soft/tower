namespace Tower.Core;

// PRF rates are feet/second squared; simulation speed is knots.
public sealed class AccelerationProfile
{
    public double? AirborneAcceleration
    {
        get; set;
    }
    public double? AirborneDeceleration
    {
        get; set;
    }
    public double? TaxiAcceleration
    {
        get; set;
    }
    public double? TaxiDeceleration
    {
        get; set;
    }

    public bool IsValid() => new[] { AirborneAcceleration, AirborneDeceleration, TaxiAcceleration, TaxiDeceleration }
        .All(value => value is null || double.IsFinite(value.Value) && value.Value > 0);
}

public static class SpeedPerformance
{
    public static double KnotsPerSecond(double feetPerSecondSquared) =>
        feetPerSecondSquared * Coordinates.MetresPerFoot / Simulation.MetresPerKnotSecond;

    public static double Rate(AircraftSpecification specification, bool airborne, bool accelerating, double fallbackKnotsPerSecond)
    {
        var profile = specification.Acceleration;
        var value = (airborne, accelerating) switch
        {
            (true, true) => profile.AirborneAcceleration,
            (true, false) => profile.AirborneDeceleration,
            (false, true) => profile.TaxiAcceleration,
            _ => profile.TaxiDeceleration
        };

        return value is { } rate && double.IsFinite(rate) && rate > 0
            ? KnotsPerSecond(rate)
            : fallbackKnotsPerSecond;
    }
}
