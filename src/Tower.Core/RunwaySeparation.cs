namespace Tower.Core;

// One most recent takeoff per physical strip, independent of callsign reuse or handoff.
public sealed class RunwayDeparture
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CallSign { get; set; } = "";
    public string Runway { get; set; } = "";
    public int? WeightCategory { get; set; }
    public string? OriginalTypeGroup { get; set; }
    public double StartedSeconds { get; set; }
    public bool Airborne { get; set; }
    public Position Position { get; set; }
}

public sealed partial class Simulation
{
    private void RecordTakeoffRoll(Aircraft aircraft)
    {
        CheckRunwayCategorySeparation(aircraft, landing: false);
        var runway = State.Airport.GetRunway(aircraft.Runway);
        State.RunwayDepartures.RemoveAll(previous => SamePhysicalRunway(runway, State.Airport.GetRunway(previous.Runway)));
        var departure = new RunwayDeparture
        {
            CallSign = aircraft.CallSign,
            Runway = aircraft.Runway,
            WeightCategory = aircraft.Specification.WeightCategory,
            OriginalTypeGroup = aircraft.Specification.OriginalTypeGroup,
            StartedSeconds = State.ElapsedSeconds,
            Position = aircraft.Position
        };
        aircraft.RunwayDepartureId = departure.Id;
        State.RunwayDepartures.Add(departure);
    }

    private void UpdateRunwayDeparture(Aircraft aircraft)
    {
        var departure = State.RunwayDepartures.Find(value => value.Id == aircraft.RunwayDepartureId);
        if (departure is not null)
        {
            departure.Position = aircraft.Position;
        }
    }

    private void MarkRunwayDepartureAirborne(Aircraft aircraft)
    {
        State.Performance.RecordMovement(State.ElapsedSeconds);
        var departure = State.RunwayDepartures.Find(value => value.Id == aircraft.RunwayDepartureId);
        if (departure is not null)
        {
            departure.Airborne = true;
        }
    }

    private void CheckRunwayCategorySeparation(Aircraft aircraft, bool landing)
    {
        var runway = State.Airport.GetRunway(aircraft.Runway);
        var previous = State.RunwayDepartures.Find(value => SamePhysicalRunway(runway, State.Airport.GetRunway(value.Runway)));
        if (previous is null || previous.Id == aircraft.RunwayDepartureId)
        {
            return;
        }

        var elapsed = State.ElapsedSeconds - previous.StartedSeconds;
        var heavy = string.Equals(previous.OriginalTypeGroup, "Heavy", StringComparison.OrdinalIgnoreCase);
        string? reason = null;
        if (!previous.Runway.Equals(aircraft.Runway, StringComparison.OrdinalIgnoreCase))
        {
            if (elapsed < 180 && (landing || heavy || aircraft.Specification.WeightCategory == 1 && previous.WeightCategory == 3))
            {
                reason = "180 seconds required after opposite-direction takeoff";
            }
        }
        else if (previous.Airborne)
        {
            var category = aircraft.Specification.WeightCategory;
            // Unknown categories are not silently assigned a historical classification.
            if (category is >= 1 and <= 3 && previous.WeightCategory is >= 1 and <= 3)
            {
                var feet = category == 3 || previous.WeightCategory == 3 ? 6000 : category == 2 ? 4500 : 3000;
                var distanceFeet = aircraft.Position.Distance(previous.Position) / Coordinates.MetresPerFoot;
                if (distanceFeet + 0.000001 < feet)
                {
                    reason = $"{feet} feet required after same-direction takeoff";
                }
                else if (!landing && heavy && elapsed < 120)
                {
                    reason = "120 seconds required after heavy takeoff";
                }
            }
        }

        if (reason is not null)
        {
            if (landing)
            {
                State.Performance.CategorySeparationArrival++;
            }
            else
            {
                State.Performance.CategorySeparationDeparture++;
            }
            State.Performance.Incidents++;
            Award(-20);
            var movement = landing ? "touchdown" : "takeoff roll";
            Message?.Invoke($"Category separation alert: {aircraft.CallSign} after {previous.CallSign} at {movement}; {reason}.");
        }
    }
}
