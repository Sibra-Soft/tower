// Generated from Forms/Form_FlightStrips.frm. Run tools/port_layouts.py to regenerate.
using Tower.Controls;

namespace Tower.WinForms.Forms;
public partial class Form_FlightStrips : Form
{
    public FlowLayoutPanel FlowLayoutPanel_StripList = null!;
    private FlightStripControl FlightStrip = null!;

    private void InitializeComponent()
    {
        FlightStrip = new FlightStripControl();
        FlowLayoutPanel_StripList = new FlowLayoutPanel();
        FlowLayoutPanel_StripList.SuspendLayout();
        SuspendLayout();
        // 
        // FlightStrip
        // 
        FlightStrip.BackColor = Color.FromArgb(0, 192, 192);
        FlightStrip.Location = new Point(3, 3);
        FlightStrip.Name = "FlightStrip";
        FlightStrip.Size = new Size(289, 37);
        FlightStrip.TabIndex = 0;
        FlightStrip.Visible = false;
        // 
        // FlowLayoutPanel_StripList
        // 
        FlowLayoutPanel_StripList.AutoScroll = true;
        FlowLayoutPanel_StripList.Controls.Add(FlightStrip);
        FlowLayoutPanel_StripList.Dock = DockStyle.Fill;
        FlowLayoutPanel_StripList.FlowDirection = FlowDirection.TopDown;
        FlowLayoutPanel_StripList.Location = new Point(0, 0);
        FlowLayoutPanel_StripList.Margin = new Padding(0);
        FlowLayoutPanel_StripList.Name = "FlowLayoutPanel_StripList";
        FlowLayoutPanel_StripList.Size = new Size(352, 222);
        FlowLayoutPanel_StripList.TabIndex = 2;
        FlowLayoutPanel_StripList.WrapContents = false;
        // 
        // Form_FlightStrips
        // 
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.Silver;
        ClientSize = new Size(352, 222);
        ControlBox = false;
        Controls.Add(FlowLayoutPanel_StripList);
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "Form_FlightStrips";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "-";
        FlowLayoutPanel_StripList.ResumeLayout(false);
        ResumeLayout(false);
    }
}
