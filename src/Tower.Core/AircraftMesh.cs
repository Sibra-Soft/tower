namespace Tower.Core;

public readonly record struct ModelPoint(double X, double Y, double Z);
public sealed record ModelFace(int[] Vertices, int ColorIndex, string Part, int Flags)
{
    // Authored flat terrain is painted by elevation; original BAO geometry keeps its depth ordering.
    public const int FlatGroundLayer = 1 << 29;
    public const int RetractableGear = 1;
    public const int LandingLight = 0x200;

    public bool Visible(AircraftAnimationState animation) =>
        ((Flags & RetractableGear) == 0 || animation.GearExtension > 0) &&
        ((Flags & LandingLight) == 0 || animation.LandingLights);
}

public sealed record AircraftMesh(string Name, ModelPoint[] Vertices, ModelFace[] Faces)
{
    public double Width { get; } = Vertices.Length == 0 ? 0 : Vertices.Max(point => point.X) - Vertices.Min(point => point.X);
    public double Bottom { get; } = Vertices.Length == 0 ? 0 : Vertices.Min(point => point.Y);
    private readonly Dictionary<string, double> gearAttachments = Faces.Where(face => (face.Flags & ModelFace.RetractableGear) != 0)
        .GroupBy(face => face.Part, StringComparer.Ordinal).ToDictionary(group => group.Key,
            group => group.SelectMany(face => face.Vertices).Max(index => Vertices[index].Y), StringComparer.Ordinal);

    public (Position Position, double Altitude) GearOffset(ModelFace face, int index, Aircraft aircraft, double extension)
    {
        var point = Vertices[index];
        var top = gearAttachments[face.Part];
        var adjusted = point with { Y = top + (point.Y - top) * extension };
        var before = WorldPoint(point, new(), 0, aircraft.Heading, aircraft.Specification.WingSpanMetres, aircraft.PitchDegrees, aircraft.BankDegrees);
        var after = WorldPoint(adjusted, new(), 0, aircraft.Heading, aircraft.Specification.WingSpanMetres, aircraft.PitchDegrees, aircraft.BankDegrees);
        return (new(after.Position.X - before.Position.X, after.Position.Y - before.Position.Y), after.Altitude - before.Altitude);
    }

    public AircraftFootprint GroundFootprint(double wingSpanMetres)
    {
        var scale = wingSpanMetres / Width;
        return new(-Vertices.Max(point => point.Z) * scale, -Vertices.Min(point => point.Z) * scale,
            Vertices.Min(point => point.X) * scale, Vertices.Max(point => point.X) * scale);
    }

    public (Position Position, double Altitude) WorldPoint(ModelPoint point, Position position, double altitudeMetres, double heading, double wingSpanMetres)
        => WorldPoint(point, position, altitudeMetres, heading, wingSpanMetres, 0, 0);

    public (Position Position, double Altitude) WorldPoint(ModelPoint point, Position position, double altitudeMetres, double heading, double wingSpanMetres,
        double pitchDegrees, double bankDegrees)
    {
        // Retain source proportions, calibrating lateral extent with the PRF wingspan.
        // The source model nose faces -Z; Y is up and X is lateral.
        var scale = wingSpanMetres / Width;
        var angle = heading * Math.PI / 180;
        var right = point.X * scale;
        var forward = -point.Z * scale;
        var up = (point.Y - Bottom) * scale;
        var pitch = pitchDegrees * Math.PI / 180;
        var bank = bankDegrees * Math.PI / 180;
        (forward, up) = (forward * Math.Cos(pitch) - up * Math.Sin(pitch), forward * Math.Sin(pitch) + up * Math.Cos(pitch));
        (right, up) = (right * Math.Cos(bank) + up * Math.Sin(bank), -right * Math.Sin(bank) + up * Math.Cos(bank));
        return (new(position.X + right * Math.Cos(angle) + forward * Math.Sin(angle),
            position.Y - right * Math.Sin(angle) + forward * Math.Cos(angle)),
            altitudeMetres + up);
    }

    public (Position Position, double Altitude)[] WorldPoints(Position position, double altitudeMetres, double heading,
        double wingSpanMetres, double pitchDegrees, double bankDegrees)
    {
        var points = Vertices.Select(point => WorldPoint(point, position, altitudeMetres, heading, wingSpanMetres, pitchDegrees, bankDegrees)).ToArray();
        var clearance = Math.Max(0, -points.Min(point => point.Altitude));
        return clearance == 0 ? points : points.Select(point => (point.Position, point.Altitude + clearance)).ToArray();
    }
}
