namespace Tower.Core;

// Values match the original EXE's view selector; camera placement uses metres/AGL.
public enum TowerViewMode
{
    Normal, Pilot, Tracking, SuperBinocular, Binocular, SpotPlane
}

public readonly record struct TowerCameraFrame(Position Observer, double EyeHeight, double Bearing,
    double Pitch, double FieldOfView, string? HiddenAircraft = null);

public sealed class TowerCamera(Position tower, double towerEyeHeight)
{
    private double towerBearing = 270;
    private double relativeBearing;
    private double fieldOfView = 90;

    public TowerViewMode Mode { get; private set; }
    public string? TargetCallsign { get; private set; }
    public bool NeedsAircraft => Mode is TowerViewMode.Tracking or TowerViewMode.SuperBinocular or TowerViewMode.SpotPlane or TowerViewMode.Pilot;
    public bool CanZoom => Mode is TowerViewMode.Normal or TowerViewMode.SpotPlane or TowerViewMode.Pilot;
    public bool AtTower => Mode is not (TowerViewMode.SpotPlane or TowerViewMode.Pilot);

    public bool SetMode(TowerViewMode mode, Aircraft? target = null)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
        var needsAircraft = mode is TowerViewMode.Tracking or TowerViewMode.SuperBinocular or TowerViewMode.SpotPlane or TowerViewMode.Pilot;
        if (needsAircraft && (target is null || !target.IsOnFrequency || target.Phase == FlightPhase.Completed))
        {
            return false;
        }
        Mode = mode;
        TargetCallsign = needsAircraft ? target!.CallSign : null;
        fieldOfView = 90;
        relativeBearing = 0;
        return true;
    }

    public void Retarget(Aircraft target)
    {
        if (NeedsAircraft && target.IsOnFrequency && target.Phase != FlightPhase.Completed)
        {
            TargetCallsign = target.CallSign;
        }
    }

    public void StopTracking(string callsign)
    {
        if (string.Equals(TargetCallsign, callsign, StringComparison.OrdinalIgnoreCase))
        {
            SetMode(TowerViewMode.Normal);
        }
    }

    public void Pan(double degrees, TowerCameraFrame current)
    {
        if (Mode is TowerViewMode.Pilot or TowerViewMode.SpotPlane)
        {
            relativeBearing = Coordinates.NormalizeHeading(relativeBearing + degrees);
        }
        else
        {
            if (NeedsAircraft)
            {
                SetMode(TowerViewMode.Normal);
            }
            towerBearing = Coordinates.NormalizeHeading(current.Bearing + degrees);
        }
    }

    public void Zoom(bool zoomIn)
    {
        if (CanZoom)
        {
            fieldOfView = Math.Clamp(fieldOfView * (zoomIn ? 0.8 : 1.25), 20, 120);
        }
    }

    public TowerCameraFrame GetFrame(Session session)
    {
        var target = NeedsAircraft ? session.Aircraft.FirstOrDefault(aircraft => aircraft.CallSign == TargetCallsign &&
            aircraft.IsOnFrequency && aircraft.Phase != FlightPhase.Completed) : null;
        if (NeedsAircraft && target is null)
        {
            SetMode(TowerViewMode.Normal);
        }
        if (target is null)
        {
            var lens = Mode == TowerViewMode.Binocular ? 2 * Math.Atan(1d / 8) * 180 / Math.PI : fieldOfView;
            return new(tower, towerEyeHeight, towerBearing, 0, lens);
        }

        var altitude = target.AltitudeFeet * Coordinates.MetresPerFoot;
        var span = Math.Max(1, target.Specification.WingSpanMetres);
        var observer = tower;
        var eyeHeight = towerEyeHeight;
        var lensAngle = fieldOfView;
        if (Mode == TowerViewMode.Pilot)
        {
            // There is no decoded cockpit/camera eye point in the current model loader.
            observer = target.Position.Offset(target.Heading, Math.Max(1, span * 0.08));
            eyeHeight = altitude + Math.Max(2, span * 0.1);
            var pitch = Math.Atan2(target.VerticalSpeedFpm * Coordinates.MetresPerFoot / 60,
                Math.Max(1, target.SpeedKnots * Simulation.MetresPerKnotSecond)) * 180 / Math.PI;
            return new(observer, eyeHeight, Coordinates.NormalizeHeading(target.Heading + relativeBearing),
                Math.Clamp(pitch, -20, 20), lensAngle, target.CallSign);
        }
        if (Mode == TowerViewMode.SpotPlane)
        {
            observer = target.Position.Offset(target.Heading + 180 + relativeBearing, Math.Max(80, span * 5));
            eyeHeight = altitude + Math.Max(15, span * 0.9);
        }
        var distance = observer.Distance(target.Position);
        var elevation = altitude + span * 0.05 - eyeHeight;
        if (Mode == TowerViewMode.SuperBinocular)
        {
            // Distance-dependent framing, as in the original; screen occupancy is modern.
            var range = Math.Max(1, Math.Sqrt(distance * distance + elevation * elevation));
            lensAngle = Math.Clamp(2 * Math.Atan(2 * span / range) * 180 / Math.PI, 0.5, 90);
        }
        return new(observer, eyeHeight, distance > 0.01 ? observer.Bearing(target.Position) : towerBearing,
            Math.Clamp(Math.Atan2(elevation, Math.Max(0.01, distance)) * 180 / Math.PI, -89, 89), lensAngle);
    }
}
