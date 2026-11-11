namespace Tower.Core;

public sealed class OriginalMetadata
{
    // Set on fresh imports; older saves retain their previous generated traffic behavior.
    public bool UseOriginalGeneratedPerformance { get; set; }
    public bool UseGeneratedDepartureRoutes { get; set; }
    public bool UseGeneratedArrivalRoutes { get; set; }
    public List<TrafficFlowEntry> ArrivalFlow { get; set; } = [];
    public List<TrafficFlowEntry> DepartureFlow { get; set; } = [];
    public Dictionary<string, GeographicFix> Fixes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, OriginalAirway> Airways { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    // Retain source text independently of interpreted simulation values.
    public Dictionary<string, string[]> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public double LocalMetresPerUnit { get; set; } = 1;
    public string LocalScaleBasis { get; set; } = "Approximation; TWR and SEC runway lengths disagree.";
    public double? TowerElevationFeetMsl
    {
        get; set;
    }
    public double? SectorElevationFeetMsl
    {
        get; set;
    }
    // Set only by new imports with the verified TWR altitude interpretation.
    // Older saved sessions retain their existing AGL behavior when this is absent.
    public double? FieldElevationFeetMsl { get; set; }
    public string AltitudeBasis { get; set; } = "Legacy AGL simulation; no verified field elevation applied.";

    public double? SourceMslToAgl(double feetMsl)
    {
        if (FieldElevationFeetMsl is not { } elevation || !double.IsFinite(elevation) || !double.IsFinite(feetMsl))
        {
            return null;
        }
        var altitude = Coordinates.MslToAgl(feetMsl, elevation);
        return double.IsFinite(altitude) && altitude >= 0 ? altitude : null;
    }

    public double? PatternAltitudeAgl(string type)
    {
        if (PatternAltitude(type) is not { } altitude)
        {
            return null;
        }
        return FieldElevationFeetMsl is null ? altitude : SourceMslToAgl(altitude);
    }
    public double? MagneticVariationDegrees
    {
        get; set;
    }
    public double? CabHeightBao
    {
        get; set;
    }
    public Dictionary<string, double> PatternValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    // Original imports supply the verified PAT conversion; old saves retain their explicit settings.
    public double? PatternMetresPerUnit
    {
        get; set;
    }
    public bool PatternUsesFixedPointRounding
    {
        get; set;
    }
    public string PatternDistanceBasis { get; set; } = "No imported horizontal PAT conversion.";
    public string PatternCategoryBasis { get; set; } = "No imported category mapping.";
    public Dictionary<string, int> PatternCategoryByType { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public double? PatternDistance(string type, string leg)
    {
        if (PatternMetresPerUnit is not { } scale || !double.IsFinite(scale) || scale <= 0 || !PatternCategoryByType.TryGetValue(type, out var category))
        {
            return null;
        }

        if (!PatternValues.TryGetValue($"cat{category}_{leg}", out var value) || value <= 0 || !double.IsFinite(value * scale))
        {
            return null;
        }
        var metres = value * scale;
        if (PatternUsesFixedPointRounding)
        {
            var fixedPoint = metres * OriginalPatternUnits.FixedPointPerMetre;
            if (fixedPoint > int.MaxValue - 0.5)
            {
                return null;
            }
            metres = Math.Floor(fixedPoint + 0.5) / OriginalPatternUnits.FixedPointPerMetre;
        }
        return metres > 0 ? metres : null;
    }
    public double? PatternAltitude(string type) => PatternCategoryByType.TryGetValue(type, out var category) && PatternValues.TryGetValue($"cat{category}_altit", out var value) && double.IsFinite(value) && value > 0 ? value : null;
}

public static class OriginalPatternUnits
{
    // GAMENT.DLL PAT reader 0x10019DC1..0x10019E62: trunc(value * 78.03 + 0.5).
    public const double FixedPointPerSourceUnit = 78.03;
    public const double FixedPointPerMetre = 256;
    public const double MetresPerSourceUnit = FixedPointPerSourceUnit / FixedPointPerMetre;
}

public static class Coordinates
{
    public const double MetresPerFoot = 0.3048;
    public static double NormalizeHeading(double degrees) => (degrees % 360 + 360) % 360;
    public static double AglToMsl(double feetAgl, double elevationFeetMsl) => feetAgl + elevationFeetMsl;
    public static double MslToAgl(double feetMsl, double elevationFeetMsl) => feetMsl - elevationFeetMsl;
    // East-positive declination; BAO MagneticVar's sign convention remains unverified.
    public static double TrueToMagnetic(double trueDegrees, double eastDeclinationDegrees) => NormalizeHeading(trueDegrees - eastDeclinationDegrees);
    public static double MagneticToTrue(double magneticDegrees, double eastDeclinationDegrees) => NormalizeHeading(magneticDegrees + eastDeclinationDegrees);
}
