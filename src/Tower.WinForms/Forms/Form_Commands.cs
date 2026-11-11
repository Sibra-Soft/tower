using Tower.Core;

namespace Tower.WinForms.Forms
{
    public partial class Form_Commands : Form
    {
        public event Action<bool>? ExpandCommandsChanged;

        public Form_Commands()
        {
            InitializeComponent();

            MenuStrip_Commands.Renderer = new MenuRenderer();
        }

        public void ConfigureInput(bool expand)
        {
            Menu_Options_ExpandCommands.Checked = expand;
            TextBox_CommandInput.ExpandCommands = expand;
        }

        private void Menu_Options_ExpandCommands_Click(object? sender, EventArgs e)
        {
            TextBox_CommandInput.ExpandCommands = Menu_Options_ExpandCommands.Checked;
            ExpandCommandsChanged?.Invoke(TextBox_CommandInput.ExpandCommands);
            TextBox_CommandInput.Focus();
        }
    }
}
