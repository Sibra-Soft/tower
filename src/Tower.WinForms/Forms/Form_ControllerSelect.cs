using Shared.Utils;
using Tower.Infrastructure;

namespace Tower.WinForms.Forms
{
    public partial class Form_ControllerSelect : Form
    {
        public Form_ControllerSelect()
        {
            InitializeComponent();

            ListBox_Controllers.SelectedIndexChanged += ListBox_Controllers_SelectedIndexChanged;

            Timer_Controls.Enabled = true;
        }

        private void Form_ControllerSelect_Load(object sender, EventArgs e)
        {
            RefreshProfiles();
        }

        private void ListBox_Controllers_SelectedIndexChanged(object? sender, EventArgs e)
        {
            for (int i = 0; ListBox_Controllers.Items.Count > i; i++)
            {
                ListBox_Controllers.SetItemCheckState(i, CheckState.Unchecked);
            }

            ListBox_Controllers.SetItemCheckState(ListBox_Controllers.SelectedIndex, CheckState.Checked);
        }

        private void RefreshProfiles()
        {
            ListBox_Controllers.Items.Clear();

            foreach (var p in Program.Profiles)
            {
                ListBox_Controllers.Items.Add(p, p.Id == Program.Settings.SelectedController);
            }

            ListBox_Controllers.SelectedIndex = Math.Max(0, Program.Profiles.FindIndex(p => p.Id == Program.Settings.SelectedController));
        }

        private void Button_NewController_Click(object sender, EventArgs e)
        {
            if (Dialogs.ShowAndWait(new Form_ControllerNew(), "New Controller", this) == DialogResult.OK)
            {
                var name = Dialogs.GetResponseParameter<string>("name");

                if (name.Length is < 1 or > 50 || Program.Profiles.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("Choose a unique controller name (1–50 characters).");
                }

                var profile = new ControllerProfile
                {
                    Name = name
                };

                Program.Profiles.Add(profile);
                Program.Settings.SelectedController = profile.Id;

                Program.Application.SaveSettings();

                RefreshProfiles();
            }
        }

        private void Button_DeleteController_Click(object sender, EventArgs e)
        {
            if (ListBox_Controllers.SelectedItem is not ControllerProfile profile)
            {
                return;
            }

            if (Program.Profiles.Count == 1)
            {
                return;
            }

            if (MessageBox.Show(this, $"Delete controller {profile.Name} and their statistics?", "Tower", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            Program.Profiles.Remove(profile);
            Program.Settings.SelectedController = Program.Profiles[0].Id;
            Program.Application.SaveSettings();

            RefreshProfiles();
        }

        private void Button_ControllerStatistics_Click(object sender, EventArgs e)
        {
            if (ListBox_Controllers.SelectedItem is ControllerProfile p)
            {
                Program.Settings.SelectedController = p.Id;

                Dialogs.ShowAndWait(new Form_Performance(), "Statistics", this);
            }
        }

        private void Button_Close_Click(object sender, EventArgs e)
        {
            if (ListBox_Controllers.SelectedItem is ControllerProfile p)
            {
                Program.Settings.SelectedController = p.Id;
                Program.Application.SaveSettings();

                DialogResult = DialogResult.OK;

                Close();
            }
        }

        private void Timer_Controls_Tick(object sender, EventArgs e)
        {
            if (ListBox_Controllers.Items.Count > 0)
            {
                if (ListBox_Controllers.SelectedItems.Count > 0)
                {
                    Button_ControllerStatistics.Enabled = true;
                    Button_DeleteController.Enabled = true;
                }
                else
                {
                    Button_ControllerStatistics.Enabled = false;
                    Button_DeleteController.Enabled = false;
                }
            }
            else
            {
                Button_ControllerStatistics.Enabled = false;
                Button_DeleteController.Enabled = false;
            }
        }
    }
}
