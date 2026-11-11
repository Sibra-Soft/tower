namespace Tower.Core;

/// <summary>Ground bounds in metres, relative to the same origin as the rendered model.</summary>
public readonly record struct AircraftFootprint(double MinForward, double MaxForward, double MinRight, double MaxRight)
{
    public bool IsValid() => double.IsFinite(MinForward) && double.IsFinite(MaxForward) &&
        double.IsFinite(MinRight) && double.IsFinite(MaxRight) && MaxForward > MinForward && MaxRight > MinRight;

    public AircraftFootprint Project(double relativeHeading)
    {
        var angle = relativeHeading * Math.PI / 180;
        var sine = Math.Sin(angle);
        var cosine = Math.Cos(angle);
        var along = Bounds(this, cosine, -sine);
        var across = Bounds(this, sine, cosine);
        return new(along.Min, along.Max, across.Min, across.Max);
    }

    private static (double Min, double Max) Bounds(AircraftFootprint footprint, double forward, double right)
    {
        var front = footprint.MaxForward * forward;
        var rear = footprint.MinForward * forward;
        var left = footprint.MinRight * right;
        var rightEdge = footprint.MaxRight * right;
        return (Math.Min(front, rear) + Math.Min(left, rightEdge),
            Math.Max(front, rear) + Math.Max(left, rightEdge));
    }
}
