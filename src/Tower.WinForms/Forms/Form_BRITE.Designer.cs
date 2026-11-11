namespace Tower.WinForms.Forms;

partial class Form_BRITE
{
    private System.ComponentModel.IContainer components = null!;
    private MenuStrip MenuStrip = null!;
    private Panel Panel_Radar = null!;
    private ToolStripMenuItem Menu_Display = null!;
    private ToolStripMenuItem Menu_Display_VideoMap = null!;
    private ToolStripMenuItem Menu_Display_Taxiways = null!;
    private ToolStripMenuItem Menu_Display_Obstructions = null!;
    private ToolStripMenuItem Menu_Display_RangeMarks = null!;
    private ToolStripSeparator Menu_Display_Separator01 = null!;
    private ToolStripMenuItem Menu_Display_DefaultCenter = null!;
    private ToolStripMenuItem Menu_Display_MouseCenter = null!;
    private ToolStripMenuItem Menu_Display_ZoomIn = null!;
    private ToolStripMenuItem Menu_Display_ZoomOut = null!;
    private ToolStripSeparator Menu_Display_Separator02 = null!;
    private ToolStripMenuItem Menu_Display_SetRange = null!;
    private ToolStripMenuItem Menu_Display_SetRangeMarks = null!;
    private ToolStripMenuItem Menu_ZoomIn = null!;
    private ToolStripMenuItem Menu_ZoomOut = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        MenuStrip = new MenuStrip();
        Panel_Radar = new Panel();
        Menu_Display = new ToolStripMenuItem();
        Menu_Display_VideoMap = new ToolStripMenuItem();
        Menu_Display_Taxiways = new ToolStripMenuItem();
        Menu_Display_Obstructions = new ToolStripMenuItem();
        Menu_Display_RangeMarks = new ToolStripMenuItem();
        Menu_Display_Separator01 = new ToolStripSeparator();
        Menu_Display_DefaultCenter = new ToolStripMenuItem();
        Menu_Display_MouseCenter = new ToolStripMenuItem();
        Menu_Display_ZoomIn = new ToolStripMenuItem();
        Menu_Display_ZoomOut = new ToolStripMenuItem();
        Menu_Display_Separator02 = new ToolStripSeparator();
        Menu_Display_SetRange = new ToolStripMenuItem();
        Menu_Display_SetRangeMarks = new ToolStripMenuItem();
        Menu_ZoomIn = new ToolStripMenuItem();
        Menu_ZoomOut = new ToolStripMenuItem();
        MenuStrip.SuspendLayout();
        SuspendLayout();

        MenuStrip.Items.AddRange(new ToolStripItem[] { Menu_Display, Menu_ZoomIn, Menu_ZoomOut });
        MenuStrip.Location = new Point(0, 0);
        MenuStrip.Name = "MenuStrip";
        MenuStrip.RenderMode = ToolStripRenderMode.System;
        MenuStrip.Size = new Size(605, 24);
        MenuStrip.TabIndex = 0;
        Menu_Display.DropDownItems.AddRange(new ToolStripItem[]
        {
            Menu_Display_VideoMap,
            Menu_Display_Taxiways,
            Menu_Display_Obstructions,
            Menu_Display_RangeMarks,
            Menu_Display_Separator01,
            Menu_Display_DefaultCenter,
            Menu_Display_MouseCenter,
            Menu_Display_ZoomIn,
            Menu_Display_ZoomOut,
            Menu_Display_Separator02,
            Menu_Display_SetRange,
            Menu_Display_SetRangeMarks
        });
        Menu_Display.Name = "Menu_Display";
        Menu_Display.Text = "&Display";
        Menu_Display.DropDownOpening += Menu_Display_DropDownOpening;

        Menu_Display_VideoMap.Name = "Menu_Display_VideoMap";
        Menu_Display_VideoMap.Text = "&Video Map";
        Menu_Display_VideoMap.Tag = 3525;
        Menu_Display_VideoMap.Click += Menu_Action_Click;

        Menu_Display_Taxiways.Name = "Menu_Display_Taxiways";
        Menu_Display_Taxiways.Text = "&Taxiways";
        Menu_Display_Taxiways.Tag = 3526;
        Menu_Display_Taxiways.Click += Menu_Action_Click;

        Menu_Display_Obstructions.Name = "Menu_Display_Obstructions";
        Menu_Display_Obstructions.Text = "O&bstructions";
        Menu_Display_Obstructions.Tag = 3527;
        Menu_Display_Obstructions.Click += Menu_Action_Click;

        Menu_Display_RangeMarks.Name = "Menu_Display_RangeMarks";
        Menu_Display_RangeMarks.Text = "&Range Marks";
        Menu_Display_RangeMarks.Tag = 3528;
        Menu_Display_RangeMarks.Click += Menu_Action_Click;

        Menu_Display_Separator01.Name = "Menu_Display_Separator01";

        Menu_Display_DefaultCenter.Name = "Menu_Display_DefaultCenter";
        Menu_Display_DefaultCenter.Text = "&Default Center";
        Menu_Display_DefaultCenter.Tag = 3529;
        Menu_Display_DefaultCenter.Click += Menu_Action_Click;

        Menu_Display_MouseCenter.Name = "Menu_Display_MouseCenter";
        Menu_Display_MouseCenter.Text = "Center Radar on &Mouseclick";
        Menu_Display_MouseCenter.Tag = 3530;
        Menu_Display_MouseCenter.Click += Menu_Action_Click;

        Menu_Display_ZoomIn.Name = "Menu_Display_ZoomIn";
        Menu_Display_ZoomIn.Text = "Zoom &In";
        Menu_Display_ZoomIn.Tag = 3531;
        Menu_Display_ZoomIn.ShortcutKeyDisplayString = "+";
        Menu_Display_ZoomIn.Click += Menu_Action_Click;

        Menu_Display_ZoomOut.Name = "Menu_Display_ZoomOut";
        Menu_Display_ZoomOut.Text = "Zoom &Out";
        Menu_Display_ZoomOut.Tag = 3532;
        Menu_Display_ZoomOut.ShortcutKeyDisplayString = "-";
        Menu_Display_ZoomOut.Click += Menu_Action_Click;

        Menu_Display_Separator02.Name = "Menu_Display_Separator02";

        Menu_Display_SetRange.Name = "Menu_Display_SetRange";
        Menu_Display_SetRange.Text = "Set R&ange...";
        Menu_Display_SetRange.Tag = 3533;
        Menu_Display_SetRange.Click += Menu_Action_Click;

        Menu_Display_SetRangeMarks.Name = "Menu_Display_SetRangeMarks";
        Menu_Display_SetRangeMarks.Text = "Set Ra&nge Marks...";
        Menu_Display_SetRangeMarks.Tag = 3534;
        Menu_Display_SetRangeMarks.Click += Menu_Action_Click;

        Menu_ZoomIn.Name = "Menu_ZoomIn";
        Menu_ZoomIn.Text = "&In!";
        Menu_ZoomIn.Tag = 3625;
        Menu_ZoomIn.Click += Menu_Action_Click;

        Menu_ZoomOut.Name = "Menu_ZoomOut";
        Menu_ZoomOut.Text = "&Out!";
        Menu_ZoomOut.Tag = 3626;
        Menu_ZoomOut.Click += Menu_Action_Click;

        Panel_Radar.BackColor = Color.Black;
        Panel_Radar.Dock = DockStyle.Fill;
        Panel_Radar.Location = new Point(0, 24);
        Panel_Radar.Name = "Panel_Radar";
        Panel_Radar.Size = new Size(605, 355);
        Panel_Radar.TabIndex = 1;

        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.Black;
        ClientSize = new Size(605, 379);
        ControlBox = false;
        Controls.Add(Panel_Radar);
        Controls.Add(MenuStrip);
        MainMenuStrip = MenuStrip;
        Name = "Form_BRITE";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "BRITE";
        MenuStrip.ResumeLayout(false);
        MenuStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
}
