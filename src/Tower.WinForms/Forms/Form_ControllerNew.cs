using Shared.Utils;

namespace Tower.WinForms.Forms
{
    public partial class Form_ControllerNew : Form
    {
        public Form_ControllerNew()
        {
            InitializeComponent();

            Timer_Controls.Enabled = true;
        }

        private void Button_OK_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;

            Dialogs.ClearResponse();
            Dialogs.AddResponseParameter("name", TextBox_Name.Text);

            Close();
        }

        private void Button_Cancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;

            Close();
        }

        private void Timer_Controls_Tick(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TextBox_Name.Text))
            {
                Button_OK.Enabled = false;
            }
            else
            {
                Button_OK.Enabled = true;
            }
        }
    }
}
