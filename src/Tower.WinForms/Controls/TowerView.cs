using Tower.Core;
using Tower.Infrastructure;
using TowerSpeech.Core;

namespace Tower.Controls;

public sealed class TowerView : Control
{
    private readonly Simulation simulation;
    private readonly DisplaySettings settings;
    private readonly AircraftModelCatalog models;
    private readonly string modelCatalogError = "";
    private readonly AirportScenery scenery;
    private readonly PanoramaRenderer? panorama;
    private sealed record ViewMesh(AircraftMesh Mesh, (Position Position, double Altitude)[] World, string Kind, Aircraft? Aircraft = null);
    private readonly List<ViewMesh> sceneryMeshes = [];
    private Point? drag;
    private readonly TowerCamera camera;
    private TowerCameraFrame frame;
    private double bearing => frame.Bearing;
    private double fieldOfView => frame.FieldOfView;
    private Position observer => frame.Observer;
    [System.ComponentModel.Browsable(false)]
    public bool HasPanorama => panorama is not null;
    public Rectangle SceneViewport
    {
        get
        {
            if (scenery.Panorama is not { } image || !camera.AtTower || ClientSize.Width < 1 || ClientSize.Height < 1)
            {
                return ClientRectangle;
            }
            var density = image.Width / (2 * Math.PI);
            var width = Math.Max(1, Math.Min(ClientSize.Width, (int)Math.Floor(density * 2)));
            var focal = width / (2 * Math.Tan(fieldOfView * Math.PI / 360));
            var foreground = image.Height - image.HorizonRow - 1;
            var nativeHeight = foreground / 0.57;
            var coveredHeight = foreground * focal / (0.57 * density);
            var height = Math.Max(1, Math.Min(ClientSize.Height, (int)Math.Floor(Math.Min(nativeHeight, coveredHeight))));
            return new Rectangle((ClientSize.Width - width) / 2, (ClientSize.Height - height) / 2, width, height);
        }
    }
    private TowerProjection Projection => new(observer, bearing, fieldOfView, SceneViewport.Width, SceneViewport.Height, frame.EyeHeight, frame.Pitch);
    public TowerViewMode ViewMode => camera.Mode;
    public TowerCameraFrame CameraFrame => frame;
    public string? TrackingCallsign => camera.TargetCallsign;
    public bool CanZoom => camera.CanZoom;
    public event Action? ViewModeChanged;
    public TowerView(Simulation simulation, DisplaySettings settings)
    {
        this.simulation = simulation;
        this.settings = settings;
        scenery = AirportScenery.Read(WorkspacePaths.DefaultRoot, simulation.State.Airport);
        panorama = scenery.Panorama is { } image ? new(image) : null;
        foreach (var item in scenery.Objects)
        {
            sceneryMeshes.Add(new(item.Mesh, item.WorldPoints(), item.Kind));
        }
        try
        {
            models = new(WorkspacePaths.DefaultRoot);
            foreach (var type in simulation.State.Airport.AircraftTypes.Keys)
            {
                _ = models.Get(type);
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            models = new();
            modelCatalogError = exception.Message;
        }
        camera = new(simulation.State.Airport.Observer, scenery.EyeHeightMetres);
        UpdateCamera();
        DoubleBuffered = true;
        Dock = DockStyle.Fill;
        MouseDown += (_, e) =>
        {
            if (!SceneViewport.Contains(e.Location))
            {
                return;
            }
            drag = e.Location;
            Capture = true;
            Focus();
        };
        MouseUp += (_, _) =>
        {
            drag = null;
            Capture = false;
        };
        MouseMove += (_, e) =>
        {
            if (drag is not { } previous)
            {
                return;
            }

            Pan((previous.X - e.X) * fieldOfView / Math.Max(1, SceneViewport.Width));
            drag = e.Location;
            Invalidate();
        };
        MouseWheel += (_, e) =>
        {
            if (SceneViewport.Contains(e.Location))
            {
                Zoom(e.Delta > 0);
            }
        };
    }

    public void Pan(double degrees)
    {
        UpdateCamera();
        var previousMode = camera.Mode;
        camera.Pan(degrees, frame);
        UpdateCamera();
        if (previousMode != camera.Mode)
        {
            ViewModeChanged?.Invoke();
        }
        Invalidate();
    }

    public void Zoom(bool zoomIn)
    {
        camera.Zoom(zoomIn);
        UpdateCamera();
        Invalidate();
    }
    public void Track(string callsign)
    {
        SetViewMode(TowerViewMode.Tracking, callsign);
    }
    public void StopTracking(string callsign)
    {
        var previousMode = camera.Mode;
        camera.StopTracking(callsign);
        UpdateCamera();
        if (previousMode != camera.Mode)
        {
            ViewModeChanged?.Invoke();
        }
        Invalidate();
    }

    public bool SetViewMode(TowerViewMode mode, string? callsign = null)
    {
        var aircraft = simulation.State.Aircraft.FirstOrDefault(aircraft => aircraft.CallSign == callsign);
        if (!camera.SetMode(mode, aircraft))
        {
            return false;
        }
        UpdateCamera();
        ViewModeChanged?.Invoke();
        Invalidate();
        return true;
    }

    public void SelectAircraft(string callsign)
    {
        if (simulation.State.Aircraft.FirstOrDefault(aircraft => aircraft.CallSign == callsign) is { } aircraft)
        {
            camera.Retarget(aircraft);
            UpdateCamera();
            Invalidate();
        }
    }

    private void UpdateCamera()
    {
        var previousMode = camera.Mode;
        frame = camera.GetFrame(simulation.State);
        if (previousMode != camera.Mode)
        {
            ViewModeChanged?.Invoke();
        }
    }
    private PointF? Project(Position point, double altitude = 0)
    {
        return Projection.Point(point, altitude) is { } projected
            ? new PointF((float)projected.X, (float)projected.Y)
            : null;
    }
    private void DrawRunway(Graphics graphics, Position from, Position to, bool surface = true)
    {
        // Synthetic rendering dimensions, not decoded original runway widths.
        const double runwayWidth = 45;
        const double dashLength = 30;
        const double dashSpacing = 60;
        var heading = from.Bearing(to);
        var length = from.Distance(to);

        void FillStrip(Position start, Position finish, double width, Brush brush)
        {
            var polygon = Projection.GroundPolygon([
                start.Offset(heading - 90, width / 2),
                finish.Offset(heading - 90, width / 2),
                finish.Offset(heading + 90, width / 2),
                start.Offset(heading + 90, width / 2)
            ]);
            if (polygon.Length >= 3)
            {
                graphics.FillPolygon(brush, polygon.Select(point => new PointF((float)point.X, (float)point.Y)).ToArray());
            }
        }

        if (surface)
        {
            FillStrip(from, to, runwayWidth, Brushes.Black);
            for (var distance = 15.0; distance < length - 15; distance += dashSpacing)
            {
                FillStrip(from.Offset(heading, distance), from.Offset(heading, Math.Min(distance + dashLength, length - 15)), 1.5, Brushes.White);
            }
        }

        if (!settings.RunwayLights)
        {
            return;
        }

        for (var distance = 0.0; distance <= length; distance += 60)
        {
            var center = from.Offset(heading, distance);
            foreach (var side in new[] { -90, 90 })
            {
                if (Project(center.Offset(heading + side, runwayWidth / 2)) is { } light)
                {
                    graphics.FillEllipse(Brushes.LightYellow, light.X - 1, light.Y - 1, 3, 3);
                }
            }
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        UpdateCamera();
        g.Clear(Color.Black);
        var viewport = SceneViewport;
        var state = g.Save();
        try
        {
            g.SetClip(viewport, System.Drawing.Drawing2D.CombineMode.Intersect);
            g.TranslateTransform(viewport.X, viewport.Y);
            DrawScene(g, viewport.Size);
        }
        finally
        {
            g.Restore(state);
        }
    }

    private void DrawScene(Graphics g, Size sceneSize)
    {
        var width = sceneSize.Width;
        var height = sceneSize.Height;

        var minutes = (simulation.State.Options.StartMinutes + simulation.State.ElapsedSeconds / 60) % 1440;
        var night = minutes < 360 || minutes > 1200;
        using var sky = new SolidBrush(night ? Color.FromArgb(12, 18, 35) : Color.FromArgb(130, 165, 190));
        g.FillRectangle(sky, 0, 0, width, height);
        using var ground = new SolidBrush(night ? Color.FromArgb(15, 24, 18) : Color.FromArgb(67, 89, 57));
        var horizon = height * 0.43 + Math.Tan(frame.Pitch * Math.PI / 180) * width / (2 * Math.Tan(fieldOfView * Math.PI / 360));
        var groundTop = (float)Math.Clamp(horizon, 0, height);
        g.FillRectangle(ground, 0, groundTop, width, height - groundTop);
        var usePanorama = panorama is not null && camera.AtTower;
        if (usePanorama)
        {
            panorama!.Draw(g, sceneSize, bearing, fieldOfView, night, frame.Pitch);
        }
        var renderScenery = sceneryMeshes.Where(item => !camera.AtTower || !item.Kind.StartsWith("TOW", StringComparison.OrdinalIgnoreCase)).ToList();
        DrawMeshes(g, renderScenery.Where(item => item.Kind.Equals("GND", StringComparison.OrdinalIgnoreCase)), night);
        var originalSurface = scenery.HasGround || usePanorama;
        foreach (var s in simulation.State.Airport.Segments.Where(segment => !segment.Kind.Equals("RUNWAY", StringComparison.OrdinalIgnoreCase)))
        {
            var segment = Projection.GroundSegment(simulation.State.Airport.Point(s.From), simulation.State.Airport.Point(s.To));
            if (segment is null)
            {
                continue;
            }

            var a = new PointF((float)segment.Value.From.X, (float)segment.Value.From.Y);
            var b = new PointF((float)segment.Value.To.X, (float)segment.Value.To.Y);
            using var pen = new Pen(Color.FromArgb(70, 70, 70), 4);
            if (!originalSurface)
            {
                g.DrawLine(pen, a, b);
            }
            if (settings.TaxiwayLights && !scenery.HasGround)
            {
                for (var i = 0; i <= 20; i++)
                {
                    var x = a.X + (b.X - a.X) * i / 20;
                    var y = a.Y + (b.Y - a.Y) * i / 20;
                    g.FillEllipse(Brushes.Blue, x - 1, y - 1, 3, 3);
                }
            }
        }
        foreach (var runway in simulation.State.Airport.Segments.Where(segment => !scenery.HasGround && segment.Kind.Equals("RUNWAY", StringComparison.OrdinalIgnoreCase)))
        {
            DrawRunway(g, simulation.State.Airport.Point(runway.From), simulation.State.Airport.Point(runway.To), !originalSurface);
        }
        var visibleAircraft = simulation.State.Aircraft.Where(a => a.IsOnFrequency && a.Phase != FlightPhase.Completed &&
            a.CallSign != frame.HiddenAircraft && observer.Distance(a.Position) <= simulation.State.Options.VisibilityNm * 1852).ToArray();
        var objects = renderScenery.Where(item => !item.Kind.Equals("GND", StringComparison.OrdinalIgnoreCase) &&
            (!item.Kind.Equals("BEACON", StringComparison.OrdinalIgnoreCase) || settings.Beacon)).ToList();
        foreach (var aircraft in visibleAircraft)
        {
            if (models.Get(aircraft.Type) is { } mesh)
            {
                var distance = Math.Max(1, observer.Distance(aircraft.Position));
                var apparentSpan = aircraft.Specification.WingSpanMetres * width / (2 * Math.Tan(fieldOfView * Math.PI / 360) * distance);
                if (apparentSpan >= 1.5)
                {
                    var world = mesh.WorldPoints(aircraft.Position, aircraft.AltitudeFeet * Coordinates.MetresPerFoot,
                        aircraft.Heading, aircraft.Specification.WingSpanMetres, aircraft.PitchDegrees, aircraft.BankDegrees);
                    objects.Add(new(mesh, world, "AIRCRAFT", aircraft));
                }
                else if (Project(aircraft.Position, aircraft.AltitudeFeet * Coordinates.MetresPerFoot) is { } pixel)
                {
                    g.FillRectangle(Brushes.White, pixel.X, pixel.Y, 1, 1);
                }
            }
        }
        DrawMeshes(g, objects, night);
        foreach (var a in visibleAircraft.OrderByDescending(a => observer.Distance(a.Position)))
        {
            if (observer.Distance(a.Position) > simulation.State.Options.VisibilityNm * 1852)
            {
                continue;
            }

            var mesh = models.Get(a.Type);
            if (Project(a.Position, a.AltitudeFeet * Coordinates.MetresPerFoot) is { } p)
            {
                if (mesh is null)
                {
                    var size = (float)Math.Clamp(18000 / Math.Max(300, observer.Distance(a.Position)), 3, 20);
                    g.DrawLine(Pens.Orange, p.X - size, p.Y, p.X + size, p.Y);
                    g.DrawLine(Pens.Orange, p.X, p.Y - size / 3, p.X, p.Y + size / 3);
                }
                if (settings.ShowAircraftLabels)
                {
                    var label = mesh is null ? $"{a.CallSign} ({a.Type}: symbol)" : a.CallSign;
                    TextRenderer.DrawText(g, label, Font, new Point((int)p.X + 5, (int)p.Y - 18), Color.White, TextFormatFlags.PreserveGraphicsTranslateTransform | TextFormatFlags.PreserveGraphicsClipping);
                }
            }
        }
        if (settings.ShowCompass)
        {
            var mode = camera.Mode switch
            {
                TowerViewMode.SuperBinocular => "Super Binocular",
                TowerViewMode.SpotPlane => "Spot Plane",
                _ => camera.Mode.ToString()
            };
            TextRenderer.DrawText(g, $"{mode}  |  Heading {bearing:000}°  |  View {fieldOfView:0.0}°", Font, new Point(12, 12), Color.White, TextFormatFlags.PreserveGraphicsTranslateTransform | TextFormatFlags.PreserveGraphicsClipping);
        }

        if (settings.ShowGauges)
        {
            TextRenderer.DrawText(g, $"Wind {simulation.State.Options.WindFromDegrees:000}° / {simulation.State.Options.WindKnots:0} kt   Visibility {simulation.State.Options.VisibilityNm:0.0} NM", Font, new Point(12, 24), Color.White, TextFormatFlags.PreserveGraphicsTranslateTransform | TextFormatFlags.PreserveGraphicsClipping);
        }

        if (settings.Beacon && (int)simulation.State.ElapsedSeconds % 2 == 0)
        {
            g.FillEllipse(Brushes.Lime, width - 24, 14, 8, 8);
        }

        if (settings.ObstructionLights)
        {
            g.FillEllipse(Brushes.Red, width - 24, 30, 6, 6);
        }

        var navigation = camera.Mode switch
        {
            TowerViewMode.Binocular => "Drag to pan • Fixed 8× magnification",
            TowerViewMode.SuperBinocular => "Select an aircraft to follow • Automatic zoom",
            TowerViewMode.Tracking => "Select an aircraft to follow • Drag to return to Normal",
            TowerViewMode.Pilot => "Drag to look around • Wheel: zoom",
            TowerViewMode.SpotPlane => "Drag to orbit • Wheel: zoom",
            _ => "Drag to pan • Wheel: zoom"
        };
        TextRenderer.DrawText(g, navigation + " • Double-click a flight strip to track", Font, new Point(12, height - 26), Color.White, TextFormatFlags.PreserveGraphicsTranslateTransform | TextFormatFlags.PreserveGraphicsClipping);
        if (modelCatalogError.Length > 0 || models.Errors.Count > 0)
        {
            TextRenderer.DrawText(g, "Some aircraft models could not be loaded; symbols are used instead.", Font, new Point(12, height - 44), Color.Orange, TextFormatFlags.PreserveGraphicsTranslateTransform | TextFormatFlags.PreserveGraphicsClipping);
        }
        if (scenery.Errors.Count > 0)
        {
            TextRenderer.DrawText(g, "Some airport scenery could not be loaded.", Font, new Point(12, height - 60), Color.Orange, TextFormatFlags.PreserveGraphicsTranslateTransform | TextFormatFlags.PreserveGraphicsClipping);
        }
    }

    private void DrawMeshes(Graphics graphics, IEnumerable<ViewMesh> objects, bool night)
    {
        var projection = Projection;
        var ordered = objects.SelectMany(item => item.Mesh.Faces.Select(face => (Item: item, Face: face,
            Depth: face.Vertices.Average(index => projection.Depth(item.World[index].Position, item.World[index].Altitude)))))
            .OrderBy(item => (item.Face.Flags & ModelFace.FlatGroundLayer) != 0 && item.Item.Kind == "GND"
                ? item.Item.World[item.Face.Vertices[0]].Altitude : double.NegativeInfinity)
            .ThenByDescending(item => item.Depth);
        foreach (var (item, face, _) in ordered)
        {
            var mesh = item.Mesh;
            var world = item.World;
            var animation = item.Aircraft is { } aircraft ? AircraftAnimation.Current(aircraft) : null;
            if (animation is not null && !face.Visible(animation))
            {
                continue;
            }
            (Position Position, double Altitude) Vertex(int index)
            {
                var point = world[index];
                if (animation is { GearExtension: > 0 and < 1 } && (face.Flags & ModelFace.RetractableGear) != 0)
                {
                    var offset = mesh.GearOffset(face, index, item.Aircraft!, animation.GearExtension);
                    return (new(point.Position.X + offset.Position.X, point.Position.Y + offset.Position.Y), point.Altitude + offset.Altitude);
                }
                return point;
            }
            var isScenery = item.Kind != "AIRCRAFT";
            if (isScenery && (!settings.ObstructionLights && face.Part.Contains("OBSLIGHT", StringComparison.OrdinalIgnoreCase) ||
                !settings.TaxiwayLights && face.Part.Contains("taxiway", StringComparison.OrdinalIgnoreCase) ||
                !settings.RunwayLights && (face.Part.Contains("Lts", StringComparison.OrdinalIgnoreCase) || face.Part.StartsWith("ThLights", StringComparison.OrdinalIgnoreCase))))
            {
                continue;
            }
            PointF? light = null;
            ProjectedPoint[] polygon = [];
            if (face.Vertices.Length == 1)
            {
                var point = Vertex(face.Vertices[0]);
                light = Project(point.Position, point.Altitude);
                if (light is null)
                {
                    continue;
                }
            }
            else
            {
                var indices = face.Vertices.Length == 2 ? new[] { face.Vertices[0], face.Vertices[1], face.Vertices[1] } : face.Vertices;
                polygon = Projection.Polygon(indices.Select(Vertex).ToArray());
                if (polygon.Length < 3)
                {
                    continue;
                }
            }
            // Palette indices are preserved in the loader. These colours and directional
            // shading are modern reconstruction choices, not an emulated BAO palette.
            var color = face.ColorIndex switch
            {
                1 => Color.FromArgb(165, 175, 185),
                3 => Color.FromArgb(60, 75, 85),
                5 => Color.FromArgb(120, 135, 145),
                7 => Color.FromArgb(40, 45, 50),
                12 => Color.Red,
                13 => Color.LimeGreen,
                15 => Color.LightYellow,
                16 => Color.White,
                _ => Color.FromArgb(225, 230, 235)
            };
            if (isScenery)
            {
                color = face.ColorIndex switch
                {
                    < 0 => Color.FromArgb(face.ColorIndex),
                    1 => Color.Firebrick,
                    3 => Color.SlateGray,
                    7 => Color.FromArgb(35, 35, 35),
                    8 => Color.FromArgb(67, 89, 57),
                    14 => Color.DodgerBlue,
                    16 => Color.Red,
                    _ => color
                };
            }
            var first = mesh.Vertices[face.Vertices[0]];
            var shade = 1d;
            if (face.Vertices.Length >= 3)
            {
                var second = mesh.Vertices[face.Vertices[1]];
                var third = mesh.Vertices[face.Vertices[2]];
                var normalY = (second.Z - first.Z) * (third.X - first.X) - (second.X - first.X) * (third.Z - first.Z);
                shade = normalY < 0 ? 0.68 : 1;
            }
            if (night && isScenery && face.ColorIndex is not (>= 12 and <= 16))
            {
                shade *= 0.25;
            }
            using var brush = new SolidBrush(Color.FromArgb((int)(color.R * shade), (int)(color.G * shade), (int)(color.B * shade)));
            if (light is { } projected)
            {
                graphics.FillEllipse(brush, projected.X - 1, projected.Y - 1, 2, 2);
                continue;
            }
            var screenPoints = polygon.Select(point => new PointF((float)point.X, (float)point.Y)).ToArray();
            if (face.Vertices.Length == 2)
            {
                using var pen = new Pen(brush, 1);
                graphics.DrawLines(pen, screenPoints);
            }
            else
            {
                graphics.FillPolygon(brush, screenPoints);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            panorama?.Dispose();
        }
        base.Dispose(disposing);
    }
}
