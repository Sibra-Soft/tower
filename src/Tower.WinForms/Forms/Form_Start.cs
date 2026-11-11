using Shared.Utils;
using System.Diagnostics;
using System.Security.Policy;
using Tower.Core;
using Tower.Infrastructure;

namespace Tower.WinForms.Forms
{
    public partial class Form_Start : Form
    {
        private static T JsonClone<T>(T value) => System.Text.Json.JsonSerializer.Deserialize<T>(System.Text.Json.JsonSerializer.Serialize(value, JsonStore.Options), JsonStore.Options)!;

        public Form_Start()
        {
            InitializeComponent();

            MenuStrip.Renderer = new MenuRenderer();

            FormClosing += Form_Start_FormClosing;

            Panel_CursorScenario.Click += Panel_CursorScenario_Click;
            Panel_CursorController.Click += Panel_CursorController_Click;
            Panel_CursorAsde.Click += Panel_CursorAsde_Click;
            Panel_CursorBrite.Click += Panel_CursorBrite_Click;
            Panel_CursorLights.Click += Panel_CursorLights_Click;
        }

        private void Panel_CursorLights_Click(object? sender, EventArgs e)
        {
            MenuSetup_Lights_Click(sender, e);
        }

        private void Panel_CursorBrite_Click(object? sender, EventArgs e)
        {
            MenuSetup_BRITE_Click(sender, e);
        }

        private void Panel_CursorAsde_Click(object? sender, EventArgs e)
        {
            MenuSetup_ASDE_Click(sender, e);
        }

        /// <summary>
        /// Start a new simulation session
        /// </summary>
        /// <param name="session">The session to use for the simulation</param>
        private void StartSession(Session session, string? scenarioPath = null)
        {
            Program.Voice.ReloadSpeechWorkspace();

            Simulation simulation;
            try
            {
                simulation = new Simulation(session, Program.Voice.GetTrafficCallsigns());
            }
            catch (InvalidDataException exception)
            {
                MessageBox.Show(this, exception.Message, "Speech Traffic", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var simulationForm = new Form_SimulationWindow(simulation, scenarioPath: scenarioPath);

            if (session.FreeTraffic is not null)
            {
                simulation.StartFreeTraffic();
            }
            else if (session.Aircraft.Count == 0 && session.Schedule.Count == 0)
            {
                simulation.AddAircraft(FlightDirection.Arrival);
                try
                {
                    simulation.AddAircraft(FlightDirection.Departure);
                }
                catch (InvalidOperationException)
                {
                    // A small recorded callsign pool may only have room for the arrival.
                }
            }

            Hide();
            try
            {
                simulationForm.ShowDialog(this);
            }
            finally
            {
                if (!IsDisposed && !Disposing)
                {
                    Show();
                }
            }
            if (!IsDisposed && !Disposing && simulationForm.EarnedReward is { } reward)
            {
                ShowPerformanceReward(reward);
            }
        }

        internal void ShowPerformanceReward(PerformanceReward reward)
        {
            using var dialog = new Form_PerformanceReward();
            dialog.Configure(reward);
            dialog.ShowDialog(this);
        }

        private void NewScenario(bool practice)
        {
            Dialogs.ClearRequest();
            Dialogs.AddRequestParameter("practice", practice);

            if (Dialogs.ShowAndWait(new Form_ScenarioNew(), "New Scenario", this) == DialogResult.OK)
            {
                var state = Simulation.CreateFreeSession(Program.Airport, JsonClone(Program.Options), Program.Settings.SelectedController);

                StartSession(state);
            }
        }

        private void Panel_CursorController_Click(object? sender, EventArgs e)
        {
            MenuFile_SelectController_Click(sender, e);
        }

        private void Form_Start_FormClosing(object? sender, FormClosingEventArgs e)
        {
            try
            {
                Program.Application.SaveSettings();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                e.Cancel = true;
                MessageBox.Show(this, "Could not save your settings. Tower will remain open. " + exception.Message,
                    "Tower", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MenuHelp_CheckUpdates_Click(object? sender, EventArgs e)
        {
            using var dialog = new Form_Update();
            if (dialog.ShowDialog(this) == DialogResult.OK && dialog.InstallerPath is { } installer)
            {
                Program.PendingUpdateInstaller = installer;
                Close();
                if (!IsDisposed)
                {
                    Program.PendingUpdateInstaller = null;
                }
            }
        }

        private void MenuFile_ManageAirports_Click(object? sender, EventArgs e)
        {
            try
            {
                using var dialog = new Form_ManageAirports(TowerSpeech.Core.WorkspacePaths.DefaultRoot);
                dialog.ShowDialog(this);
                if (!dialog.AirportsChanged)
                {
                    return;
                }
                var selected = Program.Airport.Code;
                Program.Application.ReloadAirports();
                var current = Program.Application.AvailableAirports.FirstOrDefault(a => a.Code == selected);
                if (current is not null && current.Runways.Any(r => r.Name.Equals(Program.Options.Runway, StringComparison.OrdinalIgnoreCase)) &&
                    Program.Options.ArrivalRunways.All(name => current.Runways.Any(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase))))
                {
                    Program.Airport = current;
                    Program.Voice.ReloadSpeechWorkspace();
                }
                else
                {
                    Tower.WinForms.Core.SelectAirport(current ?? Program.Application.AvailableAirports[0]);
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or
                System.Text.Json.JsonException or ArgumentException or InvalidOperationException)
            {
                MessageBox.Show(this, exception.Message, "Manage Airports", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Panel_CursorScenario_Click(object? sender, EventArgs e)
        {
            MenuFile_NewScenario_Click(sender, e);
        }

        private void MenuHelp_AboutTower_Click(object sender, EventArgs e)
        {
            Dialogs.ShowAndWait(new Form_About(), "About Tower", this);
        }

        private void MenuFile_ControllerStats_Click(object sender, EventArgs e)
        {
            Dialogs.ShowAndWait(new Form_Performance(), "Controller Statistics", this);
        }

        private void MenuFile_Exit_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void MenuSetup_Lights_Click(object? sender, EventArgs e)
        {
            Dialogs.ShowAndWait(new Form_LightsSetup(), "Lights", this);
        }

        private void MenuSetup_Display_Click(object sender, EventArgs e)
        {
            Dialogs.ShowAndWait(new Form_DisplaySetup(), "Display", this);
        }

        private void MenuFile_NewScenario_Click(object? sender, EventArgs e)
        {
            NewScenario(false);
        }

        private void MenuFile_SelectController_Click(object? sender, EventArgs e)
        {
            Dialogs.ShowAndWait(new Form_ControllerSelect(), "Controllers", this);
        }

        private void MenuFile_OpenScenario_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Tower scenarios (*.tower.json;*.sim)|*.tower.json;*.sim|Saved scenarios (*.tower.json)|*.tower.json|Original scenarios (*.sim)|*.sim"
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    var session = Path.GetExtension(dialog.FileName).Equals(".sim", StringComparison.OrdinalIgnoreCase)
                        ? Program.Application.OpenOriginalScenario(dialog.FileName)
                        : JsonStore.LoadSession(dialog.FileName);
                    StartSession(session, Path.GetExtension(dialog.FileName).Equals(".sim", StringComparison.OrdinalIgnoreCase) ? null : dialog.FileName);
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException or System.Text.Json.JsonException)
                {
                    MessageBox.Show(this, exception.Message, "Open Scenario", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void MenuFile_PracticeScenario_Click(object sender, EventArgs e)
        {
            NewScenario(true);
        }

        private void MenuSetup_ASDE_Click(object? sender, EventArgs e)
        {
            Dialogs.ShowAndWait(new Form_ASDESetup(), "ASDE Setup", this);
        }

        private void MenuSetup_BRITE_Click(object? sender, EventArgs e)
        {
            Dialogs.ShowAndWait(new Form_BRITESetup(), "BRITE Setup", this);
        }

        private void MenuSetup_Sound_Click(object sender, EventArgs e)
        {
            Dialogs.ShowAndWait(new Form_SoundSetup(), "Sound Setup", this);
        }

        private void MenuHelp_Commands_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.tower-sim.com/manual/commands",
                UseShellExecute = true
            });
        }

        private void MenuHelp_Contents_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.tower-sim.com/manual",
                UseShellExecute = true
            });
        }
    }
}
