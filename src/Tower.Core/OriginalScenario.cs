namespace Tower.Core;

public sealed class OriginalScenario
{
    public List<OriginalScenarioWaypoint> Waypoints { get; set; } = [];
    public string FileName { get; set; } = "";
    public string[] SourceLines { get; set; } = [];
    public string Sector { get; set; } = "";
    public int? Seed
    {
        get; set;
    }
    public int? DeclaredMinutes
    {
        get; set;
    }
    public string[] Description { get; set; } = [];
}

public sealed record OriginalScenarioWaypoint(string Name, string CoordinateText, string AdditionalField, double Latitude, double Longitude)
{
    public bool IsValid() => !string.IsNullOrWhiteSpace(Name) && !Name.Any(char.IsWhiteSpace) &&
        !string.IsNullOrWhiteSpace(CoordinateText) && !string.IsNullOrWhiteSpace(AdditionalField) &&
        double.IsFinite(Latitude) && Latitude is >= -90 and <= 90 &&
        double.IsFinite(Longitude) && Longitude is >= -180 and <= 180;

    public GeographicFix ToFix() => new(Name, Latitude, Longitude);
}

public enum OriginalAltitudeReference
{
    Unresolved, Msl
}

public sealed class OriginalFlightPlan
{
    public List<OriginalFlightAction>? Actions { get; set; }
    public List<OriginalRoutePoint>? ResolvedRoute { get; set; }
    public string RouteResolution { get; set; } = "Legacy route; no resolved SEC points.";
    public OriginalAltitudeReference AltitudeReference { get; set; }
    public string Header { get; set; } = "";
    public string FlightRules { get; set; } = "";
    public string ControlField { get; set; } = "";
    public double SpeedFieldKnots
    {
        get; set;
    }
    public double Altitude1Feet
    {
        get; set;
    }
    public double Altitude2Feet
    {
        get; set;
    }
    public string Squawk { get; set; } = "";
    public string[] RouteTokens { get; set; } = [];
    public bool HasDepartureClimbTarget()
    {
        var actions = RouteTokens.Select(token => token.ToUpperInvariant())
            .Where(token => token is "@NL" or "@TO" or "@LP" or "@FS" or "@TG" or "@LA").Take(2).ToArray();
        return actions.Length == 2 && actions[0] == "@TO" && actions[1] is "@NL" or "@LP";
    }
    public string Origin { get; set; } = "";
    public string Destination { get; set; } = "";
    public string Controller { get; set; } = "";
    public double? OriginBearingDegrees
    {
        get; set;
    }
    public double? DestinationBearingDegrees
    {
        get; set;
    }

    public bool IsValid() => new[] { SpeedFieldKnots, Altitude1Feet, Altitude2Feet }.All(value => double.IsFinite(value) && value >= 0)
        && Enum.IsDefined(AltitudeReference)
        && (Actions is null || Actions.Count == 3 && Actions.All(action => Enum.IsDefined(action)))
        && (ResolvedRoute is null || ResolvedRoute.Count > 0 && ResolvedRoute.All(point => point is not null &&
            !string.IsNullOrWhiteSpace(point.Name) && double.IsFinite(point.Position.X) && double.IsFinite(point.Position.Y)))
        && new[] { OriginBearingDegrees, DestinationBearingDegrees }.All(value => value is null || double.IsFinite(value.Value) && value.Value >= 0 && value.Value < 360)
        && Squawk is { Length: 4 } && Squawk.All(character => character is >= '0' and <= '7');
}

public sealed record GeographicFix(string Name, double Latitude, double Longitude)
{
    public string SourceKind { get; init; } = "";
    public Position ProjectFrom(double latitude, double longitude, Position observer)
    {
        var from = latitude * Math.PI / 180;
        var to = Latitude * Math.PI / 180;
        var delta = (Longitude - longitude) * Math.PI / 180;
        var haversine = Math.Pow(Math.Sin((to - from) / 2), 2) + Math.Cos(from) * Math.Cos(to) * Math.Pow(Math.Sin(delta / 2), 2);
        var distance = 6371000 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(haversine, 0, 1)));
        return observer.Offset(BearingFrom(latitude, longitude), distance);
    }
    public double BearingFrom(double latitude, double longitude)
    {
        var from = latitude * Math.PI / 180;
        var to = Latitude * Math.PI / 180;
        var delta = (Longitude - longitude) * Math.PI / 180;
        return Coordinates.NormalizeHeading(Math.Atan2(Math.Sin(delta) * Math.Cos(to),
            Math.Cos(from) * Math.Sin(to) - Math.Sin(from) * Math.Cos(to) * Math.Cos(delta)) * 180 / Math.PI);
    }
}
