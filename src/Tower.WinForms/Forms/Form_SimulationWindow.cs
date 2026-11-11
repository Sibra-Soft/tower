using Shared.Utils;
using System.Diagnostics;
using System.Timers;
using Tower.Controls;
using Tower.Core;
using Tower.Forms;
using Tower.Infrastructure;

namespace Tower.WinForms.Forms
{
    public partial class Form_SimulationWindow : Form
    {
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Simulation Simulation
        {
            get;
        }

        private readonly Form_BRITE Brite = new();
        private readonly Form_ASDE ASDE = new();
        private readonly Form_Commands Commands = new();
        private readonly Form_FlightStrips ArrivalStrips = new();
        private readonly Form_FlightStrips DepartureStrips = new();
        private readonly CommandTextBox CommandInput;
        private readonly Form_ButtonBar ButtonBar = new();

        private readonly RadarControl AirRadar;
        private readonly RadarControl GroundRadar;
        private readonly TowerView View;

        private readonly System.Windows.Forms.Timer Timer = new() { Interval = 33 };
        private readonly Stopwatch Clock = new();

        private double last;
        private double uiAccumulator;
        private double timeScale = 1;
        private string Selected = "";
        private string WindowCaption = "";
        private string? scenarioFilePath;
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public PerformanceReward? EarnedReward
        {
            get; private set;
        }

        private readonly Dictionary<string, FlightStripControl> FlightStrips = [];

        public Form_SimulationWindow(Simulation simulation, string? layoutDirectory = null, Action<string>? saveLayoutPreference = null, string? scenarioPath = null)
        {
            InitializeComponent();
            scenarioFilePath = scenarioPath is null ? null : Path.GetFullPath(scenarioPath);
            layoutStore = new(layoutDirectory ?? Path.GetDirectoryName(IniSettingsStore.ResolvePath(AppContext.BaseDirectory))!);
            persistLayoutPreference = saveLayoutPreference ?? (_ => Program.Application.SaveDisplaySettings());

            MenuStrip.Renderer = new MenuRenderer();

            WindowState = FormWindowState.Maximized;

            Simulation = simulation;
            CommandInput = Commands.TextBox_CommandInput;
            Commands.ConfigureInput(Program.Settings.ExpandCommandInput);
            Commands.ExpandCommandsChanged += ExpandCommandsChanged;
            Log("Type <callsign>/<command>, or right-click an aircraft or flight strip to address it.");
            View = new(simulation, Program.Settings);
            View.ViewModeChanged += UpdateViewModeMenu;
            AirRadar = new(simulation, Program.Settings, true);
            GroundRadar = new(simulation, Program.Settings, false);

            Brite.BindRadar(AirRadar, Program.Settings);
            ASDE.BindRadar(GroundRadar, Program.Settings);

            AirRadar.Selected += Select;
            GroundRadar.Selected += Select;

            AirRadar.TrackRequested += a => View.Track(a.CallSign);
            GroundRadar.TrackRequested += a => View.Track(a.CallSign);

            Commands.Text = "Commands — LCL/GND";
            ArrivalStrips.Text = "Flight Strips — Arrivals";
            DepartureStrips.Text = "Flight Strips — Departures";

            ButtonBar.ButtonClicked += ButtonBar_ButtonClicked;
            Simulation.RadioMessage += Program.Voice.Readback;
            Simulation.Message += Log;

            var input = CommandInput;

            AirRadar.CommandTargetRequested += AddressAircraft;
            GroundRadar.CommandTargetRequested += AddressAircraft;

            input.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    input.Clear();

                    e.SuppressKeyPress = true;
                }

                if (e.KeyCode == Keys.Enter)
                {
                    Execute(input.RawText);

                    input.Clear();

                    e.SuppressKeyPress = true;
                }
            };

            Load += Form_SimulationWindow_Load;
            Shown += Form_SimulationWindow_Shown;
            FormClosing += Form_SimulationWindow_FormClosing;
            FormClosed += Form_SimulationWindow_FormClosed;
            Timer.Tick += Timer_Tick;

            Controls.Add(View);

            View.BringToFront();
            UpdateViewModeMenu();
        }

        private void Menu_3DView_Normal_Click(object? sender, EventArgs e) => SetViewMode(TowerViewMode.Normal);
        private void Menu_3DView_Tracking_Click(object? sender, EventArgs e) => SetViewMode(TowerViewMode.Tracking);
        private void Menu_3DView_Binocular_Click(object? sender, EventArgs e) => SetViewMode(TowerViewMode.Binocular);
        private void Menu_3DView_SuperBinocular_Click(object? sender, EventArgs e) => SetViewMode(TowerViewMode.SuperBinocular);
        private void Menu_3DView_SpotPlane_Click(object? sender, EventArgs e) => SetViewMode(TowerViewMode.SpotPlane);
        private void Menu_3DView_Pilot_Click(object? sender, EventArgs e) => SetViewMode(TowerViewMode.Pilot);

        private void SetViewMode(TowerViewMode mode)
        {
            if (!View.SetViewMode(mode, Selected))
            {
                MessageBox.Show(this, "Select an aircraft before choosing this view.", "3D View", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            UpdateViewModeMenu();
        }

        private void UpdateViewModeMenu()
        {
            Menu_3DView_Normal.Checked = View.ViewMode == TowerViewMode.Normal;
            Menu_3DView_Tracking.Checked = View.ViewMode == TowerViewMode.Tracking;
            Menu_3DView_Binocular.Checked = View.ViewMode == TowerViewMode.Binocular;
            Menu_3DView_SuperBinocular.Checked = View.ViewMode == TowerViewMode.SuperBinocular;
            Menu_3DView_SpotPlane.Checked = View.ViewMode == TowerViewMode.SpotPlane;
            Menu_3DView_Pilot.Checked = View.ViewMode == TowerViewMode.Pilot;
            Menu_3DView_ZoomIn.Enabled = View.CanZoom;
            Menu_3DView_ZoomOut.Enabled = View.CanZoom;
        }

        private void Menu_3DView_PanLeft_Click(object? sender, EventArgs e) => View.Pan(-10);
        private void Menu_3DView_PanRight_Click(object? sender, EventArgs e) => View.Pan(10);
        private void Menu_3DView_ZoomIn_Click(object? sender, EventArgs e) => View.Zoom(true);
        private void Menu_3DView_ZoomOut_Click(object? sender, EventArgs e) => View.Zoom(false);

        private void ButtonBar_ButtonClicked(object? sender, string e)
        {
            switch (e)
            {
                case "RADIO":
                    try
                    {
                        Program.Application.SetSoundTalk(!Program.Settings.SoundTalk);
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        MessageBox.Show(this, "Could not save the talk setting. " + exception.Message,
                            "Sound Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    break;

                case "LIGHTS":
                    Menu_Display_Lights_Click(sender, new EventArgs());
                    break;

                case "COMPASS":
                    Menu_Display_Compass_Click(sender, new EventArgs());
                    break;

                case "PERKS":
                    Menu_File_Performance_Click(sender, new EventArgs());
                    break;

                case "BRITE":
                    Menu_Display_BRITE_Click(sender, new EventArgs());
                    break;

                case "ASDE":
                    Menu_Display_ASDE_Click(sender, new EventArgs());
                    break;

                case "COFFEE":
                    Menu_Simulation_Pause_Click(sender, new EventArgs());
                    break;

                case "STRIPS":
                    Menu_Display_FlightStrips_Click(sender, new EventArgs());
                    break;

                case "EXIT":
                    Menu_File_End_Click(sender, new EventArgs());
                    break;
            }
        }

        private void Execute(string text)
        {
            text = text.Trim();
            if (text.Length == 0)
            {
                return;
            }

            var result = Simulation.Execute(text, Selected);

            if (!result.Success && (result.Message.StartsWith("Unknown callsign", StringComparison.Ordinal) || result.Message.StartsWith("Ambiguous callsign", StringComparison.Ordinal) || result.Message.StartsWith("Use:", StringComparison.Ordinal)))
            {
                Log(result.Message);
            }

            RefreshStrips();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            var now = Clock.Elapsed.TotalSeconds;
            var elapsed = now - last;

            last = now;

            if (WindowState == FormWindowState.Minimized)
            {
                return;
            }

            try
            {
                Simulation.Advance(Math.Min(elapsed, 1) * timeScale);
            }
            catch (Exception exc) when (exc is InvalidDataException or ArgumentOutOfRangeException)
            {
                Simulation.Paused = true;
                Log(exc.Message);
            }

            View.Invalidate();

            uiAccumulator += elapsed;

            // Display cadence uses wall-clock time, independently of simulation speed.
            if (uiAccumulator < 5)
            {
                return;
            }

            uiAccumulator %= 5;

            AirRadar.Invalidate();
            GroundRadar.Invalidate();
            RefreshStrips();

            var time = TimeSpan.FromMinutes(Simulation.State.Options.StartMinutes) + TimeSpan.FromSeconds(Simulation.State.ElapsedSeconds);

            StatusLabel_Status.Text = $"{time:hh\\:mm\\:ss}  {(Simulation.Paused ? "PAUSED" : timeScale + "×")}  |  {(Simulation.State.Options.Practice ? "Practice" : "Rated")}  |  Rating {Simulation.State.Performance.Rating(Simulation.State)}";

            if (Simulation.Paused)
            {
                Text = WindowCaption + " - [Paused]";
            }
            else
            {
                Text = WindowCaption;
            }

            Debug.Print(Program.Voice.Status);
        }

        private void Form_SimulationWindow_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (e.Cancel)
            {
                return;
            }

            var timerWasRunning = Timer.Enabled;

            Timer.Stop();

            var answer = MessageBox.Show(
                this,
                "Are you sure?",
                "Tower",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2
            );

            e.Cancel = answer != DialogResult.Yes;

            if (!e.Cancel && layoutReady)
            {
                try
                {
                    Menu_File_Performance_Click(sender, e);

                    layoutStore.Write(currentLayoutName, CaptureWindowLayout());

                    RememberDefaultLayout(currentLayoutName);
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
                {
                    MessageBox.Show(this, "Could not save the window layout. " + exception.Message, "Window Layout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    e.Cancel = true;
                }
            }
            if (!e.Cancel)
            {
                try
                {
                    EarnedReward = Program.Application.RecordPerformance(Simulation.State);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    MessageBox.Show(this, "Could not save the performance review. " + exception.Message,
                        "Performance Review", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    e.Cancel = true;
                }
            }
            if (e.Cancel && timerWasRunning)
            {
                last = Clock.Elapsed.TotalSeconds;
                Timer.Start();
            }
        }

        private void Form_SimulationWindow_FormClosed(object? sender, FormClosedEventArgs e)
        {
            Timer.Stop();
            Clock.Stop();
            Simulation.Paused = true;
            Timer.Dispose();

            Simulation.Message -= Log;
            Commands.ExpandCommandsChanged -= ExpandCommandsChanged;
            Simulation.RadioMessage -= Program.Voice.Readback;

            Program.Voice.ClearPending();

            foreach (var form in new Form[] { Brite, ASDE, Commands, ArrivalStrips, DepartureStrips, ButtonBar })
            {
                form.Dispose();
            }
        }

        private void Form_SimulationWindow_Shown(object? sender, EventArgs e)
        {
            PositionWindows();
            ApplyDisplay();

            Commands.Show(this);
            LoadDefaultWindowLayout();

            RefreshStrips();

            Clock.Start();
            Timer.Start();

            CommandInput.Focus();
        }

        private void Form_SimulationWindow_Load(object? sender, EventArgs e)
        {
            WindowCaption = $"Tower — {Simulation.State.Airport.Name}";
            Text = WindowCaption;
        }

        private void Menu_File_Save_Click(object? sender, EventArgs e) => SaveScenario(false);

        private void Menu_File_SaveAs_Click(object? sender, EventArgs e) => SaveScenario(true);

        private void SaveScenario(bool saveAs)
        {
            var running = Timer.Enabled;
            Timer.Stop();
            try
            {
                var path = scenarioFilePath;
                if (saveAs || path is null)
                {
                    using var dialog = new SaveFileDialog
                    {
                        Title = "Save Scenario",
                        Filter = "Tower scenarios (*.tower.json)|*.tower.json",
                        DefaultExt = "tower.json",
                        AddExtension = true,
                        OverwritePrompt = true,
                        FileName = path is null ? $"{Simulation.State.Airport.Code}-{DateTime.Now:yyyyMMdd-HHmm}.tower.json" : Path.GetFileName(path)
                    };
                    if (path is not null)
                    {
                        dialog.InitialDirectory = Path.GetDirectoryName(path);
                    }
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }
                    path = dialog.FileName;
                }
                JsonStore.Write(path, Simulation.State);
                scenarioFilePath = Path.GetFullPath(path);
                Log("Scenario saved: " + scenarioFilePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                System.Text.Json.JsonException or ArgumentException or InvalidOperationException or NotSupportedException)
            {
                MessageBox.Show(this, "The scenario could not be saved. " + exception.Message, "Save Scenario", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                last = Clock.Elapsed.TotalSeconds;
                if (running)
                {
                    Timer.Start();
                }
            }
        }

        private void Menu_File_End_Click(object? sender, EventArgs e)
        {
            Close();
        }

        private void Select(Aircraft a)
        {
            Selected = a.CallSign;
            View.SelectAircraft(a.CallSign);
            AirRadar.SelectedCallsign = Selected;
            GroundRadar.SelectedCallsign = Selected;
            Commands.Text = $"Commands — {Selected}";
            RefreshStrips();
        }

        private void AddressAircraft(Aircraft aircraft)
        {
            Select(aircraft);
            CommandInput.Text = aircraft.CallSign + "/";
            CommandInput.SelectionStart = CommandInput.TextLength;
            if (!Commands.Visible)
            {
                Commands.Show(this);
            }

            Commands.Activate();
            CommandInput.Focus();
        }

        private void RefreshStrips()
        {
            var active = Simulation.State.Aircraft.Where(a => a.IsOnFrequency && a.Phase != FlightPhase.Completed).ToList();

            if (Selected.Length > 0 && active.All(a => a.CallSign != Selected))
            {
                Selected = "";
                AirRadar.SelectedCallsign = "";
                GroundRadar.SelectedCallsign = "";
                Commands.Text = "Commands";
            }

            foreach (var dead in FlightStrips.Keys.Where(k => active.All(a => a.CallSign != k || !ReferenceEquals(a, FlightStrips[k].Aircraft))).ToList())
            {
                FlightStrips[dead].Dispose();
                FlightStrips.Remove(dead);
            }
            foreach (var a in active)
            {
                if (!FlightStrips.TryGetValue(a.CallSign, out var strip))
                {
                    strip = new FlightStripControl
                    {
                        Aircraft = a,
                        Width = 289,
                        Height = 37,
                        Margin = new Padding(3),
                        Visible = true,
                    };

                    strip.MouseDown += (_, e) =>
                    {
                        if (e.Button == MouseButtons.Right)
                        {
                            AddressAircraft(a);
                        }
                        else
                        {
                            Select(a);
                        }
                    };
                    strip.DoubleClick += (_, _) => View.Track(a.CallSign);

                    FlightStrips.Add(a.CallSign, strip);

                    var window = a.Direction == FlightDirection.Arrival ? ArrivalStrips : DepartureStrips;
                    window.FlowLayoutPanel_StripList.Controls.Add(strip);
                }

                strip.Selected = a.CallSign == Selected;
                strip.Invalidate();
            }
        }

        /// <summary>
        /// Show the forms that are set to show using the saved program settings
        /// </summary>
        public void ApplyDisplay()
        {
            void Show(Form form, bool visible)
            {
                if (visible)
                {
                    if (form.Visible)
                    {

                    }
                    else
                    {
                        form.Show(this);
                    }
                }
                else
                {
                    form.Hide();
                }
            }

            Show(Brite, Program.Settings.ShowBrite);
            Show(ASDE, Program.Settings.ShowAsde);
            Show(ArrivalStrips, Program.Settings.ShowStrips);
            Show(DepartureStrips, Program.Settings.ShowStrips);
            Show(ButtonBar, Program.Settings.ShowButtons);

            Menu_Display_BRITE.Checked = Program.Settings.ShowBrite;
            Menu_Display_ASDE.Checked = Program.Settings.ShowAsde;
            Menu_Display_FlightStrips.Checked = Program.Settings.ShowStrips;
            Menu_Display_ButtonBar.Checked = Program.Settings.ShowButtons;
            Menu_Display_Compass.Checked = Program.Settings.ShowCompass;
            Menu_Display_Gauges.Checked = Program.Settings.ShowGauges;
            Menu_Display_AircraftLabels.Checked = Program.Settings.ShowAircraftLabels;

            View.Invalidate();
        }

        /// <summary>
        /// Position the windows within the screen
        /// </summary>
        private void PositionWindows()
        {
            var area = Screen.FromControl(this).WorkingArea;

            foreach (var f in new Form[] { Brite, ASDE, ArrivalStrips, DepartureStrips, Commands, ButtonBar })
            {
                f.StartPosition = FormStartPosition.Manual;
            }

            Brite.Bounds = new(area.Left + 12, area.Top + 110, Math.Min(605, area.Width / 2 - 20), Math.Min(410, area.Height - 280));
            ASDE.Bounds = new(area.Right - Math.Min(543, area.Width / 2 - 20) - 12, area.Top + 110, Math.Min(543, area.Width / 2 - 20), Math.Min(420, area.Height - 280));
            var stripWidth = Math.Min(360, Math.Max(300, (area.Width - 48) / 3));
            Commands.Bounds = new(area.Left + 12, area.Bottom - 230, Math.Min(600, Math.Max(220, area.Width - stripWidth * 2 - 48)), 200);
            ArrivalStrips.Bounds = new(area.Right - stripWidth * 2 - 24, area.Bottom - 300, stripWidth, 270);
            DepartureStrips.Bounds = new(area.Right - stripWidth - 12, area.Bottom - 300, stripWidth, 270);
            ButtonBar.Location = new(area.Right - ButtonBar.Width - 12, area.Top + 40);
        }

        private void ExpandCommandsChanged(bool expanded)
        {
            Program.Settings.ExpandCommandInput = expanded;
            try
            {
                Program.Application.SaveDisplaySettings();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(this, "The command input setting could not be saved. " + exception.Message,
                    "Commands", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Add a new log message to the command window
        /// </summary>
        /// <param name="text">The message to add to the screen</param>
        private void Log(string text)
        {
            var log = Commands.TextBox_Log;

            if (log.TextLength > 30000)
            {
                log.Text = log.Text[^15000..];
            }

            log.AppendText($"{TimeSpan.FromSeconds(Simulation.State.ElapsedSeconds):hh\\:mm\\:ss}  {text}{Environment.NewLine}");
        }

        private void Menu_Simulation_SpeedUp_Click(object sender, EventArgs e)
        {
            timeScale += 1;
        }

        private void Menu_Simulation_SlowDown_Click(object sender, EventArgs e)
        {
            timeScale -= 1;
        }

        private void Menu_Simulation_NormalSpeed_Click(object sender, EventArgs e)
        {
            timeScale = 1;
        }

        private void Menu_Simulation_Pause_Click(object? sender, EventArgs e)
        {
            Simulation.Paused = !Simulation.Paused;
        }

        private void Menu_Help_About_Click(object sender, EventArgs e)
        {
            Dialogs.ShowAndWait(new Form_About(), "About Tower", this);
        }

        private void Menu_Simulation_Sound_Click(object sender, EventArgs e)
        {
            Dialogs.ShowAndWait(new Form_SoundSetup(), "Setup Sound", this);
        }

        private void Menu_File_Performance_Click(object? sender, EventArgs e)
        {
            using var dialog = new Form_Performance();

            dialog.Configure(Simulation.State, Program.Profiles.FirstOrDefault(profile => profile.Id == Simulation.State.ControllerId));

            ShowLayoutDialog(dialog);
        }

        private void Menu_Display_FlightStrips_Click(object? sender, EventArgs e)
        {
            Program.Settings.ShowStrips = !Menu_Display_FlightStrips.Checked;
            ApplyDisplay();
        }

        private void Menu_Display_ButtonBar_Click(object sender, EventArgs e)
        {
            Program.Settings.ShowButtons = !Menu_Display_ButtonBar.Checked;
            ApplyDisplay();
        }

        private void Menu_Display_ASDE_Click(object? sender, EventArgs e)
        {
            Program.Settings.ShowAsde = !Menu_Display_ASDE.Checked;
            ApplyDisplay();
        }

        private void Menu_Display_BRITE_Click(object? sender, EventArgs e)
        {
            Program.Settings.ShowBrite = !Menu_Display_BRITE.Checked;
            ApplyDisplay();
        }

        private void Menu_Display_Lights_Click(object? sender, EventArgs e)
        {
            Dialogs.ShowAndWait(new Form_LightsSetup(), "Light Setup", this);
        }

        private void Menu_Display_Compass_Click(object? sender, EventArgs e)
        {
            Program.Settings.ShowCompass = !Menu_Display_Compass.Checked;
            ApplyDisplay();
        }

        private void Menu_Display_Gauges_Click(object sender, EventArgs e)
        {
            Program.Settings.ShowGauges = !Menu_Display_Gauges.Checked;
            ApplyDisplay();
        }

        private void Menu_Display_AircraftLabels_Click(object? sender, EventArgs e)
        {
            Program.Settings.ShowAircraftLabels = !Program.Settings.ShowAircraftLabels;
            ApplyDisplay();
            try
            {
                Program.Application.SaveDisplaySettings();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(this, "Could not save the display settings. " + exception.Message,
                    "Display Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Menu_Simulation_DeleteAircraft_Click(object sender, EventArgs e)
        {
            var aircraft = Simulation.State.Aircraft.FirstOrDefault(a => a.CallSign == Selected && a.IsOnFrequency && a.Phase != FlightPhase.Completed);

            if (aircraft is null)
            {
                MessageBox.Show(this, "Select an aircraft first.", "Delete Aircraft", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var wasPaused = Simulation.Paused;
            DialogResult answer;
            Simulation.Paused = true;

            try
            {
                answer = MessageBox.Show(this, $"Delete aircraft {aircraft.CallSign} from the simulation?",
                    "Delete Aircraft", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            }
            finally
            {
                Simulation.Paused = wasPaused;
            }

            if (answer != DialogResult.Yes || !Simulation.DeleteAircraft(aircraft))
            {
                return;
            }

            View.StopTracking(aircraft.CallSign);
            CommandInput.Clear();
            RefreshStrips();
            AirRadar.Refresh();
            GroundRadar.Refresh();
            View.Refresh();
        }
        private readonly WindowLayoutStore layoutStore;
        private readonly Action<string> persistLayoutPreference;
        private bool layoutReady;
        private WindowLayout? currentLayout;
        private string currentLayoutName = "";

        private void LoadDefaultWindowLayout()
        {
            var name = Program.Settings.DefaultWindowLayout;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = WindowLayoutStore.DefaultFileName;
            }
            try
            {
                if (!name.Equals(WindowLayoutStore.DefaultFileName, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        ApplyWindowLayout(layoutStore.Read(name));
                        currentLayoutName = Path.GetFileName(layoutStore.FilePath(name));
                        layoutReady = true;
                        return;
                    }
                    catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
                    {
                        Log("Could not restore the selected layout; using default.set. " + exception.Message);
                    }
                }
                ApplyWindowLayout(layoutStore.ReadOrCreateDefault(CaptureWindowLayout()));
                currentLayoutName = WindowLayoutStore.DefaultFileName;
                layoutReady = true;
                RememberDefaultLayout(currentLayoutName);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
            {
                Log("Could not restore the default layout. " + exception.Message);
            }
        }

        private void SaveWindowLayout_Click(object? sender, EventArgs e)
        {
            using var dialog = new Form_SaveSettings();
            dialog.Configure(layoutStore, CaptureWindowLayout(), currentLayoutName);
            if (ShowLayoutDialog(dialog) == DialogResult.OK && dialog.MakeDefault)
            {
                currentLayoutName = dialog.SavedFileName;
                layoutReady = true;
                RememberDefaultLayout(currentLayoutName);
            }
        }

        private DialogResult ShowLayoutDialog(Form dialog)
        {
            var running = Timer.Enabled;
            Timer.Stop();
            try
            {
                return dialog.ShowDialog(this);
            }
            finally
            {
                last = Clock.Elapsed.TotalSeconds;
                if (running)
                {
                    Timer.Start();
                }
            }
        }

        private void RestoreWindowLayout_Click(object? sender, EventArgs e)
        {
            using var dialog = new Form_RestoreSettings();

            dialog.Configure(layoutStore, currentLayoutName);

            if (ShowLayoutDialog(dialog) != DialogResult.OK)
            {
                return;
            }

            if (dialog.RestoreDefault)
            {
                try
                {
                    ApplyWindowLayout(layoutStore.ReadOrCreateDefault(CaptureWindowLayout()));
                    currentLayoutName = WindowLayoutStore.DefaultFileName;
                    layoutReady = true;
                    RememberDefaultLayout(currentLayoutName);
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
                {
                    MessageBox.Show(this, "Could not restore default.set. " + exception.Message,
                        "Window Layout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else if (dialog.SelectedLayout is { } layout)
            {
                ApplyWindowLayout(layout);
                currentLayoutName = dialog.SelectedFileName;
                layoutReady = true;
                RememberDefaultLayout(currentLayoutName);
            }
        }

        private void RememberDefaultLayout(string name)
        {
            Program.Settings.DefaultWindowLayout = name;
            persistLayoutPreference(name);
        }

        private WindowLayout CaptureWindowLayout()
        {
            var layout = new WindowLayout { ScreenCoordinates = true };

            if (currentLayout is { } previous)
            {
                foreach (var (key, value) in previous.Windows)
                {
                    layout.Windows[key] = value;
                }
            }

            void Capture(string key, Form form, bool includeVisibility = true)
            {
                var bounds = form.WindowState == FormWindowState.Normal ? form.Bounds : form.RestoreBounds;
                layout.Set(key, new(bounds.X, bounds.Y, bounds.Width, bounds.Height,
                    includeVisibility ? form.Visible : null, form.WindowState == FormWindowState.Maximized ? "Maximized" : "Normal"));
            }

            Capture("Operational", this, false);
            Capture("ASDE", ASDE);
            Capture("BRITE", Brite);
            Capture("ArrivalStrips", ArrivalStrips);
            Capture("DepartureStrips", DepartureStrips);
            Capture("Buttons", ButtonBar);
            Capture("Comm", Commands);

            layout.Set("3D", new(View.Left, View.Top, View.Width, View.Height));
            layout.Windows["FlightStrips"] = Program.Settings.ShowStrips ? "On" : "Off";

            // Preserve original gauge/compass coordinates even though these are not separate forms yet.
            void SetVisibility(string key, bool visible)
            {
                var suffix = layout.Windows.TryGetValue(key, out var value) && value.Contains(',') ? value[value.IndexOf(',')..] : "";
                layout.Windows[key] = (visible ? "On" : "Off") + suffix;
            }

            SetVisibility("Compass", Program.Settings.ShowCompass);
            SetVisibility("Gauges", Program.Settings.ShowGauges);
            SetVisibility("AircraftLabels", Program.Settings.ShowAircraftLabels);

            return layout;
        }

        private static Rectangle VisibleWindowBounds(Rectangle requested, Size minimum)
        {
            var area = Screen.FromRectangle(requested).WorkingArea;
            var width = Math.Clamp(requested.Width, Math.Min(minimum.Width, area.Width), area.Width);
            var height = Math.Clamp(requested.Height, Math.Min(minimum.Height, area.Height), area.Height);

            return new(Math.Clamp(requested.X, area.Left, area.Right - width), Math.Clamp(requested.Y, area.Top, area.Bottom - height), width, height);
        }

        private void ApplyWindowLayout(WindowLayout layout)
        {
            View.Dock = DockStyle.Fill;
            // The original Operational window was a toolbar. It cannot size our combined
            // simulation/TowerView form, so legacy files retain the modern maximized host.
            var area = Screen.FromControl(this).WorkingArea;

            if (layout.ScreenCoordinates && layout.Placement("Operational") is { } host)
            {
                WindowState = FormWindowState.Normal;
                StartPosition = FormStartPosition.Manual;
                Bounds = VisibleWindowBounds(new(host.X, host.Y, host.Width ?? Width, host.Height ?? Height), new(320, 240));

                if (host.State == "Maximized")
                {
                    WindowState = FormWindowState.Maximized;
                }
            }
            else if (!layout.ScreenCoordinates)
            {
                WindowState = FormWindowState.Maximized;
            }

            foreach (var (key, form) in new (string, Form)[] { ("ASDE", ASDE), ("BRITE", Brite),
                ("ArrivalStrips", ArrivalStrips), ("DepartureStrips", DepartureStrips), ("Buttons", ButtonBar), ("Comm", Commands) })
            {
                if (layout.Placement(key) is not { } placement)
                {
                    continue;
                }

                form.StartPosition = FormStartPosition.Manual;
                form.WindowState = FormWindowState.Normal;

                var offset = layout.ScreenCoordinates ? Point.Empty : area.Location;
                // The button bar fits its fixed button column; old saved widths may
                // contain the generic 100-pixel minimum and must not stretch it.
                var buttonBar = form == ButtonBar;
                var size = buttonBar ? form.Size : new Size(placement.Width ?? form.Width, placement.Height ?? form.Height);
                form.Bounds = VisibleWindowBounds(new(placement.X + offset.X, placement.Y + offset.Y, size.Width, size.Height),
                    buttonBar ? form.Size : new Size(100, 60));
            }

            Program.Settings.ShowAsde = layout.Visibility("ASDE") ?? Program.Settings.ShowAsde;
            Program.Settings.ShowBrite = layout.Visibility("BRITE") ?? Program.Settings.ShowBrite;
            Program.Settings.ShowStrips = layout.Visibility("FlightStrips") ?? Program.Settings.ShowStrips;
            Program.Settings.ShowButtons = layout.Visibility("Buttons") ?? Program.Settings.ShowButtons;
            Program.Settings.ShowCompass = layout.Visibility("Compass") ?? Program.Settings.ShowCompass;
            Program.Settings.ShowGauges = layout.Visibility("Gauges") ?? Program.Settings.ShowGauges;
            Program.Settings.ShowAircraftLabels = layout.Visibility("AircraftLabels") ?? Program.Settings.ShowAircraftLabels;

            ApplyDisplay();

            foreach (var (key, form) in new (string, Form)[] { ("ArrivalStrips", ArrivalStrips), ("DepartureStrips", DepartureStrips), ("Comm", Commands) })
            {
                var visible = layout.Visibility(key) ?? (key == "Comm" || Program.Settings.ShowStrips);

                if (visible)
                {
                    if (!form.Visible)
                    {
                        form.Show(this);
                    }
                }
                else
                {
                    form.Hide();
                }
            }

            // A saved bitmap-sized viewport must never constrain a polygon airport.
            if (View.HasPanorama && layout.Placement("3D") is { Width: { } width, Height: { } height } view)
            {
                View.Dock = DockStyle.None;

                var left = Math.Clamp(view.X, 0, Math.Max(0, ClientSize.Width - 1));
                var top = Math.Clamp(view.Y, MenuStrip.Bottom, Math.Max(MenuStrip.Bottom, ClientSize.Height - StatusStrip.Height - 1));

                View.Bounds = new(left, top, Math.Min(width, Math.Max(1, ClientSize.Width - left)), Math.Min(height, Math.Max(1, ClientSize.Height - StatusStrip.Height - top)));
            }

            currentLayout = layout;
        }

        private void Menu_Help_Contents_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.tower-sim.com/manual",
                UseShellExecute = true
            });
        }

        private void Menu_Help_Commands_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.tower-sim.com/manual/commands",
                UseShellExecute = true
            });
        }
    }
}
