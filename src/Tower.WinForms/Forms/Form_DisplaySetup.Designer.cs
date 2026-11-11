using Tower.Controls;

namespace Tower.WinForms.Forms;

public partial class Form_DisplaySetup : Form
{
    private Button Button_Cancel = null!;
    private Button Button_OK = null!;
    private CheckBox CheckBox_Gauges = null!;
    private CheckBox CheckBox_Compass = null!;
    private CheckBox CheckBox_ButtonBar = null!;
    private CheckBox CheckBox_FlightStrips = null!;
    private CheckBox CheckBox_BriteScope = null!;
    private CheckBox CheckBox_ASDEScope = null!;
    private CheckBox CheckBox_AircraftLabels = null!;

    private void InitializeComponent()
    {
        Button_Cancel = new Button();
        Button_OK = new Button();
        CheckBox_Gauges = new CheckBox();
        CheckBox_Compass = new CheckBox();
        CheckBox_ButtonBar = new CheckBox();
        CheckBox_FlightStrips = new CheckBox();
        CheckBox_BriteScope = new CheckBox();
        CheckBox_ASDEScope = new CheckBox();
        CheckBox_AircraftLabels = new CheckBox();
        SuspendLayout();
        // 
        // Button_Cancel
        // 
        Button_Cancel.BackColor = SystemColors.ButtonFace;
        Button_Cancel.DialogResult = DialogResult.Cancel;
        Button_Cancel.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Button_Cancel.Location = new Point(180, 39);
        Button_Cancel.Name = "Button_Cancel";
        Button_Cancel.Size = new Size(82, 25);
        Button_Cancel.TabIndex = 8;
        Button_Cancel.Text = "&Cancel";
        Button_Cancel.UseVisualStyleBackColor = false;
        Button_Cancel.Click += Button_Cancel_Click;
        // 
        // Button_OK
        // 
        Button_OK.BackColor = SystemColors.ButtonFace;
        Button_OK.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Button_OK.Location = new Point(180, 9);
        Button_OK.Name = "Button_OK";
        Button_OK.Size = new Size(82, 25);
        Button_OK.TabIndex = 7;
        Button_OK.Text = "&OK";
        Button_OK.UseVisualStyleBackColor = false;
        Button_OK.Click += Button_OK_Click;
        // 
        // CheckBox_Gauges
        // 
        CheckBox_Gauges.AutoSize = true;
        CheckBox_Gauges.BackColor = Color.FromArgb(255, 255, 255);
        CheckBox_Gauges.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        CheckBox_Gauges.Location = new Point(18, 144);
        CheckBox_Gauges.Name = "CheckBox_Gauges";
        CheckBox_Gauges.Size = new Size(75, 19);
        CheckBox_Gauges.TabIndex = 5;
        CheckBox_Gauges.Text = "Gauges";
        CheckBox_Gauges.UseVisualStyleBackColor = false;
        // 
        // CheckBox_Compass
        // 
        CheckBox_Compass.AutoSize = true;
        CheckBox_Compass.BackColor = Color.FromArgb(255, 255, 255);
        CheckBox_Compass.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        CheckBox_Compass.Location = new Point(18, 117);
        CheckBox_Compass.Name = "CheckBox_Compass";
        CheckBox_Compass.Size = new Size(85, 19);
        CheckBox_Compass.TabIndex = 4;
        CheckBox_Compass.Text = "Compass";
        CheckBox_Compass.UseVisualStyleBackColor = false;
        // 
        // CheckBox_ButtonBar
        // 
        CheckBox_ButtonBar.AutoSize = true;
        CheckBox_ButtonBar.BackColor = Color.FromArgb(255, 255, 255);
        CheckBox_ButtonBar.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        CheckBox_ButtonBar.Location = new Point(18, 90);
        CheckBox_ButtonBar.Name = "CheckBox_ButtonBar";
        CheckBox_ButtonBar.Size = new Size(93, 19);
        CheckBox_ButtonBar.TabIndex = 3;
        CheckBox_ButtonBar.Text = "Button Bar";
        CheckBox_ButtonBar.UseVisualStyleBackColor = false;
        // 
        // CheckBox_FlightStrips
        // 
        CheckBox_FlightStrips.AutoSize = true;
        CheckBox_FlightStrips.BackColor = Color.FromArgb(255, 255, 255);
        CheckBox_FlightStrips.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        CheckBox_FlightStrips.Location = new Point(18, 63);
        CheckBox_FlightStrips.Name = "CheckBox_FlightStrips";
        CheckBox_FlightStrips.Size = new Size(103, 19);
        CheckBox_FlightStrips.TabIndex = 2;
        CheckBox_FlightStrips.Text = "Flight Strips";
        CheckBox_FlightStrips.UseVisualStyleBackColor = false;
        // 
        // CheckBox_BriteScope
        // 
        CheckBox_BriteScope.AutoSize = true;
        CheckBox_BriteScope.BackColor = Color.FromArgb(255, 255, 255);
        CheckBox_BriteScope.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        CheckBox_BriteScope.Location = new Point(18, 36);
        CheckBox_BriteScope.Name = "CheckBox_BriteScope";
        CheckBox_BriteScope.Size = new Size(110, 19);
        CheckBox_BriteScope.TabIndex = 1;
        CheckBox_BriteScope.Text = "BRITE Scope";
        CheckBox_BriteScope.UseVisualStyleBackColor = false;
        // 
        // CheckBox_ASDEScope
        // 
        CheckBox_ASDEScope.AutoSize = true;
        CheckBox_ASDEScope.BackColor = Color.FromArgb(255, 255, 255);
        CheckBox_ASDEScope.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        CheckBox_ASDEScope.Location = new Point(18, 9);
        CheckBox_ASDEScope.Name = "CheckBox_ASDEScope";
        CheckBox_ASDEScope.Size = new Size(106, 19);
        CheckBox_ASDEScope.TabIndex = 0;
        CheckBox_ASDEScope.Text = "ASDE Scope";
        CheckBox_ASDEScope.UseVisualStyleBackColor = false;
        // 
        // CheckBox_AircraftLabels
        // 
        CheckBox_AircraftLabels.AutoSize = true;
        CheckBox_AircraftLabels.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        CheckBox_AircraftLabels.Location = new Point(18, 171);
        CheckBox_AircraftLabels.Name = "CheckBox_AircraftLabels";
        CheckBox_AircraftLabels.Size = new Size(197, 19);
        CheckBox_AircraftLabels.TabIndex = 6;
        CheckBox_AircraftLabels.Text = "Aircraft labels (TowerView)";
        CheckBox_AircraftLabels.UseVisualStyleBackColor = true;
        // 
        // Form_DisplaySetup
        // 
        AcceptButton = Button_OK;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = SystemColors.Window;
        CancelButton = Button_Cancel;
        ClientSize = new Size(275, 203);
        Controls.Add(CheckBox_AircraftLabels);
        Controls.Add(Button_Cancel);
        Controls.Add(Button_OK);
        Controls.Add(CheckBox_Gauges);
        Controls.Add(CheckBox_Compass);
        Controls.Add(CheckBox_ButtonBar);
        Controls.Add(CheckBox_FlightStrips);
        Controls.Add(CheckBox_BriteScope);
        Controls.Add(CheckBox_ASDEScope);
        Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "Form_DisplaySetup";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "-";
        Load += Form_DisplaySetup_Load;
        ResumeLayout(false);
        PerformLayout();
    }
}
