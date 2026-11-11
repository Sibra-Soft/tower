namespace Tower.Core;

/// <summary>Continuous modern reconstruction using retained PRF values, not an emulation of BAO fixed point.</summary>
public static class FlightDynamics
{
    public static double Profile(Aircraft aircraft, string key, double fallback) =>
        aircraft.Specification.OriginalPerformance.TryGetValue(key, out var value) && double.IsFinite(value) ? value : fallback;

    public static double GroundRadius(Aircraft aircraft)
    {
        var length = aircraft.Specification.GroundFootprint is { } footprint
            ? footprint.MaxForward - footprint.MinForward : aircraft.Specification.WingSpanMetres;
        return Math.Clamp(length * .65, 10, 45);
    }

    public static double DepartureSpeed(Aircraft aircraft) => Math.Max(aircraft.Specification.TakeoffSpeed,
        Math.Min(Profile(aircraft, "Climb_Speed", 220), Profile(aircraft, "Top_Speed", 250)));

    public static double LandingSpeed(Aircraft aircraft)
    {
        var minimum = Profile(aircraft, "Stall_Speed", aircraft.Specification.ApproachSpeed * .75) * 1.1;
        var recorded = aircraft.Specification.LandingSpeed ?? 0;
        return Math.Min(aircraft.Specification.ApproachSpeed, recorded >= minimum
            ? recorded : Math.Max(minimum, aircraft.Specification.ApproachSpeed * .85));
    }

    public static double TurnRate(Aircraft aircraft)
    {
        var speed = Math.Max(1, aircraft.SpeedKnots * Simulation.MetresPerKnotSecond);
        var bankLimit = 9.80665 * Math.Tan(25 * Math.PI / 180) / speed * 180 / Math.PI;
        var rate = aircraft.Phase == FlightPhase.Approach || aircraft.CircuitLeg == "final"
            ? Profile(aircraft, "Turn_Rate_on_Final", aircraft.Specification.TurnRate) : aircraft.Specification.TurnRate;
        return Math.Max(.1, Math.Min(rate, bankLimit));
    }

    public static void UpdateAttitude(Aircraft aircraft, double previousHeading, double previousAltitude, double dt, ScenarioOptions? weather = null)
    {
        var rate = RouteMovement.AngleDifference(previousHeading, aircraft.Heading) / dt;
        var bank = aircraft.IsAirborne ? Math.Clamp(Math.Atan(aircraft.SpeedKnots * Simulation.MetresPerKnotSecond *
            rate * Math.PI / 180 / 9.80665) * 180 / Math.PI, -25, 25) : 0;
        aircraft.BankDegrees = Towards(aircraft.BankDegrees, bank, 12 * dt);
        double pitch;
        if (aircraft.Phase == FlightPhase.Approach)
        {
            pitch = aircraft.TouchdownDistanceMetres is not null && aircraft.AltitudeFeet < 50
                ? Profile(aircraft, "Touchdown_Pitch", 4) : Profile(aircraft, "Approach_Pitch", 2);
        }
        else if (aircraft.Phase == FlightPhase.TakeoffRoll)
        {
            var rotationSpeed = weather is null ? aircraft.SpeedKnots : WindMotion.RunwayAirspeed(aircraft.SpeedKnots, aircraft.Heading, weather);
            var fraction = aircraft.Path.Count == 3 && aircraft.PathIndex == 1 ? 0
                : Math.Clamp((rotationSpeed / Math.Max(1, aircraft.Specification.TakeoffSpeed) - .8) / .2, 0, 1);
            pitch = Math.Clamp(Profile(aircraft, "Takeoff_Pitch", 8), 2, 15) * fraction;
        }
        else if (aircraft.IsAirborne)
        {
            var vertical = (aircraft.AltitudeFeet - previousAltitude) * Coordinates.MetresPerFoot / dt;
            pitch = Math.Clamp(Math.Atan2(vertical, Math.Max(1, aircraft.SpeedKnots * Simulation.MetresPerKnotSecond)) * 180 / Math.PI + 2, -10, 15);
        }
        else
        {
            pitch = 0;
        }
        aircraft.PitchDegrees = Towards(aircraft.PitchDegrees, pitch, (aircraft.IsAirborne ? 3 : 2) * dt);
    }

    private static double Towards(double value, double target, double step) => value + Math.Clamp(target - value, -step, step);
}
