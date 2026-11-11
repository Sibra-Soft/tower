using Tower.Controls;

namespace Tower.WinForms.Forms;

public partial class Form_ControllerNew : Form
{
    private Button Button_Cancel = null!;
    private Button Button_OK = null!;
    private TextBox TextBox_Name = null!;
    private Label Label_TextBox_Name = null!;
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        Button_Cancel = new Button();
        Button_OK = new Button();
        TextBox_Name = new TextBox();
        Label_TextBox_Name = new Label();
        Timer_Controls = new System.Windows.Forms.Timer(components);
        SuspendLayout();
        // 
        // Button_Cancel
        // 
        Button_Cancel.BackColor = SystemColors.ButtonFace;
        Button_Cancel.DialogResult = DialogResult.Cancel;
        Button_Cancel.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Button_Cancel.Location = new Point(108, 153);
        Button_Cancel.Name = "Button_Cancel";
        Button_Cancel.Size = new Size(82, 25);
        Button_Cancel.TabIndex = 3;
        Button_Cancel.Text = "&Cancel";
        Button_Cancel.UseVisualStyleBackColor = false;
        Button_Cancel.Click += Button_Cancel_Click;
        // 
        // Button_OK
        // 
        Button_OK.BackColor = SystemColors.ButtonFace;
        Button_OK.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Button_OK.Location = new Point(18, 153);
        Button_OK.Name = "Button_OK";
        Button_OK.Size = new Size(82, 25);
        Button_OK.TabIndex = 2;
        Button_OK.Text = "&OK";
        Button_OK.UseVisualStyleBackColor = false;
        Button_OK.Click += Button_OK_Click;
        // 
        // TextBox_Name
        // 
        TextBox_Name.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        TextBox_Name.Location = new Point(149, 24);
        TextBox_Name.Name = "TextBox_Name";
        TextBox_Name.Size = new Size(199, 21);
        TextBox_Name.TabIndex = 1;
        // 
        // Label_TextBox_Name
        // 
        Label_TextBox_Name.AutoSize = true;
        Label_TextBox_Name.BackColor = Color.Transparent;
        Label_TextBox_Name.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Label_TextBox_Name.Location = new Point(27, 27);
        Label_TextBox_Name.Name = "Label_TextBox_Name";
        Label_TextBox_Name.Size = new Size(116, 15);
        Label_TextBox_Name.TabIndex = 0;
        Label_TextBox_Name.Text = "Controller Name:";
        // 
        // Timer_Controls
        // 
        Timer_Controls.Interval = 10;
        Timer_Controls.Tick += Timer_Controls_Tick;
        // 
        // Form_ControllerNew
        // 
        AcceptButton = Button_OK;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = SystemColors.Window;
        CancelButton = Button_Cancel;
        ClientSize = new Size(454, 191);
        Controls.Add(Button_Cancel);
        Controls.Add(Button_OK);
        Controls.Add(TextBox_Name);
        Controls.Add(Label_TextBox_Name);
        Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "Form_ControllerNew";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "-";
        ResumeLayout(false);
        PerformLayout();
    }

    private System.Windows.Forms.Timer Timer_Controls;
    private System.ComponentModel.IContainer components;
}
