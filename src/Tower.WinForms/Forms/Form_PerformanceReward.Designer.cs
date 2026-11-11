namespace Tower.WinForms.Forms;

public partial class Form_PerformanceReward
{
    private PictureBox PictureBox_Reward = null!;
    private Label Label_Title = null!;
    private Label Label_Message = null!;
    private Button Button_OK = null!;

    private void InitializeComponent()
    {
        PictureBox_Reward = new PictureBox();
        Label_Title = new Label();
        Label_Message = new Label();
        Button_OK = new Button();
        ((System.ComponentModel.ISupportInitialize)PictureBox_Reward).BeginInit();
        SuspendLayout();
        // 
        // PictureBox_Reward
        // 
        PictureBox_Reward.Image = Properties.Resources.PERKSICON;
        PictureBox_Reward.Location = new Point(20, 25);
        PictureBox_Reward.Name = "PictureBox_Reward";
        PictureBox_Reward.Size = new Size(64, 64);
        PictureBox_Reward.SizeMode = PictureBoxSizeMode.CenterImage;
        PictureBox_Reward.TabIndex = 0;
        PictureBox_Reward.TabStop = false;
        // 
        // Label_Title
        // 
        Label_Title.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
        Label_Title.Location = new Point(100, 20);
        Label_Title.Name = "Label_Title";
        Label_Title.Size = new Size(350, 42);
        Label_Title.TabIndex = 1;
        Label_Title.Text = "Letter of Appreciation";
        // 
        // Label_Message
        // 
        Label_Message.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
        Label_Message.Location = new Point(100, 62);
        Label_Message.Name = "Label_Message";
        Label_Message.Size = new Size(358, 79);
        Label_Message.TabIndex = 2;
        Label_Message.Text = "The Tower chief thinks you've done a great job.";
        // 
        // Button_OK
        // 
        Button_OK.BackColor = SystemColors.ButtonFace;
        Button_OK.DialogResult = DialogResult.OK;
        Button_OK.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
        Button_OK.Location = new Point(190, 157);
        Button_OK.Name = "Button_OK";
        Button_OK.Size = new Size(90, 28);
        Button_OK.TabIndex = 0;
        Button_OK.Text = "OK";
        Button_OK.UseVisualStyleBackColor = false;
        // 
        // Form_PerformanceReward
        // 
        AcceptButton = Button_OK;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.White;
        CancelButton = Button_OK;
        ClientSize = new Size(470, 202);
        Controls.Add(PictureBox_Reward);
        Controls.Add(Label_Title);
        Controls.Add(Label_Message);
        Controls.Add(Button_OK);
        Font = new Font("Segoe UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "Form_PerformanceReward";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Performance Reward";
        ((System.ComponentModel.ISupportInitialize)PictureBox_Reward).EndInit();
        ResumeLayout(false);
    }
}
