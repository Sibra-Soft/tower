using System.Reflection.Metadata;
using Tower.Controls;

namespace Tower.WinForms.Forms;

public partial class Form_Start : Form
{
    private ToolStripMenuItem MenuFile = null!;
    private ToolStripMenuItem MenuFile_NewScenario = null!;
    private ToolStripMenuItem MenuFile_ManageAirports = null!;
    private ToolStripMenuItem MenuFile_PracticeScenario = null!;
    private ToolStripSeparator MenuFile_Space01 = null!;
    private ToolStripMenuItem MenuFile_SelectController = null!;
    private ToolStripMenuItem MenuFile_ControllerStats = null!;
    private ToolStripSeparator MenuFile_Space02 = null!;
    private ToolStripMenuItem MenuFile_Exit = null!;
    private ToolStripMenuItem MenuSetup = null!;
    private ToolStripMenuItem MenuSetup_ASDE = null!;
    private ToolStripMenuItem MenuSetup_BRITE = null!;
    private ToolStripMenuItem MenuSetup_Lights = null!;
    private ToolStripMenuItem MenuSetup_Display = null!;
    private ToolStripMenuItem MenuOptions = null!;
    private ToolStripMenuItem MenuOptions_PrintFlightStrips = null!;
    private ToolStripMenuItem MenuHelp = null!;
    private ToolStripMenuItem MenuHelp_Contents = null!;
    private ToolStripMenuItem MenuHelp_Commands = null!;
    private ToolStripSeparator MenuHelp_Space01 = null!;
    private ToolStripMenuItem MenuHelp_AboutTower = null!;
    private ToolStripMenuItem MenuHelp_CheckUpdates = null!;

    private void InitializeComponent()
    {
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form_Start));
        MenuStrip = new MenuStrip();
        MenuFile = new ToolStripMenuItem();
        MenuFile_NewScenario = new ToolStripMenuItem();
        MenuFile_PracticeScenario = new ToolStripMenuItem();
        MenuFile_OpenScenario = new ToolStripMenuItem();
        MenuFile_ManageAirports = new ToolStripMenuItem();
        MenuFile_Space01 = new ToolStripSeparator();
        MenuFile_SelectController = new ToolStripMenuItem();
        MenuFile_ControllerStats = new ToolStripMenuItem();
        MenuFile_Space02 = new ToolStripSeparator();
        MenuFile_Exit = new ToolStripMenuItem();
        MenuSetup = new ToolStripMenuItem();
        MenuSetup_ASDE = new ToolStripMenuItem();
        MenuSetup_BRITE = new ToolStripMenuItem();
        MenuSetup_Lights = new ToolStripMenuItem();
        MenuSetup_Display = new ToolStripMenuItem();
        MenuSetup_Sound = new ToolStripMenuItem();
        MenuOptions = new ToolStripMenuItem();
        MenuOptions_PrintFlightStrips = new ToolStripMenuItem();
        MenuHelp = new ToolStripMenuItem();
        MenuHelp_Contents = new ToolStripMenuItem();
        MenuHelp_Commands = new ToolStripMenuItem();
        MenuHelp_Space01 = new ToolStripSeparator();
        MenuHelp_AboutTower = new ToolStripMenuItem();
        MenuHelp_CheckUpdates = new ToolStripMenuItem();
        Panel_BackgroundImage = new HotspotPanel();
        Panel_CursorAsde = new HotspotPanel();
        Panel_CursorLights = new HotspotPanel();
        Panel_CursorController = new HotspotPanel();
        Panel_CursorFlightStrips = new HotspotPanel();
        Panel_CursorBrite = new HotspotPanel();
        Panel_CursorScenario = new HotspotPanel();
        MenuStrip.SuspendLayout();
        Panel_BackgroundImage.SuspendLayout();
        SuspendLayout();
        // 
        // MenuStrip
        // 
        MenuStrip.Items.AddRange(new ToolStripItem[] { MenuFile, MenuSetup, MenuOptions, MenuHelp });
        MenuStrip.Location = new Point(0, 0);
        MenuStrip.Name = "MenuStrip";
        MenuStrip.RenderMode = ToolStripRenderMode.System;
        MenuStrip.Size = new Size(624, 24);
        MenuStrip.TabIndex = 0;
        // 
        // MenuFile
        // 
        MenuFile.DropDownItems.AddRange(new ToolStripItem[] { MenuFile_NewScenario, MenuFile_PracticeScenario, MenuFile_OpenScenario, MenuFile_ManageAirports, MenuFile_Space01, MenuFile_SelectController, MenuFile_ControllerStats, MenuFile_Space02, MenuFile_Exit });
        MenuFile.Name = "MenuFile";
        MenuFile.Size = new Size(37, 20);
        MenuFile.Text = "&File";
        // 
        // MenuFile_NewScenario
        // 
        MenuFile_NewScenario.Name = "MenuFile_NewScenario";
        MenuFile_NewScenario.ShortcutKeys = Keys.Control | Keys.N;
        MenuFile_NewScenario.Size = new Size(216, 22);
        MenuFile_NewScenario.Text = "&New Scenario...";
        MenuFile_NewScenario.Click += MenuFile_NewScenario_Click;
        // 
        // MenuFile_PracticeScenario
        // 
        MenuFile_PracticeScenario.Name = "MenuFile_PracticeScenario";
        MenuFile_PracticeScenario.ShortcutKeys = Keys.Control | Keys.P;
        MenuFile_PracticeScenario.Size = new Size(216, 22);
        MenuFile_PracticeScenario.Text = "&Practice Scenario...";
        MenuFile_PracticeScenario.Click += MenuFile_PracticeScenario_Click;
        // 
        // MenuFile_OpenScenario
        // 
        MenuFile_OpenScenario.Name = "MenuFile_OpenScenario";
        MenuFile_OpenScenario.ShortcutKeys = Keys.Control | Keys.O;
        MenuFile_OpenScenario.Size = new Size(216, 22);
        MenuFile_OpenScenario.Text = "&Open Scenario...";
        MenuFile_OpenScenario.Click += MenuFile_OpenScenario_Click;
        // 
        // MenuFile_ManageAirports
        // 
        MenuFile_ManageAirports.Name = "MenuFile_ManageAirports";
        MenuFile_ManageAirports.Size = new Size(216, 22);
        MenuFile_ManageAirports.Text = "&Manage Airports...";
        MenuFile_ManageAirports.Click += MenuFile_ManageAirports_Click;
        // 
        // MenuFile_Space01
        // 
        MenuFile_Space01.Name = "MenuFile_Space01";
        MenuFile_Space01.Size = new Size(213, 6);
        // 
        // MenuFile_SelectController
        // 
        MenuFile_SelectController.Name = "MenuFile_SelectController";
        MenuFile_SelectController.ShortcutKeys = Keys.Control | Keys.C;
        MenuFile_SelectController.Size = new Size(216, 22);
        MenuFile_SelectController.Text = "&Select Controller...";
        MenuFile_SelectController.Click += MenuFile_SelectController_Click;
        // 
        // MenuFile_ControllerStats
        // 
        MenuFile_ControllerStats.Name = "MenuFile_ControllerStats";
        MenuFile_ControllerStats.ShortcutKeys = Keys.Control | Keys.S;
        MenuFile_ControllerStats.Size = new Size(216, 22);
        MenuFile_ControllerStats.Text = "&Controller Statistics";
        MenuFile_ControllerStats.Click += MenuFile_ControllerStats_Click;
        // 
        // MenuFile_Space02
        // 
        MenuFile_Space02.Name = "MenuFile_Space02";
        MenuFile_Space02.Size = new Size(213, 6);
        // 
        // MenuFile_Exit
        // 
        MenuFile_Exit.Name = "MenuFile_Exit";
        MenuFile_Exit.ShortcutKeys = Keys.Control | Keys.Q;
        MenuFile_Exit.Size = new Size(216, 22);
        MenuFile_Exit.Text = "&Exit";
        MenuFile_Exit.Click += MenuFile_Exit_Click;
        // 
        // MenuSetup
        // 
        MenuSetup.DropDownItems.AddRange(new ToolStripItem[] { MenuSetup_ASDE, MenuSetup_BRITE, MenuSetup_Lights, MenuSetup_Display, MenuSetup_Sound });
        MenuSetup.Name = "MenuSetup";
        MenuSetup.Size = new Size(49, 20);
        MenuSetup.Text = "&Setup";
        // 
        // MenuSetup_ASDE
        // 
        MenuSetup_ASDE.Name = "MenuSetup_ASDE";
        MenuSetup_ASDE.Size = new Size(112, 22);
        MenuSetup_ASDE.Text = "&ASDE";
        MenuSetup_ASDE.Click += MenuSetup_ASDE_Click;
        // 
        // MenuSetup_BRITE
        // 
        MenuSetup_BRITE.Name = "MenuSetup_BRITE";
        MenuSetup_BRITE.Size = new Size(112, 22);
        MenuSetup_BRITE.Text = "&BRITE";
        MenuSetup_BRITE.Click += MenuSetup_BRITE_Click;
        // 
        // MenuSetup_Lights
        // 
        MenuSetup_Lights.Name = "MenuSetup_Lights";
        MenuSetup_Lights.Size = new Size(112, 22);
        MenuSetup_Lights.Text = "&Lights";
        MenuSetup_Lights.Click += MenuSetup_Lights_Click;
        // 
        // MenuSetup_Display
        // 
        MenuSetup_Display.Name = "MenuSetup_Display";
        MenuSetup_Display.Size = new Size(112, 22);
        MenuSetup_Display.Text = "&Display";
        MenuSetup_Display.Click += MenuSetup_Display_Click;
        // 
        // MenuSetup_Sound
        // 
        MenuSetup_Sound.Name = "MenuSetup_Sound";
        MenuSetup_Sound.Size = new Size(112, 22);
        MenuSetup_Sound.Text = "&Sound";
        MenuSetup_Sound.Click += MenuSetup_Sound_Click;
        // 
        // MenuOptions
        // 
        MenuOptions.DropDownItems.AddRange(new ToolStripItem[] { MenuOptions_PrintFlightStrips });
        MenuOptions.Name = "MenuOptions";
        MenuOptions.Size = new Size(61, 20);
        MenuOptions.Text = "&Options";
        // 
        // MenuOptions_PrintFlightStrips
        // 
        MenuOptions_PrintFlightStrips.Name = "MenuOptions_PrintFlightStrips";
        MenuOptions_PrintFlightStrips.ShortcutKeys = Keys.Control | Keys.F;
        MenuOptions_PrintFlightStrips.Size = new Size(204, 22);
        MenuOptions_PrintFlightStrips.Text = "&Print Flight Strips";
        // 
        // MenuHelp
        // 
        MenuHelp.DropDownItems.AddRange(new ToolStripItem[] { MenuHelp_Contents, MenuHelp_Commands, MenuHelp_CheckUpdates, MenuHelp_Space01, MenuHelp_AboutTower });
        MenuHelp_CheckUpdates.Name = "MenuHelp_CheckUpdates";
        MenuHelp_CheckUpdates.Size = new Size(229, 22);
        MenuHelp_CheckUpdates.Text = "Check for &Updates...";
        MenuHelp_CheckUpdates.Click += MenuHelp_CheckUpdates_Click;
        MenuHelp.Name = "MenuHelp";
        MenuHelp.Size = new Size(44, 20);
        MenuHelp.Text = "&Help";
        // 
        // MenuHelp_Contents
        // 
        MenuHelp_Contents.Name = "MenuHelp_Contents";
        MenuHelp_Contents.Size = new Size(229, 22);
        MenuHelp_Contents.Text = "&Online User Manual";
        MenuHelp_Contents.Click += MenuHelp_Contents_Click;
        // 
        // MenuHelp_Commands
        // 
        MenuHelp_Commands.Name = "MenuHelp_Commands";
        MenuHelp_Commands.Size = new Size(229, 22);
        MenuHelp_Commands.Text = "Online &Commands Reference";
        MenuHelp_Commands.Click += MenuHelp_Commands_Click;
        // 
        // MenuHelp_Space01
        // 
        MenuHelp_Space01.Name = "MenuHelp_Space01";
        MenuHelp_Space01.Size = new Size(226, 6);
        // 
        // MenuHelp_AboutTower
        // 
        MenuHelp_AboutTower.Name = "MenuHelp_AboutTower";
        MenuHelp_AboutTower.Size = new Size(229, 22);
        MenuHelp_AboutTower.Text = "&About Tower...";
        MenuHelp_AboutTower.Click += MenuHelp_AboutTower_Click;
        // 
        // Panel_BackgroundImage
        // 
        Panel_BackgroundImage.BackColor = Color.Transparent;
        Panel_BackgroundImage.BackgroundImage = Properties.Resources.BIGTWR;
        Panel_BackgroundImage.BackgroundImageLayout = ImageLayout.Stretch;
        Panel_BackgroundImage.Controls.Add(Panel_CursorAsde);
        Panel_BackgroundImage.Controls.Add(Panel_CursorLights);
        Panel_BackgroundImage.Controls.Add(Panel_CursorController);
        Panel_BackgroundImage.Controls.Add(Panel_CursorFlightStrips);
        Panel_BackgroundImage.Controls.Add(Panel_CursorBrite);
        Panel_BackgroundImage.Controls.Add(Panel_CursorScenario);
        Panel_BackgroundImage.Dock = DockStyle.Fill;
        Panel_BackgroundImage.Location = new Point(0, 24);
        Panel_BackgroundImage.Name = "Panel_BackgroundImage";
        Panel_BackgroundImage.Size = new Size(624, 417);
        Panel_BackgroundImage.TabIndex = 1;
        // 
        // Panel_CursorAsde
        // 
        Panel_CursorAsde.BackColor = Color.Transparent;
        Panel_CursorAsde.CursorFile = "ASDECURSOR.cur";
        Panel_CursorAsde.Location = new Point(518, 184);
        Panel_CursorAsde.Name = "Panel_CursorAsde";
        Panel_CursorAsde.Size = new Size(106, 101);
        Panel_CursorAsde.TabIndex = 1;
        // 
        // Panel_CursorLights
        // 
        Panel_CursorLights.BackColor = Color.Transparent;
        Panel_CursorLights.CursorFile = "LIGHTSCURSOR.cur";
        Panel_CursorLights.Location = new Point(168, 266);
        Panel_CursorLights.Name = "Panel_CursorLights";
        Panel_CursorLights.Size = new Size(45, 54);
        Panel_CursorLights.TabIndex = 2;
        // 
        // Panel_CursorController
        // 
        Panel_CursorController.BackColor = Color.Transparent;
        Panel_CursorController.CursorFile = "CHARCURSOR.cur";
        Panel_CursorController.Location = new Point(359, 262);
        Panel_CursorController.Name = "Panel_CursorController";
        Panel_CursorController.Size = new Size(85, 91);
        Panel_CursorController.TabIndex = 2;
        // 
        // Panel_CursorFlightStrips
        // 
        Panel_CursorFlightStrips.BackColor = Color.Transparent;
        Panel_CursorFlightStrips.CursorFile = "STRIPSCURSOR.cur";
        Panel_CursorFlightStrips.Location = new Point(254, 294);
        Panel_CursorFlightStrips.Name = "Panel_CursorFlightStrips";
        Panel_CursorFlightStrips.Size = new Size(64, 72);
        Panel_CursorFlightStrips.TabIndex = 1;
        // 
        // Panel_CursorBrite
        // 
        Panel_CursorBrite.BackColor = Color.Transparent;
        Panel_CursorBrite.CursorFile = "BRITECURSOR.cur";
        Panel_CursorBrite.Location = new Point(13, 271);
        Panel_CursorBrite.Name = "Panel_CursorBrite";
        Panel_CursorBrite.Size = new Size(148, 123);
        Panel_CursorBrite.TabIndex = 0;
        // 
        // Panel_CursorScenario
        // 
        Panel_CursorScenario.BackColor = Color.Transparent;
        Panel_CursorScenario.CursorFile = "STARTCURSOR.cur";
        Panel_CursorScenario.Location = new Point(136, 3);
        Panel_CursorScenario.Name = "Panel_CursorScenario";
        Panel_CursorScenario.Size = new Size(485, 177);
        Panel_CursorScenario.TabIndex = 0;
        // 
        // Form_Start
        // 
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.White;
        BackgroundImageLayout = ImageLayout.Stretch;
        ClientSize = new Size(624, 441);
        Controls.Add(Panel_BackgroundImage);
        Controls.Add(MenuStrip);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Icon = (Icon)resources.GetObject("$this.Icon");
        MainMenuStrip = MenuStrip;
        MaximizeBox = false;
        Name = "Form_Start";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Tower";
        MenuStrip.ResumeLayout(false);
        MenuStrip.PerformLayout();
        Panel_BackgroundImage.ResumeLayout(false);
        ResumeLayout(false);
        PerformLayout();
    }

    private MenuStrip MenuStrip;
    private HotspotPanel Panel_BackgroundImage;
    private HotspotPanel Panel_CursorLights;
    private HotspotPanel Panel_CursorController;
    private HotspotPanel Panel_CursorFlightStrips;
    private HotspotPanel Panel_CursorAsde;
    private HotspotPanel Panel_CursorBrite;
    private HotspotPanel Panel_CursorScenario;
    private ToolStripMenuItem MenuFile_OpenScenario;
    private ToolStripMenuItem MenuSetup_Sound;
}
