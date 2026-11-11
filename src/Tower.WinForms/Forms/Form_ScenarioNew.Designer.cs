using Tower.Controls;

namespace Tower.WinForms.Forms;

public partial class Form_ScenarioNew : Form
{
    private Button Button_Setup = null!;
    private Button Button_Cancel = null!;
    private Button Button_OK = null!;
    private GroupBox Frame_TrafficRate = null!;
    private NumericUpDown TextBox_AircraftRate = null!;
    private NumericUpDown TextBox_MinuteRate = null!;
    private Label Label_Line = null!;
    private Panel Line_Space = null!;
    private Label Label_TextBox_MinuteRate = null!;
    private Label Label_TextBox_AircraftRate = null!;
    private CheckBox CheckBox_Practice = null!;
    private GroupBox Frame_Airport = null!;
    private ComboBox Combox_AirportSelect = null!;
    private Label Label_Difficulty = null!;

    private void InitializeComponent()
    {
        Button_Setup = new Button();
        Button_Cancel = new Button();
        Button_OK = new Button();
        Frame_TrafficRate = new GroupBox();
        TextBox_AircraftRate = new NumericUpDown();
        TextBox_MinuteRate = new NumericUpDown();
        Label_Line = new Label();
        Line_Space = new Panel();
        Label_TextBox_MinuteRate = new Label();
        Label_TextBox_AircraftRate = new Label();
        CheckBox_Practice = new CheckBox();
        Frame_Airport = new GroupBox();
        Combox_AirportSelect = new ComboBox();
        Label_Difficulty = new Label();
        Frame_TrafficRate.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)TextBox_AircraftRate).BeginInit();
        ((System.ComponentModel.ISupportInitialize)TextBox_MinuteRate).BeginInit();
        Frame_Airport.SuspendLayout();
        SuspendLayout();
        // 
        // Button_Setup
        // 
        Button_Setup.BackColor = SystemColors.ButtonFace;
        Button_Setup.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Button_Setup.Location = new Point(450, 91);
        Button_Setup.Name = "Button_Setup";
        Button_Setup.Size = new Size(82, 25);
        Button_Setup.TabIndex = 7;
        Button_Setup.Text = "&Setup...";
        Button_Setup.UseVisualStyleBackColor = false;
        Button_Setup.Click += Button_Setup_Click;
        // 
        // Button_Cancel
        // 
        Button_Cancel.BackColor = SystemColors.ButtonFace;
        Button_Cancel.DialogResult = DialogResult.Cancel;
        Button_Cancel.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Button_Cancel.Location = new Point(450, 42);
        Button_Cancel.Name = "Button_Cancel";
        Button_Cancel.Size = new Size(82, 25);
        Button_Cancel.TabIndex = 6;
        Button_Cancel.Text = "&Cancel";
        Button_Cancel.UseVisualStyleBackColor = false;
        Button_Cancel.Click += Button_Cancel_Click;
        // 
        // Button_OK
        // 
        Button_OK.BackColor = SystemColors.ButtonFace;
        Button_OK.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Button_OK.Location = new Point(450, 9);
        Button_OK.Name = "Button_OK";
        Button_OK.Size = new Size(82, 25);
        Button_OK.TabIndex = 5;
        Button_OK.Text = "&OK";
        Button_OK.UseVisualStyleBackColor = false;
        Button_OK.Click += Button_OK_Click;
        // 
        // Frame_TrafficRate
        // 
        Frame_TrafficRate.BackColor = Color.FromArgb(255, 255, 255);
        Frame_TrafficRate.Controls.Add(TextBox_AircraftRate);
        Frame_TrafficRate.Controls.Add(TextBox_MinuteRate);
        Frame_TrafficRate.Controls.Add(Label_Line);
        Frame_TrafficRate.Controls.Add(Line_Space);
        Frame_TrafficRate.Controls.Add(Label_TextBox_MinuteRate);
        Frame_TrafficRate.Controls.Add(Label_TextBox_AircraftRate);
        Frame_TrafficRate.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Frame_TrafficRate.Location = new Point(279, 9);
        Frame_TrafficRate.Name = "Frame_TrafficRate";
        Frame_TrafficRate.Size = new Size(145, 109);
        Frame_TrafficRate.TabIndex = 4;
        Frame_TrafficRate.TabStop = false;
        Frame_TrafficRate.Text = " Traffic Rate: - ";
        // 
        // TextBox_AircraftRate
        // 
        TextBox_AircraftRate.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        TextBox_AircraftRate.Location = new Point(18, 27);
        TextBox_AircraftRate.Maximum = new decimal(new int[] { 60, 0, 0, 0 });
        TextBox_AircraftRate.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        TextBox_AircraftRate.Name = "TextBox_AircraftRate";
        TextBox_AircraftRate.Size = new Size(55, 21);
        TextBox_AircraftRate.TabIndex = 10;
        TextBox_AircraftRate.Value = new decimal(new int[] { 1, 0, 0, 0 });
        // 
        // TextBox_MinuteRate
        // 
        TextBox_MinuteRate.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        TextBox_MinuteRate.Location = new Point(18, 72);
        TextBox_MinuteRate.Maximum = new decimal(new int[] { 60, 0, 0, 0 });
        TextBox_MinuteRate.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        TextBox_MinuteRate.Name = "TextBox_MinuteRate";
        TextBox_MinuteRate.Size = new Size(55, 21);
        TextBox_MinuteRate.TabIndex = 8;
        TextBox_MinuteRate.Value = new decimal(new int[] { 1, 0, 0, 0 });
        // 
        // Label_Line
        // 
        Label_Line.AutoSize = true;
        Label_Line.BackColor = Color.FromArgb(255, 255, 255);
        Label_Line.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Label_Line.Location = new Point(54, 50);
        Label_Line.Name = "Label_Line";
        Label_Line.Size = new Size(41, 15);
        Label_Line.TabIndex = 14;
        Label_Line.Text = "Every";
        // 
        // Line_Space
        // 
        Line_Space.BackColor = SystemColors.ControlDark;
        Line_Space.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Line_Space.Location = new Point(18, 59);
        Line_Space.Name = "Line_Space";
        Line_Space.Size = new Size(117, 1);
        Line_Space.TabIndex = 0;
        // 
        // Label_TextBox_MinuteRate
        // 
        Label_TextBox_MinuteRate.AutoSize = true;
        Label_TextBox_MinuteRate.BackColor = Color.Transparent;
        Label_TextBox_MinuteRate.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Label_TextBox_MinuteRate.Location = new Point(81, 76);
        Label_TextBox_MinuteRate.Name = "Label_TextBox_MinuteRate";
        Label_TextBox_MinuteRate.Size = new Size(58, 15);
        Label_TextBox_MinuteRate.TabIndex = 13;
        Label_TextBox_MinuteRate.Text = "Minutes";
        // 
        // Label_TextBox_AircraftRate
        // 
        Label_TextBox_AircraftRate.AutoSize = true;
        Label_TextBox_AircraftRate.BackColor = Color.Transparent;
        Label_TextBox_AircraftRate.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Label_TextBox_AircraftRate.Location = new Point(81, 29);
        Label_TextBox_AircraftRate.Name = "Label_TextBox_AircraftRate";
        Label_TextBox_AircraftRate.Size = new Size(52, 15);
        Label_TextBox_AircraftRate.TabIndex = 11;
        Label_TextBox_AircraftRate.Text = "Aircraft";
        // 
        // CheckBox_Practice
        // 
        CheckBox_Practice.AutoSize = true;
        CheckBox_Practice.BackColor = Color.FromArgb(255, 255, 255);
        CheckBox_Practice.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        CheckBox_Practice.Location = new Point(9, 99);
        CheckBox_Practice.Name = "CheckBox_Practice";
        CheckBox_Practice.Size = new Size(227, 19);
        CheckBox_Practice.TabIndex = 3;
        CheckBox_Practice.Text = "Practice (does not affect rating)";
        CheckBox_Practice.UseVisualStyleBackColor = false;
        // 
        // Frame_Airport
        // 
        Frame_Airport.BackColor = Color.FromArgb(255, 255, 255);
        Frame_Airport.Controls.Add(Combox_AirportSelect);
        Frame_Airport.Controls.Add(Label_Difficulty);
        Frame_Airport.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Frame_Airport.Location = new Point(9, 9);
        Frame_Airport.Name = "Frame_Airport";
        Frame_Airport.Size = new Size(262, 82);
        Frame_Airport.TabIndex = 0;
        Frame_Airport.TabStop = false;
        Frame_Airport.Text = " Airport ";
        // 
        // Combox_AirportSelect
        // 
        Combox_AirportSelect.DropDownStyle = ComboBoxStyle.DropDownList;
        Combox_AirportSelect.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Combox_AirportSelect.Location = new Point(18, 23);
        Combox_AirportSelect.Name = "Combox_AirportSelect";
        Combox_AirportSelect.Size = new Size(226, 23);
        Combox_AirportSelect.TabIndex = 2;
        // 
        // Label_Difficulty
        // 
        Label_Difficulty.AutoSize = true;
        Label_Difficulty.BackColor = Color.Transparent;
        Label_Difficulty.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Label_Difficulty.Location = new Point(18, 54);
        Label_Difficulty.Name = "Label_Difficulty";
        Label_Difficulty.Size = new Size(12, 15);
        Label_Difficulty.TabIndex = 1;
        Label_Difficulty.Text = "-";
        // 
        // Form_ScenarioNew
        // 
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = SystemColors.Window;
        CancelButton = Button_Cancel;
        ClientSize = new Size(542, 127);
        Controls.Add(Button_Setup);
        Controls.Add(Button_Cancel);
        Controls.Add(Button_OK);
        Controls.Add(Frame_TrafficRate);
        Controls.Add(CheckBox_Practice);
        Controls.Add(Frame_Airport);
        Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "Form_ScenarioNew";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "-";
        Load += Form_ScenarioNew_Load;
        Frame_TrafficRate.ResumeLayout(false);
        Frame_TrafficRate.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)TextBox_AircraftRate).EndInit();
        ((System.ComponentModel.ISupportInitialize)TextBox_MinuteRate).EndInit();
        Frame_Airport.ResumeLayout(false);
        Frame_Airport.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
}
