using Tower.Controls;

namespace Tower.WinForms.Forms;

public partial class Form_ScenarioSetup : Form
{
    private GroupBox Frame2 = null!;
    private RadioButton Option2 = null!;
    private RadioButton Option1 = null!;
    private CheckBox CheckBox_PrintFlightStrips = null!;
    private CheckedListBox ListBox_Runways = null!;
    private Button Button_Cancel = null!;
    private Button Button_OK = null!;
    private GroupBox Frame1 = null!;
    private NumericUpDown TextBox_TimeMinutes = null!;
    private NumericUpDown TextBox_TimeHours = null!;
    private Label Label1 = null!;
    private Label Label2 = null!;

    private void InitializeComponent()
    {
        Frame2 = new GroupBox();
        Option2 = new RadioButton();
        Option1 = new RadioButton();
        CheckBox_PrintFlightStrips = new CheckBox();
        ListBox_Runways = new CheckedListBox();
        Button_Cancel = new Button();
        Button_OK = new Button();
        Frame1 = new GroupBox();
        TextBox_TimeMinutes = new NumericUpDown();
        TextBox_TimeHours = new NumericUpDown();
        Label1 = new Label();
        Label2 = new Label();
        Frame2.SuspendLayout();
        Frame1.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)TextBox_TimeMinutes).BeginInit();
        ((System.ComponentModel.ISupportInitialize)TextBox_TimeHours).BeginInit();
        SuspendLayout();
        // 
        // Frame2
        // 
        Frame2.BackColor = Color.FromArgb(255, 255, 255);
        Frame2.Controls.Add(Option2);
        Frame2.Controls.Add(Option1);
        Frame2.Controls.Add(CheckBox_PrintFlightStrips);
        Frame2.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Frame2.Location = new Point(9, 134);
        Frame2.Name = "Frame2";
        Frame2.Size = new Size(181, 73);
        Frame2.TabIndex = 10;
        Frame2.TabStop = false;
        // 
        // Option2
        // 
        Option2.BackColor = Color.FromArgb(255, 255, 255);
        Option2.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Option2.Location = new Point(18, 45);
        Option2.Name = "Option2";
        Option2.Size = new Size(145, 19);
        Option2.TabIndex = 13;
        Option2.Text = "Strip Paper";
        Option2.UseVisualStyleBackColor = false;
        // 
        // Option1
        // 
        Option1.BackColor = Color.FromArgb(255, 255, 255);
        Option1.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Option1.Location = new Point(18, 27);
        Option1.Name = "Option1";
        Option1.Size = new Size(145, 19);
        Option1.TabIndex = 12;
        Option1.Text = "Plain Paper";
        Option1.UseVisualStyleBackColor = false;
        // 
        // CheckBox_PrintFlightStrips
        // 
        CheckBox_PrintFlightStrips.AutoSize = true;
        CheckBox_PrintFlightStrips.BackColor = Color.FromArgb(255, 255, 255);
        CheckBox_PrintFlightStrips.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        CheckBox_PrintFlightStrips.Location = new Point(11, -2);
        CheckBox_PrintFlightStrips.Name = "CheckBox_PrintFlightStrips";
        CheckBox_PrintFlightStrips.Size = new Size(137, 19);
        CheckBox_PrintFlightStrips.TabIndex = 11;
        CheckBox_PrintFlightStrips.Text = "Print Flight Strips";
        CheckBox_PrintFlightStrips.UseVisualStyleBackColor = false;
        // 
        // ListBox_Runways
        // 
        ListBox_Runways.CheckOnClick = true;
        ListBox_Runways.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        ListBox_Runways.IntegralHeight = false;
        ListBox_Runways.Location = new Point(207, 99);
        ListBox_Runways.Name = "ListBox_Runways";
        ListBox_Runways.Size = new Size(136, 107);
        ListBox_Runways.TabIndex = 8;
        // 
        // Button_Cancel
        // 
        Button_Cancel.BackColor = SystemColors.ButtonFace;
        Button_Cancel.DialogResult = DialogResult.Cancel;
        Button_Cancel.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Button_Cancel.Location = new Point(261, 41);
        Button_Cancel.Name = "Button_Cancel";
        Button_Cancel.Size = new Size(82, 25);
        Button_Cancel.TabIndex = 7;
        Button_Cancel.Text = "&Cancel";
        Button_Cancel.UseVisualStyleBackColor = false;
        // 
        // Button_OK
        // 
        Button_OK.BackColor = SystemColors.ButtonFace;
        Button_OK.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Button_OK.Location = new Point(261, 13);
        Button_OK.Name = "Button_OK";
        Button_OK.Size = new Size(82, 25);
        Button_OK.TabIndex = 6;
        Button_OK.Text = "&OK";
        Button_OK.UseVisualStyleBackColor = false;
        Button_OK.Click += Button_OK_Click;
        // 
        // Frame1
        // 
        Frame1.BackColor = Color.FromArgb(255, 255, 255);
        Frame1.Controls.Add(TextBox_TimeMinutes);
        Frame1.Controls.Add(TextBox_TimeHours);
        Frame1.Controls.Add(Label1);
        Frame1.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Frame1.Location = new Point(27, 18);
        Frame1.Name = "Frame1";
        Frame1.Size = new Size(127, 64);
        Frame1.TabIndex = 0;
        Frame1.TabStop = false;
        Frame1.Text = " Time of Day ";
        // 
        // TextBox_TimeMinutes
        // 
        TextBox_TimeMinutes.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        TextBox_TimeMinutes.Location = new Point(72, 27);
        TextBox_TimeMinutes.Maximum = new decimal(new int[] { 59, 0, 0, 0 });
        TextBox_TimeMinutes.Name = "TextBox_TimeMinutes";
        TextBox_TimeMinutes.Size = new Size(40, 21);
        TextBox_TimeMinutes.TabIndex = 3;
        // 
        // TextBox_TimeHours
        // 
        TextBox_TimeHours.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        TextBox_TimeHours.Location = new Point(14, 27);
        TextBox_TimeHours.Maximum = new decimal(new int[] { 23, 0, 0, 0 });
        TextBox_TimeHours.Name = "TextBox_TimeHours";
        TextBox_TimeHours.Size = new Size(40, 21);
        TextBox_TimeHours.TabIndex = 1;
        TextBox_TimeHours.Value = new decimal(new int[] { 12, 0, 0, 0 });
        // 
        // Label1
        // 
        Label1.AutoSize = true;
        Label1.BackColor = Color.Transparent;
        Label1.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Label1.Location = new Point(58, 28);
        Label1.Name = "Label1";
        Label1.Size = new Size(11, 15);
        Label1.TabIndex = 5;
        Label1.Text = ":";
        // 
        // Label2
        // 
        Label2.AutoSize = true;
        Label2.BackColor = Color.Transparent;
        Label2.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Label2.Location = new Point(207, 81);
        Label2.Name = "Label2";
        Label2.Size = new Size(126, 15);
        Label2.TabIndex = 9;
        Label2.Text = "Available Runways";
        // 
        // Form_ScenarioSetup
        // 
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = SystemColors.Window;
        CancelButton = Button_Cancel;
        ClientSize = new Size(355, 217);
        Controls.Add(Frame2);
        Controls.Add(ListBox_Runways);
        Controls.Add(Button_Cancel);
        Controls.Add(Button_OK);
        Controls.Add(Frame1);
        Controls.Add(Label2);
        Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "Form_ScenarioSetup";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "-";
        Load += Form_ScenarioSetup_Load;
        Frame2.ResumeLayout(false);
        Frame2.PerformLayout();
        Frame1.ResumeLayout(false);
        Frame1.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)TextBox_TimeMinutes).EndInit();
        ((System.ComponentModel.ISupportInitialize)TextBox_TimeHours).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }
}
