using Tower.Core;

namespace Tower.Controls;

public sealed class CaptionPanel : Panel
{
    public CaptionPanel()
    {
        BorderStyle = BorderStyle.Fixed3D;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            ClientRectangle,
            ForeColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left
        );
    }
}

public sealed class FlightStripControl : Control
{
    private readonly AircraftStatusToolTip statusToolTip;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Aircraft? Aircraft
    {
        get; set;
    }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Selected
    {
        get; set;
    }

    public FlightStripControl()
    {
        DoubleBuffered = true;
        Size = new(289, 37);
        statusToolTip = new(this, _ => Aircraft);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (Selected)
        {
            BackColor = Color.White;
        }
        else
        {
            BackColor = Color.FromArgb(0, 192, 192);
        }

        var a = Aircraft;
        statusToolTip.Refresh();

        // Preserve ctlFlightStrip's two-row layout, colour and 72/126 pixel dividers.
        var sx = Width / 289f;
        var sy = Height / 37f;

        using var pen = new Pen(Color.Black, 1);

        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        e.Graphics.DrawLine(Pens.Black, 72 * sx, 0, 72 * sx, Height);
        e.Graphics.DrawLine(Pens.Black, 126 * sx, 0, 126 * sx, Height);
        e.Graphics.DrawLine(Pens.Black, 72 * sx, 18 * sy, 126 * sx, 18 * sy);

        if (a is null)
        {
            return;
        }

        void Text(string text, float x, float y, int width) => TextRenderer.DrawText(e.Graphics, text, Font,
            new Rectangle((int)(x * sx), (int)(y * sy), (int)(width * sx), (int)(17 * sy)), Color.Black,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding
        );

        Text(a.CallSign, 8, 1, 62);
        Text(a.Type, 23, 20, 47);
        Text($"RWY {a.Runway}", 76, 1, 48);
        Text(a.Gate, 76, 20, 48);
        Text($"{a.AltitudeFeet:0} ft  {a.SpeedKnots:0} kt", 135, 1, 150);
        var route = a.OriginalPlan is { } plan ? $"{plan.Origin} > {plan.Destination}"
            : a.GeneratedRoute is { } generated ? $"{generated.Origin} > {generated.Destination}" : "";
        Text(route, 135, 20, 150);
    }
}
