using Shared.Utils;
using Tower.Core;
using Tower.Infrastructure;
using TowerSpeech.Core;

namespace Tower.WinForms.Forms
{
    public partial class Form_ScenarioNew : Form
    {
        private bool updatingTrafficRate;
        public Form_ScenarioNew()
        {
            InitializeComponent();

            var airportDescriptions = new Dictionary<Airport, string>();
            foreach (var airport in Program.Application.AvailableAirports)
            {
                var description = airport.Name;
                var path = Path.Combine(WorkspacePaths.DefaultRoot, "AIRPORTS", airport.Code + ".CFG");
                try
                {
                    if (File.Exists(path))
                    {
                        var configured = LegacyAirportReader.ReadIni(path).GetValueOrDefault("Config0.Description");
                        if (!string.IsNullOrWhiteSpace(configured))
                        {
                            description = configured;
                        }
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // Missing/unreadable optional descriptions retain the airport name.
                }
                airportDescriptions[airport] = description;
            }
            Combox_AirportSelect.FormattingEnabled = true;
            Combox_AirportSelect.Format += (_, e) =>
            {
                if (e.ListItem is Airport airport)
                {
                    e.Value = airportDescriptions.GetValueOrDefault(airport, airport.Name);
                }
            };
            Combox_AirportSelect.Items.AddRange([.. Program.Application.AvailableAirports.Cast<object>()]);
            Combox_AirportSelect.SelectedItem = Program.Airport;

            Combox_AirportSelect.SelectedIndexChanged += Combox_AirportSelect_SelectedIndexChanged;
            TextBox_AircraftRate.Maximum = 99;
            TextBox_MinuteRate.Maximum = 99;
            TextBox_AircraftRate.ValueChanged += (_, _) => UpdateTrafficRateCaption(TrafficRateField.Aircraft);
            TextBox_MinuteRate.ValueChanged += (_, _) => UpdateTrafficRateCaption(TrafficRateField.Minutes);
        }

        private void Combox_AirportSelect_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (Combox_AirportSelect.SelectedItem is Airport chosen)
            {
                Core.SelectAirport(chosen);
                Label_Difficulty.Text = $"Difficulty: {Program.Airport.Difficulty}";
            }
        }

        private void Button_OK_Click(object sender, EventArgs e)
        {
            if (!ValidateChildren())
            {
                return;
            }

            Program.Options.TrafficAircraftCount = (int)TextBox_AircraftRate.Value;
            Program.Options.TrafficIntervalMinutes = (int)TextBox_MinuteRate.Value;
            Program.Options.AircraftPerMinute = (double)(TextBox_AircraftRate.Value / TextBox_MinuteRate.Value);
            Program.Options.Practice = CheckBox_Practice.Checked;
            DialogResult = DialogResult.OK;

            Close();
        }

        private void Form_ScenarioNew_Load(object sender, EventArgs e)
        {
            var practice = Dialogs.GetRequestParameter<bool>("practice");
            var rate = new TrafficRate(Program.Options.TrafficAircraftCount, Program.Options.TrafficIntervalMinutes);

            CheckBox_Practice.Checked = practice;

            if (rate.Aircraft is < 1 or > 99 || rate.Minutes is < 1 or > 99 || Math.Abs(rate.PerMinute - Program.Options.AircraftPerMinute) > 1e-10)
            {
                rate = TrafficRate.FromPerMinute(Program.Options.AircraftPerMinute);
            }

            updatingTrafficRate = true;

            try
            {
                TextBox_AircraftRate.Value = rate.Aircraft;
                TextBox_MinuteRate.Value = rate.Minutes;
            }
            finally
            {
                updatingTrafficRate = false;
            }

            UpdateTrafficRateCaption(TrafficRateField.Minutes);

            if (Combox_AirportSelect.SelectedItem is Airport airport)
            {
                Label_Difficulty.Text = $"Difficulty: {airport.Difficulty}";
            }

        }

        private void UpdateTrafficRateCaption(TrafficRateField changed)
        {
            if (updatingTrafficRate)
            {
                return;
            }

            var (Rate, Display) = new TrafficRate((int)TextBox_AircraftRate.Value, (int)TextBox_MinuteRate.Value).Adjust(changed);

            updatingTrafficRate = true;

            try
            {
                TextBox_AircraftRate.Value = Rate.Aircraft;
                TextBox_MinuteRate.Value = Rate.Minutes;

                Frame_TrafficRate.Text = $" Traffic Rate: {Display} ";
            }
            finally
            {
                updatingTrafficRate = false;
            }
        }

        private void Button_Setup_Click(object sender, EventArgs e)
        {
            Dialogs.ShowAndWait(new Form_ScenarioSetup(), "Scenario Setup", this);
        }

        private void Button_Cancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;

            Close();
        }
    }
}
