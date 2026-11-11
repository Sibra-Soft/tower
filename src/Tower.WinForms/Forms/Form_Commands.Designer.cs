using Tower.Controls;

namespace Tower.WinForms.Forms;
public partial class Form_Commands : Form
{
    private Panel PictureBox_PanelBottom = null!;
    private CaptionPanel Panel_lclGnd = null!;
    public CaptionPanel Panel_CommandInput = null!;
    public CommandTextBox TextBox_CommandInput = null!;
    private MenuStrip MenuStrip_Commands = null!;
    private ToolStripMenuItem Menu_Options = null!;
    private ToolStripMenuItem Menu_Options_ExpandCommands = null!;

    private void InitializeComponent()
    {
        PictureBox_PanelBottom = new Panel();
        Panel_lclGnd = new CaptionPanel();
        Panel_CommandInput = new CaptionPanel();
        TextBox_CommandInput = new CommandTextBox();
        Panel_Log = new Panel();
        TextBox_Log = new TextBox();
        MenuStrip_Commands = new MenuStrip();
        Menu_Options = new ToolStripMenuItem();
        Menu_Options_ExpandCommands = new ToolStripMenuItem();
        PictureBox_PanelBottom.SuspendLayout();
        Panel_CommandInput.SuspendLayout();
        Panel_Log.SuspendLayout();
        MenuStrip_Commands.SuspendLayout();
        SuspendLayout();
        // 
        // PictureBox_PanelBottom
        // 
        PictureBox_PanelBottom.BackColor = SystemColors.Control;
        PictureBox_PanelBottom.Controls.Add(Panel_lclGnd);
        PictureBox_PanelBottom.Controls.Add(Panel_CommandInput);
        PictureBox_PanelBottom.Dock = DockStyle.Bottom;
        PictureBox_PanelBottom.Location = new Point(0, 194);
        PictureBox_PanelBottom.Name = "PictureBox_PanelBottom";
        PictureBox_PanelBottom.Size = new Size(452, 37);
        PictureBox_PanelBottom.TabIndex = 0;
        // 
        // Panel_lclGnd
        // 
        Panel_lclGnd.AutoSize = true;
        Panel_lclGnd.BackColor = SystemColors.Control;
        Panel_lclGnd.BorderStyle = BorderStyle.Fixed3D;
        Panel_lclGnd.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold);
        Panel_lclGnd.Location = new Point(9, 9);
        Panel_lclGnd.Name = "Panel_lclGnd";
        Panel_lclGnd.Size = new Size(75, 19);
        Panel_lclGnd.TabIndex = 2;
        Panel_lclGnd.Text = "Lcl/GND:";
        // 
        // Panel_CommandInput
        // 
        Panel_CommandInput.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Panel_CommandInput.BackColor = SystemColors.Control;
        Panel_CommandInput.BorderStyle = BorderStyle.Fixed3D;
        Panel_CommandInput.Controls.Add(TextBox_CommandInput);
        Panel_CommandInput.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold);
        Panel_CommandInput.Location = new Point(90, 9);
        Panel_CommandInput.Name = "Panel_CommandInput";
        Panel_CommandInput.Size = new Size(350, 19);
        Panel_CommandInput.TabIndex = 3;
        // 
        // TextBox_CommandInput
        // 
        TextBox_CommandInput.BorderStyle = BorderStyle.None;
        TextBox_CommandInput.Dock = DockStyle.Fill;
        TextBox_CommandInput.Location = new Point(0, 0);
        TextBox_CommandInput.Name = "TextBox_CommandInput";
        TextBox_CommandInput.Size = new Size(346, 15);
        TextBox_CommandInput.TabIndex = 0;
        // 
        // Panel_Log
        // 
        Panel_Log.Controls.Add(TextBox_Log);
        Panel_Log.Dock = DockStyle.Fill;
        Panel_Log.Location = new Point(0, 24);
        Panel_Log.Name = "Panel_Log";
        Panel_Log.Padding = new Padding(3);
        Panel_Log.Size = new Size(452, 170);
        Panel_Log.TabIndex = 1;
        // 
        // TextBox_Log
        // 
        TextBox_Log.BackColor = SystemColors.Control;
        TextBox_Log.BorderStyle = BorderStyle.None;
        TextBox_Log.Dock = DockStyle.Fill;
        TextBox_Log.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
        TextBox_Log.Location = new Point(3, 3);
        TextBox_Log.Multiline = true;
        TextBox_Log.Name = "TextBox_Log";
        TextBox_Log.ReadOnly = true;
        TextBox_Log.ScrollBars = ScrollBars.Vertical;
        TextBox_Log.Size = new Size(446, 164);
        TextBox_Log.TabIndex = 5;
        // 
        // MenuStrip_Commands
        // 
        MenuStrip_Commands.Items.AddRange(new ToolStripItem[] { Menu_Options });
        MenuStrip_Commands.Location = new Point(0, 0);
        MenuStrip_Commands.Name = "MenuStrip_Commands";
        MenuStrip_Commands.RenderMode = ToolStripRenderMode.System;
        MenuStrip_Commands.Size = new Size(452, 24);
        MenuStrip_Commands.TabIndex = 2;
        // 
        // Menu_Options
        // 
        Menu_Options.DropDownItems.AddRange(new ToolStripItem[] { Menu_Options_ExpandCommands });
        Menu_Options.Name = "Menu_Options";
        Menu_Options.Size = new Size(61, 20);
        Menu_Options.Text = "&Options";
        // 
        // Menu_Options_ExpandCommands
        // 
        Menu_Options_ExpandCommands.CheckOnClick = true;
        Menu_Options_ExpandCommands.Name = "Menu_Options_ExpandCommands";
        Menu_Options_ExpandCommands.Size = new Size(32, 19);
        Menu_Options_ExpandCommands.Text = "&Expand Commands";
        Menu_Options_ExpandCommands.Click += Menu_Options_ExpandCommands_Click;
        // 
        // Form_Commands
        // 
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = SystemColors.Control;
        ClientSize = new Size(452, 231);
        ControlBox = false;
        Controls.Add(Panel_Log);
        Controls.Add(PictureBox_PanelBottom);
        Controls.Add(MenuStrip_Commands);
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MainMenuStrip = MenuStrip_Commands;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "Form_Commands";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = " ";
        PictureBox_PanelBottom.ResumeLayout(false);
        PictureBox_PanelBottom.PerformLayout();
        Panel_CommandInput.ResumeLayout(false);
        Panel_CommandInput.PerformLayout();
        Panel_Log.ResumeLayout(false);
        Panel_Log.PerformLayout();
        MenuStrip_Commands.ResumeLayout(false);
        MenuStrip_Commands.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    private Panel Panel_Log;
    public TextBox TextBox_Log;
}
