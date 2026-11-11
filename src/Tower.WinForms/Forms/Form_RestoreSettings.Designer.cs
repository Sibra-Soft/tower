namespace Tower.WinForms.Forms;

public partial class Form_RestoreSettings
{
    private ListBox ListBox_Files = null!;
    private Button Button_Default = null!;
    private Button Button_OK = null!;
    private Button Button_Cancel = null!;

    private void InitializeComponent()
    {
        ListBox_Files = new ListBox();
        Button_Default = new Button();
        Button_OK = new Button();
        Button_Cancel = new Button();
        SuspendLayout();
        // 
        // ListBox_Files
        // 
        ListBox_Files.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        ListBox_Files.Location = new Point(16, 16);
        ListBox_Files.Name = "ListBox_Files";
        ListBox_Files.Size = new Size(148, 94);
        ListBox_Files.Sorted = true;
        ListBox_Files.TabIndex = 0;
        ListBox_Files.SelectedIndexChanged += ListBox_Files_SelectedIndexChanged;
        ListBox_Files.DoubleClick += Button_OK_Click;
        // 
        // Button_Default
        // 
        Button_Default.BackColor = SystemColors.ButtonFace;
        Button_Default.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Button_Default.Location = new Point(20, 126);
        Button_Default.Name = "Button_Default";
        Button_Default.Size = new Size(144, 28);
        Button_Default.TabIndex = 1;
        Button_Default.Text = "&Restore Default";
        Button_Default.UseVisualStyleBackColor = false;
        Button_Default.Click += Button_Default_Click;
        // 
        // Button_OK
        // 
        Button_OK.BackColor = SystemColors.ButtonFace;
        Button_OK.Enabled = false;
        Button_OK.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Button_OK.Location = new Point(192, 20);
        Button_OK.Name = "Button_OK";
        Button_OK.Size = new Size(80, 28);
        Button_OK.TabIndex = 2;
        Button_OK.Text = "OK";
        Button_OK.UseVisualStyleBackColor = false;
        Button_OK.Click += Button_OK_Click;
        // 
        // Button_Cancel
        // 
        Button_Cancel.BackColor = SystemColors.ButtonFace;
        Button_Cancel.DialogResult = DialogResult.Cancel;
        Button_Cancel.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Button_Cancel.Location = new Point(192, 60);
        Button_Cancel.Name = "Button_Cancel";
        Button_Cancel.Size = new Size(80, 28);
        Button_Cancel.TabIndex = 3;
        Button_Cancel.Text = "Cancel";
        Button_Cancel.UseVisualStyleBackColor = false;
        // 
        // Form_RestoreSettings
        // 
        AcceptButton = Button_OK;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        CancelButton = Button_Cancel;
        ClientSize = new Size(288, 174);
        Controls.Add(ListBox_Files);
        Controls.Add(Button_Default);
        Controls.Add(Button_OK);
        Controls.Add(Button_Cancel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "Form_RestoreSettings";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Restore Settings";
        ResumeLayout(false);
    }
}
