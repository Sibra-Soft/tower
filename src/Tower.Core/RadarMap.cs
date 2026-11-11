namespace Tower.Core;

public sealed class RadarMap
{
    public List<RadarMapLine> Lines { get; set; } = [];
    public List<RadarMapArc> Arcs { get; set; } = [];

    public void Validate()
    {
        if (Lines is null || Arcs is null || Lines.Count > 10000 || Arcs.Count > 10000 ||
            Lines.Any(line => line is null || line.Points is null || line.Points.Count is < 2 or > 10000 ||
                line.Points.Any(point => !Finite(point)) || !Style(line.Color, line.Width) ||
                line.DashMetres is null || line.DashMetres.Length is not (0 or 2 or 4) ||
                line.DashMetres.Any(length => !double.IsFinite(length) || length <= 0)) ||
            Arcs.Any(arc => arc is null || !Finite(arc.Center) || !Style(arc.Color, arc.Width) ||
                !double.IsFinite(arc.RadiusMetres) || arc.RadiusMetres <= 0 ||
                !Angle(arc.StartDegrees) || !Angle(arc.EndDegrees) ||
                !double.IsFinite(arc.DegreesOn) || arc.DegreesOn is <= 0 or > 360 ||
                !double.IsFinite(arc.DegreesOff) || arc.DegreesOff is < 0 or > 360))
        {
            throw new InvalidDataException("Invalid radar map geometry.");
        }
    }

    private static bool Finite(Position point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
    private static bool Angle(double angle) => double.IsFinite(angle) && angle is >= 0 and <= 360;
    private static bool Style(string color, double width) => !string.IsNullOrWhiteSpace(color) &&
        color.Length <= 32 && double.IsFinite(width) && width is > 0 and <= 16;
}

public sealed record RadarMapLine(List<Position> Points, string Color, double Width, double[] DashMetres);
public sealed record RadarMapArc(Position Center, double RadiusMetres, double StartDegrees, double EndDegrees,
    double DegreesOn, double DegreesOff, string Color, double Width);
