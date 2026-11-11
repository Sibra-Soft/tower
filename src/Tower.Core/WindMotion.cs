namespace Tower.Core;

public readonly record struct WindTrack(double Heading, double GroundSpeedKnots, bool CanHoldTrack);

/// <summary>Steady horizontal wind triangle; directions describe where the wind comes from.</summary>
public static class WindMotion
{
    public static Position Vector(double fromDegrees, double knots) => new Position(0, 0).Offset(fromDegrees + 180, knots);

    public static double Headwind(double course, ScenarioOptions options) =>
        options.WindKnots * Math.Cos((options.WindFromDegrees - course) * Math.PI / 180);

    public static WindTrack Track(double airspeed, double course, ScenarioOptions options)
    {
        var radians = course * Math.PI / 180;
        var wind = Vector(options.WindFromDegrees, options.WindKnots);
        var across = wind.X * Math.Cos(radians) - wind.Y * Math.Sin(radians);
        var along = wind.X * Math.Sin(radians) + wind.Y * Math.Cos(radians);
        if (airspeed <= 0.000001)
        {
            return new(Coordinates.NormalizeHeading(course), 0, options.WindKnots <= 0.000001);
        }
        var correction = Math.Asin(Math.Clamp(-across / airspeed, -1, 1)) * 180 / Math.PI;
        var speed = Math.Sqrt(Math.Max(0, airspeed * airspeed - across * across)) + along;
        return new(Coordinates.NormalizeHeading(course + correction), Math.Max(0, speed), Math.Abs(across) < airspeed && speed > 0);
    }

    public static Position GroundVelocity(double airspeed, double heading, ScenarioOptions options)
    {
        var air = new Position(0, 0).Offset(heading, airspeed);
        var wind = Vector(options.WindFromDegrees, options.WindKnots);
        return new(air.X + wind.X, air.Y + wind.Y);
    }

    public static double TakeoffGroundSpeed(Aircraft aircraft, double runwayHeading, ScenarioOptions options) =>
        Math.Max(5, Math.Sqrt(Math.Max(0, Math.Pow(aircraft.Specification.TakeoffSpeed, 2) -
            Math.Pow(options.WindKnots * Math.Sin((options.WindFromDegrees - runwayHeading) * Math.PI / 180), 2))) - Headwind(runwayHeading, options));

    public static double RunwayAirspeed(double groundSpeed, double runwayHeading, ScenarioOptions options) =>
        Math.Sqrt(Math.Pow(groundSpeed + Headwind(runwayHeading, options), 2) +
            Math.Pow(options.WindKnots * Math.Sin((options.WindFromDegrees - runwayHeading) * Math.PI / 180), 2));

    public static double TakeoffDistance(Aircraft aircraft, double calmDistance, double runwayHeading, ScenarioOptions options) =>
        calmDistance * Math.Pow(TakeoffGroundSpeed(aircraft, runwayHeading, options) / Math.Max(1, aircraft.Specification.TakeoffSpeed), 2);
}

public sealed partial class Simulation
{
    private void FlyHeading(Aircraft aircraft, double heading, double dt)
    {
        var velocity = WindMotion.GroundVelocity(aircraft.SpeedKnots, heading, State.Options);
        aircraft.Position = new(aircraft.Position.X + velocity.X * MetresPerKnotSecond * dt,
            aircraft.Position.Y + velocity.Y * MetresPerKnotSecond * dt);
        aircraft.Heading = heading;
        aircraft.GroundTrackDegrees = new Position(0, 0).Bearing(velocity);
    }

    private double AirTrackDistance(Aircraft aircraft, double course, double dt)
    {
        var solution = WindMotion.Track(aircraft.SpeedKnots, course, State.Options);
        aircraft.Heading = solution.Heading;
        aircraft.GroundTrackDegrees = Coordinates.NormalizeHeading(course);
        if (!solution.CanHoldTrack)
        {
            FlyHeading(aircraft, solution.Heading, dt);
            return 0;
        }
        return solution.GroundSpeedKnots * MetresPerKnotSecond * dt;
    }

    private void FlyTrack(Aircraft aircraft, double course, double dt)
    {
        var distance = AirTrackDistance(aircraft, course, dt);
        aircraft.Position = aircraft.Position.Offset(course, distance);
    }

    private int AdvanceAirRoute(Aircraft aircraft, IReadOnlyList<Position> path, int waypoint, double dt, double? exitHeading = null)
    {
        var course = aircraft.GroundTrackDegrees ?? aircraft.Heading;
        aircraft.Heading = course; // Route geometry follows the ground track, not the crab angle.
        var distance = AirTrackDistance(aircraft, course, dt);
        if (!WindMotion.Track(aircraft.SpeedKnots, course, State.Options).CanHoldTrack)
        {
            aircraft.Movement = null;
            return waypoint;
        }
        aircraft.Heading = course;
        var next = RouteMovement.Advance(aircraft, path, waypoint, distance, exitHeading);
        aircraft.GroundTrackDegrees = aircraft.Heading;
        aircraft.Heading = WindMotion.Track(aircraft.SpeedKnots, aircraft.Heading, State.Options).Heading;
        return next;
    }
}
