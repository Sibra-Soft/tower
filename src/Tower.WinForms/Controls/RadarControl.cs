using Tower.Core;
using Tower.Infrastructure;

namespace Tower.Controls;

public sealed class RadarControl : Control
{
    private readonly Simulation simulation;
    private readonly DisplaySettings settings;
    private readonly bool air;
    private readonly AircraftStatusToolTip statusToolTip;
    private readonly Dictionary<Aircraft, RectangleF> labels = [];
    private Aircraft? dragging;
    private Point dragStart;
    private bool panning;
    private Position panOffset;
    private Position initialPanOffset;
    private bool centerOnNextClick;
    private string selectedCallsign = "";
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool CenterOnNextClick => centerOnNextClick;
    public event Action? ViewChanged;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string SelectedCallsign
    {
        get => selectedCallsign;
        set
        {
            if (selectedCallsign == value) return;
            selectedCallsign = value;
            Invalidate();
        }
    }
    public event Action<Aircraft>? Selected;
    public event Action<Aircraft>? CommandTargetRequested;
    //public event Action<string>? Command;
    public event Action<Aircraft>? TrackRequested;
    public RadarControl(Simulation simulation, DisplaySettings settings, bool air)
    {
        this.simulation = simulation;
        this.settings = settings;
        this.air = air;
        Dock = DockStyle.Fill;
        DoubleBuffered = true;
        BackColor = Color.Black;
        ForeColor = Color.Lime;
        Font = new Font("Arial", 8);
        Cursor = Cursors.Cross;
        MouseDoubleClick += (_, e) => { var a = labels.FirstOrDefault(kv => kv.Value.Contains(e.Location) || Distance(Screen(kv.Key.Position), e.Location) < 10).Key; if (a is not null) { TrackRequested?.Invoke(a); } };
        MouseDown += Down;
        MouseMove += MoveLabel;
        MouseUp += (_, e) => { if (e.Button == MouseButtons.Left) { dragging = null; panning = false; Capture = false; Cursor = Cursors.Cross; } };
        MouseCaptureChanged += (_, _) => { if (!Capture) { dragging = null; panning = false; Cursor = Cursors.Cross; } };
        MouseWheel += (_, e) => ChangeZoom(e.Delta > 0);
        statusToolTip = new(this, point => labels.Reverse().FirstOrDefault(pair =>
            pair.Value.Contains(point) || Distance(Screen(pair.Key.Position), point) < 10).Key);
    }
    public void ZoomIn() => ChangeZoom(true);
    public void ZoomOut() => ChangeZoom(false);

    private void ChangeZoom(bool zoomIn)
    {
        var factor = zoomIn ? 0.8 : 1.25;
        if (air)
        {
            settings.BriteRange = Math.Clamp(settings.BriteRange * factor, 2, 50);
        }
        else
        {
            settings.AsdeRange = Math.Clamp(settings.AsdeRange * factor, 0.1, 10);
        }
        Invalidate();
        ViewChanged?.Invoke();
    }

    public bool HandleZoomKey(Keys keyData)
    {
        if ((keyData & (Keys.Control | Keys.Alt)) != Keys.None)
        {
            return false;
        }
        switch (keyData & Keys.KeyCode)
        {
            case Keys.Add:
            case Keys.Oemplus:
                ZoomIn();
                return true;
            case Keys.Subtract:
            case Keys.OemMinus:
                ZoomOut();
                return true;
            case Keys.Escape when centerOnNextClick:
                SetCenterOnNextClick(false);
                return true;
            default:
                return false;
        }
    }

    public void ResetCenter()
    {
        panOffset = default;
        SetCenterOnNextClick(false);
        Invalidate();
    }

    public void SetCenterOnNextClick(bool enabled)
    {
        dragging = null;
        panning = false;
        Capture = false;
        centerOnNextClick = enabled;
        Cursor = enabled ? Cursors.SizeAll : Cursors.Cross;
        if (enabled)
        {
            Focus();
        }
        ViewChanged?.Invoke();
    }
    private double WorldScale => Math.Max(1, Math.Min(Width, Height) / 2 - 16) / ((air ? settings.BriteRange : settings.AsdeRange) * 1852);
    private Position Center => simulation.State.Airport.Point(simulation.State.Airport.GetRunway(simulation.State.Options.Runway).Threshold).Towards(
        simulation.State.Airport.Point(simulation.State.Airport.GetRunway(simulation.State.Options.Runway).End), 1500);
    private PointF Screen(Position p) => new((float)(Width / 2 + (p.X - Center.X - panOffset.X) * WorldScale), (float)(Height / 2 - (p.Y - Center.Y - panOffset.Y) * WorldScale));
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        var airport = simulation.State.Airport;
        labels.Clear();
        using var mapPen = new Pen(Color.FromArgb(0, 95, 0), 1);
        if (air && settings.ShowRangeMarks)
        {
            for (var nm = settings.RangeMarks; nm <= settings.BriteRange && nm > 0; nm += settings.RangeMarks)
            {
                var r = (float)(nm * 1852 * WorldScale);
                g.DrawEllipse(mapPen, Width / 2 - r, Height / 2 - r, r * 2, r * 2);
            }
        }
        var showVideoMap = air ? settings.BriteMap : settings.AsdeMap;
        var showTaxiways = air ? settings.BriteTaxiways : settings.AsdeTaxiways;
        if (air && showVideoMap)
        {
            DrawVideoMap(g, airport.RadarMap);
        }
        if (air && settings.BriteObstructions)
        {
            DrawSectorSymbols(g, airport);
        }
        foreach (var segment in airport.Segments)
        {
            var runway = segment.Kind.Equals("RUNWAY", StringComparison.OrdinalIgnoreCase);
            if (runway ? !showVideoMap : !showTaxiways || segment.Kind.Equals("VROUTE", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            using var pen = new Pen(runway ? Color.Green : Color.FromArgb(0, 75, 0), runway ? 3 : 1);
            g.DrawLine(pen, Screen(airport.Point(segment.From)), Screen(airport.Point(segment.To)));
        }
        foreach (var node in airport.Nodes.Values)
        {
            var point = Screen(node.Position);
            if (!air && settings.AsdeGateNames && node.Kind.Equals("GATE", StringComparison.OrdinalIgnoreCase))
            {
                var name = string.IsNullOrWhiteSpace(node.Label) ? node.Id : node.Label;
                TextRenderer.DrawText(g, name, Font, Point.Round(point), Color.Green);
            }
            if (!air && settings.RunwayNames && node.Kind.Equals("RUNWAY", StringComparison.OrdinalIgnoreCase))
            {
                TextRenderer.DrawText(g, node.Label, Font, Point.Round(point), Color.Cyan);
            }
            if (node.Kind.Equals("OBSTRUCTION", StringComparison.OrdinalIgnoreCase) && (air ? settings.BriteObstructions : showVideoMap))
            {
                g.DrawRectangle(Pens.Red, point.X - 3, point.Y - 3, 6, 6);
            }
        }
        if (!air && settings.TaxiNames)
        {
            foreach (var route in airport.Routes)
            {
                if (route.Value.Count == 0)
                {
                    continue;
                }
                var point = Screen(airport.Point(route.Value[route.Value.Count / 2]));
                TextRenderer.DrawText(g, route.Key, Font, Point.Round(point), Color.Green);
            }
            if (airport.Routes.Count == 0)
            {
                foreach (var node in airport.Nodes.Values.Where(n => n.Kind.Equals("TAXIWAY", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(n.Label)))
                {
                    TextRenderer.DrawText(g, node.Label, Font, Point.Round(Screen(node.Position)), Color.Green);
                }
            }
        }
        foreach (var a in simulation.State.Aircraft.Where(a => a.IsOnFrequency && a.Phase != FlightPhase.Completed && (a.AltitudeFeet > 200) == air))
        {
            var p = Screen(a.Position);
            if (p.X < 0 || p.X > Width || p.Y < 0 || p.Y > Height)
            {
                continue;
            }

            var selected = a.CallSign == SelectedCallsign;
            var color = selected ? Color.White : Color.Lime;
            var altitudeMsl = a.AltitudeFeet + (airport.Original.FieldElevationFeetMsl ?? 0);
            var values = FormattableString.Invariant($"{(int)(altitudeMsl / 100)}   {(int)((a.SpeedKnots + 5) / 10)}");
            if (air)
            {
                DrawAircraftLabel(g, a, p, values, a.BriteLabelBearing, color);
                continue;
            }
            using var pen = new Pen(color, selected ? 2 : 1);
            if (!air && airport.Outlines.TryGetValue(a.Type, out var outline) && outline.Count >= 3)
            {
                var minX = outline.Min(v => v.X);
                var maxX = outline.Max(v => v.X);
                var midX = (minX + maxX) / 2;
                var midY = (outline.Min(v => v.Y) + outline.Max(v => v.Y)) / 2;
                var scale = Math.Max(0.10, a.Specification.WingSpanMetres * WorldScale / Math.Max(1, maxX - minX));
                var radians = a.Heading * Math.PI / 180;
                var points = outline.Select(v => { var x = (v.X - midX) * scale; var y = (v.Y - midY) * scale; return new PointF((float)(p.X + x * Math.Cos(radians) - y * Math.Sin(radians)), (float)(p.Y + x * Math.Sin(radians) + y * Math.Cos(radians))); }).ToArray();
                g.DrawPolygon(pen, points);
            }
            else
            {
                g.DrawLine(pen, p.X - 4, p.Y, p.X + 4, p.Y);
            }

            g.DrawLine(pen, p.X, p.Y - 4, p.X, p.Y + 4);
            var vector = Screen(a.Position.Offset(a.GroundTrackDegrees ?? a.Heading,
                (a.GroundTrackDegrees is null ? a.SpeedKnots : a.GroundSpeedKnots) * Simulation.MetresPerKnotSecond * (air ? 30 : 5)));
            g.DrawLine(pen, p, vector);
            DrawAircraftLabel(g, a, p, values, a.AsdeLabelBearing, color);
        }
        TextRenderer.DrawText(g, $"{(air ? "BRITE" : "ASDE")}  {(air ? settings.BriteRange : settings.AsdeRange):0.00} NM", Font, new Point(5, 5), Color.Green);
        statusToolTip.Refresh();
    }

    private void DrawVideoMap(Graphics graphics, RadarMap map)
    {
        foreach (var line in map.Lines)
        {
            using var pen = MapPen(line.Color, line.Width);
            if (line.DashMetres.Length > 0)
            {
                // GDI+ dash lengths are multiples of pen width; source lengths are nautical miles.
                pen.DashPattern = line.DashMetres.Select(length => (float)Math.Max(0.1, length * WorldScale / pen.Width)).ToArray();
            }
            graphics.DrawLines(pen, line.Points.Select(Screen).ToArray());
        }
        foreach (var arc in map.Arcs)
        {
            using var pen = MapPen(arc.Color, arc.Width);
            var sweep = arc.EndDegrees - arc.StartDegrees;
            if (sweep <= 0)
            {
                sweep += 360;
            }
            for (var offset = 0.0; offset < sweep; offset += arc.DegreesOn + arc.DegreesOff)
            {
                var length = Math.Min(arc.DegreesOn, sweep - offset);
                var steps = Math.Max(1, (int)Math.Ceiling(length));
                var points = new PointF[steps + 1];
                for (var step = 0; step <= steps; step++)
                {
                    points[step] = Screen(arc.Center.Offset(arc.StartDegrees + offset + length * step / steps, arc.RadiusMetres));
                }
                graphics.DrawLines(pen, points);
            }
        }
    }

    private static Pen MapPen(string name, double width)
    {
        var color = Color.FromName(name);
        return new Pen(color.IsKnownColor && color.A > 0 ? color : Color.Green, (float)width);
    }

    private void DrawSectorSymbols(Graphics graphics, Airport airport)
    {
        using var pen = new Pen(Color.Green);
        foreach (var fix in airport.Original.Fixes.Values)
        {
            if (fix.SourceKind is not ("p" or "o"))
            {
                continue;
            }
            var point = Screen(fix.ProjectFrom(airport.Latitude, airport.Longitude, airport.Observer));
            if (fix.SourceKind == "p")
            {
                graphics.DrawLine(pen, point.X - 3, point.Y, point.X + 3, point.Y);
                graphics.DrawLine(pen, point.X, point.Y - 3, point.X, point.Y + 3);
            }
            else
            {
                graphics.DrawPolygon(pen, new PointF[]
                {
                    new(point.X, point.Y - 3), new(point.X - 3, point.Y + 3), new(point.X + 3, point.Y + 3)
                });
            }
        }
    }

    private void DrawAircraftLabel(Graphics graphics, Aircraft aircraft, PointF position, string values, double bearing, Color color)
    {
        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        var width = Math.Max(TextRenderer.MeasureText(graphics, aircraft.CallSign, Font, Size.Empty, flags).Width,
            TextRenderer.MeasureText(graphics, values, Font, Size.Empty, flags).Width);
        var lineHeight = air ? Math.Max(1, Font.Height - (int)Math.Round(2 * DeviceDpi / 96.0)) : Font.Height;
        var height = lineHeight * 2;
        var radians = SnapLabelBearing(bearing) * Math.PI / 180;
        var direction = new PointF((float)Math.Sin(radians), (float)-Math.Cos(radians));
        var scale = DeviceDpi / 96f;
        var end = new PointF(position.X + direction.X * 20 * scale, position.Y + direction.Y * 20 * scale);
        // Intersect the ray with an upright text rectangle, so its nearest edge
        // follows the leader continuously through all four quadrants.
        var edge = Math.Min(width / (2 * Math.Max(0.000001, Math.Abs(direction.X))),
            height / (2 * Math.Max(0.000001, Math.Abs(direction.Y))));
        var labelDistance = (float)edge + 4 * scale;
        var rectangle = new Rectangle(Point.Round(new PointF(
            end.X + direction.X * labelDistance - width / 2f,
            end.Y + direction.Y * labelDistance - height / 2f)), new Size(width, height));
        labels[aircraft] = rectangle;

        var start = new PointF(position.X + direction.X * 7 * scale, position.Y + direction.Y * 7 * scale);
        using var leader = new Pen(color);
        graphics.DrawLine(leader, start, end);
        if (air)
        {
            TextRenderer.DrawText(graphics, "T", Font,
                new Point((int)Math.Round(position.X - 3 * scale), (int)Math.Round(position.Y - 6 * scale)), color, flags);
        }
        TextRenderer.DrawText(graphics, aircraft.CallSign, Font,
            new Rectangle(rectangle.Location, new Size(width, lineHeight)), color, flags);
        TextRenderer.DrawText(graphics, values, Font,
            new Rectangle(rectangle.X, rectangle.Y + lineHeight, width, lineHeight), color, flags);
    }

    private void Down(object? sender, MouseEventArgs e)
    {
        if (e.Button is not (MouseButtons.Left or MouseButtons.Right)) return;
        Focus();
        if (centerOnNextClick && e.Button == MouseButtons.Left)
        {
            panOffset = new(panOffset.X + (e.X - Width / 2.0) / WorldScale,
                panOffset.Y - (e.Y - Height / 2.0) / WorldScale);
            SetCenterOnNextClick(false);
            Invalidate();
            return;
        }
        if (centerOnNextClick && e.Button == MouseButtons.Right)
        {
            SetCenterOnNextClick(false);
        }
        var a = labels.Reverse().FirstOrDefault(kv => kv.Value.Contains(e.Location) || Distance(Screen(kv.Key.Position), e.Location) < 10).Key;
        if (a is null)
        {
            if (!air && e.Button == MouseButtons.Left)
            {
                panning = true;
                dragStart = e.Location;
                initialPanOffset = panOffset;
                Capture = true;
                Cursor = Cursors.Hand;
            }
            return;
        }
        SelectedCallsign = a.CallSign;
        Selected?.Invoke(a);
        Invalidate();
        if (e.Button == MouseButtons.Left)
        {
            dragging = a;
            dragStart = e.Location;
            Capture = true;
        }
        if (e.Button == MouseButtons.Right)
        {
            CommandTargetRequested?.Invoke(a);
        }
    }
    public static IEnumerable<string> CommandsFor(Aircraft a) => a.Phase switch
    {
        FlightPhase.Approach or FlightPhase.Circuit => ["cleared to land", "touch and go", "low approach", "go around", "speed 140", "say position"],
        FlightPhase.AtGate when a.Pushback is { Completed: false } => ["pushback", "pushback north", "pushback east", "pushback south", "pushback west"],
        FlightPhase.AtGate or FlightPhase.ReadyForTaxi => [$"taxi to runway {a.Runway}"],
        FlightPhase.ArrivalHold => ["taxi to parking"],
        FlightPhase.TaxiIn or FlightPhase.TaxiOut or FlightPhase.Pushback => [a.Holding ? "continue taxi" : "hold position"],
        FlightPhase.HoldingShort => ["line up and wait", "cleared for takeoff"],
        FlightPhase.LineUp => [a.Holding ? "continue taxi" : "hold position", "cleared for takeoff"],
        FlightPhase.LinedUp => ["cleared for takeoff"],
        FlightPhase.TakeoffRoll => ["cancel takeoff"],
        FlightPhase.AbortedTakeoff => ["cleared for takeoff"],
        FlightPhase.Departing or FlightPhase.Breakout => ["contact departure"],
        FlightPhase.Vectoring => ["go around", "say heading"],
        _ => []
    };
    private void MoveLabel(object? sender, MouseEventArgs e)
    {
        if (panning)
        {
            panOffset = new(initialPanOffset.X - (e.X - dragStart.X) / WorldScale, initialPanOffset.Y + (e.Y - dragStart.Y) / WorldScale);
            Invalidate();
            return;
        }
        if (dragging is null)
        {
            return;
        }

        var target = Screen(dragging.Position);
        var dx = e.X - target.X;
        var dy = e.Y - target.Y;
        if (dx * dx + dy * dy >= 16)
        {
            var bearing = SnapLabelBearing(Math.Atan2(dx, -dy) * 180 / Math.PI);
            if (air)
            {
                dragging.BriteLabelBearing = bearing;
            }
            else
            {
                dragging.AsdeLabelBearing = bearing;
            }
            Invalidate();
        }
    }
    private static double SnapLabelBearing(double bearing) => Coordinates.NormalizeHeading(
        Math.Floor((Coordinates.NormalizeHeading(bearing) + 22.5) / 45) * 45);

    private static double Distance(PointF a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
