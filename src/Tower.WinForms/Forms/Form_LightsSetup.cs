namespace Tower.WinForms.Forms
{
    public partial class Form_LightsSetup : Form
    {
        public Form_LightsSetup()
        {
            InitializeComponent();

            var groups = new[] {
                ("Runway", Program.Settings.RunwayLights),
                ("Taxiways", Program.Settings.TaxiwayLights),
                ("Beacon", Program.Settings.Beacon),
                ("Obstructions", Program.Settings.ObstructionLights)
            };

            foreach (var (name, value) in groups)
            {
                var radioButtonOn = Controls.Find($"OptionButton_{name}_On", true).FirstOrDefault() as RadioButton;
                var radioButtonOff = Controls.Find($"OptionButton_{name}_Off", true).FirstOrDefault() as RadioButton;

                radioButtonOn?.Checked = value;
                radioButtonOff?.Checked = !value;
            }
        }

        private void Button_OK_Click(object sender, EventArgs e)
        {
            Program.Settings.RunwayLights = OptionButton_Runway_On.Checked;
            Program.Settings.TaxiwayLights = OptionButton_Taxiways_On.Checked;
            Program.Settings.Beacon = OptionButton_Beacon_On.Checked;
            Program.Settings.ObstructionLights = OptionButton_Obstructions_On.Checked;

            Program.Application.SaveSettings();

            Close();
        }
    }
}
