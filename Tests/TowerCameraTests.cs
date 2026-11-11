using Tower.Core;
using Xunit;

namespace Tower.Tests;

public sealed class TowerCameraTests
{
    private static (Session Session, Aircraft Aircraft, TowerCamera Camera) Create()
    {
        var session = new Session();
        var aircraft = new Aircraft { CallSign = "VIEW", Position = new(3000, 4000), AltitudeFeet = 5000,
            Heading = 110, Phase = FlightPhase.Approach, SpeedKnots = 140 };
        aircraft.Specification.WingSpanMetres = 15;
        session.Aircraft.Add(aircraft);
        return (session, aircraft, new TowerCamera(new(0, 0), 60));
    }

    [Fact]
    public void Normal_and_fixed_eight_times_binocular_remain_at_the_tower_without_a_target()
    {
        var (session, _, camera) = Create();
        var normal = camera.GetFrame(session);
        Assert.Equal(TowerViewMode.Normal, camera.Mode);
        Assert.Equal(270, normal.Bearing);
        Assert.Equal(90, normal.FieldOfView);
        Assert.True(camera.SetMode(TowerViewMode.Binocular));
        var binocular = camera.GetFrame(session);
        Assert.Equal(normal.Observer, binocular.Observer);
        Assert.Equal(normal.EyeHeight, binocular.EyeHeight);
        Assert.Null(camera.TargetCallsign);
        Assert.Equal(8, Math.Tan(normal.FieldOfView * Math.PI / 360) / Math.Tan(binocular.FieldOfView * Math.PI / 360), 10);
        camera.Pan(10, binocular);
        Assert.Equal(TowerViewMode.Binocular, camera.Mode);
        Assert.Equal(280, camera.GetFrame(session).Bearing);
    }

    [Theory]
    [InlineData(TowerViewMode.Tracking)]
    [InlineData(TowerViewMode.SuperBinocular)]
    [InlineData(TowerViewMode.Pilot)]
    [InlineData(TowerViewMode.SpotPlane)]
    public void Target_modes_require_an_active_aircraft_and_fall_back_when_it_disappears(TowerViewMode mode)
    {
        var (session, aircraft, camera) = Create();
        Assert.False(camera.SetMode(mode));
        Assert.Equal(TowerViewMode.Normal, camera.Mode);
        aircraft.Phase = FlightPhase.Completed;
        Assert.False(camera.SetMode(mode, aircraft));
        aircraft.Phase = FlightPhase.Approach;
        Assert.True(camera.SetMode(mode, aircraft));
        Assert.Equal(aircraft.CallSign, camera.TargetCallsign);
        session.Aircraft.Remove(aircraft);
        var frame = camera.GetFrame(session);
        Assert.Equal(TowerViewMode.Normal, camera.Mode);
        Assert.Null(camera.TargetCallsign);
        Assert.Equal(new Position(0, 0), frame.Observer);
        Assert.Equal(60, frame.EyeHeight);
        Assert.Equal(90, frame.FieldOfView);
    }

    [Theory]
    [InlineData(TowerViewMode.Tracking)]
    [InlineData(TowerViewMode.SuperBinocular)]
    [InlineData(TowerViewMode.SpotPlane)]
    public void Look_at_camera_centres_the_target_in_both_heading_and_elevation(TowerViewMode mode)
    {
        var (session, aircraft, camera) = Create();
        camera.SetMode(mode, aircraft);
        var frame = camera.GetFrame(session);
        var projection = new TowerProjection(frame.Observer, frame.Bearing, frame.FieldOfView, 1000, 600, frame.EyeHeight, frame.Pitch);
        var target = projection.Point(aircraft.Position, aircraft.AltitudeFeet * Coordinates.MetresPerFoot + aircraft.Specification.WingSpanMetres * 0.05);
        Assert.NotNull(target);
        Assert.Equal(500, target.Value.X, 6);
        Assert.Equal(258, target.Value.Y, 6);
    }

    [Fact]
    public void Super_binocular_adjusts_framing_to_target_distance()
    {
        var (session, aircraft, camera) = Create();
        camera.SetMode(TowerViewMode.SuperBinocular, aircraft);
        var near = camera.GetFrame(session);
        aircraft.Position = new(6000, 8000);
        var far = camera.GetFrame(session);
        Assert.True(far.FieldOfView < near.FieldOfView);
        Assert.InRange(far.FieldOfView, 0.5, 90);
        Assert.False(camera.CanZoom);
        Assert.Equal(near.Observer, far.Observer);
    }

    [Fact]
    public void Pilot_moves_and_turns_with_the_aircraft_and_hides_its_own_model()
    {
        var (session, aircraft, camera) = Create();
        camera.SetMode(TowerViewMode.Pilot, aircraft);
        var first = camera.GetFrame(session);
        Assert.Equal(aircraft.CallSign, first.HiddenAircraft);
        Assert.Equal(aircraft.Heading, first.Bearing);
        Assert.True(first.EyeHeight > aircraft.AltitudeFeet * Coordinates.MetresPerFoot);
        aircraft.Position = aircraft.Position.Offset(aircraft.Heading, 1000);
        aircraft.Heading += 20;
        aircraft.VerticalSpeedFpm = 1200;
        var second = camera.GetFrame(session);
        Assert.True(first.Observer.Distance(second.Observer) > 900);
        Assert.Equal(aircraft.Heading, second.Bearing);
        Assert.True(second.Pitch > 0);
        camera.Pan(-10, second);
        Assert.Equal(TowerViewMode.Pilot, camera.Mode);
        Assert.Equal(aircraft.Heading - 10, camera.GetFrame(session).Bearing);
    }

    [Fact]
    public void Spot_plane_orbits_the_aircraft_and_restores_the_tower_on_normal()
    {
        var (session, aircraft, camera) = Create();
        camera.SetMode(TowerViewMode.SpotPlane, aircraft);
        var first = camera.GetFrame(session);
        Assert.Null(first.HiddenAircraft);
        Assert.True(first.Pitch < 0);
        camera.Pan(10, first);
        var second = camera.GetFrame(session);
        Assert.Equal(Coordinates.NormalizeHeading(first.Bearing + 10), second.Bearing, 6);
        Assert.True(first.Observer.Distance(second.Observer) > 1);
        camera.SetMode(TowerViewMode.Normal);
        var normal = camera.GetFrame(session);
        Assert.Equal(new Position(0, 0), normal.Observer);
        Assert.Equal(270, normal.Bearing);
    }

    [Fact]
    public void Manual_pan_releases_tracking_from_the_current_target_bearing()
    {
        var (session, aircraft, camera) = Create();
        camera.SetMode(TowerViewMode.Tracking, aircraft);
        var tracked = camera.GetFrame(session);
        camera.Pan(10, tracked);
        Assert.Equal(TowerViewMode.Normal, camera.Mode);
        Assert.Null(camera.TargetCallsign);
        Assert.Equal(tracked.Bearing + 10, camera.GetFrame(session).Bearing, 6);
    }

    [Fact]
    public void Selection_changes_retarget_without_changing_the_active_view_mode()
    {
        var (session, aircraft, camera) = Create();
        var other = new Aircraft { CallSign = "OTHER", Position = new(-2000, 3000) };
        session.Aircraft.Add(other);
        camera.SetMode(TowerViewMode.SuperBinocular, aircraft);
        camera.Retarget(other);
        Assert.Equal(TowerViewMode.SuperBinocular, camera.Mode);
        Assert.Equal(other.CallSign, camera.TargetCallsign);
        Assert.Equal(new Position(0, 0).Bearing(other.Position), camera.GetFrame(session).Bearing);
    }

    [Fact]
    public void Normal_zoom_preserves_existing_limits_and_binocular_zoom_is_fixed()
    {
        var (session, _, camera) = Create();
        camera.Zoom(true);
        Assert.Equal(72, camera.GetFrame(session).FieldOfView);
        for (var index = 0; index < 30; index++)
        {
            camera.Zoom(true);
        }
        Assert.Equal(20, camera.GetFrame(session).FieldOfView);
        for (var index = 0; index < 30; index++)
        {
            camera.Zoom(false);
        }
        Assert.Equal(120, camera.GetFrame(session).FieldOfView);
        camera.SetMode(TowerViewMode.Binocular);
        var before = camera.GetFrame(session);
        camera.Zoom(true);
        Assert.Equal(before, camera.GetFrame(session));
    }

    [Fact]
    public void Pitched_ground_points_lines_and_polygons_use_the_same_camera()
    {
        var projection = new TowerProjection(new(0, 0), 0, 90, 1000, 600, 3000, -45);
        var point = projection.Point(new(0, 3000));
        Assert.NotNull(point);
        Assert.Equal(500, point.Value.X, 6);
        Assert.Equal(258, point.Value.Y, 6);
        var line = projection.GroundSegment(new(-100, 3000), new(100, 3000));
        Assert.NotNull(line);
        Assert.Equal(point.Value.Y, line.Value.From.Y, 6);
        Assert.Equal(point.Value.Y, line.Value.To.Y, 6);
        Assert.Equal(4, projection.GroundPolygon([new(-100, 2900), new(100, 2900), new(100, 3100), new(-100, 3100)]).Length);
    }
}
