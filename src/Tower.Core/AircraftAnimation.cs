namespace Tower.Core;

public sealed class AircraftAnimationState
{
    public double GearExtension { get; set; } = 1;
    public bool GearCommandedDown { get; set; } = true;
    public bool LandingLights { get; set; }
    public bool IsValid() => double.IsFinite(GearExtension) && GearExtension is >= 0 and <= 1;
}

// Original DBT conditional parts; timings and phase thresholds are modern reconstruction.
public static class AircraftAnimation
{
    public static AircraftAnimationState Current(Aircraft aircraft) => aircraft.Animation ?? Initial(aircraft);

    private static AircraftAnimationState Initial(Aircraft aircraft)
    {
        var down = !aircraft.IsAirborne || aircraft.Specification.OriginalFixedGear == true ||
            aircraft.AltitudeFeet < 200 ||
            aircraft.AltitudeFeet <= 1500 && aircraft.Phase is FlightPhase.Approach or FlightPhase.LowApproach or FlightPhase.Circuit;
        return new() { GearExtension = down ? 1 : 0, GearCommandedDown = down, LandingLights = Lights(aircraft) };
    }

    public static void Update(Aircraft aircraft, double seconds)
    {
        var state = aircraft.Animation ??= Initial(aircraft);
        if (!aircraft.IsAirborne || aircraft.Specification.OriginalFixedGear == true)
        {
            // Ground contact always uses the fully deployed source geometry.
            state.GearExtension = 1;
            state.GearCommandedDown = true;
        }
        else
        {
            if (aircraft.Phase == FlightPhase.Approach)
            {
                if (aircraft.AltitudeFeet <= 1500)
                {
                    state.GearCommandedDown = true;
                }
                else if (aircraft.AltitudeFeet >= 2000)
                {
                    state.GearCommandedDown = false;
                }
            }
            else if (aircraft.Phase == FlightPhase.LowApproach ||
                aircraft.Phase == FlightPhase.Circuit && aircraft.CircuitLeg is "base" or "final")
            {
                state.GearCommandedDown = true;
            }
            else if (aircraft.Phase is FlightPhase.Departing or FlightPhase.GoAround or FlightPhase.Breakout && aircraft.AltitudeFeet >= 200)
            {
                state.GearCommandedDown = false;
            }
            // Heading/orbit instructions preserve the current gear command.
            var target = state.GearCommandedDown ? 1d : 0;
            state.GearExtension += Math.Clamp(target - state.GearExtension, -seconds / 6, seconds / 8);
        }
        state.LandingLights = Lights(aircraft);
    }

    private static bool Lights(Aircraft aircraft) => aircraft.IsAirborne ? aircraft.AltitudeFeet < 10000
        : aircraft.Phase is FlightPhase.LineUp or FlightPhase.LinedUp or FlightPhase.TakeoffRoll or FlightPhase.LandingRoll or FlightPhase.AbortedTakeoff;
}
