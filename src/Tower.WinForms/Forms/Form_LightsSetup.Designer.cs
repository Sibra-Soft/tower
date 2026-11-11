using Tower.Controls;

namespace Tower.WinForms.Forms;

public partial class Form_LightsSetup : Form
{
    private Panel Frame_ObstructionsLights = null!;
    private RadioButton OptionButton_Obstructions_Off = null!;
    private RadioButton OptionButton_Obstructions_On = null!;
    private Panel Frame_BeaconLights = null!;
    private RadioButton OptionButton_Beacon_Off = null!;
    private RadioButton OptionButton_Beacon_On = null!;
    private Panel Frame_RunwayLights = null!;
    private RadioButton OptionButton_Runway_Off = null!;
    private RadioButton OptionButton_Runway_On = null!;
    private Panel Frame_TaxiwayLights = null!;
    private RadioButton OptionButton_Taxiways_Off = null!;
    private RadioButton OptionButton_Taxiways_On = null!;
    private Button Button_OK = null!;
    private Button Button_Cancel = null!;
    private Label Label_CheckBox_Obstructions = null!;
    private Label Label_CheckBox_Beacon = null!;
    private Label Label_CheckBox_Taxiways = null!;
    private Label Label_CheckBox_Runways = null!;

    private void InitializeComponent()
    {
        Frame_ObstructionsLights = new Panel();
        OptionButton_Obstructions_Off = new RadioButton();
        OptionButton_Obstructions_On = new RadioButton();
        Frame_BeaconLights = new Panel();
        OptionButton_Beacon_Off = new RadioButton();
        OptionButton_Beacon_On = new RadioButton();
        Frame_RunwayLights = new Panel();
        OptionButton_Runway_Off = new RadioButton();
        OptionButton_Runway_On = new RadioButton();
        Frame_TaxiwayLights = new Panel();
        OptionButton_Taxiways_Off = new RadioButton();
        OptionButton_Taxiways_On = new RadioButton();
        Button_OK = new Button();
        Button_Cancel = new Button();
        Label_CheckBox_Obstructions = new Label();
        Label_CheckBox_Beacon = new Label();
        Label_CheckBox_Taxiways = new Label();
        Label_CheckBox_Runways = new Label();
        Frame_ObstructionsLights.SuspendLayout();
        Frame_BeaconLights.SuspendLayout();
        Frame_RunwayLights.SuspendLayout();
        Frame_TaxiwayLights.SuspendLayout();
        SuspendLayout();
        // 
        // Frame_ObstructionsLights
        // 
        Frame_ObstructionsLights.BackColor = Color.FromArgb(255, 255, 255);
        Frame_ObstructionsLights.Controls.Add(OptionButton_Obstructions_Off);
        Frame_ObstructionsLights.Controls.Add(OptionButton_Obstructions_On);
        Frame_ObstructionsLights.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Frame_ObstructionsLights.Location = new Point(135, 126);
        Frame_ObstructionsLights.Name = "Frame_ObstructionsLights";
        Frame_ObstructionsLights.Size = new Size(127, 28);
        Frame_ObstructionsLights.TabIndex = 15;
        Frame_ObstructionsLights.Text = "Frame1";
        // 
        // OptionButton_Obstructions_Off
        // 
        OptionButton_Obstructions_Off.Appearance = Appearance.Button;
        OptionButton_Obstructions_Off.BackColor = SystemColors.ButtonFace;
        OptionButton_Obstructions_Off.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        OptionButton_Obstructions_Off.Location = new Point(63, 0);
        OptionButton_Obstructions_Off.Name = "OptionButton_Obstructions_Off";
        OptionButton_Obstructions_Off.Size = new Size(55, 25);
        OptionButton_Obstructions_Off.TabIndex = 16;
        OptionButton_Obstructions_Off.Text = "OFF";
        OptionButton_Obstructions_Off.TextAlign = ContentAlignment.MiddleCenter;
        OptionButton_Obstructions_Off.UseVisualStyleBackColor = false;
        // 
        // OptionButton_Obstructions_On
        // 
        OptionButton_Obstructions_On.Appearance = Appearance.Button;
        OptionButton_Obstructions_On.BackColor = SystemColors.ButtonFace;
        OptionButton_Obstructions_On.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        OptionButton_Obstructions_On.Location = new Point(9, 0);
        OptionButton_Obstructions_On.Name = "OptionButton_Obstructions_On";
        OptionButton_Obstructions_On.Size = new Size(55, 25);
        OptionButton_Obstructions_On.TabIndex = 17;
        OptionButton_Obstructions_On.Text = "ON";
        OptionButton_Obstructions_On.TextAlign = ContentAlignment.MiddleCenter;
        OptionButton_Obstructions_On.UseVisualStyleBackColor = false;
        // 
        // Frame_BeaconLights
        // 
        Frame_BeaconLights.BackColor = Color.FromArgb(255, 255, 255);
        Frame_BeaconLights.Controls.Add(OptionButton_Beacon_Off);
        Frame_BeaconLights.Controls.Add(OptionButton_Beacon_On);
        Frame_BeaconLights.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Frame_BeaconLights.Location = new Point(135, 90);
        Frame_BeaconLights.Name = "Frame_BeaconLights";
        Frame_BeaconLights.Size = new Size(127, 28);
        Frame_BeaconLights.TabIndex = 12;
        Frame_BeaconLights.Text = "Frame1";
        // 
        // OptionButton_Beacon_Off
        // 
        OptionButton_Beacon_Off.Appearance = Appearance.Button;
        OptionButton_Beacon_Off.BackColor = SystemColors.ButtonFace;
        OptionButton_Beacon_Off.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        OptionButton_Beacon_Off.Location = new Point(63, 0);
        OptionButton_Beacon_Off.Name = "OptionButton_Beacon_Off";
        OptionButton_Beacon_Off.Size = new Size(55, 25);
        OptionButton_Beacon_Off.TabIndex = 13;
        OptionButton_Beacon_Off.Text = "OFF";
        OptionButton_Beacon_Off.TextAlign = ContentAlignment.MiddleCenter;
        OptionButton_Beacon_Off.UseVisualStyleBackColor = false;
        // 
        // OptionButton_Beacon_On
        // 
        OptionButton_Beacon_On.Appearance = Appearance.Button;
        OptionButton_Beacon_On.BackColor = SystemColors.ButtonFace;
        OptionButton_Beacon_On.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        OptionButton_Beacon_On.Location = new Point(9, 0);
        OptionButton_Beacon_On.Name = "OptionButton_Beacon_On";
        OptionButton_Beacon_On.Size = new Size(55, 25);
        OptionButton_Beacon_On.TabIndex = 14;
        OptionButton_Beacon_On.Text = "ON";
        OptionButton_Beacon_On.TextAlign = ContentAlignment.MiddleCenter;
        OptionButton_Beacon_On.UseVisualStyleBackColor = false;
        // 
        // Frame_RunwayLights
        // 
        Frame_RunwayLights.BackColor = Color.FromArgb(255, 255, 255);
        Frame_RunwayLights.Controls.Add(OptionButton_Runway_Off);
        Frame_RunwayLights.Controls.Add(OptionButton_Runway_On);
        Frame_RunwayLights.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Frame_RunwayLights.Location = new Point(135, 18);
        Frame_RunwayLights.Name = "Frame_RunwayLights";
        Frame_RunwayLights.Size = new Size(127, 28);
        Frame_RunwayLights.TabIndex = 9;
        Frame_RunwayLights.Text = "Frame1";
        // 
        // OptionButton_Runway_Off
        // 
        OptionButton_Runway_Off.Appearance = Appearance.Button;
        OptionButton_Runway_Off.BackColor = SystemColors.ButtonFace;
        OptionButton_Runway_Off.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        OptionButton_Runway_Off.Location = new Point(63, 0);
        OptionButton_Runway_Off.Name = "OptionButton_Runway_Off";
        OptionButton_Runway_Off.Size = new Size(55, 25);
        OptionButton_Runway_Off.TabIndex = 10;
        OptionButton_Runway_Off.Text = "OFF";
        OptionButton_Runway_Off.TextAlign = ContentAlignment.MiddleCenter;
        OptionButton_Runway_Off.UseVisualStyleBackColor = false;
        // 
        // OptionButton_Runway_On
        // 
        OptionButton_Runway_On.Appearance = Appearance.Button;
        OptionButton_Runway_On.BackColor = SystemColors.ButtonFace;
        OptionButton_Runway_On.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        OptionButton_Runway_On.Location = new Point(9, 0);
        OptionButton_Runway_On.Name = "OptionButton_Runway_On";
        OptionButton_Runway_On.Size = new Size(55, 25);
        OptionButton_Runway_On.TabIndex = 11;
        OptionButton_Runway_On.Text = "ON";
        OptionButton_Runway_On.TextAlign = ContentAlignment.MiddleCenter;
        OptionButton_Runway_On.UseVisualStyleBackColor = false;
        // 
        // Frame_TaxiwayLights
        // 
        Frame_TaxiwayLights.BackColor = Color.FromArgb(255, 255, 255);
        Frame_TaxiwayLights.Controls.Add(OptionButton_Taxiways_Off);
        Frame_TaxiwayLights.Controls.Add(OptionButton_Taxiways_On);
        Frame_TaxiwayLights.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Frame_TaxiwayLights.Location = new Point(135, 54);
        Frame_TaxiwayLights.Name = "Frame_TaxiwayLights";
        Frame_TaxiwayLights.Size = new Size(127, 28);
        Frame_TaxiwayLights.TabIndex = 6;
        Frame_TaxiwayLights.Text = "Frame1";
        // 
        // OptionButton_Taxiways_Off
        // 
        OptionButton_Taxiways_Off.Appearance = Appearance.Button;
        OptionButton_Taxiways_Off.BackColor = SystemColors.ButtonFace;
        OptionButton_Taxiways_Off.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        OptionButton_Taxiways_Off.Location = new Point(63, 0);
        OptionButton_Taxiways_Off.Name = "OptionButton_Taxiways_Off";
        OptionButton_Taxiways_Off.Size = new Size(55, 25);
        OptionButton_Taxiways_Off.TabIndex = 7;
        OptionButton_Taxiways_Off.Text = "OFF";
        OptionButton_Taxiways_Off.TextAlign = ContentAlignment.MiddleCenter;
        OptionButton_Taxiways_Off.UseVisualStyleBackColor = false;
        // 
        // OptionButton_Taxiways_On
        // 
        OptionButton_Taxiways_On.Appearance = Appearance.Button;
        OptionButton_Taxiways_On.BackColor = SystemColors.ButtonFace;
        OptionButton_Taxiways_On.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        OptionButton_Taxiways_On.Location = new Point(9, 0);
        OptionButton_Taxiways_On.Name = "OptionButton_Taxiways_On";
        OptionButton_Taxiways_On.Size = new Size(55, 25);
        OptionButton_Taxiways_On.TabIndex = 8;
        OptionButton_Taxiways_On.Text = "ON";
        OptionButton_Taxiways_On.TextAlign = ContentAlignment.MiddleCenter;
        OptionButton_Taxiways_On.UseVisualStyleBackColor = false;
        // 
        // Button_OK
        // 
        Button_OK.BackColor = SystemColors.ButtonFace;
        Button_OK.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Button_OK.Location = new Point(50, 189);
        Button_OK.Name = "Button_OK";
        Button_OK.Size = new Size(82, 25);
        Button_OK.TabIndex = 1;
        Button_OK.Text = "&OK";
        Button_OK.UseVisualStyleBackColor = false;
        Button_OK.Click += Button_OK_Click;
        // 
        // Button_Cancel
        // 
        Button_Cancel.BackColor = SystemColors.ButtonFace;
        Button_Cancel.DialogResult = DialogResult.Cancel;
        Button_Cancel.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Button_Cancel.Location = new Point(140, 189);
        Button_Cancel.Name = "Button_Cancel";
        Button_Cancel.Size = new Size(82, 25);
        Button_Cancel.TabIndex = 0;
        Button_Cancel.Text = "&Cancel";
        Button_Cancel.UseVisualStyleBackColor = false;
        // 
        // Label_CheckBox_Obstructions
        // 
        Label_CheckBox_Obstructions.AutoSize = true;
        Label_CheckBox_Obstructions.BackColor = Color.Transparent;
        Label_CheckBox_Obstructions.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Label_CheckBox_Obstructions.Location = new Point(9, 129);
        Label_CheckBox_Obstructions.Name = "Label_CheckBox_Obstructions";
        Label_CheckBox_Obstructions.Size = new Size(91, 15);
        Label_CheckBox_Obstructions.TabIndex = 5;
        Label_CheckBox_Obstructions.Text = "Obstructions:";
        // 
        // Label_CheckBox_Beacon
        // 
        Label_CheckBox_Beacon.AutoSize = true;
        Label_CheckBox_Beacon.BackColor = Color.Transparent;
        Label_CheckBox_Beacon.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Label_CheckBox_Beacon.Location = new Point(9, 93);
        Label_CheckBox_Beacon.Name = "Label_CheckBox_Beacon";
        Label_CheckBox_Beacon.Size = new Size(117, 15);
        Label_CheckBox_Beacon.TabIndex = 4;
        Label_CheckBox_Beacon.Text = "Rotating Beacon:";
        // 
        // Label_CheckBox_Taxiways
        // 
        Label_CheckBox_Taxiways.AutoSize = true;
        Label_CheckBox_Taxiways.BackColor = Color.Transparent;
        Label_CheckBox_Taxiways.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Label_CheckBox_Taxiways.Location = new Point(9, 60);
        Label_CheckBox_Taxiways.Name = "Label_CheckBox_Taxiways";
        Label_CheckBox_Taxiways.Size = new Size(69, 15);
        Label_CheckBox_Taxiways.TabIndex = 3;
        Label_CheckBox_Taxiways.Text = "Taxiways:";
        // 
        // Label_CheckBox_Runways
        // 
        Label_CheckBox_Runways.AutoSize = true;
        Label_CheckBox_Runways.BackColor = Color.Transparent;
        Label_CheckBox_Runways.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Label_CheckBox_Runways.Location = new Point(9, 26);
        Label_CheckBox_Runways.Name = "Label_CheckBox_Runways";
        Label_CheckBox_Runways.Size = new Size(68, 15);
        Label_CheckBox_Runways.TabIndex = 2;
        Label_CheckBox_Runways.Text = "Runways:";
        // 
        // Form_LightsSetup
        // 
        AcceptButton = Button_OK;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(255, 255, 255);
        CancelButton = Button_Cancel;
        ClientSize = new Size(272, 223);
        Controls.Add(Frame_ObstructionsLights);
        Controls.Add(Frame_BeaconLights);
        Controls.Add(Frame_RunwayLights);
        Controls.Add(Frame_TaxiwayLights);
        Controls.Add(Button_OK);
        Controls.Add(Button_Cancel);
        Controls.Add(Label_CheckBox_Obstructions);
        Controls.Add(Label_CheckBox_Beacon);
        Controls.Add(Label_CheckBox_Taxiways);
        Controls.Add(Label_CheckBox_Runways);
        Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "Form_LightsSetup";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "-";
        Frame_ObstructionsLights.ResumeLayout(false);
        Frame_BeaconLights.ResumeLayout(false);
        Frame_RunwayLights.ResumeLayout(false);
        Frame_TaxiwayLights.ResumeLayout(false);
        ResumeLayout(false);
        PerformLayout();
    }
}
