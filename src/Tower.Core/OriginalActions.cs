namespace Tower.Core;

// IDs follow GAMENT.DLL's action table at 0x1006CE00.
public enum OriginalFlightAction
{
    NoLanding, Takeoff, LeavePattern, FullStop, TouchAndGo, LowApproach
}

public sealed class OriginalActionProgress
{
    public List<OriginalFlightAction> Actions { get; set; } = [];
    public int Index { get; set; }
    public bool Completed { get; set; }
    public bool FullStopLanded { get; set; }

    public OriginalFlightAction Current() => Actions[Index];

    public bool IsValid() => Actions is { Count: 3 } && Actions.All(action => Enum.IsDefined(action)) && Index is >= 0 and <= 2;

    public static List<OriginalFlightAction> Parse(IEnumerable<string> tokens)
    {
        var actions = new List<OriginalFlightAction>();
        foreach (var token in tokens)
        {
            OriginalFlightAction? action = token.ToUpperInvariant() switch
            {
                "@NL" => OriginalFlightAction.NoLanding,
                "@TO" => OriginalFlightAction.Takeoff,
                "@LP" => OriginalFlightAction.LeavePattern,
                "@FS" => OriginalFlightAction.FullStop,
                "@TG" => OriginalFlightAction.TouchAndGo,
                "@LA" => OriginalFlightAction.LowApproach,
                _ => null
            };
            if (action is { } value && actions.Count < 3)
            {
                actions.Add(value);
            }
        }
        while (actions.Count < 3)
        {
            actions.Add(OriginalFlightAction.NoLanding);
        }
        return actions;
    }
}

public sealed partial class Simulation
{
    private static LandingIntent PlannedLandingIntent(Aircraft aircraft) => aircraft.OriginalActions is { Completed: false } progress
        ? progress.Current() switch
        {
            OriginalFlightAction.TouchAndGo => LandingIntent.TouchAndGo,
            OriginalFlightAction.LowApproach => LandingIntent.LowApproach,
            _ => LandingIntent.FullStop
        } : LandingIntent.FullStop;

    private bool ContinueOriginalActionsAfterAirborne(Aircraft aircraft)
    {
        if (aircraft.OriginalActions is not { Completed: false } actions)
        {
            return false;
        }
        actions.Index = Math.Min(2, actions.Index + 1);
        aircraft.LandingCleared = false;
        aircraft.LandingIntent = LandingIntent.FullStop;
        var next = actions.Current();
        if (next is OriginalFlightAction.FullStop or OriginalFlightAction.TouchAndGo or OriginalFlightAction.LowApproach)
        {
            BeginCircuit(aircraft, "downwind", aircraft.RightTraffic, false, aircraft.Runway);
            aircraft.LandingIntent = PlannedLandingIntent(aircraft);
        }
        else
        {
            CancelOriginalRoute(aircraft);
            aircraft.Phase = FlightPhase.Departing;
            var runway = State.Airport.GetRunway(aircraft.Runway);
            aircraft.Heading = State.Airport.Point(runway.Threshold).Bearing(State.Airport.Point(runway.End));
            var plan = aircraft.OriginalPlan!;
            var altitude = plan.AltitudeReference == OriginalAltitudeReference.Msl
                ? State.Airport.Original.SourceMslToAgl(plan.Altitude1Feet) : null;
            aircraft.FollowOriginalDepartureAltitude = altitude is > 0;
            aircraft.FollowOriginalDepartureRoute = plan.DestinationBearingDegrees is not null;
            aircraft.OriginalDepartureRouteStarted = false;
            if (altitude is > 0 && plan.ResolvedRoute is { Count: > 0 })
            {
                aircraft.OriginalRoute = new()
                {
                    Stage = OriginalRouteStage.InitialClimb,
                    Points = plan.ResolvedRoute.ToList(),
                    TargetAltitudeFeetAgl = altitude.Value,
                    UseAltitude2 = next == OriginalFlightAction.LeavePattern
                };
            }
        }
        return true;
    }
}
