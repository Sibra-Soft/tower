using Tower.Controls;

namespace Tower.WinForms.Forms;

public partial class Form_ControllerSelect : Form
{
    private Button Button_ControllerStatistics = null!;
    private Button Button_DeleteController = null!;
    private Button Button_NewController = null!;
    private Button Button_Close = null!;
    private CheckedListBox ListBox_Controllers = null!;

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        Button_ControllerStatistics = new Button();
        Button_DeleteController = new Button();
        Button_NewController = new Button();
        Button_Close = new Button();
        ListBox_Controllers = new CheckedListBox();
        Timer_Controls = new System.Windows.Forms.Timer(components);
        SuspendLayout();
        // 
        // Button_ControllerStatistics
        // 
        Button_ControllerStatistics.BackColor = SystemColors.ButtonFace;
        Button_ControllerStatistics.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Button_ControllerStatistics.Location = new Point(405, 81);
        Button_ControllerStatistics.Name = "Button_ControllerStatistics";
        Button_ControllerStatistics.Size = new Size(127, 25);
        Button_ControllerStatistics.TabIndex = 4;
        Button_ControllerStatistics.Text = "Controller &Statistics";
        Button_ControllerStatistics.UseVisualStyleBackColor = false;
        Button_ControllerStatistics.Click += Button_ControllerStatistics_Click;
        // 
        // Button_DeleteController
        // 
        Button_DeleteController.BackColor = SystemColors.ButtonFace;
        Button_DeleteController.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Button_DeleteController.Location = new Point(405, 45);
        Button_DeleteController.Name = "Button_DeleteController";
        Button_DeleteController.Size = new Size(127, 25);
        Button_DeleteController.TabIndex = 3;
        Button_DeleteController.Text = "&Delete Controller";
        Button_DeleteController.UseVisualStyleBackColor = false;
        Button_DeleteController.Click += Button_DeleteController_Click;
        // 
        // Button_NewController
        // 
        Button_NewController.BackColor = SystemColors.ButtonFace;
        Button_NewController.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
        Button_NewController.Location = new Point(405, 9);
        Button_NewController.Name = "Button_NewController";
        Button_NewController.Size = new Size(127, 25);
        Button_NewController.TabIndex = 2;
        Button_NewController.Text = "&New Controller";
        Button_NewController.UseVisualStyleBackColor = false;
        Button_NewController.Click += Button_NewController_Click;
        // 
        // Button_Close
        // 
        Button_Close.BackColor = SystemColors.ButtonFace;
        Button_Close.DialogResult = DialogResult.Cancel;
        Button_Close.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
        Button_Close.Location = new Point(405, 331);
        Button_Close.Name = "Button_Close";
        Button_Close.Size = new Size(127, 25);
        Button_Close.TabIndex = 1;
        Button_Close.Text = "&Continue";
        Button_Close.UseVisualStyleBackColor = false;
        Button_Close.Click += Button_Close_Click;
        // 
        // ListBox_Controllers
        // 
        ListBox_Controllers.CheckOnClick = true;
        ListBox_Controllers.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
        ListBox_Controllers.IntegralHeight = false;
        ListBox_Controllers.Location = new Point(9, 9);
        ListBox_Controllers.Name = "ListBox_Controllers";
        ListBox_Controllers.Size = new Size(379, 347);
        ListBox_Controllers.TabIndex = 0;
        // 
        // Timer_Controls
        // 
        Timer_Controls.Interval = 10;
        Timer_Controls.Tick += Timer_Controls_Tick;
        // 
        // Form_ControllerSelect
        // 
        AcceptButton = Button_Close;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = SystemColors.Window;
        ClientSize = new Size(547, 367);
        Controls.Add(Button_ControllerStatistics);
        Controls.Add(Button_DeleteController);
        Controls.Add(Button_NewController);
        Controls.Add(Button_Close);
        Controls.Add(ListBox_Controllers);
        Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "Form_ControllerSelect";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Select Controller";
        Load += Form_ControllerSelect_Load;
        ResumeLayout(false);
    }

    private System.Windows.Forms.Timer Timer_Controls;
    private System.ComponentModel.IContainer components;
}
