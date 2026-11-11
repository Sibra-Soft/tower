using Tower.Controls;

namespace Tower.WinForms.Forms;
public partial class Form_ASDESetup : Form
{
    private Button Button_Cancel = null!;
    private Button Button_OK = null!;
    private TextBox TextBox_Range = null!;
    private CheckBox CheckBox_TaxiNames = null!;
    private CheckBox CheckBox_GateNames = null!;
    private CheckBox CheckBox_RunwayNames = null!;
    private CheckBox CheckBox_Taxiways = null!;
    private CheckBox CheckBox_VideoMap = null!;
    private Label Label_MilesCaption = null!;
    private Label Label_TextBox_Range = null!;

    private void InitializeComponent()
    {
        Button_Cancel = new Button();
        Button_OK = new Button();
        TextBox_Range = new TextBox();
        CheckBox_TaxiNames = new CheckBox();
        CheckBox_GateNames = new CheckBox();
        CheckBox_RunwayNames = new CheckBox();
        CheckBox_Taxiways = new CheckBox();
        CheckBox_VideoMap = new CheckBox();
        Label_MilesCaption = new Label();
        Label_TextBox_Range = new Label();
        SuspendLayout();
        // 
        // Button_Cancel
        // 
        Button_Cancel.BackColor = SystemColors.ButtonFace;
        Button_Cancel.DialogResult = DialogResult.Cancel;
        Button_Cancel.Location = new Point(171, 40);
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
        Button_OK.Location = new Point(171, 9);
        Button_OK.Name = "Button_OK";
        Button_OK.Size = new Size(82, 25);
        Button_OK.TabIndex = 7;
        Button_OK.Text = "&OK";
        Button_OK.UseVisualStyleBackColor = false;
        Button_OK.Click += Button_OK_Click;
        // 
        // TextBox_Range
        // 
        TextBox_Range.Location = new Point(63, 153);
        TextBox_Range.Name = "TextBox_Range";
        TextBox_Range.Size = new Size(46, 20);
        TextBox_Range.TabIndex = 5;
        TextBox_Range.Text = "1.60";
        // 
        // CheckBox_GateNames
        //
        CheckBox_GateNames.AutoSize = true;
        CheckBox_GateNames.Location = new Point(9, 117);
        CheckBox_GateNames.Name = "CheckBox_GateNames";
        CheckBox_GateNames.Size = new Size(100, 17);
        CheckBox_GateNames.TabIndex = 4;
        CheckBox_GateNames.Text = "Gate Names";
        CheckBox_GateNames.UseVisualStyleBackColor = true;
        //
        // CheckBox_TaxiNames
        // 
        CheckBox_TaxiNames.AutoSize = true;
        CheckBox_TaxiNames.BackColor = Color.FromArgb(255, 255, 255);
        CheckBox_TaxiNames.Location = new Point(9, 90);
        CheckBox_TaxiNames.Name = "CheckBox_TaxiNames";
        CheckBox_TaxiNames.Size = new Size(114, 17);
        CheckBox_TaxiNames.TabIndex = 3;
        CheckBox_TaxiNames.Text = "Taxiway Names";
        CheckBox_TaxiNames.UseVisualStyleBackColor = false;
        // 
        // CheckBox_RunwayNames
        // 
        CheckBox_RunwayNames.AutoSize = true;
        CheckBox_RunwayNames.BackColor = Color.FromArgb(255, 255, 255);
        CheckBox_RunwayNames.Location = new Point(9, 63);
        CheckBox_RunwayNames.Name = "CheckBox_RunwayNames";
        CheckBox_RunwayNames.Size = new Size(113, 17);
        CheckBox_RunwayNames.TabIndex = 2;
        CheckBox_RunwayNames.Text = "Runway Names";
        CheckBox_RunwayNames.UseVisualStyleBackColor = false;
        // 
        // CheckBox_Taxiways
        // 
        CheckBox_Taxiways.AutoSize = true;
        CheckBox_Taxiways.BackColor = Color.FromArgb(255, 255, 255);
        CheckBox_Taxiways.Location = new Point(9, 36);
        CheckBox_Taxiways.Name = "CheckBox_Taxiways";
        CheckBox_Taxiways.Size = new Size(78, 17);
        CheckBox_Taxiways.TabIndex = 1;
        CheckBox_Taxiways.Text = "Taxiways";
        CheckBox_Taxiways.UseVisualStyleBackColor = false;
        // 
        // CheckBox_VideoMap
        // 
        CheckBox_VideoMap.AutoSize = true;
        CheckBox_VideoMap.BackColor = Color.FromArgb(255, 255, 255);
        CheckBox_VideoMap.Location = new Point(9, 9);
        CheckBox_VideoMap.Name = "CheckBox_VideoMap";
        CheckBox_VideoMap.Size = new Size(86, 17);
        CheckBox_VideoMap.TabIndex = 0;
        CheckBox_VideoMap.Text = "Video Map";
        CheckBox_VideoMap.UseVisualStyleBackColor = false;
        // 
        // Label_MilesCaption
        // 
        Label_MilesCaption.AutoSize = true;
        Label_MilesCaption.BackColor = Color.Transparent;
        Label_MilesCaption.Location = new Point(117, 156);
        Label_MilesCaption.Name = "Label_MilesCaption";
        Label_MilesCaption.Size = new Size(35, 13);
        Label_MilesCaption.TabIndex = 6;
        Label_MilesCaption.Text = "miles";
        // 
        // Label_TextBox_Range
        // 
        Label_TextBox_Range.AutoSize = true;
        Label_TextBox_Range.BackColor = Color.Transparent;
        Label_TextBox_Range.Location = new Point(9, 156);
        Label_TextBox_Range.Name = "Label_TextBox_Range";
        Label_TextBox_Range.Size = new Size(48, 13);
        Label_TextBox_Range.TabIndex = 4;
        Label_TextBox_Range.Text = "Range:";
        // 
        // Form_ASDESetup
        // 
        AcceptButton = Button_OK;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = SystemColors.Window;
        CancelButton = Button_Cancel;
        ClientSize = new Size(263, 182);
        Controls.Add(Button_Cancel);
        Controls.Add(Button_OK);
        Controls.Add(TextBox_Range);
        Controls.Add(CheckBox_TaxiNames);
        Controls.Add(CheckBox_GateNames);
        Controls.Add(CheckBox_RunwayNames);
        Controls.Add(CheckBox_Taxiways);
        Controls.Add(CheckBox_VideoMap);
        Controls.Add(Label_MilesCaption);
        Controls.Add(Label_TextBox_Range);
        Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "Form_ASDESetup";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "-";
        Load += Form_ASDESetup_Load;
        Shown += Form_ASDESetup_Shown;
        ResumeLayout(false);
        PerformLayout();
    }
}
