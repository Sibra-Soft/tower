namespace Tower.WinForms.Forms;

partial class Form_ASDE
{
    private System.ComponentModel.IContainer components = null!;
    private MenuStrip MenuStrip = null!;
    private Panel Panel_Radar = null!;
    private ToolStripMenuItem Menu_Display = null!;
    private ToolStripMenuItem Menu_Display_VideoMap = null!;
    private ToolStripMenuItem Menu_Display_Taxiways = null!;
    private ToolStripMenuItem Menu_Display_RunwayNames = null!;
    private ToolStripMenuItem Menu_Display_TaxiwayNames = null!;
    private ToolStripMenuItem Menu_Display_GateNames = null!;
    private ToolStripSeparator Menu_Display_Separator01 = null!;
    private ToolStripMenuItem Menu_Display_DefaultCenter = null!;
    private ToolStripMenuItem Menu_Display_MouseCenter = null!;
    private ToolStripMenuItem Menu_Display_ZoomIn = null!;
    private ToolStripMenuItem Menu_Display_ZoomOut = null!;
    private ToolStripSeparator Menu_Display_Separator02 = null!;
    private ToolStripMenuItem Menu_Display_SetRange = null!;
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
        Menu_Display_RunwayNames = new ToolStripMenuItem();
        Menu_Display_TaxiwayNames = new ToolStripMenuItem();
        Menu_Display_GateNames = new ToolStripMenuItem();
        Menu_Display_Separator01 = new ToolStripSeparator();
        Menu_Display_DefaultCenter = new ToolStripMenuItem();
        Menu_Display_MouseCenter = new ToolStripMenuItem();
        Menu_Display_ZoomIn = new ToolStripMenuItem();
        Menu_Display_ZoomOut = new ToolStripMenuItem();
        Menu_Display_Separator02 = new ToolStripSeparator();
        Menu_Display_SetRange = new ToolStripMenuItem();
        Menu_ZoomIn = new ToolStripMenuItem();
        Menu_ZoomOut = new ToolStripMenuItem();
        MenuStrip.SuspendLayout();
        SuspendLayout();

        MenuStrip.Items.AddRange(new ToolStripItem[] { Menu_Display, Menu_ZoomIn, Menu_ZoomOut });
        MenuStrip.Location = new Point(0, 0);
        MenuStrip.Name = "MenuStrip";
        MenuStrip.RenderMode = ToolStripRenderMode.System;
        MenuStrip.Size = new Size(543, 24);
        MenuStrip.TabIndex = 0;
        Menu_Display.DropDownItems.AddRange(new ToolStripItem[]
        {
            Menu_Display_VideoMap,
            Menu_Display_Taxiways,
            Menu_Display_RunwayNames,
            Menu_Display_TaxiwayNames,
            Menu_Display_GateNames,
            Menu_Display_Separator01,
            Menu_Display_DefaultCenter,
            Menu_Display_MouseCenter,
            Menu_Display_ZoomIn,
            Menu_Display_ZoomOut,
            Menu_Display_Separator02,
            Menu_Display_SetRange
        });
        Menu_Display.Name = "Menu_Display";
        Menu_Display.Text = "&Display";
        Menu_Display.DropDownOpening += Menu_Display_DropDownOpening;

        Menu_Display_VideoMap.Name = "Menu_Display_VideoMap";
        Menu_Display_VideoMap.Text = "&Video Map";
        Menu_Display_VideoMap.Tag = 3325;
        Menu_Display_VideoMap.Click += Menu_Action_Click;

        Menu_Display_Taxiways.Name = "Menu_Display_Taxiways";
        Menu_Display_Taxiways.Text = "&Taxiways";
        Menu_Display_Taxiways.Tag = 3326;
        Menu_Display_Taxiways.Click += Menu_Action_Click;

        Menu_Display_RunwayNames.Name = "Menu_Display_RunwayNames";
        Menu_Display_RunwayNames.Text = "&Runway Names";
        Menu_Display_RunwayNames.Tag = 3327;
        Menu_Display_RunwayNames.Click += Menu_Action_Click;

        Menu_Display_GateNames.Name = "Menu_Display_GateNames";
        Menu_Display_GateNames.Text = "&Gate Names";
        Menu_Display_GateNames.Tag = 3334;
        Menu_Display_GateNames.Click += Menu_Action_Click;
        Menu_Display_TaxiwayNames.Name = "Menu_Display_TaxiwayNames";
        Menu_Display_TaxiwayNames.Text = "Taxiway &Names";
        Menu_Display_TaxiwayNames.Tag = 3328;
        Menu_Display_TaxiwayNames.Click += Menu_Action_Click;

        Menu_Display_Separator01.Name = "Menu_Display_Separator01";

        Menu_Display_DefaultCenter.Name = "Menu_Display_DefaultCenter";
        Menu_Display_DefaultCenter.Text = "&Default Center";
        Menu_Display_DefaultCenter.Tag = 3329;
        Menu_Display_DefaultCenter.Click += Menu_Action_Click;

        Menu_Display_MouseCenter.Name = "Menu_Display_MouseCenter";
        Menu_Display_MouseCenter.Text = "Center Radar on &Mouseclick";
        Menu_Display_MouseCenter.Tag = 3330;
        Menu_Display_MouseCenter.Click += Menu_Action_Click;

        Menu_Display_ZoomIn.Name = "Menu_Display_ZoomIn";
        Menu_Display_ZoomIn.Text = "Zoom &In";
        Menu_Display_ZoomIn.Tag = 3331;
        Menu_Display_ZoomIn.ShortcutKeyDisplayString = "+";
        Menu_Display_ZoomIn.Click += Menu_Action_Click;

        Menu_Display_ZoomOut.Name = "Menu_Display_ZoomOut";
        Menu_Display_ZoomOut.Text = "Zoom &Out";
        Menu_Display_ZoomOut.Tag = 3332;
        Menu_Display_ZoomOut.ShortcutKeyDisplayString = "-";
        Menu_Display_ZoomOut.Click += Menu_Action_Click;

        Menu_Display_Separator02.Name = "Menu_Display_Separator02";

        Menu_Display_SetRange.Name = "Menu_Display_SetRange";
        Menu_Display_SetRange.Text = "Set R&ange...";
        Menu_Display_SetRange.Tag = 3333;
        Menu_Display_SetRange.Click += Menu_Action_Click;

        Menu_ZoomIn.Name = "Menu_ZoomIn";
        Menu_ZoomIn.Text = "&In!";
        Menu_ZoomIn.Tag = 3425;
        Menu_ZoomIn.Click += Menu_Action_Click;

        Menu_ZoomOut.Name = "Menu_ZoomOut";
        Menu_ZoomOut.Text = "&Out!";
        Menu_ZoomOut.Tag = 3426;
        Menu_ZoomOut.Click += Menu_Action_Click;

        Panel_Radar.BackColor = Color.Black;
        Panel_Radar.Dock = DockStyle.Fill;
        Panel_Radar.Location = new Point(0, 24);
        Panel_Radar.Name = "Panel_Radar";
        Panel_Radar.Size = new Size(543, 391);
        Panel_Radar.TabIndex = 1;

        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.Black;
        ClientSize = new Size(543, 415);
        ControlBox = false;
        Controls.Add(Panel_Radar);
        Controls.Add(MenuStrip);
        MainMenuStrip = MenuStrip;
        Name = "Form_ASDE";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "ASDE";
        MenuStrip.ResumeLayout(false);
        MenuStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
}
