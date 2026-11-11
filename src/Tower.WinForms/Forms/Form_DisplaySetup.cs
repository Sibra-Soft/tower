namespace Tower.WinForms.Forms
{
    public partial class Form_DisplaySetup : Form
    {
        public Form_DisplaySetup()
        {
            InitializeComponent();

            CheckBox_ASDEScope.Checked = Program.Settings.ShowAsde;
            CheckBox_BriteScope.Checked = Program.Settings.ShowBrite;
            CheckBox_FlightStrips.Checked = Program.Settings.ShowStrips;
            CheckBox_Gauges.Checked = Program.Settings.ShowGauges;
            CheckBox_Compass.Checked = Program.Settings.ShowCompass;
            CheckBox_ButtonBar.Checked = Program.Settings.ShowButtons;
            CheckBox_AircraftLabels.Checked = Program.Settings.ShowAircraftLabels;
        }

        private void Form_DisplaySetup_Load(object sender, EventArgs e)
        {

        }

        private void Button_OK_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;

            Program.Settings.ShowAsde = CheckBox_ASDEScope.Checked;
            Program.Settings.ShowBrite = CheckBox_BriteScope.Checked;
            Program.Settings.ShowButtons = CheckBox_ButtonBar.Checked;
            Program.Settings.ShowStrips = CheckBox_FlightStrips.Checked;
            Program.Settings.ShowCompass = CheckBox_Compass.Checked;
            Program.Settings.ShowGauges = CheckBox_Gauges.Checked;
            Program.Settings.ShowAircraftLabels = CheckBox_AircraftLabels.Checked;

            Program.Application.SaveSettings();

            Close();
        }

        private void Button_Cancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;

            Close();
        }
    }
}
