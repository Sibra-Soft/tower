using Tower.Core;

namespace Tower.Controls;

internal sealed class AircraftStatusToolTip : IDisposable
{
    private readonly Control owner;
    private readonly Func<Point, Aircraft?> aircraftAt;
    private readonly ToolTip toolTip = new() { InitialDelay = 500, ReshowDelay = 100, AutoPopDelay = 10000, ShowAlways = true };
    private Point location;
    private bool hovering;
    private bool disposed;

    public AircraftStatusToolTip(Control owner, Func<Point, Aircraft?> aircraftAt)
    {
        this.owner = owner;
        this.aircraftAt = aircraftAt;
        owner.MouseMove += MouseMove;
        owner.MouseLeave += MouseLeave;
        owner.Disposed += OwnerDisposed;
    }

    private void MouseMove(object? sender, MouseEventArgs e)
    {
        location = e.Location;
        hovering = e.Button == MouseButtons.None;
        Refresh();
    }

    private void MouseLeave(object? sender, EventArgs e)
    {
        hovering = false;
        Refresh();
    }

    public void Refresh()
    {
        if (disposed)
        {
            return;
        }
        var aircraft = hovering && !owner.Capture ? aircraftAt(location) : null;
        var text = aircraft is { Phase: not FlightPhase.Completed }
            ? aircraft.CallSign + Environment.NewLine + Describe(aircraft) : "";
        if (toolTip.GetToolTip(owner) != text)
        {
            toolTip.SetToolTip(owner, text);
        }
    }

    private static string Describe(Aircraft aircraft)
    {
        if (aircraft.Holding && aircraft.HoldShortRunway is { } crossing)
        {
            return "Holding short of runway " + crossing;
        }
        var status = aircraft.Phase switch
        {
            FlightPhase.AtGate when aircraft.Pushback is { Completed: false } => "Awaiting pushback at gate " + aircraft.Gate,
            FlightPhase.AtGate => "At gate " + aircraft.Gate,
            FlightPhase.Pushback => "Pushing back from gate " + aircraft.Gate,
            FlightPhase.ReadyForTaxi => "Ready for taxi",
            FlightPhase.TaxiOut => "Taxiing to runway " + aircraft.Runway,
            FlightPhase.TaxiIn => "Taxiing to gate " + aircraft.Gate,
            FlightPhase.HoldingShort => "Holding short of runway " + aircraft.Runway,
            FlightPhase.LineUp => "Lining up on runway " + aircraft.Runway,
            FlightPhase.LinedUp => "Lined up on runway " + aircraft.Runway,
            FlightPhase.TakeoffRoll => "Taking off from runway " + aircraft.Runway,
            FlightPhase.Departing => "Departing runway " + aircraft.Runway,
            FlightPhase.Approach => "Approaching runway " + aircraft.Runway,
            FlightPhase.Circuit => "Flying the traffic pattern for runway " + aircraft.Runway,
            FlightPhase.GoAround => "Going around from runway " + aircraft.Runway,
            FlightPhase.LandingRoll => "Landing rollout on runway " + aircraft.Runway,
            FlightPhase.ArrivalHold => "Awaiting taxi to parking",
            FlightPhase.LowApproach => "Low approach to runway " + aircraft.Runway,
            FlightPhase.AbortedTakeoff => "Takeoff aborted on runway " + aircraft.Runway,
            FlightPhase.Vectoring => "Following heading instructions",
            FlightPhase.Breakout => "Leaving the traffic pattern",
            _ => "Flight completed"
        };
        return aircraft.Holding && aircraft.Phase is FlightPhase.TaxiIn or FlightPhase.TaxiOut or FlightPhase.Pushback
            ? "Holding position - " + status : status;
    }

    private void OwnerDisposed(object? sender, EventArgs e) => Dispose();

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        owner.MouseMove -= MouseMove;
        owner.MouseLeave -= MouseLeave;
        owner.Disposed -= OwnerDisposed;
        toolTip.Dispose();
    }
}
