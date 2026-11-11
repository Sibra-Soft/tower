using Tower.Controls;

namespace Tower.WinForms.Forms;

public partial class Form_SimulationWindow : Form
{
    private ToolStripMenuItem Menu_File = null!;
    private ToolStripMenuItem Menu_File_Save = null!;
    private ToolStripMenuItem Menu_File_SaveAs = null!;
    private ToolStripMenuItem Menu_File_Performance = null!;
    private ToolStripSeparator Menu_File_Space01 = null!;
    private ToolStripMenuItem Menu_File_End = null!;
    private ToolStripMenuItem Menu_Display = null!;
    private ToolStripMenuItem Menu_Display_ASDE = null!;
    private ToolStripMenuItem Menu_Display_BRITE = null!;
    private ToolStripMenuItem Menu_Display_FlightStrips = null!;
    private ToolStripMenuItem Menu_Display_Lights = null!;
    private ToolStripSeparator Menu_Display_Space01 = null!;
    private ToolStripMenuItem Menu_Display_ButtonBar = null!;
    private ToolStripMenuItem Menu_Display_Compass = null!;
    private ToolStripMenuItem Menu_Display_Gauges = null!;
    private ToolStripMenuItem Menu_Display_AircraftLabels = null!;

    private void InitializeComponent()
    {
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form_SimulationWindow));
        MenuStrip = new MenuStrip();
        Menu_File = new ToolStripMenuItem();
        Menu_File_Save = new ToolStripMenuItem();
        Menu_File_SaveAs = new ToolStripMenuItem();
        Menu_File_Performance = new ToolStripMenuItem();
        Menu_File_Space01 = new ToolStripSeparator();
        Menu_File_End = new ToolStripMenuItem();
        Menu_Display = new ToolStripMenuItem();
        Menu_Display_ASDE = new ToolStripMenuItem();
        Menu_Display_BRITE = new ToolStripMenuItem();
        Menu_Display_FlightStrips = new ToolStripMenuItem();
        Menu_Display_Lights = new ToolStripMenuItem();
        Menu_Display_Space01 = new ToolStripSeparator();
        Menu_Display_ButtonBar = new ToolStripMenuItem();
        Menu_Display_Compass = new ToolStripMenuItem();
        Menu_Display_Gauges = new ToolStripMenuItem();
        Menu_Display_AircraftLabels = new ToolStripMenuItem();
        Menu_3DView = new ToolStripMenuItem();
        Menu_3DView_Normal = new ToolStripMenuItem();
        Menu_3DView_Tracking = new ToolStripMenuItem();
        Menu_3DView_Binocular = new ToolStripMenuItem();
        Menu_3DView_SuperBinocular = new ToolStripMenuItem();
        Menu_3DView_SpotPlane = new ToolStripMenuItem();
        Menu_3DView_Pilot = new ToolStripMenuItem();
        Menu_3DView_Separator1 = new ToolStripSeparator();
        Menu_3DView_PanLeft = new ToolStripMenuItem();
        Menu_3DView_PanRight = new ToolStripMenuItem();
        Menu_3DView_Separator2 = new ToolStripSeparator();
        Menu_3DView_ZoomIn = new ToolStripMenuItem();
        Menu_3DView_ZoomOut = new ToolStripMenuItem();
        Menu_Simulation = new ToolStripMenuItem();
        Menu_Simulation_Sound = new ToolStripMenuItem();
        Menu_Simulation_DeleteAircraft = new ToolStripMenuItem();
        Menu_Simulation_Separator1 = new ToolStripSeparator();
        Menu_Simulation_SpeedUp = new ToolStripMenuItem();
        Menu_Simulation_SlowDown = new ToolStripMenuItem();
        Menu_Simulation_NormalSpeed = new ToolStripMenuItem();
        Menu_Simulation_Pause = new ToolStripMenuItem();
        Menu_Options = new ToolStripMenuItem();
        Menu_Options_SaveSettings = new ToolStripMenuItem();
        Menu_Options_RestoreSettings = new ToolStripMenuItem();
        Menu_Help = new ToolStripMenuItem();
        Menu_Help_Contents = new ToolStripMenuItem();
        Menu_Help_Commands = new ToolStripMenuItem();
        Menu_Help_Separator1 = new ToolStripSeparator();
        Menu_Help_About = new ToolStripMenuItem();
        StatusStrip = new StatusStrip();
        StatusLabel_Status = new ToolStripStatusLabel();
        MenuStrip.SuspendLayout();
        StatusStrip.SuspendLayout();
        SuspendLayout();
        // 
        // MenuStrip
        // 
        MenuStrip.Items.AddRange(new ToolStripItem[] { Menu_File, Menu_Display, Menu_3DView, Menu_Simulation, Menu_Options, Menu_Help });
        MenuStrip.Location = new Point(0, 0);
        MenuStrip.Name = "MenuStrip";
        MenuStrip.RenderMode = ToolStripRenderMode.System;
        MenuStrip.Size = new Size(574, 24);
        MenuStrip.TabIndex = 0;
        // 
        // Menu_File
        // 
        Menu_File.DropDownItems.AddRange(new ToolStripItem[] { Menu_File_Save, Menu_File_SaveAs, Menu_File_Performance, Menu_File_Space01, Menu_File_End });
        Menu_File.Name = "Menu_File";
        Menu_File.Size = new Size(37, 20);
        Menu_File.Text = "&File";
        // 
        // Menu_File_Save
        // 
        Menu_File_Save.Name = "Menu_File_Save";
        Menu_File_Save.ShortcutKeys = Keys.Control | Keys.S;
        Menu_File_Save.Size = new Size(243, 22);
        Menu_File_Save.Text = "&Save Scenario";
        Menu_File_Save.Click += Menu_File_Save_Click;
        // 
        // Menu_File_SaveAs
        // 
        Menu_File_SaveAs.Name = "Menu_File_SaveAs";
        Menu_File_SaveAs.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
        Menu_File_SaveAs.Size = new Size(243, 22);
        Menu_File_SaveAs.Text = "Save Scenario &As...";
        Menu_File_SaveAs.Click += Menu_File_SaveAs_Click;
        // 
        // Menu_File_Performance
        // 
        Menu_File_Performance.Name = "Menu_File_Performance";
        Menu_File_Performance.Size = new Size(243, 22);
        Menu_File_Performance.Text = "&Performance Review...";
        Menu_File_Performance.Click += Menu_File_Performance_Click;
        // 
        // Menu_File_Space01
        // 
        Menu_File_Space01.Name = "Menu_File_Space01";
        Menu_File_Space01.Size = new Size(240, 6);
        // 
        // Menu_File_End
        // 
        Menu_File_End.Name = "Menu_File_End";
        Menu_File_End.ShortcutKeys = Keys.Control | Keys.E;
        Menu_File_End.Size = new Size(243, 22);
        Menu_File_End.Text = "&End Scenario";
        Menu_File_End.Click += Menu_File_End_Click;
        // 
        // Menu_Display
        // 
        Menu_Display.DropDownItems.AddRange(new ToolStripItem[] { Menu_Display_ASDE, Menu_Display_BRITE, Menu_Display_FlightStrips, Menu_Display_Lights, Menu_Display_Space01, Menu_Display_ButtonBar, Menu_Display_Compass, Menu_Display_Gauges, Menu_Display_AircraftLabels });
        Menu_Display.Name = "Menu_Display";
        Menu_Display.Size = new Size(57, 20);
        Menu_Display.Text = "&Display";
        // 
        // Menu_Display_ASDE
        // 
        Menu_Display_ASDE.Name = "Menu_Display_ASDE";
        Menu_Display_ASDE.ShortcutKeys = Keys.Control | Keys.A;
        Menu_Display_ASDE.Size = new Size(176, 22);
        Menu_Display_ASDE.Text = "&ASDE";
        Menu_Display_ASDE.Click += Menu_Display_ASDE_Click;
        // 
        // Menu_Display_BRITE
        // 
        Menu_Display_BRITE.Name = "Menu_Display_BRITE";
        Menu_Display_BRITE.ShortcutKeys = Keys.Control | Keys.B;
        Menu_Display_BRITE.Size = new Size(176, 22);
        Menu_Display_BRITE.Text = "&BRITE";
        Menu_Display_BRITE.Click += Menu_Display_BRITE_Click;
        // 
        // Menu_Display_FlightStrips
        // 
        Menu_Display_FlightStrips.Name = "Menu_Display_FlightStrips";
        Menu_Display_FlightStrips.ShortcutKeys = Keys.Control | Keys.F;
        Menu_Display_FlightStrips.Size = new Size(176, 22);
        Menu_Display_FlightStrips.Text = "&Flight Strips";
        Menu_Display_FlightStrips.Click += Menu_Display_FlightStrips_Click;
        // 
        // Menu_Display_Lights
        // 
        Menu_Display_Lights.Name = "Menu_Display_Lights";
        Menu_Display_Lights.Size = new Size(176, 22);
        Menu_Display_Lights.Text = "&Lights...";
        Menu_Display_Lights.Click += Menu_Display_Lights_Click;
        // 
        // Menu_Display_Space01
        // 
        Menu_Display_Space01.Name = "Menu_Display_Space01";
        Menu_Display_Space01.Size = new Size(173, 6);
        // 
        // Menu_Display_ButtonBar
        // 
        Menu_Display_ButtonBar.Checked = true;
        Menu_Display_ButtonBar.CheckState = CheckState.Checked;
        Menu_Display_ButtonBar.Name = "Menu_Display_ButtonBar";
        Menu_Display_ButtonBar.Size = new Size(176, 22);
        Menu_Display_ButtonBar.Text = "&Button Bar";
        Menu_Display_ButtonBar.Click += Menu_Display_ButtonBar_Click;
        // 
        // Menu_Display_Compass
        // 
        Menu_Display_Compass.Checked = true;
        Menu_Display_Compass.CheckState = CheckState.Checked;
        Menu_Display_Compass.Name = "Menu_Display_Compass";
        Menu_Display_Compass.Size = new Size(176, 22);
        Menu_Display_Compass.Text = "&Compass";
        Menu_Display_Compass.Click += Menu_Display_Compass_Click;
        // 
        // Menu_Display_Gauges
        // 
        Menu_Display_Gauges.Checked = true;
        Menu_Display_Gauges.CheckState = CheckState.Checked;
        Menu_Display_Gauges.Name = "Menu_Display_Gauges";
        Menu_Display_Gauges.Size = new Size(176, 22);
        Menu_Display_Gauges.Text = "&Gauges";
        Menu_Display_Gauges.Click += Menu_Display_Gauges_Click;
        // 
        // Menu_Display_AircraftLabels
        // 
        Menu_Display_AircraftLabels.Name = "Menu_Display_AircraftLabels";
        Menu_Display_AircraftLabels.Size = new Size(176, 22);
        Menu_Display_AircraftLabels.Text = "Aircraft &Labels";
        Menu_Display_AircraftLabels.Click += Menu_Display_AircraftLabels_Click;
        // 
        // Menu_3DView
        // 
        Menu_3DView.DropDownItems.AddRange(new ToolStripItem[] { Menu_3DView_Normal, Menu_3DView_Tracking, Menu_3DView_Binocular, Menu_3DView_SuperBinocular, Menu_3DView_SpotPlane, Menu_3DView_Pilot, Menu_3DView_Separator1, Menu_3DView_PanLeft, Menu_3DView_PanRight, Menu_3DView_Separator2, Menu_3DView_ZoomIn, Menu_3DView_ZoomOut });
        Menu_3DView.Name = "Menu_3DView";
        Menu_3DView.Size = new Size(61, 20);
        Menu_3DView.Text = "3D &View";
        // 
        // Menu_3DView_Normal
        // 
        Menu_3DView_Normal.Checked = true;
        Menu_3DView_Normal.CheckState = CheckState.Checked;
        Menu_3DView_Normal.Name = "Menu_3DView_Normal";
        Menu_3DView_Normal.Size = new Size(157, 22);
        Menu_3DView_Normal.Text = "&Normal";
        Menu_3DView_Normal.Click += Menu_3DView_Normal_Click;
        // 
        // Menu_3DView_Tracking
        // 
        Menu_3DView_Tracking.Name = "Menu_3DView_Tracking";
        Menu_3DView_Tracking.Size = new Size(157, 22);
        Menu_3DView_Tracking.Text = "&Tracking";
        Menu_3DView_Tracking.Click += Menu_3DView_Tracking_Click;
        // 
        // Menu_3DView_Binocular
        // 
        Menu_3DView_Binocular.Name = "Menu_3DView_Binocular";
        Menu_3DView_Binocular.Size = new Size(157, 22);
        Menu_3DView_Binocular.Text = "&Binocular";
        Menu_3DView_Binocular.Click += Menu_3DView_Binocular_Click;
        // 
        // Menu_3DView_SuperBinocular
        // 
        Menu_3DView_SuperBinocular.Name = "Menu_3DView_SuperBinocular";
        Menu_3DView_SuperBinocular.Size = new Size(157, 22);
        Menu_3DView_SuperBinocular.Text = "&Super Binocular";
        Menu_3DView_SuperBinocular.Click += Menu_3DView_SuperBinocular_Click;
        // 
        // Menu_3DView_SpotPlane
        // 
        Menu_3DView_SpotPlane.Name = "Menu_3DView_SpotPlane";
        Menu_3DView_SpotPlane.Size = new Size(157, 22);
        Menu_3DView_SpotPlane.Text = "Spot &Plane";
        Menu_3DView_SpotPlane.Click += Menu_3DView_SpotPlane_Click;
        // 
        // Menu_3DView_Pilot
        // 
        Menu_3DView_Pilot.Name = "Menu_3DView_Pilot";
        Menu_3DView_Pilot.Size = new Size(157, 22);
        Menu_3DView_Pilot.Text = "&Pilot";
        Menu_3DView_Pilot.Click += Menu_3DView_Pilot_Click;
        // 
        // Menu_3DView_Separator1
        // 
        Menu_3DView_Separator1.Name = "Menu_3DView_Separator1";
        Menu_3DView_Separator1.Size = new Size(154, 6);
        // 
        // Menu_3DView_PanLeft
        // 
        Menu_3DView_PanLeft.Name = "Menu_3DView_PanLeft";
        Menu_3DView_PanLeft.Size = new Size(157, 22);
        Menu_3DView_PanLeft.Text = "Pan &Left";
        Menu_3DView_PanLeft.Click += Menu_3DView_PanLeft_Click;
        // 
        // Menu_3DView_PanRight
        // 
        Menu_3DView_PanRight.Name = "Menu_3DView_PanRight";
        Menu_3DView_PanRight.Size = new Size(157, 22);
        Menu_3DView_PanRight.Text = "Pan &Right";
        Menu_3DView_PanRight.Click += Menu_3DView_PanRight_Click;
        // 
        // Menu_3DView_Separator2
        // 
        Menu_3DView_Separator2.Name = "Menu_3DView_Separator2";
        Menu_3DView_Separator2.Size = new Size(154, 6);
        // 
        // Menu_3DView_ZoomIn
        // 
        Menu_3DView_ZoomIn.Name = "Menu_3DView_ZoomIn";
        Menu_3DView_ZoomIn.Size = new Size(157, 22);
        Menu_3DView_ZoomIn.Text = "Zoom &In";
        Menu_3DView_ZoomIn.Click += Menu_3DView_ZoomIn_Click;
        // 
        // Menu_3DView_ZoomOut
        // 
        Menu_3DView_ZoomOut.Name = "Menu_3DView_ZoomOut";
        Menu_3DView_ZoomOut.Size = new Size(157, 22);
        Menu_3DView_ZoomOut.Text = "Zoom &Out";
        Menu_3DView_ZoomOut.Click += Menu_3DView_ZoomOut_Click;
        // 
        // Menu_Simulation
        // 
        Menu_Simulation.DropDownItems.AddRange(new ToolStripItem[] { Menu_Simulation_Sound, Menu_Simulation_DeleteAircraft, Menu_Simulation_Separator1, Menu_Simulation_SpeedUp, Menu_Simulation_SlowDown, Menu_Simulation_NormalSpeed, Menu_Simulation_Pause });
        Menu_Simulation.Name = "Menu_Simulation";
        Menu_Simulation.Size = new Size(76, 20);
        Menu_Simulation.Text = "&Simulation";
        // 
        // Menu_Simulation_Sound
        // 
        Menu_Simulation_Sound.Name = "Menu_Simulation_Sound";
        Menu_Simulation_Sound.Size = new Size(182, 22);
        Menu_Simulation_Sound.Text = "&Sound...";
        Menu_Simulation_Sound.Click += Menu_Simulation_Sound_Click;
        // 
        // Menu_Simulation_DeleteAircraft
        // 
        Menu_Simulation_DeleteAircraft.Name = "Menu_Simulation_DeleteAircraft";
        Menu_Simulation_DeleteAircraft.ShortcutKeys = Keys.Delete;
        Menu_Simulation_DeleteAircraft.Size = new Size(182, 22);
        Menu_Simulation_DeleteAircraft.Text = "&Delete Aircraft...";
        Menu_Simulation_DeleteAircraft.Click += Menu_Simulation_DeleteAircraft_Click;
        // 
        // Menu_Simulation_Separator1
        // 
        Menu_Simulation_Separator1.Name = "Menu_Simulation_Separator1";
        Menu_Simulation_Separator1.Size = new Size(179, 6);
        // 
        // Menu_Simulation_SpeedUp
        // 
        Menu_Simulation_SpeedUp.Name = "Menu_Simulation_SpeedUp";
        Menu_Simulation_SpeedUp.ShortcutKeys = Keys.F10;
        Menu_Simulation_SpeedUp.Size = new Size(182, 22);
        Menu_Simulation_SpeedUp.Text = "Speed &Up";
        Menu_Simulation_SpeedUp.Click += Menu_Simulation_SpeedUp_Click;
        // 
        // Menu_Simulation_SlowDown
        // 
        Menu_Simulation_SlowDown.Name = "Menu_Simulation_SlowDown";
        Menu_Simulation_SlowDown.ShortcutKeys = Keys.F9;
        Menu_Simulation_SlowDown.Size = new Size(182, 22);
        Menu_Simulation_SlowDown.Text = "Slow &Down";
        Menu_Simulation_SlowDown.Click += Menu_Simulation_SlowDown_Click;
        // 
        // Menu_Simulation_NormalSpeed
        // 
        Menu_Simulation_NormalSpeed.Name = "Menu_Simulation_NormalSpeed";
        Menu_Simulation_NormalSpeed.ShortcutKeys = Keys.F8;
        Menu_Simulation_NormalSpeed.Size = new Size(182, 22);
        Menu_Simulation_NormalSpeed.Text = "&Normal Speed";
        Menu_Simulation_NormalSpeed.Click += Menu_Simulation_NormalSpeed_Click;
        // 
        // Menu_Simulation_Pause
        // 
        Menu_Simulation_Pause.Name = "Menu_Simulation_Pause";
        Menu_Simulation_Pause.ShortcutKeys = Keys.Control | Keys.P;
        Menu_Simulation_Pause.Size = new Size(182, 22);
        Menu_Simulation_Pause.Text = "Pause";
        Menu_Simulation_Pause.Click += Menu_Simulation_Pause_Click;
        // 
        // Menu_Options
        // 
        Menu_Options.DropDownItems.AddRange(new ToolStripItem[] { Menu_Options_SaveSettings, Menu_Options_RestoreSettings });
        Menu_Options.Name = "Menu_Options";
        Menu_Options.Size = new Size(61, 20);
        Menu_Options.Text = "&Options";
        // 
        // Menu_Options_SaveSettings
        // 
        Menu_Options_SaveSettings.Name = "Menu_Options_SaveSettings";
        Menu_Options_SaveSettings.Size = new Size(167, 22);
        Menu_Options_SaveSettings.Text = "&Save Settings...";
        Menu_Options_SaveSettings.Click += SaveWindowLayout_Click;
        // 
        // Menu_Options_RestoreSettings
        // 
        Menu_Options_RestoreSettings.Name = "Menu_Options_RestoreSettings";
        Menu_Options_RestoreSettings.Size = new Size(167, 22);
        Menu_Options_RestoreSettings.Text = "&Restore Settings...";
        Menu_Options_RestoreSettings.Click += RestoreWindowLayout_Click;
        // 
        // Menu_Help
        // 
        Menu_Help.DropDownItems.AddRange(new ToolStripItem[] { Menu_Help_Contents, Menu_Help_Commands, Menu_Help_Separator1, Menu_Help_About });
        Menu_Help.Name = "Menu_Help";
        Menu_Help.Size = new Size(44, 20);
        Menu_Help.Text = "&Help";
        // 
        // Menu_Help_Contents
        // 
        Menu_Help_Contents.Name = "Menu_Help_Contents";
        Menu_Help_Contents.Size = new Size(229, 22);
        Menu_Help_Contents.Text = "&Online User Manual";
        Menu_Help_Contents.Click += Menu_Help_Contents_Click;
        // 
        // Menu_Help_Commands
        // 
        Menu_Help_Commands.Name = "Menu_Help_Commands";
        Menu_Help_Commands.Size = new Size(229, 22);
        Menu_Help_Commands.Text = "Online &Commands Reference";
        Menu_Help_Commands.Click += Menu_Help_Commands_Click;
        // 
        // Menu_Help_Separator1
        // 
        Menu_Help_Separator1.Name = "Menu_Help_Separator1";
        Menu_Help_Separator1.Size = new Size(226, 6);
        // 
        // Menu_Help_About
        // 
        Menu_Help_About.Name = "Menu_Help_About";
        Menu_Help_About.Size = new Size(229, 22);
        Menu_Help_About.Text = "&About Tower...";
        Menu_Help_About.Click += Menu_Help_About_Click;
        // 
        // StatusStrip
        // 
        StatusStrip.Items.AddRange(new ToolStripItem[] { StatusLabel_Status });
        StatusStrip.Location = new Point(0, 336);
        StatusStrip.Name = "StatusStrip";
        StatusStrip.Size = new Size(574, 22);
        StatusStrip.TabIndex = 1;
        StatusStrip.Text = "statusStrip1";
        // 
        // StatusLabel_Status
        // 
        StatusLabel_Status.Name = "StatusLabel_Status";
        StatusLabel_Status.Size = new Size(559, 17);
        StatusLabel_Status.Spring = true;
        StatusLabel_Status.Text = "-";
        StatusLabel_Status.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // Form_SimulationWindow
        // 
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = SystemColors.Control;
        ClientSize = new Size(574, 358);
        Controls.Add(StatusStrip);
        Controls.Add(MenuStrip);
        Icon = (Icon)resources.GetObject("$this.Icon");
        MainMenuStrip = MenuStrip;
        Name = "Form_SimulationWindow";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Tower";
        MenuStrip.ResumeLayout(false);
        MenuStrip.PerformLayout();
        StatusStrip.ResumeLayout(false);
        StatusStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    private MenuStrip MenuStrip;
    private StatusStrip StatusStrip;
    private ToolStripStatusLabel StatusLabel_Status;
    private ToolStripMenuItem Menu_Simulation;
    private ToolStripMenuItem Menu_Help;
    private ToolStripMenuItem Menu_Help_About;
    private ToolStripMenuItem Menu_Simulation_Sound;
    private ToolStripMenuItem Menu_Simulation_DeleteAircraft;
    private ToolStripSeparator Menu_Simulation_Separator1;
    private ToolStripMenuItem Menu_Simulation_SpeedUp;
    private ToolStripMenuItem Menu_Simulation_SlowDown;
    private ToolStripMenuItem Menu_Simulation_NormalSpeed;
    private ToolStripMenuItem Menu_Simulation_Pause;
    private ToolStripMenuItem Menu_Help_Contents;
    private ToolStripMenuItem Menu_Help_Commands;
    private ToolStripSeparator Menu_Help_Separator1;
    private ToolStripMenuItem Menu_3DView;
    private ToolStripMenuItem Menu_3DView_Normal;
    private ToolStripMenuItem Menu_3DView_Tracking;
    private ToolStripSeparator Menu_3DView_Separator1;
    private ToolStripMenuItem Menu_3DView_PanLeft;
    private ToolStripMenuItem Menu_3DView_PanRight;
    private ToolStripSeparator Menu_3DView_Separator2;
    private ToolStripMenuItem Menu_3DView_ZoomIn;
    private ToolStripMenuItem Menu_3DView_ZoomOut;
    private ToolStripMenuItem Menu_Options;
    private ToolStripMenuItem Menu_Options_SaveSettings;
    private ToolStripMenuItem Menu_Options_RestoreSettings;
    private ToolStripMenuItem Menu_3DView_Binocular;
    private ToolStripMenuItem Menu_3DView_SuperBinocular;
    private ToolStripMenuItem Menu_3DView_SpotPlane;
    private ToolStripMenuItem Menu_3DView_Pilot;
}
