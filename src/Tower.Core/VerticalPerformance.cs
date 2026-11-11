namespace Tower.Core;

public enum AltitudeBand
{
    SurfaceTo500, To5000, To10000, To25000, Above25000
}

public static class VerticalPerformance
{
    // Continuous boundaries: labels such as 501 denote the next whole-foot band.
    public static AltitudeBand Band(double altitudeFeet) => altitudeFeet <= 500 ? AltitudeBand.SurfaceTo500 :
        altitudeFeet <= 5000 ? AltitudeBand.To5000 : altitudeFeet <= 10000 ? AltitudeBand.To10000 :
        altitudeFeet <= 25000 ? AltitudeBand.To25000 : AltitudeBand.Above25000;

    public static double Rate(AircraftSpecification specification, double altitudeFeet, bool climbing, double fallbackFpm)
    {
        var rates = climbing ? specification.ClimbRatesFpm : specification.DescentRatesFpm;
        return rates.TryGetValue(Band(altitudeFeet), out var rate) && double.IsFinite(rate) && rate > 0 ? rate : fallbackFpm;
    }
}
