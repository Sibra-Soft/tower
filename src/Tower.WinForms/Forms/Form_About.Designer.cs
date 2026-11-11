using Tower.Controls;

namespace Tower.WinForms.Forms;

public partial class Form_About : Form
{
    private Button Button_Close = null!;
    private Label Label3 = null!;
    private Label Label2 = null!;
    private Label Label1 = null!;
    private PictureBox Image_ProgramIcon = null!;

    private void InitializeComponent()
    {
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form_About));
        Button_Close = new Button();
        Label3 = new Label();
        Label2 = new Label();
        Label1 = new Label();
        Image_ProgramIcon = new PictureBox();
        label4 = new Label();
        label5 = new Label();
        label6 = new Label();
        LinkLabel_Website = new LinkLabel();
        ((System.ComponentModel.ISupportInitialize)Image_ProgramIcon).BeginInit();
        SuspendLayout();
        // 
        // Button_Close
        // 
        Button_Close.BackColor = SystemColors.ButtonFace;
        Button_Close.DialogResult = DialogResult.Cancel;
        Button_Close.Font = new Font("Verdana", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
        Button_Close.Location = new Point(193, 311);
        Button_Close.Name = "Button_Close";
        Button_Close.Size = new Size(82, 25);
        Button_Close.TabIndex = 2;
        Button_Close.Text = "&Close";
        Button_Close.UseVisualStyleBackColor = false;
        // 
        // Label3
        // 
        Label3.BackColor = Color.Transparent;
        Label3.Font = new Font("Verdana", 8.25F);
        Label3.Location = new Point(12, 70);
        Label3.Name = "Label3";
        Label3.Size = new Size(445, 73);
        Label3.TabIndex = 3;
        Label3.Text = resources.GetString("Label3.Text");
        // 
        // Label2
        // 
        Label2.AutoSize = true;
        Label2.BackColor = Color.Transparent;
        Label2.Font = new Font("Verdana", 8.25F);
        Label2.Location = new Point(61, 33);
        Label2.Name = "Label2";
        Label2.Size = new Size(172, 13);
        Label2.TabIndex = 1;
        Label2.Text = "Copyright © 2026 Sibra-Soft";
        Label2.Click += Label2_Click;
        // 
        // Label1
        // 
        Label1.AutoSize = true;
        Label1.BackColor = Color.Transparent;
        Label1.Font = new Font("Verdana", 8.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
        Label1.Location = new Point(61, 21);
        Label1.Name = "Label1";
        Label1.Size = new Size(100, 13);
        Label1.TabIndex = 0;
        Label1.Text = "Tower Version";
        // 
        // Image_ProgramIcon
        // 
        Image_ProgramIcon.Image = Properties.Resources.MAINICON;
        Image_ProgramIcon.Location = new Point(12, 18);
        Image_ProgramIcon.Name = "Image_ProgramIcon";
        Image_ProgramIcon.Size = new Size(32, 32);
        Image_ProgramIcon.TabIndex = 0;
        Image_ProgramIcon.TabStop = false;
        // 
        // label4
        // 
        label4.AutoSize = true;
        label4.BackColor = Color.Transparent;
        label4.Font = new Font("Verdana", 8.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
        label4.Location = new Point(12, 136);
        label4.Name = "label4";
        label4.Size = new Size(76, 13);
        label4.TabIndex = 4;
        label4.Text = "Disclaimer";
        // 
        // label5
        // 
        label5.BackColor = Color.Transparent;
        label5.Font = new Font("Verdana", 8.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
        label5.Location = new Point(12, 152);
        label5.Name = "label5";
        label5.Size = new Size(445, 95);
        label5.TabIndex = 5;
        label5.Text = resources.GetString("label5.Text");
        // 
        // label6
        // 
        label6.AutoSize = true;
        label6.Font = new Font("Verdana", 8.25F);
        label6.Location = new Point(12, 274);
        label6.Name = "label6";
        label6.Size = new Size(110, 13);
        label6.TabIndex = 6;
        label6.Text = "Visit our website: ";
        // 
        // LinkLabel_Website
        // 
        LinkLabel_Website.ActiveLinkColor = Color.Blue;
        LinkLabel_Website.AutoSize = true;
        LinkLabel_Website.Font = new Font("Verdana", 8.25F);
        LinkLabel_Website.LinkBehavior = LinkBehavior.HoverUnderline;
        LinkLabel_Website.Location = new Point(113, 274);
        LinkLabel_Website.Name = "LinkLabel_Website";
        LinkLabel_Website.Size = new Size(166, 13);
        LinkLabel_Website.TabIndex = 7;
        LinkLabel_Website.TabStop = true;
        LinkLabel_Website.Text = "https://www.tower-sim.com";
        LinkLabel_Website.LinkClicked += LinkLabel_Website_LinkClicked;
        // 
        // Form_About
        // 
        AcceptButton = Button_Close;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = SystemColors.Window;
        ClientSize = new Size(469, 348);
        Controls.Add(LinkLabel_Website);
        Controls.Add(label6);
        Controls.Add(label5);
        Controls.Add(label4);
        Controls.Add(Button_Close);
        Controls.Add(Label3);
        Controls.Add(Label2);
        Controls.Add(Label1);
        Controls.Add(Image_ProgramIcon);
        Font = new Font("Verdana", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "Form_About";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "About Tower";
        ((System.ComponentModel.ISupportInitialize)Image_ProgramIcon).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    private Label label4;
    private Label label5;
    private Label label6;
    private LinkLabel LinkLabel_Website;
}
