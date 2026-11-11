namespace Tower.WinForms.Forms;

partial class Form_ManageAirports
{
    private DataGridView Grid_Airports = null!;
    private DataGridViewTextBoxColumn Column_Name = null!;
    private DataGridViewTextBoxColumn Column_Code = null!;
    private DataGridViewTextBoxColumn Column_Version = null!;
    private DataGridViewTextBoxColumn Column_Installed = null!;
    private DataGridViewTextBoxColumn Column_Status = null!;
    private TableLayoutPanel Layout_Main = null!;
    private FlowLayoutPanel Panel_Actions = null!;
    private Button Button_Install = null!;
    private Button Button_Remove = null!;
    private Button Button_Close = null!;
    private Label Label_Status = null!;

    private void InitializeComponent()
    {
        DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
        Grid_Airports = new DataGridView();
        Column_Name = new DataGridViewTextBoxColumn();
        Column_Code = new DataGridViewTextBoxColumn();
        Column_Version = new DataGridViewTextBoxColumn();
        Column_Installed = new DataGridViewTextBoxColumn();
        Column_Status = new DataGridViewTextBoxColumn();
        Layout_Main = new TableLayoutPanel();
        Label_Status = new Label();
        Panel_Actions = new FlowLayoutPanel();
        Button_Install = new Button();
        Button_Remove = new Button();
        Button_Close = new Button();
        ((System.ComponentModel.ISupportInitialize)Grid_Airports).BeginInit();
        Layout_Main.SuspendLayout();
        Panel_Actions.SuspendLayout();
        SuspendLayout();
        // 
        // Grid_Airports
        // 
        Grid_Airports.AllowUserToAddRows = false;
        Grid_Airports.AllowUserToDeleteRows = false;
        Grid_Airports.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
        dataGridViewCellStyle1.BackColor = SystemColors.Control;
        dataGridViewCellStyle1.Font = new Font("Segoe UI", 9F);
        dataGridViewCellStyle1.ForeColor = SystemColors.WindowText;
        dataGridViewCellStyle1.SelectionBackColor = SystemColors.Control;
        dataGridViewCellStyle1.SelectionForeColor = SystemColors.HighlightText;
        dataGridViewCellStyle1.WrapMode = DataGridViewTriState.True;
        Grid_Airports.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
        Grid_Airports.Columns.AddRange(new DataGridViewColumn[] { Column_Name, Column_Code, Column_Version, Column_Installed, Column_Status });
        Grid_Airports.Dock = DockStyle.Fill;
        Grid_Airports.Location = new Point(15, 15);
        Grid_Airports.MultiSelect = false;
        Grid_Airports.Name = "Grid_Airports";
        Grid_Airports.ReadOnly = true;
        Grid_Airports.RowHeadersVisible = false;
        Grid_Airports.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        Grid_Airports.Size = new Size(790, 290);
        Grid_Airports.TabIndex = 0;
        Grid_Airports.CurrentCellChanged += Grid_Airports_SelectionChanged;
        Grid_Airports.SelectionChanged += Grid_Airports_SelectionChanged;
        // 
        // Column_Name
        // 
        Column_Name.FillWeight = 170F;
        Column_Name.HeaderText = "Airport";
        Column_Name.Name = "Column_Name";
        Column_Name.ReadOnly = true;
        // 
        // Column_Code
        // 
        Column_Code.FillWeight = 50F;
        Column_Code.HeaderText = "Code";
        Column_Code.Name = "Column_Code";
        Column_Code.ReadOnly = true;
        // 
        // Column_Version
        // 
        Column_Version.FillWeight = 70F;
        Column_Version.HeaderText = "Version";
        Column_Version.Name = "Column_Version";
        Column_Version.ReadOnly = true;
        // 
        // Column_Installed
        // 
        Column_Installed.FillWeight = 120F;
        Column_Installed.HeaderText = "Installed";
        Column_Installed.Name = "Column_Installed";
        Column_Installed.ReadOnly = true;
        // 
        // Column_Status
        // 
        Column_Status.FillWeight = 120F;
        Column_Status.HeaderText = "Status";
        Column_Status.Name = "Column_Status";
        Column_Status.ReadOnly = true;
        // 
        // Layout_Main
        // 
        Layout_Main.ColumnCount = 1;
        Layout_Main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        Layout_Main.Controls.Add(Grid_Airports, 0, 0);
        Layout_Main.Controls.Add(Label_Status, 0, 1);
        Layout_Main.Controls.Add(Panel_Actions, 0, 2);
        Layout_Main.Dock = DockStyle.Fill;
        Layout_Main.Location = new Point(0, 0);
        Layout_Main.Name = "Layout_Main";
        Layout_Main.Padding = new Padding(12);
        Layout_Main.RowCount = 3;
        Layout_Main.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        Layout_Main.RowStyles.Add(new RowStyle());
        Layout_Main.RowStyles.Add(new RowStyle());
        Layout_Main.Size = new Size(820, 400);
        Layout_Main.TabIndex = 0;
        // 
        // Label_Status
        // 
        Label_Status.AutoSize = true;
        Label_Status.Location = new Point(15, 318);
        Label_Status.Margin = new Padding(3, 10, 3, 10);
        Label_Status.Name = "Label_Status";
        Label_Status.Size = new Size(247, 15);
        Label_Status.TabIndex = 1;
        Label_Status.Text = "Select an airport, or install an airport package.";
        // 
        // Panel_Actions
        // 
        Panel_Actions.AutoSize = true;
        Panel_Actions.Controls.Add(Button_Install);
        Panel_Actions.Controls.Add(Button_Remove);
        Panel_Actions.Controls.Add(Button_Close);
        Panel_Actions.Dock = DockStyle.Fill;
        Panel_Actions.Location = new Point(15, 346);
        Panel_Actions.Name = "Panel_Actions";
        Panel_Actions.Size = new Size(790, 39);
        Panel_Actions.TabIndex = 2;
        // 
        // Button_Install
        // 
        Button_Install.AutoSize = true;
        Button_Install.Location = new Point(3, 3);
        Button_Install.Name = "Button_Install";
        Button_Install.Padding = new Padding(8, 4, 8, 4);
        Button_Install.Size = new Size(75, 33);
        Button_Install.TabIndex = 0;
        Button_Install.Text = "&Install...";
        Button_Install.Click += Button_Install_Click;
        // 
        // Button_Remove
        // 
        Button_Remove.AutoSize = true;
        Button_Remove.Enabled = false;
        Button_Remove.Location = new Point(84, 3);
        Button_Remove.Name = "Button_Remove";
        Button_Remove.Padding = new Padding(8, 4, 8, 4);
        Button_Remove.Size = new Size(85, 33);
        Button_Remove.TabIndex = 1;
        Button_Remove.Text = "&Remove...";
        Button_Remove.Click += Button_Remove_Click;
        // 
        // Button_Close
        // 
        Button_Close.AutoSize = true;
        Button_Close.DialogResult = DialogResult.Cancel;
        Button_Close.Location = new Point(175, 3);
        Button_Close.Name = "Button_Close";
        Button_Close.Padding = new Padding(8, 4, 8, 4);
        Button_Close.Size = new Size(75, 33);
        Button_Close.TabIndex = 2;
        Button_Close.Text = "&Close";
        // 
        // Form_ManageAirports
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = Button_Close;
        ClientSize = new Size(820, 400);
        Controls.Add(Layout_Main);
        MinimizeBox = false;
        MinimumSize = new Size(680, 300);
        Name = "Form_ManageAirports";
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Manage Airports";
        FormClosing += Form_ManageAirports_FormClosing;
        Load += Form_ManageAirports_Load;
        ((System.ComponentModel.ISupportInitialize)Grid_Airports).EndInit();
        Layout_Main.ResumeLayout(false);
        Layout_Main.PerformLayout();
        Panel_Actions.ResumeLayout(false);
        Panel_Actions.PerformLayout();
        ResumeLayout(false);
    }
}
