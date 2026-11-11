namespace Tower.WinForms.Forms;

partial class Form_SoundSetup
{
    private System.ComponentModel.IContainer components = null!;
    private TableLayoutPanel TableLayoutPanel_Settings = null!;
    private Label Label_Engine = null!;
    private ComboBox ComboBox_Engine = null!;
    private Label Label_Voice = null!;
    private ComboBox ComboBox_Voice = null!;
    private Label Label_Rate = null!;
    private NumericUpDown NumericUpDown_Rate = null!;
    private Label Label_RadioEffect = null!;
    private CheckBox CheckBox_RadioEffect = null!;
    private Label Label_Noise = null!;
    private NumericUpDown NumericUpDown_Noise = null!;

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
        TableLayoutPanel_Settings = new TableLayoutPanel();
        CheckBox_Talk = new CheckBox();
        label1 = new Label();
        Label_Engine = new Label();
        ComboBox_Engine = new ComboBox();
        Label_Voice = new Label();
        ComboBox_Voice = new ComboBox();
        Label_Rate = new Label();
        NumericUpDown_Rate = new NumericUpDown();
        Label_RadioEffect = new Label();
        CheckBox_RadioEffect = new CheckBox();
        Label_Noise = new Label();
        NumericUpDown_Noise = new NumericUpDown();
        FlowLayoutPanel_Buttons = new FlowLayoutPanel();
        Button_Cancel = new Button();
        Button_Save = new Button();
        TableLayoutPanel_Settings.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)NumericUpDown_Rate).BeginInit();
        ((System.ComponentModel.ISupportInitialize)NumericUpDown_Noise).BeginInit();
        FlowLayoutPanel_Buttons.SuspendLayout();
        SuspendLayout();
        // 
        // TableLayoutPanel_Settings
        // 
        TableLayoutPanel_Settings.ColumnCount = 2;
        TableLayoutPanel_Settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
        TableLayoutPanel_Settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        TableLayoutPanel_Settings.Controls.Add(CheckBox_Talk, 1, 0);
        TableLayoutPanel_Settings.Controls.Add(label1, 0, 0);
        TableLayoutPanel_Settings.Controls.Add(Label_Engine, 0, 1);
        TableLayoutPanel_Settings.Controls.Add(ComboBox_Engine, 1, 1);
        TableLayoutPanel_Settings.Controls.Add(Label_Voice, 0, 2);
        TableLayoutPanel_Settings.Controls.Add(ComboBox_Voice, 1, 2);
        TableLayoutPanel_Settings.Controls.Add(Label_Rate, 0, 3);
        TableLayoutPanel_Settings.Controls.Add(NumericUpDown_Rate, 1, 3);
        TableLayoutPanel_Settings.Controls.Add(Label_RadioEffect, 0, 4);
        TableLayoutPanel_Settings.Controls.Add(CheckBox_RadioEffect, 1, 4);
        TableLayoutPanel_Settings.Controls.Add(Label_Noise, 0, 5);
        TableLayoutPanel_Settings.Controls.Add(NumericUpDown_Noise, 1, 5);
        TableLayoutPanel_Settings.Dock = DockStyle.Fill;
        TableLayoutPanel_Settings.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        TableLayoutPanel_Settings.Location = new Point(0, 0);
        TableLayoutPanel_Settings.Name = "TableLayoutPanel_Settings";
        TableLayoutPanel_Settings.Padding = new Padding(12);
        TableLayoutPanel_Settings.RowCount = 8;
        TableLayoutPanel_Settings.RowStyles.Add(new RowStyle());
        TableLayoutPanel_Settings.RowStyles.Add(new RowStyle());
        TableLayoutPanel_Settings.RowStyles.Add(new RowStyle());
        TableLayoutPanel_Settings.RowStyles.Add(new RowStyle());
        TableLayoutPanel_Settings.RowStyles.Add(new RowStyle());
        TableLayoutPanel_Settings.RowStyles.Add(new RowStyle());
        TableLayoutPanel_Settings.RowStyles.Add(new RowStyle());
        TableLayoutPanel_Settings.RowStyles.Add(new RowStyle());
        TableLayoutPanel_Settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        TableLayoutPanel_Settings.Size = new Size(560, 276);
        TableLayoutPanel_Settings.TabIndex = 0;
        // 
        // CheckBox_Talk
        // 
        CheckBox_Talk.AutoSize = true;
        CheckBox_Talk.Checked = true;
        CheckBox_Talk.CheckState = CheckState.Checked;
        CheckBox_Talk.Dock = DockStyle.Fill;
        CheckBox_Talk.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        CheckBox_Talk.Location = new Point(155, 15);
        CheckBox_Talk.Name = "CheckBox_Talk";
        CheckBox_Talk.Size = new Size(390, 19);
        CheckBox_Talk.TabIndex = 9;
        CheckBox_Talk.Text = "On";
        CheckBox_Talk.UseVisualStyleBackColor = true;
        CheckBox_Talk.CheckedChanged += SpeechSelectionChanged;
        // 
        // label1
        // 
        label1.Anchor = AnchorStyles.Left;
        label1.AutoSize = true;
        label1.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        label1.Location = new Point(15, 17);
        label1.Name = "label1";
        label1.Size = new Size(54, 15);
        label1.TabIndex = 8;
        label1.Text = "Talking";
        // 
        // Label_Engine
        // 
        Label_Engine.Anchor = AnchorStyles.Left;
        Label_Engine.AutoSize = true;
        Label_Engine.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Label_Engine.Location = new Point(15, 44);
        Label_Engine.Name = "Label_Engine";
        Label_Engine.Size = new Size(103, 15);
        Label_Engine.TabIndex = 1;
        Label_Engine.Text = "Speech engine";
        // 
        // ComboBox_Engine
        // 
        ComboBox_Engine.Dock = DockStyle.Fill;
        ComboBox_Engine.DropDownStyle = ComboBoxStyle.DropDownList;
        ComboBox_Engine.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        ComboBox_Engine.Items.AddRange(new object[] { "Original BAO recordings", "Windows text to speech" });
        ComboBox_Engine.Location = new Point(155, 40);
        ComboBox_Engine.Name = "ComboBox_Engine";
        ComboBox_Engine.SelectedIndexChanged += SpeechSelectionChanged;
        ComboBox_Engine.Size = new Size(390, 23);
        ComboBox_Engine.TabIndex = 1;
        // 
        // Label_Voice
        // 
        Label_Voice.Anchor = AnchorStyles.Left;
        Label_Voice.AutoSize = true;
        Label_Voice.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Label_Voice.Location = new Point(15, 73);
        Label_Voice.Name = "Label_Voice";
        Label_Voice.Size = new Size(101, 15);
        Label_Voice.TabIndex = 2;
        Label_Voice.Text = "Windows voice";
        // 
        // ComboBox_Voice
        // 
        ComboBox_Voice.Dock = DockStyle.Fill;
        ComboBox_Voice.DropDownStyle = ComboBoxStyle.DropDownList;
        ComboBox_Voice.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        ComboBox_Voice.Items.AddRange(new object[] { "Automatic English voice" });
        ComboBox_Voice.Location = new Point(155, 69);
        ComboBox_Voice.Name = "ComboBox_Voice";
        ComboBox_Voice.Size = new Size(390, 23);
        ComboBox_Voice.TabIndex = 2;
        // 
        // Label_Rate
        // 
        Label_Rate.Anchor = AnchorStyles.Left;
        Label_Rate.AutoSize = true;
        Label_Rate.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Label_Rate.Location = new Point(15, 101);
        Label_Rate.Name = "Label_Rate";
        Label_Rate.Size = new Size(84, 15);
        Label_Rate.TabIndex = 3;
        Label_Rate.Text = "Speech rate";
        // 
        // NumericUpDown_Rate
        // 
        NumericUpDown_Rate.Dock = DockStyle.Fill;
        NumericUpDown_Rate.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        NumericUpDown_Rate.Location = new Point(155, 98);
        NumericUpDown_Rate.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
        NumericUpDown_Rate.Minimum = new decimal(new int[] { 10, 0, 0, int.MinValue });
        NumericUpDown_Rate.Name = "NumericUpDown_Rate";
        NumericUpDown_Rate.Size = new Size(390, 21);
        NumericUpDown_Rate.TabIndex = 3;
        // 
        // Label_RadioEffect
        // 
        Label_RadioEffect.Anchor = AnchorStyles.Left;
        Label_RadioEffect.AutoSize = true;
        Label_RadioEffect.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Label_RadioEffect.Location = new Point(15, 127);
        Label_RadioEffect.Name = "Label_RadioEffect";
        Label_RadioEffect.Size = new Size(84, 15);
        Label_RadioEffect.TabIndex = 4;
        Label_RadioEffect.Text = "Radio effect";
        // 
        // CheckBox_RadioEffect
        // 
        CheckBox_RadioEffect.AutoSize = true;
        CheckBox_RadioEffect.Dock = DockStyle.Fill;
        CheckBox_RadioEffect.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        CheckBox_RadioEffect.Location = new Point(155, 125);
        CheckBox_RadioEffect.Name = "CheckBox_RadioEffect";
        CheckBox_RadioEffect.Size = new Size(390, 19);
        CheckBox_RadioEffect.TabIndex = 4;
        CheckBox_RadioEffect.Text = "Apply radio filter to Windows speech";
        CheckBox_RadioEffect.UseVisualStyleBackColor = true;
        // 
        // Label_Noise
        // 
        Label_Noise.Anchor = AnchorStyles.Left;
        Label_Noise.AutoSize = true;
        Label_Noise.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Label_Noise.Location = new Point(15, 153);
        Label_Noise.Name = "Label_Noise";
        Label_Noise.Size = new Size(70, 15);
        Label_Noise.TabIndex = 5;
        Label_Noise.Text = "Noise (%)";
        // 
        // NumericUpDown_Noise
        // 
        NumericUpDown_Noise.Dock = DockStyle.Fill;
        NumericUpDown_Noise.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        NumericUpDown_Noise.Location = new Point(155, 150);
        NumericUpDown_Noise.Maximum = new decimal(new int[] { 30, 0, 0, 0 });
        NumericUpDown_Noise.Name = "NumericUpDown_Noise";
        NumericUpDown_Noise.Size = new Size(390, 21);
        NumericUpDown_Noise.TabIndex = 5;
        // 
        // FlowLayoutPanel_Buttons
        // 
        FlowLayoutPanel_Buttons.AutoSize = true;
        FlowLayoutPanel_Buttons.Controls.Add(Button_Cancel);
        FlowLayoutPanel_Buttons.Controls.Add(Button_Save);
        FlowLayoutPanel_Buttons.Dock = DockStyle.Bottom;
        FlowLayoutPanel_Buttons.FlowDirection = FlowDirection.RightToLeft;
        FlowLayoutPanel_Buttons.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        FlowLayoutPanel_Buttons.Location = new Point(0, 235);
        FlowLayoutPanel_Buttons.Name = "FlowLayoutPanel_Buttons";
        FlowLayoutPanel_Buttons.Padding = new Padding(5);
        FlowLayoutPanel_Buttons.Size = new Size(560, 41);
        FlowLayoutPanel_Buttons.TabIndex = 7;
        // 
        // Button_Cancel
        // 
        Button_Cancel.BackColor = SystemColors.ButtonFace;
        Button_Cancel.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Button_Cancel.Location = new Point(465, 8);
        Button_Cancel.Name = "Button_Cancel";
        Button_Cancel.Size = new Size(82, 25);
        Button_Cancel.TabIndex = 7;
        Button_Cancel.Text = "&Cancel";
        Button_Cancel.UseVisualStyleBackColor = false;
        Button_Cancel.Click += Button_Cancel_Click;
        // 
        // Button_Save
        // 
        Button_Save.BackColor = SystemColors.ButtonFace;
        Button_Save.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Button_Save.Location = new Point(377, 8);
        Button_Save.Name = "Button_Save";
        Button_Save.Size = new Size(82, 25);
        Button_Save.TabIndex = 8;
        Button_Save.Text = "&Save";
        Button_Save.UseVisualStyleBackColor = false;
        Button_Save.Click += Button_Save_Click;
        // 
        // Form_SoundSetup
        // 
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.White;
        ClientSize = new Size(560, 276);
        Controls.Add(FlowLayoutPanel_Buttons);
        Controls.Add(TableLayoutPanel_Settings);
        Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "Form_SoundSetup";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Speech Setup";
        Load += Form_SoundSetup_Load;
        TableLayoutPanel_Settings.ResumeLayout(false);
        TableLayoutPanel_Settings.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)NumericUpDown_Rate).EndInit();
        ((System.ComponentModel.ISupportInitialize)NumericUpDown_Noise).EndInit();
        FlowLayoutPanel_Buttons.ResumeLayout(false);
        ResumeLayout(false);
        PerformLayout();
    }

    private FlowLayoutPanel FlowLayoutPanel_Buttons;
    private Button Button_Cancel;
    private Button Button_Save;
    private CheckBox CheckBox_Talk;
    private Label label1;
}
