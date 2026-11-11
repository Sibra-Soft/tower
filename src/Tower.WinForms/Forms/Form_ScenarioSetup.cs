namespace Tower.WinForms.Forms
{
    public partial class Form_ScenarioSetup : Form
    {
        public Form_ScenarioSetup()
        {
            InitializeComponent();
        }

        private void Form_ScenarioSetup_Load(object sender, EventArgs e)
        {
            TextBox_TimeHours.Value = Program.Options.StartMinutes / 60;
            TextBox_TimeMinutes.Value = Program.Options.StartMinutes % 60;

            CheckBox_PrintFlightStrips.Checked = Program.Settings.PrintStrips;
            Option1.Checked = !Program.Settings.StripPaper;
            Option2.Checked = Program.Settings.StripPaper;

            foreach (var r in Program.Airport.Runways)
            {
                ListBox_Runways.Items.Add(r.Name, Program.Options.ArrivalRunways.Count == 0 ? r.Name == Program.Options.Runway : Program.Options.ArrivalRunways.Contains(r.Name));
            }
        }

        private void Button_OK_Click(object sender, EventArgs e)
        {
            if (ListBox_Runways.CheckedItems.Count == 0) throw new InvalidOperationException("Select at least one arrival runway for this scenario.");
            
            Program.Options.ArrivalRunways = ListBox_Runways.CheckedItems.Cast<string>().ToList();
            Program.Options.Runway = (string)ListBox_Runways.CheckedItems[0]!;
            Program.Options.StartMinutes = (int)TextBox_TimeHours.Value * 60 + (int)TextBox_TimeMinutes.Value;
            
            Program.Settings.PrintStrips = CheckBox_PrintFlightStrips.Checked;
            Program.Settings.StripPaper = Option2.Checked;
            
            Program.Application.SaveSettings(); 

            DialogResult = DialogResult.OK;

            Close();
        }
    }
}
