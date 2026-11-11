using Tower.Controls;

namespace Tower.WinForms.Forms;

public partial class Form_BRITESetup : Form
{
    private TextBox TextBox_RangeMarks = null!;
    private TextBox TextBox_Range = null!;
    private CheckBox CheckBox_RangeMarks = null!;
    private CheckBox CheckBox_Obstructions = null!;
    private CheckBox CheckBox_VideoMap = null!;
    private CheckBox CheckBox_Taxiways = null!;
    private Button Button_OK = null!;
    private Button Button_Cancel = null!;
    private Label Label_TextBox_RangeMarks = null!;
    private Label Label_MilesCaption_02 = null!;
    private Label Label_TextBox_Range = null!;
    private Label Label_MilesCaption_01 = null!;

    private void InitializeComponent()
    {
        TextBox_RangeMarks = new TextBox();
        TextBox_Range = new TextBox();
        CheckBox_RangeMarks = new CheckBox();
        CheckBox_Obstructions = new CheckBox();
        CheckBox_VideoMap = new CheckBox();
        CheckBox_Taxiways = new CheckBox();
        Button_OK = new Button();
        Button_Cancel = new Button();
        Label_TextBox_RangeMarks = new Label();
        Label_MilesCaption_02 = new Label();
        Label_TextBox_Range = new Label();
        Label_MilesCaption_01 = new Label();
        SuspendLayout();
        // 
        // TextBox_RangeMarks
        // 
        TextBox_RangeMarks.Location = new Point(99, 153);
        TextBox_RangeMarks.Name = "TextBox_RangeMarks";
        TextBox_RangeMarks.Size = new Size(46, 20);
        TextBox_RangeMarks.TabIndex = 9;
        TextBox_RangeMarks.Text = "5";
        // 
        // TextBox_Range
        // 
        TextBox_Range.Location = new Point(99, 126);
        TextBox_Range.Name = "TextBox_Range";
        TextBox_Range.Size = new Size(46, 20);
        TextBox_Range.TabIndex = 6;
        TextBox_Range.Text = "14.50";
        // 
        // CheckBox_RangeMarks
        // 
        CheckBox_RangeMarks.AutoSize = true;
        CheckBox_RangeMarks.BackColor = Color.FromArgb(255, 255, 255);
        CheckBox_RangeMarks.Location = new Point(9, 90);
        CheckBox_RangeMarks.Name = "CheckBox_RangeMarks";
        CheckBox_RangeMarks.Size = new Size(101, 17);
        CheckBox_RangeMarks.TabIndex = 5;
        CheckBox_RangeMarks.Text = "Range Marks";
        CheckBox_RangeMarks.UseVisualStyleBackColor = false;
        // 
        // CheckBox_Obstructions
        // 
        CheckBox_Obstructions.AutoSize = true;
        CheckBox_Obstructions.BackColor = Color.FromArgb(255, 255, 255);
        CheckBox_Obstructions.Location = new Point(9, 63);
        CheckBox_Obstructions.Name = "CheckBox_Obstructions";
        CheckBox_Obstructions.Size = new Size(97, 17);
        CheckBox_Obstructions.TabIndex = 4;
        CheckBox_Obstructions.Text = "Obstructions";
        CheckBox_Obstructions.UseVisualStyleBackColor = false;
        // 
        // CheckBox_VideoMap
        // 
        CheckBox_VideoMap.AutoSize = true;
        CheckBox_VideoMap.BackColor = Color.FromArgb(255, 255, 255);
        CheckBox_VideoMap.Location = new Point(9, 9);
        CheckBox_VideoMap.Name = "CheckBox_VideoMap";
        CheckBox_VideoMap.Size = new Size(86, 17);
        CheckBox_VideoMap.TabIndex = 3;
        CheckBox_VideoMap.Text = "Video Map";
        CheckBox_VideoMap.UseVisualStyleBackColor = false;
        // 
        // CheckBox_Taxiways
        // 
        CheckBox_Taxiways.AutoSize = true;
        CheckBox_Taxiways.BackColor = Color.FromArgb(255, 255, 255);
        CheckBox_Taxiways.Location = new Point(9, 36);
        CheckBox_Taxiways.Name = "CheckBox_Taxiways";
        CheckBox_Taxiways.Size = new Size(78, 17);
        CheckBox_Taxiways.TabIndex = 2;
        CheckBox_Taxiways.Text = "Taxiways";
        CheckBox_Taxiways.UseVisualStyleBackColor = false;
        // 
        // Button_OK
        // 
        Button_OK.BackColor = SystemColors.ButtonFace;
        Button_OK.Location = new Point(207, 9);
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
        Button_Cancel.Location = new Point(207, 40);
        Button_Cancel.Name = "Button_Cancel";
        Button_Cancel.Size = new Size(82, 25);
        Button_Cancel.TabIndex = 0;
        Button_Cancel.Text = "&Cancel";
        Button_Cancel.UseVisualStyleBackColor = false;
        Button_Cancel.Click += Button_Cancel_Click;
        // 
        // Label_TextBox_RangeMarks
        // 
        Label_TextBox_RangeMarks.AutoSize = true;
        Label_TextBox_RangeMarks.BackColor = Color.Transparent;
        Label_TextBox_RangeMarks.Location = new Point(9, 156);
        Label_TextBox_RangeMarks.Name = "Label_TextBox_RangeMarks";
        Label_TextBox_RangeMarks.Size = new Size(86, 13);
        Label_TextBox_RangeMarks.TabIndex = 11;
        Label_TextBox_RangeMarks.Text = "Range Marks:";
        // 
        // Label_MilesCaption_02
        // 
        Label_MilesCaption_02.AutoSize = true;
        Label_MilesCaption_02.BackColor = Color.Transparent;
        Label_MilesCaption_02.Location = new Point(153, 156);
        Label_MilesCaption_02.Name = "Label_MilesCaption_02";
        Label_MilesCaption_02.Size = new Size(35, 13);
        Label_MilesCaption_02.TabIndex = 10;
        Label_MilesCaption_02.Text = "miles";
        // 
        // Label_TextBox_Range
        // 
        Label_TextBox_Range.AutoSize = true;
        Label_TextBox_Range.BackColor = Color.Transparent;
        Label_TextBox_Range.Location = new Point(9, 129);
        Label_TextBox_Range.Name = "Label_TextBox_Range";
        Label_TextBox_Range.Size = new Size(48, 13);
        Label_TextBox_Range.TabIndex = 8;
        Label_TextBox_Range.Text = "Range:";
        // 
        // Label_MilesCaption_01
        // 
        Label_MilesCaption_01.AutoSize = true;
        Label_MilesCaption_01.BackColor = Color.Transparent;
        Label_MilesCaption_01.Location = new Point(153, 129);
        Label_MilesCaption_01.Name = "Label_MilesCaption_01";
        Label_MilesCaption_01.Size = new Size(35, 13);
        Label_MilesCaption_01.TabIndex = 7;
        Label_MilesCaption_01.Text = "miles";
        // 
        // Form_BRITESetup
        // 
        AcceptButton = Button_OK;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = SystemColors.Window;
        CancelButton = Button_Cancel;
        ClientSize = new Size(300, 181);
        Controls.Add(TextBox_RangeMarks);
        Controls.Add(TextBox_Range);
        Controls.Add(CheckBox_RangeMarks);
        Controls.Add(CheckBox_Obstructions);
        Controls.Add(CheckBox_VideoMap);
        Controls.Add(CheckBox_Taxiways);
        Controls.Add(Button_OK);
        Controls.Add(Button_Cancel);
        Controls.Add(Label_TextBox_RangeMarks);
        Controls.Add(Label_MilesCaption_02);
        Controls.Add(Label_TextBox_Range);
        Controls.Add(Label_MilesCaption_01);
        Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "Form_BRITESetup";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "-";
        Load += Form_BRITESetup_Load;
        Shown += Form_BRITESetup_Shown;
        ResumeLayout(false);
        PerformLayout();
    }
}
