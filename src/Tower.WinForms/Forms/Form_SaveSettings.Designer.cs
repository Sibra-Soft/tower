namespace Tower.WinForms.Forms;

public partial class Form_SaveSettings
{
    private Label Label_FileName = null!;
    private TextBox TextBox_FileName = null!;
    private ListBox ListBox_Files = null!;
    private CheckBox CheckBox_MakeDefault = null!;
    private Button Button_OK = null!;
    private Button Button_Cancel = null!;

    private void InitializeComponent()
    {
        Label_FileName = new Label();
        TextBox_FileName = new TextBox();
        ListBox_Files = new ListBox();
        CheckBox_MakeDefault = new CheckBox();
        Button_OK = new Button();
        Button_Cancel = new Button();
        SuspendLayout();
        // 
        // Label_FileName
        // 
        Label_FileName.AutoSize = true;
        Label_FileName.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Label_FileName.Location = new Point(16, 6);
        Label_FileName.Name = "Label_FileName";
        Label_FileName.Size = new Size(77, 15);
        Label_FileName.TabIndex = 0;
        Label_FileName.Text = "File Name:";
        // 
        // TextBox_FileName
        // 
        TextBox_FileName.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        TextBox_FileName.Location = new Point(16, 28);
        TextBox_FileName.Name = "TextBox_FileName";
        TextBox_FileName.Size = new Size(148, 21);
        TextBox_FileName.TabIndex = 0;
        // 
        // ListBox_Files
        // 
        ListBox_Files.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        ListBox_Files.Location = new Point(16, 64);
        ListBox_Files.Name = "ListBox_Files";
        ListBox_Files.Size = new Size(148, 94);
        ListBox_Files.Sorted = true;
        ListBox_Files.TabIndex = 1;
        ListBox_Files.SelectedIndexChanged += ListBox_Files_SelectedIndexChanged;
        // 
        // CheckBox_MakeDefault
        // 
        CheckBox_MakeDefault.AutoSize = true;
        CheckBox_MakeDefault.Checked = true;
        CheckBox_MakeDefault.CheckState = CheckState.Checked;
        CheckBox_MakeDefault.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        CheckBox_MakeDefault.Location = new Point(24, 174);
        CheckBox_MakeDefault.Name = "CheckBox_MakeDefault";
        CheckBox_MakeDefault.Size = new Size(109, 19);
        CheckBox_MakeDefault.TabIndex = 2;
        CheckBox_MakeDefault.Text = "Make default";
        // 
        // Button_OK
        // 
        Button_OK.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Button_OK.Location = new Point(188, 20);
        Button_OK.Name = "Button_OK";
        Button_OK.Size = new Size(80, 28);
        Button_OK.TabIndex = 3;
        Button_OK.Text = "OK";
        Button_OK.Click += Button_OK_Click;
        // 
        // Button_Cancel
        // 
        Button_Cancel.DialogResult = DialogResult.Cancel;
        Button_Cancel.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Button_Cancel.Location = new Point(188, 60);
        Button_Cancel.Name = "Button_Cancel";
        Button_Cancel.Size = new Size(80, 28);
        Button_Cancel.TabIndex = 4;
        Button_Cancel.Text = "Cancel";
        // 
        // Form_SaveSettings
        // 
        AcceptButton = Button_OK;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        CancelButton = Button_Cancel;
        ClientSize = new Size(288, 208);
        Controls.Add(Label_FileName);
        Controls.Add(TextBox_FileName);
        Controls.Add(ListBox_Files);
        Controls.Add(CheckBox_MakeDefault);
        Controls.Add(Button_OK);
        Controls.Add(Button_Cancel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "Form_SaveSettings";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Save Settings";
        ResumeLayout(false);
        PerformLayout();
    }
}
