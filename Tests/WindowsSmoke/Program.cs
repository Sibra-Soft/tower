using System.Reflection;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using Tower.Controls;
using Tower.Core;
using Tower.Infrastructure;
using Tower.WinForms.Forms;
using TowerSpeech.Core;

internal static class Program
{
    private static string TestData => Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA") ?? WorkspacePaths.DefaultRoot;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string Output = Path.GetFullPath(Environment.GetEnvironmentVariable("TOWER_SMOKE_OUTPUT") ?? "obj/WindowsSmoke");
    private static readonly List<string> Report = [];

    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Application.EnableVisualStyles();
        Directory.CreateDirectory(Output);
        try
        {
            if (args.Contains("--updates-only"))
            {
                CheckUpdates();
                Console.WriteLine("PASS designer update menu/dialog, release notes, verified download/consent, unavailable/offline/cancel and deferred installer command.");
                return 0;
            }
            if (args.Contains("--aircraft-animation-only"))
            {
                CheckAircraftAnimationViews();
                Console.WriteLine("PASS all 44 models, retractable/fixed gear views, partial extension and landing light visibility.");
                return 0;
            }
            if (args.Contains("--wind-only"))
            {
                CheckWindViews();
                Console.WriteLine("PASS calm/headwind/crosswind views, ground track/crab and wind save/resume.");
                return 0;
            }
            if (args.Contains("--aircraft-status-only"))
            {
                CheckAircraftStatusTooltips();
                Console.WriteLine("PASS ASDE/BRITE/strip status hover, live updates, empty space, mouse leave, completion and disposal.");
                return 0;
            }
            if (args.Contains("--ground-handling-only"))
            {
                CheckGroundHandlingViews();
                Console.WriteLine("PASS Canyon simultaneous pushback, departure queue, runway access and arrival-to-parking views.");
                return 0;
            }
            if (args.Contains("--pushback-routing-only"))
            {
                CheckPushbackRoutingViews();
                Console.WriteLine("PASS Canyon pushback/taxi views on both sides of the gates and runways 9/27.");
                return 0;
            }
            if (args.Contains("--talk-only"))
            {
                CheckTalkSettings();
                Console.WriteLine("PASS Talk default, Sound Setup save/cancel, RADIO toggle, INI persistence and muted readback.");
                return 0;
            }
            if (args.Contains("--flight-dynamics-only"))
            {
                CheckFlightDynamics();
                Console.WriteLine("PASS aircraft pitch/bank, arrival/flare/rollout views and motion captures.");
                return 0;
            }
            if (args.Contains("--command-input-only"))
            {
                CheckCommandInput();
                Console.WriteLine("PASS expanded command input, Backspace, paste, menu/designer, initial radio logs, readbacks and raw command execution.");
                return 0;
            }
            if (args.Contains("--ci-only"))
            {
                CheckScenarioSaving();
                CheckBriteAppearance();
                CheckAsdeLabels();
                CheckCommandInput();
                CheckLayoutPersistence();
                CheckUpdates();
                Console.WriteLine("PASS CI Windows scenario save, radar controls, command input, radio logs, polygon layout resizing and offline update checks.");
                return 0;
            }
            if (args.Contains("--brite-map-only"))
            {
                CheckBriteMap();
                Console.WriteLine("PASS CYN original radar maps, symbols, layer switches, zoom/center, missing MAP and save/reload.");
                return 0;
            }
            if (args.Contains("--scenario-save-only"))
            {
                CheckScenarioSaving();
                Console.WriteLine("PASS Save Scenario menu, repeated save, session reload, route/clearance/label state and timer/pause preservation.");
                return 0;
            }
            if (args.Contains("--airport-manager-only"))
            {
                CheckAirportManager();
                Console.WriteLine("PASS airport manager designer, version/date display, Canyon protection, busy close, remove and reinstall refresh.");
                return 0;
            }
            if (args.Contains("--panorama-only"))
            {
                CheckPanoramaViewport();
                Console.WriteLine("PASS panorama viewport sizing, source scale, camera projection, zoom, night, resize and polygon fallback.");
                return 0;
            }
            if (args.Contains("--radar-labels-only"))
            {
                CheckBriteAppearance();
                CheckAsdeLabels();
                Console.WriteLine("PASS ASDE and BRITE label rotation, independent save/load, command targeting and ASDE map dragging.");
                return 0;
            }
            if (args.Contains("--brite-only"))
            {
                CheckBriteAppearance();
                Console.WriteLine("PASS BRITE T targets, 360-degree label rotation, tracking, save/resume and command targeting.");
                return 0;
            }
            if (args.Contains("--free-traffic-only"))
            {
                CheckFreeTraffic();
                Console.WriteLine("PASS free traffic, VFR entry labels, radar and strips on CYN.");
                return 0;
            }
            if (args.Contains("--performance-only"))
            {
                CheckPerformanceReview();
                Console.WriteLine("PASS Performance Review categories, difficulty, rating, practice status and reward icon/text.");
                return 0;
            }
            if (args.Contains("--performance-close-only"))
            {
                CheckPerformanceClose();
                Console.WriteLine("PASS real Start -> Simulation -> No/Yes close -> persisted result -> modal reward, practice/error/replay exclusions.");
                return 0;
            }
            if (args.Contains("--layouts-only"))
            {
                CheckLayoutPersistence();
                Console.WriteLine("PASS layout creation, cancelled close, autosave, restart and selected layout restoration.");
                return 0;
            }
            var data = TestData;
            IReadOnlyList<string> airports = File.Exists(Path.Combine(data, "CYN", "CYN.TWR")) ? ["CYN"] : [];
            Report.Add("Game data: " + data);
            if (airports.Count == 0)
            {
                CheckWindow(Airport.Training(), "18");
                Report.Add("LIMITATION: original Data unavailable; only the training UI was checked.");
            }
            foreach (var code in airports)
            {
                var airport = OriginalData.LoadAirport(data, code);
                var scenery = AirportScenery.Read(data, airport);
                Require(scenery.Errors.Count == 0, code + " scenery failed to load.");
                Require(code == "CYN" ? scenery.Objects.Count == 10 : scenery.Panorama is not null, code + " scenery or panorama is missing.");
                var runways = OriginalData.ReadDefaultRunways(Path.Combine(Path.GetDirectoryName(data)!, "settings.ini"), airport);
                CheckWindow(airport, runways[0]);
            }
            if (airports.Count > 0)
            {
                var models = new AircraftModelCatalog(data);
                Require(models.Types.Count == 44, "The original aircraft model catalogue is incomplete.");
                foreach (var type in models.Types)
                {
                    Require(models.Get(type) is not null, "Aircraft model failed to decode: " + type);
                }
                Report.Add("PASS all 44 aircraft models and original airport scenery/panoramas loaded.");
            }
            CheckSpeech(data, airports);
            File.WriteAllLines(Path.Combine(Output, "report.txt"), Report);
            Console.WriteLine(string.Join(Environment.NewLine, Report));
            return 0;
        }
        catch (Exception exception)
        {
            File.WriteAllText(Path.Combine(Output, "failure.txt"), exception.ToString());
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private)!.GetValue(target)!;

    private static void CheckFreeTraffic()
    {
        var data = TestData;
        foreach (var (code, runway) in new[] { ("CYN", "9") })
        {
            var airport = OriginalData.LoadAirport(data, code);
            var simulation = new Simulation(Simulation.CreateFreeSession(airport,
                new() { Runway = runway, ArrivalRunways = [runway], AircraftPerMinute = 1 }, seed: 42));
            simulation.StartFreeTraffic();
            var local = simulation.State.Aircraft.FirstOrDefault(aircraft => aircraft.Direction == FlightDirection.Arrival)
                ?? simulation.AddAircraft(FlightDirection.Arrival, "VOTER51", "C130",
                    freeTrafficFlight: new() { Task = FreeTrafficTask.Arrival });
            using var window = new Form_SimulationWindow(simulation, Path.Combine(Output, "Layouts", code), _ => { })
            {
                Opacity = 0,
                ShowInTaskbar = false
            };
            foreach (var name in new[] { "Brite", "ASDE", "Commands", "ArrivalStrips", "DepartureStrips", "ButtonBar" })
            {
                var child = Field<Form>(window, name);
                child.Opacity = 0;
                child.ShowInTaskbar = false;
            }
            window.Show();
            Application.DoEvents();
            Field<System.Windows.Forms.Timer>(window, "Timer").Stop();
            Call(window, "RefreshStrips");
            var arrivals = Field<Form_FlightStrips>(window, "ArrivalStrips");
            Require(arrivals.FlowLayoutPanel_StripList.Controls.OfType<FlightStripControl>().Any(strip => strip.Aircraft == local),
                code + " is missing the arrival strip.");
            Require(!local.FreeTraffic!.IsVfr && !local.LandingCleared, "Traffic received an automatic clearance.");
            using var strip = new FlightStripControl { Aircraft = local, Size = new(410, 60) };
            Render(strip, code + "-Arrival-Strip");
            Render(Field<RadarControl>(window, "AirRadar"), code + "-FreeTraffic-BRITE");
            Render(arrivals, code + "-FreeTraffic-Arrivals");
            AnswerDialog(window.Close, 6);
        }
    }

    private sealed class UpdateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request, cancellationToken);
    }

    private static void CheckUpdates()
    {
        using var start = new Form_Start();
        Require(Field<ToolStripMenuItem>(start, "MenuHelp_CheckUpdates").Text?.Contains("Updates") == true, "Update menu is missing from the designer.");
        static void Wait(Func<bool> complete)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!complete())
            {
                if (clock.Elapsed.TotalSeconds > 5)
                {
                    throw new TimeoutException("Update UI did not complete.");
                }
                Application.DoEvents();
                Thread.Sleep(10);
            }
        }
        var payload = System.Text.Encoding.ASCII.GetBytes("Offline update fixture - never executed.");
        const string installerName = "Tower-Windows-x64-Setup.exe";
        var release = System.Text.Json.JsonSerializer.Serialize(new
        {
            tag_name = "v1.0.1", draft = false, prerelease = false, body = "Improved simulation and radio messages.",
            assets = new[]
            {
                new { name = installerName, state = "uploaded", size = payload.Length,
                    browser_download_url = $"https://github.com/{GitHubUpdates.Repository}/releases/download/v1.0.1/{installerName}" },
                new { name = "Tower-1.0.1-SHA256SUMS.txt", state = "uploaded", size = 100,
                    browser_download_url = $"https://github.com/{GitHubUpdates.Repository}/releases/download/v1.0.1/Tower-1.0.1-SHA256SUMS.txt" }
            }
        });
        foreach (var scenario in new[] { "available", "current", "private", "offline", "cancel" })
        {
            using var client = new HttpClient(new UpdateHandler(async (request, cancellation) =>
            {
                if (scenario == "cancel")
                {
                    await Task.Delay(Timeout.Infinite, cancellation);
                }
                if (scenario == "offline")
                {
                    throw new HttpRequestException("Offline fixture.");
                }
                if (scenario == "private")
                {
                    return new(System.Net.HttpStatusCode.NotFound);
                }
                if (request.RequestUri!.AbsolutePath.EndsWith("/latest", StringComparison.Ordinal))
                {
                    return new(System.Net.HttpStatusCode.OK) { Content = new StringContent(release) };
                }
                if (request.RequestUri.AbsolutePath.EndsWith("SHA256SUMS.txt", StringComparison.Ordinal))
                {
                    return new(System.Net.HttpStatusCode.OK) { Content = new StringContent(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload)) + "  " + installerName) };
                }
                return new(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(payload) };
            }));
            using var updates = new GitHubUpdates(client);
            using var dialog = new Form_Update { Opacity = 0, ShowInTaskbar = false };
            dialog.Configure(updates, scenario == "current" ? "1.0.1" : "1.0.0");
            dialog.Show();
            Application.DoEvents();
            if (scenario == "cancel")
            {
                dialog.Close();
                Require(!dialog.IsDisposed, "Closing a pending check must cancel it before disposing the dialog.");
            }
            Wait(() => Field<Button>(dialog, "Button_Check").Enabled);
            var status = Field<Label>(dialog, "Label_Status").Text;
            var expected = scenario switch { "available" => "1.0.1 is available", "current" => "up to date", "private" => "No public", "offline" => "Could not check", _ => "cancelled" };
            Require(status.Contains(expected), "Incorrect update status: " + scenario + " / " + status);
            Require(Field<Button>(dialog, "Button_Install").Enabled == (scenario == "available"), "Incorrect install button state.");
            if (scenario == "available")
            {
                Require(Field<TextBox>(dialog, "TextBox_Notes").Text.Contains("radio messages"), "Release notes missing.");
                Render(dialog, "Updates-Available");
                AnswerDialog(() => Field<Button>(dialog, "Button_Install").PerformClick(), 7);
                Require(dialog.InstallerPath is null, "Declining installation downloaded an update.");
                AnswerDialog(() => Field<Button>(dialog, "Button_Install").PerformClick(), 6);
                Wait(() => dialog.InstallerPath is not null && dialog.IsDisposed);
                var path = dialog.InstallerPath!;
                try
                {
                    Require(File.ReadAllBytes(path).SequenceEqual(payload), "Unverified installer returned by the form.");
                    var handoff = typeof(Form_Start).Assembly.GetType("Tower.WinForms.UpdateHandoff")!;
                    var info = (System.Diagnostics.ProcessStartInfo)handoff.GetMethod("StartInfo", BindingFlags.Static | BindingFlags.NonPublic)!
                        .Invoke(null, [path, Path.Combine(Path.GetTempPath(), "Tower user's installation"), Environment.ProcessId])!;
                    var script = System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(info.ArgumentList.Last()));
                    Require(script.IndexOf("Wait-Process", StringComparison.Ordinal) < script.IndexOf("Start-Process", StringComparison.Ordinal) &&
                        script.Contains("Tower user''s installation") && !info.UseShellExecute && info.CreateNoWindow,
                        "Installer handoff must wait for Tower and preserve quoted paths without opening a console.");
                }
                finally
                {
                    File.Delete(path);
                    Directory.Delete(Path.GetDirectoryName(path)!);
                }
            }
            else
            {
                dialog.Close();
            }
        }
    }

    private static void CheckAircraftAnimationViews()
    {
        var catalog = new AircraftModelCatalog(TestData);
        Require(catalog.Types.Count == 44, "Original model catalogue is incomplete.");
        foreach (var type in catalog.Types)
        {
            var mesh = catalog.Get(type) ?? throw new InvalidOperationException("Failed model: " + type);
            foreach (var face in mesh.Faces.Where(face => (face.Flags & ModelFace.RetractableGear) != 0))
            {
                var aircraft = new Aircraft();
                foreach (var index in face.Vertices)
                {
                    var offset = mesh.GearOffset(face, index, aircraft, .5);
                    Require(double.IsFinite(offset.Altitude) && double.IsFinite(offset.Position.X) && double.IsFinite(offset.Position.Y),
                        "Invalid animated vertex: " + type);
                }
            }
        }
        foreach (var type in new[] { "B737", "AT72", "C172", "F18" })
        {
            var airport = OriginalData.LoadAirport(TestData, "CYN");
            var simulation = new Simulation(new() { Airport = airport, Options = new() { Runway = "9", AircraftPerMinute = 0 } });
            var aircraft = simulation.AddAircraft(FlightDirection.Departure, "ANIM", type, "9", "G1");
            aircraft.Phase = FlightPhase.Vectoring;
            aircraft.Position = airport.Observer.Offset(0, 2000);
            aircraft.AltitudeFeet = 3000;
            aircraft.Heading = 90;
            using var view = new TowerView(simulation, new() { ShowAircraftLabels = false, ShowCompass = false, ShowGauges = false }) { Size = new(1280, 720) };
            Require(view.SetViewMode(TowerViewMode.SpotPlane, "ANIM"), "Animation camera failed.");
            view.Pan(110);
            view.Zoom(true);
            view.Zoom(true);
            foreach (var (name, extension) in new[] { ("Down", 1d), ("Half", .5), ("Up", 0d) })
            {
                aircraft.Animation = new() { GearExtension = extension, GearCommandedDown = extension > 0, LandingLights = true };
                Render(view, "Animation-" + type + "-" + name);
            }
            var down = File.ReadAllBytes(Path.Combine(Output, "Animation-" + type + "-Down.png"));
            var up = File.ReadAllBytes(Path.Combine(Output, "Animation-" + type + "-Up.png"));
            Require(down.SequenceEqual(up) == (type == "C172"), "Unexpected fixed/retractable gear rendering: " + type);
            if (type == "B737")
            {
                var half = File.ReadAllBytes(Path.Combine(Output, "Animation-B737-Half.png"));
                Require(!half.SequenceEqual(up) && !half.SequenceEqual(down), "Intermediate gear geometry is not rendered.");
                simulation.State.Options.StartMinutes = 1320;
                aircraft.Animation = new() { GearExtension = 1, LandingLights = true };
                Render(view, "Animation-B737-Night-Lights");
                aircraft.Animation.LandingLights = false;
                Render(view, "Animation-B737-Night-NoLights");
                Require(!File.ReadAllBytes(Path.Combine(Output, "Animation-B737-Night-Lights.png"))
                    .SequenceEqual(File.ReadAllBytes(Path.Combine(Output, "Animation-B737-Night-NoLights.png"))), "Landing lights do not affect the rendered model.");
            }
        }
    }

    private static void CheckWindViews()
    {
        var diagnostics = new List<string>();
        foreach (var (name, from, knots) in new[] { ("Calm", 90d, 0d), ("Headwind", 90d, 15d), ("Crosswind", 0d, 15d) })
        {
            var airport = OriginalData.LoadAirport(TestData, "CYN");
            var simulation = new Simulation(new() { Airport = airport,
                Options = new() { Runway = "9", AircraftPerMinute = 0, WindFromDegrees = from, WindKnots = knots } });
            var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "AAL1535", "C182", "9", useTrafficFlow: false);
            var runway = airport.GetRunway("9");
            var heading = airport.Point(runway.Threshold).Bearing(airport.Point(runway.End));
            aircraft.Position = airport.Point(runway.Threshold).Offset(heading + 180, 6 * 1852);
            aircraft.Phase = FlightPhase.Approach;
            aircraft.Heading = heading;
            aircraft.SpeedKnots = aircraft.TargetSpeedKnots = aircraft.Specification.ApproachSpeed;
            aircraft.AltitudeFeet = 6 * 1852 * Math.Tan(3 * Math.PI / 180) / Coordinates.MetresPerFoot;
            Require(simulation.Execute("AAL1535/CL").Success, "Wind landing clearance rejected.");
            simulation.Advance(20);
            if (name == "Crosswind")
            {
                Require(Math.Abs(RouteMovement.AngleDifference(aircraft.GroundTrackDegrees!.Value, aircraft.Heading)) > 5,
                    "Crosswind crab is not visible in the aircraft heading.");
            }
            using var view = new TowerView(simulation, new()) { Size = new(1280, 720) };
            using var radar = new RadarControl(simulation, new() { BriteRange = 10 }, true) { Size = new(1000, 750) };
            Require(view.SetViewMode(TowerViewMode.SpotPlane, aircraft.CallSign), "Wind aircraft not trackable.");
            view.Pan(110);
            Render(view, "Wind-" + name + "-Tower");
            Render(radar, "Wind-" + name + "-BRITE");
            diagnostics.Add(FormattableString.Invariant($"{name}: heading={aircraft.Heading:0.000}, track={aircraft.GroundTrackDegrees:0.000}, air={aircraft.SpeedKnots:0.000}, ground={aircraft.GroundSpeedKnots:0.000}"));
            var path = Path.Combine(Output, "Wind-" + name + ".json");
            JsonStore.Write(path, simulation.State);
            var restored = new Simulation(JsonStore.LoadSession(path));
            simulation.Advance(10);
            restored.Advance(10);
            var resumed = restored.State.Aircraft.Single();
            Require(resumed.Position == aircraft.Position && resumed.Heading == aircraft.Heading && resumed.GroundTrackDegrees == aircraft.GroundTrackDegrees,
                "Wind save/resume changed the flight movement.");
        }
        File.WriteAllLines(Path.Combine(Output, "Wind-Comparisons.txt"), diagnostics);
    }

    private static void CheckAircraftStatusTooltips()
    {
        var simulation = new Simulation(new() { Airport = Airport.Training(), Options = new() { AircraftPerMinute = 0 } });
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "STATUS11", "C172", assignedGate: "G1", useTrafficFlow: false);
        object Component(Control control) => control.GetType().GetField("statusToolTip", Private)!.GetValue(control)!;
        string Text(Control control) => Field<ToolTip>(Component(control), "toolTip").GetToolTip(control) ?? "";
        void Move(Control control, Point point) => Call(control, "OnMouseMove", new MouseEventArgs(MouseButtons.None, 0, point.X, point.Y, 0));
        foreach (var air in new[] { false, true })
        {
            aircraft.Phase = FlightPhase.TaxiOut;
            aircraft.AltitudeFeet = air ? 5000 : 0;
            using var radar = new RadarControl(simulation, new(), air) { Size = new(700, 600) };
            Render(radar, "Status-" + air);
            var label = Field<Dictionary<Aircraft, RectangleF>>(radar, "labels")[aircraft];
            Move(radar, new((int)label.X + 2, (int)label.Y + 2));
            Require(Text(radar) == "STATUS11" + Environment.NewLine + "Taxiing to runway 18", "Radar hover status is incorrect.");
            Require(radar.SelectedCallsign.Length == 0, "Hover selected the aircraft.");
            aircraft.Phase = FlightPhase.HoldingShort;
            Render(radar, "Status-Updated-" + air);
            Require(Text(radar).Contains("Holding short of runway 18"), "Stationary hover did not update.");
            Move(radar, new(2, 2));
            Require(Text(radar).Length == 0, "Empty radar space retained a stale status.");
            Move(radar, new((int)label.X + 2, (int)label.Y + 2));
            Call(radar, "OnMouseLeave", EventArgs.Empty);
            Require(Text(radar).Length == 0, "Mouse leave retained a status.");
            Move(radar, new((int)label.X + 2, (int)label.Y + 2));
            aircraft.Phase = FlightPhase.Completed;
            Render(radar, "Status-Completed-" + air);
            Require(Text(radar).Length == 0, "Completed aircraft retained a status.");
            var component = Component(radar);
            radar.Dispose();
            Require(Field<bool>(component, "disposed"), "Radar tooltip was not disposed.");
        }
        aircraft.Phase = FlightPhase.Pushback;
        aircraft.Holding = true;
        using var strip = new FlightStripControl { Aircraft = aircraft };
        Render(strip, "Status-Strip");
        Move(strip, new(10, 10));
        Require(Text(strip).Contains("Holding position - Pushing back from gate G1"), "Strip pushback/hold status is incorrect.");
        aircraft.Holding = false;
        aircraft.Phase = FlightPhase.ReadyForTaxi;
        Render(strip, "Status-Strip-Updated");
        Require(Text(strip).EndsWith("Ready for taxi"), "Strip status did not update.");
        strip.Aircraft = null;
        Render(strip, "Status-Strip-Empty");
        Require(Text(strip).Length == 0, "Empty strip retained a status.");
    }

    private static void CheckGroundHandlingViews()
    {
        var airport = OriginalData.LoadAirport(TestData, "CYN");
        var simulation = new Simulation(new() { Airport = airport, Options = new() { Runway = "9", AircraftPerMinute = 0 } });
        var first = simulation.AddAircraft(FlightDirection.Departure, "DEPART1", "B737", "9", "G3");
        var second = simulation.AddAircraft(FlightDirection.Departure, "DEPART2", "C182", "9", "G4");
        var arrival = simulation.AddAircraft(FlightDirection.Arrival, "ARRIVE", "C182", "32", "G5");
        using var view = new TowerView(simulation, new()) { Size = new(1280, 720) };
        using var radar = new RadarControl(simulation, new() { AsdeRange = 0.8 }, false) { Size = new(1200, 900), SelectedCallsign = first.CallSign };
        Require(view.SetViewMode(TowerViewMode.SpotPlane, first.CallSign), "Ground aircraft not trackable.");
        void Capture(string phase)
        {
            Render(view, "GroundHandling-" + phase + "-Tower");
            Render(radar, "GroundHandling-" + phase + "-ASDE");
        }
        Capture("Parked");
        Require(simulation.Execute("DEPART1/PB").Success, "First pushback rejected.");
        Require(!simulation.Execute("DEPART2/PB").Success, "Opposite stands must not claim the same pushback connection together.");
        for (var secondIndex = 0; secondIndex < 1500; secondIndex++)
        {
            foreach (var aircraft in new[] { first, second, arrival }.Where(aircraft => aircraft.IsOnFrequency))
            {
                var occupied = simulation.State.Aircraft.Any(other => other != aircraft && other.IsOnRunway);
                var command = aircraft.Phase switch
                {
                    FlightPhase.AtGate when aircraft.Pushback is { Completed: false } => "PB",
                    FlightPhase.ReadyForTaxi => "T" + aircraft.Runway,
                    FlightPhase.HoldingShort when !occupied => "CT",
                    FlightPhase.Approach or FlightPhase.Circuit when !aircraft.LandingCleared => "CL",
                    FlightPhase.ArrivalHold => "TP",
                    FlightPhase.TaxiIn or FlightPhase.TaxiOut when aircraft.Holding && !occupied => "TU",
                    _ => null
                };
                if (command is not null)
                {
                    _ = simulation.Execute(aircraft.CallSign + "/" + command);
                }
            }
            simulation.Advance(1);
            if (secondIndex is 29 or 199 or 599)
            {
                Capture("After-" + (secondIndex + 1));
            }
        }
        Require(arrival.Phase == FlightPhase.Completed, "Arrival did not finish parking.");
        Require(first.Phase is FlightPhase.Departing or FlightPhase.Completed && second.Phase is FlightPhase.Departing or FlightPhase.Completed,
            "Departure ground cycle did not finish.");
        Capture("Completed-Ground-Cycles");
    }

    private static void CheckPushbackRoutingViews()
    {
        foreach (var (gate, runway) in new[] { ("G3", "9"), ("G4", "9"), ("G3", "27") })
        {
            var airport = OriginalData.LoadAirport(TestData, "CYN");
            var simulation = new Simulation(new() { Airport = airport, Options = new() { Runway = runway, AircraftPerMinute = 0 } });
            var aircraft = simulation.AddAircraft(FlightDirection.Departure, "UAL117", "B737", runway, gate);
            using var view = new TowerView(simulation, new()) { Size = new(1280, 720) };
            using var radar = new RadarControl(simulation, new() { AsdeRange = 1.5 }, false) { Size = new(1000, 750), SelectedCallsign = aircraft.CallSign };
            Require(view.SetViewMode(TowerViewMode.SpotPlane, aircraft.CallSign), "Pushback aircraft is not trackable.");
            view.Pan(110);
            void Capture(string phase)
            {
                Render(view, "CYN-" + gate + "-" + runway + "-" + phase + "-Tower");
                Render(radar, "CYN-" + gate + "-" + runway + "-" + phase + "-ASDE");
            }
            Capture("Parked");
            Require(simulation.Execute("UAL117/PB").Success, "Pushback was rejected.");
            for (var tick = 0; tick < 3000 && aircraft.Pushback!.SampleIndex < 64; tick++) simulation.Advance(0.1);
            Capture("Pushing");
            for (var tick = 0; tick < 6000 && aircraft.Phase == FlightPhase.Pushback; tick++) simulation.Advance(0.1);
            Require(aircraft.Phase == FlightPhase.ReadyForTaxi, "Pushback did not finish.");
            Capture("Ready");
            Require(simulation.Execute("UAL117/T" + runway).Success, "Taxi was rejected.");
            var heading = aircraft.Heading;
            for (var tick = 0; tick < 50; tick++) simulation.Advance(0.1);
            if (runway == "9") Require(Math.Abs(RouteMovement.AngleDifference(heading, aircraft.Heading)) < 1, "Taxi reversed the correct alley heading.");
            for (var tick = 0; tick < 150; tick++) simulation.Advance(0.1);
            Capture("Taxi");
        }
    }

    private static void CheckTalkSettings()
    {
        var runtime = typeof(Form_Start).Assembly.GetType("Tower.WinForms.Program")!;
        var settings = (DisplaySettings)runtime.GetProperty("Settings")!.GetValue(null)!;
        var airport = Airport.Training();
        runtime.GetField("Airport")!.SetValue(null, airport);
        Require(settings.SoundTalk, "Talk must default to enabled.");
        using (var setup = new Form_SoundSetup())
        {
            Call(setup, "Form_SoundSetup_Load", setup, EventArgs.Empty);
            var talk = Field<CheckBox>(setup, "CheckBox_Talk");
            Require(talk.Checked, "Talk checkbox did not load the current setting.");
            talk.Checked = false;
            Call(setup, "Button_Save_Click", setup, EventArgs.Empty);
            Require(setup.DialogResult == DialogResult.OK && !settings.SoundTalk, "Talk checkbox did not save.");
        }
        var voice = runtime.GetProperty("Voice")!.GetValue(null)!;
        voice.GetType().GetMethod("ClearPending")!.Invoke(voice, null);
        var status = voice.GetType().GetProperty("Status")!.GetValue(voice);
        voice.GetType().GetMethod("Readback")!.Invoke(voice, [new AircraftRadioMessage("TALK", "CYN", "ready to taxi", "9")]);
        Require(Equals(status, voice.GetType().GetProperty("Status")!.GetValue(voice)), "Muted radio submitted a transmission.");
        using var window = new Form_SimulationWindow(new Simulation(new() { Airport = airport }));
        try
        {
            foreach (var enabled in new[] { true, false })
            {
                Call(window, "ButtonBar_ButtonClicked", window, "RADIO");
                Require(settings.SoundTalk == enabled, "RADIO did not toggle Talk.");
                var saved = new DisplaySettings();
                new IniSettingsStore(IniSettingsStore.ResolvePath(AppContext.BaseDirectory)).Load(saved, new());
                Require(saved.SoundTalk == enabled, "RADIO did not persist Talk.");
            }
            using var setup = new Form_SoundSetup();
            Call(setup, "Form_SoundSetup_Load", setup, EventArgs.Empty);
            Require(!Field<CheckBox>(setup, "CheckBox_Talk").Checked, "Sound Setup did not reflect RADIO.");
            Field<CheckBox>(setup, "CheckBox_Talk").Checked = true;
            Call(setup, "Button_Cancel_Click", setup, EventArgs.Empty);
            Require(!settings.SoundTalk, "Cancel changed Talk.");
        }
        finally
        {
            Call(window, "Form_SimulationWindow_FormClosed", window, new FormClosedEventArgs(CloseReason.ApplicationExitCall));
        }
    }

    private static void CheckPerformanceClose()
    {
        var runtime = typeof(Form_Start).Assembly.GetType("Tower.WinForms.Program")!;
        var core = typeof(Form_Start).Assembly.GetType("Tower.WinForms.Core")!;
        var profilesPath = (string)core.GetProperty("ProfilesPath", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        Require(!File.Exists(Path.Combine(Path.GetDirectoryName(profilesPath)!, "Tower.slnx")),
            "Run this check from an isolated copy outside the checkout to protect real settings/profiles.");
        var profilesField = runtime.GetField("Profiles", BindingFlags.Public | BindingFlags.Static)!;
        var settingsProperty = runtime.GetProperty("Settings")!;
        foreach (var scenario in new[] { "reward", "practice", "overrun", "replay" })
        {
            var profile = new ControllerProfile { Name = "Reward Check", Sessions = 1, AirportRatings = new() { ["CYN"] = [100] } };
            profilesField.SetValue(null, new List<ControllerProfile> { profile });
            settingsProperty.SetValue(null, new DisplaySettings { SelectedController = profile.Id, SoundTalk = false });
            var session = new Session
            {
                Airport = OriginalData.LoadAirport(TestData, "CYN"), ControllerId = profile.Id,
                Options = new() { Runway = "9", AircraftPerMinute = 0, Practice = scenario == "practice" },
                Performance = new() { Movements = 1, RunwayOverruns = scenario == "overrun" ? 1 : 0 },
                Schedule = [new() { TimeSeconds = 100000, CallSign = "AAL1535", Runway = "9" }]
            };
            if (scenario == "replay")
            {
                profile.RecordedSessions.Add(session.Id);
            }
            using var start = new Form_Start { Opacity = 0, ShowInTaskbar = false };
            var attemptedClose = false;
            var rewardsShown = 0;
            Form_SimulationWindow? simulationWindow = null;
            Exception? failure = null;
            using var driver = new System.Windows.Forms.Timer { Interval = 50 };
            driver.Tick += (_, _) =>
            {
                try
                {
                    if (Application.OpenForms.OfType<Form_PerformanceReward>().SingleOrDefault() is { } earned)
                    {
                        rewardsShown++;
                        Require(scenario == "reward", "Unexpected reward for " + scenario);
                        Require(earned.Modal && earned.Owner == start && simulationWindow is { Visible: false },
                            "Reward must be owned by Start after the simulation has closed.");
                        Require(session.ResultsRecorded && simulationWindow!.Simulation.Paused, "Reward shown before results/stopping.");
                        var saved = JsonStore.Read<List<ControllerProfile>>(profilesPath).Single();
                        Require(saved.RecordedSessions.Contains(session.Id) && saved.RewardCounts.GetValueOrDefault(9) == 1,
                            "Reward was displayed before its profile was saved.");
                        Require(Field<Label>(earned, "Label_Title").Text == "Aircraft Save Award", "Incorrect actual earned reward.");
                        Render(earned, "Actual-Earned-Reward");
                        earned.Close();
                        return;
                    }
                    if (attemptedClose || Application.OpenForms.OfType<Form_SimulationWindow>().SingleOrDefault() is not { } window)
                    {
                        return;
                    }
                    attemptedClose = true;
                    simulationWindow = window;
                    window.Opacity = 0;
                    window.ShowInTaskbar = false;
                    AnswerDialog(window.Close, 7);
                    Require(window.Visible && !session.ResultsRecorded && profile.Sessions == 1,
                        "No must keep the session open without credit or reward.");
                    Require(Field<System.Windows.Forms.Timer>(window, "Timer").Enabled, "No did not resume the UI timer.");
                    AnswerDialog(window.Close, 6);
                }
                catch (Exception exception)
                {
                    failure = exception;
                    driver.Stop();
                    foreach (var dialog in Application.OpenForms.OfType<Form>().Where(form => form != start).ToArray())
                    {
                        dialog.Dispose();
                    }
                    start.Dispose();
                }
            };
            driver.Start();
            typeof(Form_Start).GetMethod("StartSession", Private)!.Invoke(start, [session, null]);
            driver.Stop();
            if (failure is not null)
            {
                throw new InvalidOperationException("Performance close check failed: " + scenario, failure);
            }
            Require(attemptedClose && rewardsShown == (scenario == "reward" ? 1 : 0), "Incorrect actual reward count: " + scenario);
            Require(simulationWindow is { IsDisposed: true } && simulationWindow.Simulation.Paused, "Yes did not close/stop the simulation.");
            var recorded = ((List<ControllerProfile>)profilesField.GetValue(null)!).Single();
            Require(recorded.Sessions == (scenario is "reward" or "overrun" ? 2 : 1), "Session credit duplicated or missing: " + scenario);
            Require(session.ResultsRecorded == (scenario != "practice"), "Incorrect final recording status: " + scenario);
        }
    }

    private static void CheckPerformanceReview()
    {
        var core = typeof(Form_Start).Assembly.GetType("Tower.WinForms.Core")!;
        var profilesPath = (string)core.GetProperty("ProfilesPath", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        Require(profilesPath == Path.Combine(Path.GetDirectoryName(IniSettingsStore.ResolvePath(AppContext.BaseDirectory))!, "controllers.json"),
            "Controller profiles must belong to this installation rather than shared AppData.");
        var session = new Session
        {
            Airport = new() { Name = "Canyon Field", Difficulty = 50 },
            Performance = new()
            {
                TaxiOperations = 1, TakeoffLandingCommands = 2, Handoffs = 3, Holds = 4,
                Reports = 5, Requests = 6, Breakouts = 7, Patterns = 8, StraightIns = 9,
                CategorySeparationArrival = 2, RunwaySeparation = 2, CategorySeparationDeparture = 3,
                Crashes = 1, Movements = 5
            }
        };
        using (var review = new Form_Performance { Opacity = 0, ShowInTaskbar = false })
        {
            review.Configure(session, new() { Name = "Review Check" });
            review.Show();
            Application.DoEvents();
            Require(review.Text == "Performance Review - Review Check", "The review was not bound to its session/controller.");
            Require(Field<TextBox>(review, "TextBox_Difficulty").Text == "50", "Incorrect airport difficulty.");
            var names = new[] { "TaxiOperations", "TakeoffLanding", "Handoffs", "Holds", "Report", "Request", "BreakOut", "Pattern", "StraightIn" };
            for (var index = 0; index < names.Length; index++)
            {
                Require(Field<Label>(review, "Label_" + names[index] + "Value").Text == (index + 1).ToString(), "Incorrect command counter.");
            }
            Require(Field<TextBox>(review, "TextBox_Rating").Text == "375", "Incorrect review rating.");
            Require(Field<Label>(review, "Label_CrashesValue").Text == "1", "Incorrect crash counter.");
            Require(Field<Label>(review, "Label_Status").Text.Contains("Practice"), "Practice status is missing.");
            Render(review, "PerformanceReview");
            review.Close();
        }
        using var reward = new Form_PerformanceReward { Opacity = 0, ShowInTaskbar = false };
        foreach (var (status, expected) in new[] { ("baseline", "baseline"), ("errors", "Runway overruns: 1"), ("rated", "10%"), ("recorded", "will not be credited again") })
        {
            using var review = new Form_Performance { Opacity = 0, ShowInTaskbar = false };
            var current = new Session
            {
                Options = new() { Practice = false }, ResultsRecorded = status == "recorded",
                Performance = new() { Movements = 1, RunwayOverruns = status == "errors" ? 1 : 0 }
            };
            var profile = new ControllerProfile();
            if (status != "baseline")
            {
                profile.AirportRatings["TRAINING"] = [100];
                profile.Sessions = 1;
            }
            review.Configure(current, profile);
            review.Show();
            Application.DoEvents();
            Require(Field<Label>(review, "Label_Status").Text.Contains(expected), "Incorrect reward eligibility status: " + status);
            if (status == "errors")
            {
                Render(review, "PerformanceReview-OperationalErrors");
            }
            review.Close();
        }
        reward.Configure(PerformanceRewards.Catalogue[9]);
        reward.Show();
        Application.DoEvents();
        Require(Field<PictureBox>(reward, "PictureBox_Reward").Image is not null, "Reward icon resource was not loaded.");
        Require(Field<Label>(reward, "Label_Title").Text == "Aircraft Save Award", "Incorrect reward title.");
        Require(Field<Label>(reward, "Label_Message").Text.Contains("heartfelt thanks"), "Incorrect reward message.");
        Render(reward, "PerformanceReward");
        reward.Close();
        using var start = new Form_Start { Opacity = 0, ShowInTaskbar = false };
        var displayed = false;
        using var closeReward = new System.Windows.Forms.Timer { Interval = 100 };
        closeReward.Tick += (_, _) =>
        {
            var earned = Application.OpenForms.OfType<Form_PerformanceReward>().SingleOrDefault();
            if (earned is null) return;
            closeReward.Stop();
            displayed = earned.Modal && earned.Owner == start && earned.Text == PerformanceRewards.Catalogue[9].Title;
            earned.Close();
        };
        closeReward.Start();
        typeof(Form_Start).GetMethod("ShowPerformanceReward", Private)!.Invoke(start, [PerformanceRewards.Catalogue[9]]);
        Require(displayed, "Start did not display the earned reward as an owned modal dialog.");
    }

    private static void CheckLayoutPersistence()
    {
        var directory = Path.Combine(Output, "LayoutPersistence-" + Guid.NewGuid().ToString("N"));
        var store = new WindowLayoutStore(directory);
        var runtime = typeof(Form_SimulationWindow).Assembly.GetType("Tower.WinForms.Program")!;
        var settings = (DisplaySettings)runtime.GetProperty("Settings")!.GetValue(null)!;
        settings.DefaultWindowLayout = "";
        string remembered = "";
        Form_SimulationWindow Open()
        {
            var simulation = new Simulation(new() { Options = new() { AircraftPerMinute = 0 } });
            var window = new Form_SimulationWindow(simulation, directory, name => remembered = name)
            {
                Opacity = 0,
                ShowInTaskbar = false
            };
            foreach (var name in new[] { "Brite", "ASDE", "Commands", "ArrivalStrips", "DepartureStrips", "ButtonBar" })
            {
                var child = Field<Form>(window, name);
                child.Opacity = 0;
                child.ShowInTaskbar = false;
            }
            window.Show();
            Application.DoEvents();
            Field<System.Windows.Forms.Timer>(window, "Timer").Stop();
            return window;
        }
        using (var first = Open())
        {
            Require(File.Exists(store.FilePath("default.set")), "default.set was not created.");
            Require(remembered == "default.set", "Default layout was not remembered.");
            var initial = File.ReadAllText(store.FilePath("default.set"));
            var area = Screen.PrimaryScreen!.WorkingArea;
            Field<Form>(first, "ASDE").Bounds = new(area.Left + 45, area.Top + 75, 320, 280);
            Field<Form>(first, "DepartureStrips").Bounds = new(area.Left + 90, area.Top + 120, 330, 290);
            AnswerDialog(first.Close, 7);
            Require(!first.IsDisposed, "No closed the simulation.");
            Require(File.ReadAllText(store.FilePath("default.set")) == initial, "No changed the saved layout.");
            AnswerDialog(first.Close, 6);
            Require(first.IsDisposed, "Yes did not close the simulation.");
        }
        var saved = store.Read("default.set");
        using (var second = Open())
        {
            foreach (var name in new[] { "ASDE", "DepartureStrips" })
            {
                var placement = saved.Placement(name)!;
                Require(Field<Form>(second, name).Bounds == new Rectangle(placement.X, placement.Y, placement.Width!.Value, placement.Height!.Value),
                    name + " bounds were not restored in a new scenario.");
            }
            AnswerDialog(second.Close, 6);
        }
        var custom = store.Read("default.set");
        custom.Set("ASDE", new(120, 130, 350, 300, true));
        custom.Set("3D", new(0, 24, 320, 180));
        store.Write("custom.set", custom);
        settings.DefaultWindowLayout = "custom.set";
        using var third = Open();
        Require(Field<Form>(third, "ASDE").Size == new Size(350, 300), "The selected custom layout was not loaded.");
        var polygonView = Field<TowerView>(third, "View");
        void CheckPolygonFill()
        {
            var menu = Field<MenuStrip>(third, "MenuStrip");
            var status = Field<StatusStrip>(third, "StatusStrip");
            Require(!polygonView.HasPanorama && polygonView.Dock == DockStyle.Fill, "Saved 3D bounds constrained a polygon airport.");
            Require(polygonView.Bounds == new Rectangle(0, menu.Bottom, third.ClientSize.Width, status.Top - menu.Bottom),
                "Polygon TowerView does not fill the area between menu and status bar.");
            Require(polygonView.SceneViewport == polygonView.ClientRectangle, "Polygon scene does not fill its control.");
        }
        CheckPolygonFill();
        third.WindowState = FormWindowState.Normal;
        third.Size = new(900, 650);
        Application.DoEvents();
        CheckPolygonFill();
        third.WindowState = FormWindowState.Maximized;
        Application.DoEvents();
        CheckPolygonFill();
        Render(polygonView, "Polygon-Fill-CustomLayout");
        AnswerDialog(third.Close, 6);
        Require(remembered == "custom.set", "The selected custom filename was not retained.");
    }
    private static void Call(object target, string name, params object[] arguments)
    {
        var method = target.GetType().GetMethods(Private).Single(method => method.Name == name &&
            method.GetParameters().Length == arguments.Length &&
            method.GetParameters().Zip(arguments).All(pair => pair.First.ParameterType.IsInstanceOfType(pair.Second)));
        method.Invoke(target, arguments);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Render(Control control, string name)
    {
        control.CreateControl();
        using var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new(Point.Empty, control.Size));
        bitmap.Save(Path.Combine(Output, name + ".png"));
    }

    private static void CheckFlightDynamics()
    {
        foreach (var (code, runway, type) in new[] { ("CYN", "9", "C182"), ("CYN", "14", "B737") })
        {
            var airport = OriginalData.LoadAirport(TestData, code);
            var simulation = new Simulation(new() { Airport = airport, Options = new() { Runway = runway, AircraftPerMinute = 0, WindKnots = 0 } });
            var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "UAL782", type, useTrafficFlow: false);
            Require(simulation.Execute("UAL782/CL").Success, "Landing clearance failed.");
            for (var second = 0; second < 1200 && aircraft.AltitudeFeet > 160; second++)
            {
                simulation.Advance(1);
            }
            using var host = new Form { ClientSize = new(960, 540), Opacity = 0, ShowInTaskbar = false };
            using var view = new TowerView(simulation, new DisplaySettings());
            host.Controls.Add(view);
            host.Show();
            Application.DoEvents();
            Require(view.SetViewMode(TowerViewMode.SpotPlane, aircraft.CallSign), "Spot camera failed.");
            var frames = new List<byte[]>();
            var savedStages = new HashSet<string>();
            var landed = false;
            var stopped = false;
            for (var frame = 0; frame < 1800 && !stopped; frame++)
            {
                var stage = aircraft.Phase == FlightPhase.Approach && aircraft.AltitudeFeet < 40 ? "Flare" :
                    aircraft.Phase == FlightPhase.LandingRoll && aircraft.SpeedKnots > aircraft.Specification.TaxiSpeed * 2 ? "Braking" :
                    aircraft.Phase == FlightPhase.LandingRoll && Math.Abs(RouteMovement.AngleDifference(
                        airport.Point(airport.GetRunway(runway).Threshold).Bearing(airport.Point(airport.GetRunway(runway).End)), aircraft.Heading)) > 5 ? "Turn" : "";
                Simulation? restored = null;
                if (stage.Length > 0 && savedStages.Add(stage))
                {
                    var save = Path.Combine(Output, code + "-" + runway + "-" + stage + ".tower.json");
                    JsonStore.Write(save, simulation.State);
                    restored = new Simulation(JsonStore.LoadSession(save));
                }
                simulation.Advance(.1);
                if (restored is not null)
                {
                    restored.Advance(.1);
                    var copy = restored.State.Aircraft[0];
                    Require(copy.Position == aircraft.Position && copy.PitchDegrees == aircraft.PitchDegrees &&
                        copy.BankDegrees == aircraft.BankDegrees && copy.SpeedKnots == aircraft.SpeedKnots,
                        "Saved motion diverged during " + stage + ".");
                }
                using var bitmap = new Bitmap(960, 540);
                view.DrawToBitmap(bitmap, new(Point.Empty, bitmap.Size));
                using var jpeg = new MemoryStream();
                bitmap.Save(jpeg, System.Drawing.Imaging.ImageFormat.Jpeg);
                frames.Add(jpeg.ToArray());
                if (!landed && aircraft.Phase == FlightPhase.LandingRoll)
                {
                    bitmap.Save(Path.Combine(Output, code + "-" + runway + "-Touchdown.png"));
                    landed = true;
                }
                stopped = aircraft.Phase == FlightPhase.ArrivalHold;
            }
            Require(landed && stopped && savedStages.Count == 3, code + " did not complete landing/rollout and save/resume checks.");
            MotionCapture.Write(Path.Combine(Output, code + "-" + runway + "-Landing.avi"), frames, 960, 540, 10);
            host.Close();
        }
        var turnAirport = OriginalData.LoadAirport(TestData, "CYN");
        var turnSimulation = new Simulation(new() { Airport = turnAirport, Options = new() { Runway = "9", AircraftPerMinute = 0 } });
        var turning = turnSimulation.AddAircraft(FlightDirection.Arrival, "TURN", "B737", useTrafficFlow: false);
        turning.SpeedKnots = 180;
        turning.AltitudeFeet = 2000;
        Require(turnSimulation.Execute("TURN/MR1").Success, "Turn command failed.");
        using var turnHost = new Form { ClientSize = new(960, 540), Opacity = 0, ShowInTaskbar = false };
        using var turnView = new TowerView(turnSimulation, new DisplaySettings());
        turnHost.Controls.Add(turnView);
        turnHost.Show();
        Application.DoEvents();
        Require(turnView.SetViewMode(TowerViewMode.SpotPlane, turning.CallSign), "Turn camera failed.");
        var turnFrames = new List<byte[]>();
        for (var frame = 0; frame < 750; frame++)
        {
            turnSimulation.Advance(.1);
            using var bitmap = new Bitmap(960, 540);
            turnView.DrawToBitmap(bitmap, new(Point.Empty, bitmap.Size));
            using var jpeg = new MemoryStream();
            bitmap.Save(jpeg, System.Drawing.Imaging.ImageFormat.Jpeg);
            turnFrames.Add(jpeg.ToArray());
            if (frame == 40)
            {
                Require(turning.BankDegrees > 15 && turning.BankDegrees <= 25, "The aircraft is not banking into its turn.");
                bitmap.Save(Path.Combine(Output, "CYN-Airborne-Turn.png"));
            }
        }
        Require(Math.Abs(turning.BankDegrees) < .1, "The aircraft did not roll out of its turn.");
        MotionCapture.Write(Path.Combine(Output, "CYN-Airborne-Turn.avi"), turnFrames, 960, 540, 10);
        turnHost.Close();
    }

    private static void CheckBriteMap()
    {
        var fixture = Path.Combine(Output, "BriteMapSource");
        Directory.CreateDirectory(fixture);
        var original = Path.Combine(Directory.GetParent(WorkspacePaths.DefaultRoot)!.FullName, "Context", "Original Bao Tower Binaries.zip");
        using (var archive = System.IO.Compression.ZipFile.OpenRead(original))
        {
            foreach (var entry in archive.Entries.Where(entry => System.Text.RegularExpressions.Regex.IsMatch(entry.FullName,
                @"^CYN/[A-Z0-9_]+\.(TWR|SEC|SPF|MAP|PAT|PRF|CPL|INF|TYP)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)))
            {
                var target = Path.Combine(fixture, entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var input = entry.Open();
                using var output = File.Create(target);
                input.CopyTo(output);
            }
        }
        foreach (var (code, count, arcs) in new[] { ("CYN", 10, 4) })
        {
            var airport = OriginalData.LoadAirport(fixture, code);
            Require(airport.RadarMap.Lines.Count == count && airport.RadarMap.Arcs.Count == arcs, code + " map records lost.");
            var symbols = airport.Original.Fixes.Values.Where(fix => fix.SourceKind is "p" or "o").ToArray();
            Require(symbols.Length > 0 && (code != "CYN" || symbols.Length == 2), code + " SEC symbols lost.");
            var simulation = new Simulation(new Session { Airport = airport, Options = new() { Runway = airport.Runways[0].Name, AircraftPerMinute = 0 } });
            var settings = new DisplaySettings { BriteRange = 10, BriteMap = true, BriteObstructions = true, BriteTaxiways = false, ShowRangeMarks = false };
            using var radar = new RadarControl(simulation, settings, true) { Size = new(800, 800) };
            radar.CreateControl();
            Bitmap Capture()
            {
                var image = new Bitmap(radar.Width, radar.Height);
                radar.DrawToBitmap(image, new(Point.Empty, radar.Size));
                return image;
            }
            int GreenPixels(Bitmap image)
            {
                var count = 0;
                for (var y = 30; y < image.Height; y++)
                {
                    for (var x = 0; x < image.Width; x++)
                    {
                        if (image.GetPixel(x, y).G > 0)
                        {
                            count++;
                        }
                    }
                }
                return count;
            }
            Render(radar, "BRITE-Map-" + code + "-10NM");
            settings.BriteMap = false;
            using var symbolsOnly = Capture();
            Require(GreenPixels(symbolsOnly) > 0, code + " symbols not rendered at normal range.");
            settings.BriteObstructions = false;
            using var empty = Capture();
            Require(GreenPixels(empty) == 0, code + " layer switches leave geometry behind.");
            settings.BriteMap = true;
            settings.BriteRange = 50;
            using var mapOnly = Capture();
            Require(GreenPixels(mapOnly) > 50, code + " geographic map not rendered.");
            settings.BriteObstructions = true;
            Render(radar, "BRITE-Map-" + code + "-50NM");
            radar.ZoomIn();
            Require(settings.BriteRange == 40, "Map zoom did not change range.");
            radar.SetCenterOnNextClick(true);
            Call(radar, "Down", radar, new MouseEventArgs(MouseButtons.Left, 1, 600, 300, 0));
            Require(Field<Position>(radar, "panOffset") != default, "Map did not recenter.");
            Render(radar, "BRITE-Map-" + code + "-Recentered");
            radar.ResetCenter();
            Require(Field<Position>(radar, "panOffset") == default, "Map center was not reset.");
            var saved = Path.Combine(Output, code + "-RadarMap.tower.json");
            JsonStore.Write(saved, simulation.State);
            var restored = JsonStore.LoadSession(saved);
            Require(restored.Airport.RadarMap.Lines.Count == count && restored.Airport.RadarMap.Arcs.Count == arcs &&
                restored.Airport.Original.Fixes.Values.Count(fix => fix.SourceKind is "p" or "o") == symbols.Length, "Saved map differs.");
        }
        File.Delete(Path.Combine(fixture, "CYN", "CYN.MAP"));
        Require(OriginalData.LoadAirport(fixture, "CYN").RadarMap.Lines.Count == 0, "Missing optional MAP prevents loading.");
    }

    private static void CheckCommandInput()
    {
        using (var form = new Form_Commands())
        {
            Require(form.MainMenuStrip is not null && form.Panel_CommandInput.Controls.Contains(form.TextBox_CommandInput), "Command controls are missing from the designer tree.");
            form.ConfigureInput(false);
            var changed = 0;
            form.ExpandCommandsChanged += _ => changed++;
            Field<ToolStripMenuItem>(form, "Menu_Options_ExpandCommands").PerformClick();
            var input = form.TextBox_CommandInput;
            input.Text = "ABC/";
            Call(input, "OnKeyPress", new KeyPressEventArgs('t'));
            Call(input, "OnKeyPress", new KeyPressEventArgs('p'));
            Require(input.RawText == "ABC/tp" && input.Text == "ABC, taxi to parking", "Typed codes were not expanded.");
            Call(input, "OnKeyDown", new KeyEventArgs(Keys.Back));
            Require(input.RawText == "ABC/t" && input.Text == "ABC, taxi to", "Backspace did not remove parking.");
            Call(input, "OnKeyDown", new KeyEventArgs(Keys.Back));
            Require(input.RawText == "ABC/" && input.Text == "ABC, ", "Backspace did not remove taxi to.");
            input.SelectAll();
            var previousClipboard = Clipboard.GetDataObject();
            try
            {
                Clipboard.SetText("NEW/T14/HS14");
                input.Paste();
                Require(input.RawText == "NEW/T14/HS14" && input.Text.Contains("taxi to runway 14 / hold short runway 14"), "Pasted codes changed their meaning.");
            }
            finally
            {
                if (previousClipboard is not null)
                {
                    Clipboard.SetDataObject(previousClipboard, true);
                }
                else
                {
                    Clipboard.Clear();
                }
            }
            Field<ToolStripMenuItem>(form, "Menu_Options_ExpandCommands").PerformClick();
            Require(input.Text == input.RawText && changed == 2, "Switching modes changed the executable input.");
        }
        var simulation = new Simulation(new Session { Options = new() { AircraftPerMinute = 0 } });
        using var window = new Form_SimulationWindow(simulation, Path.Combine(Output, "CommandLayouts"), _ => { }) { Opacity = 0, ShowInTaskbar = false };
        foreach (var name in new[] { "Brite", "ASDE", "Commands", "ArrivalStrips", "DepartureStrips", "ButtonBar" })
        {
            var child = Field<Form>(window, name);
            child.Opacity = 0;
            child.ShowInTaskbar = false;
        }
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "LOG123", useTrafficFlow: false);
        var first = aircraft.LastRadioMessage!;
        var commands = Field<Form_Commands>(window, "Commands");
        Require(commands.TextBox_Log.Text.Contains(first.Text), "Initial radio was not logged before showing the window.");
        window.Show();
        Application.DoEvents();
        Field<System.Windows.Forms.Timer>(window, "Timer").Stop();
        Require(commands.TextBox_Log.Lines.Count(line => line.EndsWith("LOG123: " + first.Text, StringComparison.Ordinal)) == 1, "Showing the window lost or duplicated the initial radio.");
        Require(!commands.TextBox_Log.Text.Contains("right-click menu"), "The obsolete menu hint remains.");
        var commandInput = Field<CommandTextBox>(window, "CommandInput");
        commandInput.ExpandCommands = true;
        Call(window, "AddressAircraft", aircraft);
        Require(commandInput.RawText == "LOG123/" && commandInput.Text == "LOG123, ", "Addressing an aircraft did not preserve raw input.");
        commandInput.Text = "LOG123/TP";
        Render(commands, "Commands-Expanded");
        foreach (var command in new[] { "T18", "HY", "TU" })
        {
            var before = commands.TextBox_Log.Lines.Length;
            commandInput.Text = "LOG123/" + command;
            Call(commandInput, "OnKeyDown", new KeyEventArgs(Keys.Enter));
            var radio = aircraft.LastRadioMessage!;
            Require(radio.IntentKey == "command." + (command == "T18" ? "T" : command) + ".pilot", "Expanded Enter executed the visible text instead of the original code.");
            Require(commands.TextBox_Log.Lines.Skip(before - 1).Count(line => line.EndsWith("LOG123: " + radio.Text, StringComparison.Ordinal)) == 1, "Command radio was lost or duplicated.");
            Require(commandInput.RawText.Length == 0, "Enter did not clear raw input.");
        }
        Require(aircraft.LastRadioMessage!.Text == "Proceeding.", "The TU readback was replaced with a generic Roger response.");
        commandInput.Text = "LOG123/CL";
        Call(commandInput, "OnKeyDown", new KeyEventArgs(Keys.Escape));
        Require(commandInput.RawText.Length == 0, "Escape did not clear input.");
        AnswerClose(window, 6);
    }

    private static void CheckScenarioSaving()
    {
        var simulation = new Simulation(Simulation.CreateFreeSession(Airport.Training(), new() { Runway = "18", AircraftPerMinute = 0 }, seed: 123));
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "SAVE123", useTrafficFlow: false);
        Require(simulation.Execute("SAVE123/CL").Success, "Test clearance failed.");
        aircraft.BriteLabelBearing = 90;
        aircraft.AsdeLabelBearing = 225;
        simulation.Advance(3);
        var file = Path.Combine(Output, "Saved-Scenario.tower.json");
        using var window = new Form_SimulationWindow(simulation, Path.Combine(Output, "SaveLayouts"), _ => { }, scenarioPath: file);
        var save = Field<ToolStripMenuItem>(window, "Menu_File_Save");
        Require(save.ShortcutKeys == (Keys.Control | Keys.S), "Ctrl+S is missing.");
        Require(Field<ToolStripMenuItem>(window, "Menu_File_SaveAs").ShortcutKeys == (Keys.Control | Keys.Shift | Keys.S), "Save As shortcut is missing.");
        var timer = Field<System.Windows.Forms.Timer>(window, "Timer");
        timer.Start();
        simulation.Paused = true;
        save.PerformClick();
        Require(timer.Enabled && simulation.Paused, "Saving changed timer or pause state.");
        timer.Stop();
        var loaded = JsonStore.LoadSession(file);
        Require(loaded.ElapsedSeconds == simulation.State.ElapsedSeconds && loaded.TrafficFlowRandomState == simulation.State.TrafficFlowRandomState,
            "Saved time or generator state differs.");
        var restored = new Simulation(loaded);
        var savedAircraft = restored.State.Aircraft.Single();
        Require(savedAircraft.Position == aircraft.Position && savedAircraft.LandingCleared && savedAircraft.BriteLabelBearing == 90 &&
            savedAircraft.AsdeLabelBearing == 225 && savedAircraft.Path.SequenceEqual(aircraft.Path), "Saved aircraft state differs.");
        simulation.Paused = false;
        simulation.Advance(1);
        save.PerformClick();
        Require(!timer.Enabled && !simulation.Paused, "Saving a stopped timer restarted it or paused the simulation.");
        Require(JsonStore.LoadSession(file).ElapsedSeconds == simulation.State.ElapsedSeconds, "Repeated Save did not update the same file.");
        Require(Field<string>(window, "scenarioFilePath") == Path.GetFullPath(file), "Saved scenario path was not retained.");
    }

    private static void CheckAirportManager()
    {
        var source = TestData;
        var packages = Path.Combine(Output, "ManagerPackages-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(packages);
        Tower.Tests.AirportPackageFixture.Build(TestData, Path.Combine(packages, "TST-1.0.0.towerairport"), "TST");
        Tower.Tests.AirportPackageFixture.Build(TestData, Path.Combine(packages, "TWO-1.0.0.towerairport"), "TWO");
        var data = Path.Combine(Output, "ManagerFixture-" + Guid.NewGuid().ToString("N"), "Data");
        foreach (var file in Directory.GetFiles(Path.Combine(source, "CYN"), "*", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(source, "*.VOC")).Append(CompiledSpeechData.FilePath(source)))
        {
            var target = Path.Combine(data, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        AirportPackages.Install(Path.Combine(packages, "TST-1.0.0.towerairport"), data);
        AirportPackages.Install(Path.Combine(packages, "TWO-1.0.0.towerairport"), data);
        File.Delete(Path.Combine(data, "PACKAGES", "TWO.json"));
        using var manager = new Form_ManageAirports(data) { Opacity = 0, ShowInTaskbar = false };
        manager.Show();
        Application.DoEvents();
        var grid = Field<DataGridView>(manager, "Grid_Airports");
        Require(grid.Columns.Count == 5 && grid.Rows.Count == 3, "Manager columns or airports are missing.");
        var canyon = grid.Rows.Cast<DataGridViewRow>().Single(row => row.Cells[1].Value?.ToString() == "CYN");
        grid.CurrentCell = canyon.Cells[0];
        Require(!Field<Button>(manager, "Button_Remove").Enabled, "Canyon can be removed in the UI.");
        var installed = grid.Rows.Cast<DataGridViewRow>().Single(row => row.Cells[1].Value?.ToString() == "TST");
        grid.CurrentCell = installed.Cells[0];
        Require(Field<Button>(manager, "Button_Remove").Enabled, "Installed add-on cannot be removed.");
        Require(installed.Cells[2].Value?.ToString() == "1.0.0" && installed.Cells[3].Value?.ToString() != "Unknown", "Version or installation date is missing.");
        var unregistered = grid.Rows.Cast<DataGridViewRow>().Single(row => row.Cells[1].Value?.ToString() == "TWO");
        Require(unregistered.Cells[2].Value?.ToString() == "Unknown" && unregistered.Cells[3].Value?.ToString() == "Unknown", "Unregistered airport received invented metadata.");
        Render(manager, "Manage-Airports");
        Call(manager, "SetBusy", true);
        var closing = new FormClosingEventArgs(CloseReason.UserClosing, false);
        Call(manager, "Form_ManageAirports_FormClosing", manager, closing);
        Require(closing.Cancel && !Field<Button>(manager, "Button_Install").Enabled, "Busy manager can close or start another install.");
        Call(manager, "SetBusy", false);
        AirportPackages.Remove(data, "TST");
        Call(manager, "RefreshAirports");
        Require(grid.Rows.Count == 2, "Removed airport remains in the manager.");
        AirportPackages.Install(Path.Combine(packages, "TST-1.0.0.towerairport"), data);
        Call(manager, "RefreshAirports");
        Require(grid.Rows.Count == 3, "Reinstalled airport is missing from the manager.");
        Require(typeof(Form_ManageAirports).Assembly.GetManifestResourceNames().Any(n => n.EndsWith("Form_ManageAirports.resources", StringComparison.Ordinal)), "Designer resource is missing.");
    }

    private static void CheckPanoramaViewport()
    {
        var data = TestData;
        foreach (var code in new[] { "CYN" })
        {
            var airport = OriginalData.LoadAirport(data, code);
            var simulation = new Simulation(new() { Airport = airport, Options = new() { Runway = airport.Runways[0].Name, AircraftPerMinute = 0 } });
            using var view = new TowerView(simulation, new());
            var scenery = AirportScenery.Read(data, airport);
            foreach (var size in new[] { new Size(640, 480), new Size(1280, 720), new Size(1920, 1080) })
            {
                view.Size = size;
                Render(view, code + "-Panorama-" + size.Width);
                var viewport = view.SceneViewport;
                Require(view.ClientRectangle.Contains(viewport), "Scene viewport exceeds the component.");
                var projection = (TowerProjection)typeof(TowerView).GetProperty("Projection", Private)!.GetValue(view)!;
                Require(projection.Width == viewport.Width && projection.Height == viewport.Height, "Aircraft and background projections differ.");
                if (scenery.Panorama is { } image)
                {
                    Require(viewport.Height < size.Height && viewport.Width <= image.Width / Math.PI, "Panorama is still enlarged to fill the window.");
                    using var rendered = new Bitmap(Path.Combine(Output, code + "-Panorama-" + size.Width + ".png"));
                    Require(rendered.GetPixel(15, 15).ToArgb() == Color.Black.ToArgb(), "Overlays escaped the panorama viewport.");
                    var focal = viewport.Width / (2 * Math.Tan(view.CameraFrame.FieldOfView * Math.PI / 360));
                    var sampledRows = viewport.Height * 0.57 * image.Width / (2 * Math.PI * focal);
                    Require(sampledRows <= image.Height - image.HorizonRow, "Viewport exceeds native foreground coverage.");
                }
                else
                {
                    Require(viewport == view.ClientRectangle, "Polygon scenery was unnecessarily reduced.");
                }
            }
            if (scenery.Panorama is not null)
            {
                view.Pan(90);
                view.Zoom(true);
                Render(view, code + "-Panorama-Zoom");
                simulation.State.Options.StartMinutes = 60;
                Render(view, code + "-Panorama-Night");
                var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "VIEW", useTrafficFlow: false);
                Require(view.SetViewMode(TowerViewMode.Pilot, aircraft.CallSign), "Pilot view was not available.");
                Render(view, code + "-Panorama-Pilot");
                Require(view.SceneViewport == view.ClientRectangle, "Pilot view was constrained by the tower panorama.");
            }
        }
    }

    private static void CheckRadarSelection(RadarControl radar, Aircraft aircraft)
    {
        void CheckColor(bool selected)
        {
            using var bitmap = new Bitmap(radar.Width, radar.Height);
            radar.DrawToBitmap(bitmap, radar.ClientRectangle);
            var label = Field<Dictionary<Aircraft, RectangleF>>(radar, "labels")[aircraft];
            var white = 0;
            var green = 0;
            for (var y = Math.Max(0, (int)label.Top); y < Math.Min(bitmap.Height, (int)label.Bottom); y++)
            {
                for (var x = Math.Max(0, (int)label.Left); x < Math.Min(bitmap.Width, (int)label.Right); x++)
                {
                    var color = bitmap.GetPixel(x, y);
                    if (color.R > 200 && color.G > 200 && color.B > 200) white++;
                    if (color.R < 50 && color.G > 100 && color.B < 50) green++;
                }
            }
            Require(selected ? white > 0 : white == 0 && green > 0, "Radar label color does not match explicit selection.");
        }
        radar.SelectedCallsign = "";
        CheckColor(false);
        var target = (PointF)typeof(RadarControl).GetMethod("Screen", Private)!.Invoke(radar, [aircraft.Position])!;
        Call(radar, "MoveLabel", radar, new MouseEventArgs(MouseButtons.None, 0, (int)target.X, (int)target.Y, 0));
        Call(radar, "Down", radar, new MouseEventArgs(MouseButtons.Middle, 1, (int)target.X, (int)target.Y, 0));
        Require(radar.SelectedCallsign.Length == 0, "Hover or middle-click selected a radar aircraft.");
        foreach (var button in new[] { MouseButtons.Left, MouseButtons.Right })
        {
            Call(radar, "Down", radar, new MouseEventArgs(button, 1, (int)target.X, (int)target.Y, 0));
            Require(radar.SelectedCallsign == aircraft.CallSign, "Radar click did not select the aircraft.");
            CheckColor(true);
            radar.Capture = false;
            radar.SelectedCallsign = "";
            CheckColor(false);
        }
    }

    private static void CheckAsdeLabels()
    {
        var airport = Airport.Training();
        airport.Original.FieldElevationFeetMsl = 500;
        var simulation = new Simulation(new() { Airport = airport, Options = new() { Runway = "18", AircraftPerMinute = 0 } });
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "ASDE123", "C182", useTrafficFlow: false);
        aircraft.SpeedKnots = 20;
        using var radar = new RadarControl(simulation, new() { AsdeRange = 2 }, false) { Size = new(500, 400) };
        var labels = Field<Dictionary<Aircraft, RectangleF>>(radar, "labels");
        Render(radar, "ASDE-Label-Default");
        CheckRadarSelection(radar, aircraft);
        var label = labels[aircraft];
        var phase = aircraft.Phase;
        var target = (PointF)typeof(RadarControl).GetMethod("Screen", Private)!.Invoke(radar, [aircraft.Position])!;
        Call(radar, "Down", radar, new MouseEventArgs(MouseButtons.Left, 1, (int)label.X + 2, (int)label.Y + 2, 0));
        for (var bearing = 0; bearing <= 360; bearing += 45)
        {
            var angle = (bearing + 12) * Math.PI / 180;
            Call(radar, "MoveLabel", radar, new MouseEventArgs(MouseButtons.Left, 0,
                (int)Math.Round(target.X + 60 * Math.Sin(angle)), (int)Math.Round(target.Y - 60 * Math.Cos(angle)), 0));
            Require(Math.Abs(RouteMovement.AngleDifference(aircraft.AsdeLabelBearing, bearing % 360)) < 1, "ASDE label rotation failed.");
            Render(radar, "ASDE-Label-" + bearing);
            Require(labels[aircraft].Size == label.Size && !labels[aircraft].Contains(target), "ASDE label layout changed or overlaps the aircraft.");
        }
        radar.Capture = false;
        Require(aircraft.BriteLabelBearing == 45 && aircraft.Phase == phase, "ASDE rotation changed BRITE or aircraft state.");
        Aircraft? addressed = null;
        radar.CommandTargetRequested += value => addressed = value;
        label = labels[aircraft];
        Call(radar, "Down", radar, new MouseEventArgs(MouseButtons.Right, 1, (int)label.X + 2, (int)label.Y + 2, 0));
        Require(addressed == aircraft, "ASDE rotated label command targeting failed.");
        aircraft.AsdeLabelBearing = 225;
        var file = Path.Combine(Output, "ASDE-Label-Session.json");
        JsonStore.Write(file, simulation.State);
        Require(JsonStore.LoadSession(file).Aircraft.Single().AsdeLabelBearing == 225, "ASDE label bearing was lost on reload.");
        var legacy = System.Text.Json.JsonSerializer.Deserialize<Aircraft>("{}", JsonStore.Options)!;
        Require(legacy.AsdeLabelBearing == 45, "Legacy ASDE default is missing.");
        Call(radar, "Down", radar, new MouseEventArgs(MouseButtons.Left, 1, 490, 390, 0));
        Call(radar, "MoveLabel", radar, new MouseEventArgs(MouseButtons.Left, 0, 470, 370, 0));
        Require(Field<Position>(radar, "panOffset") != default, "ASDE map dragging was lost.");
        radar.Capture = false;
    }

    private static void CheckBriteAppearance()
    {
        var airport = Airport.Training();
        var runway = airport.Runways[0];
        var simulation = new Simulation(new() { Airport = airport, Options = new() { Runway = runway.Name, AircraftPerMinute = 0 } });
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "UAL117", "C182", useTrafficFlow: false);
        aircraft.Position = airport.Point(runway.Threshold).Towards(airport.Point(runway.End), 1500).Offset(270, 3 * 1852);
        aircraft.AltitudeFeet = 3800;
        aircraft.SpeedKnots = 190;
        using var radar = new RadarControl(simulation, new() { BriteRange = 10, ShowRangeMarks = true, RangeMarks = 5 }, true) { Size = new(380, 370) };
        Render(radar, "BRITE-Compact-Targets");
        CheckRadarSelection(radar, aircraft);
        var labels = Field<Dictionary<Aircraft, RectangleF>>(radar, "labels");
        Require(labels.TryGetValue(aircraft, out var rectangle), "BRITE target is missing.");
        Require(rectangle.Width < 70 && rectangle.Height < 32, "BRITE label is not compact.");
        Aircraft? addressed = null;
        radar.CommandTargetRequested += value => addressed = value;
        var x = (int)rectangle.X + 2;
        var y = (int)rectangle.Y + 2;
        Call(radar, "Down", radar, new MouseEventArgs(MouseButtons.Right, 1, x, y, 0));
        Require(addressed == aircraft, "Compact label right-click did not address the aircraft.");
        Render(radar, "BRITE-Compact-Selected");
        var before = (aircraft.LabelX, aircraft.LabelY);
        Call(radar, "Down", radar, new MouseEventArgs(MouseButtons.Left, 1, x, y, 0));
        var target = (PointF)typeof(RadarControl).GetMethod("Screen", Private)!.Invoke(radar, [aircraft.Position])!;
        for (var bearing = 0; bearing <= 360; bearing += 45)
        {
            var radians = (bearing + 12) * Math.PI / 180;
            var pointer = new Point((int)Math.Round(target.X + Math.Sin(radians) * 60),
                (int)Math.Round(target.Y - Math.Cos(radians) * 60));
            Call(radar, "MoveLabel", radar, new MouseEventArgs(MouseButtons.Left, 0, pointer.X, pointer.Y, 0));
            Require(Math.Abs(RouteMovement.AngleDifference(aircraft.BriteLabelBearing, bearing % 360)) < 1, "BRITE label did not rotate to the mouse bearing.");
            Render(radar, "BRITE-Rotation-" + bearing);
            var label = labels[aircraft];
            Require(!label.Contains(target), "BRITE label overlaps its aircraft.");
            Require(label.Size == rectangle.Size, "BRITE label changes size while rotating.");
        }
        Require(aircraft.LabelX == before.LabelX && aircraft.LabelY == before.LabelY, "BRITE rotation changed ASDE offsets.");
        radar.Capture = false;
        aircraft.BriteLabelBearing = 135;
        Render(radar, "BRITE-Rotation-Follow-Before");
        var oldLabel = labels[aircraft];
        aircraft.Position = aircraft.Position.Offset(90, 500);
        Render(radar, "BRITE-Rotation-Follow-After");
        Require(labels[aircraft].Left > oldLabel.Left && aircraft.BriteLabelBearing == 135, "BRITE label did not follow its aircraft.");
        var saved = Path.Combine(Output, "BRITE-Label-Session.json");
        JsonStore.Write(saved, simulation.State);
        var restored = JsonStore.LoadSession(saved);
        Require(restored.Aircraft.Single().BriteLabelBearing == 135, "Saved BRITE label bearing was lost.");
        var legacy = System.Text.Json.JsonSerializer.Deserialize<Aircraft>("{}", JsonStore.Options)!;
        Require(legacy.BriteLabelBearing == 45, "Older saves did not receive the default BRITE label bearing.");
        var movedLabel = labels[aircraft];
        addressed = null;
        Call(radar, "Down", radar, new MouseEventArgs(MouseButtons.Right, 1, (int)movedLabel.X + 2, (int)movedLabel.Y + 2, 0));
        Require(addressed == aircraft, "Rotated label lost command targeting.");
        target = (PointF)typeof(RadarControl).GetMethod("Screen", Private)!.Invoke(radar, [aircraft.Position])!;
        Call(radar, "Down", radar, new MouseEventArgs(MouseButtons.Left, 1, (int)target.X, (int)target.Y, 0));
        Call(radar, "MoveLabel", radar, new MouseEventArgs(MouseButtons.Left, 0, (int)target.X - 60, (int)target.Y, 0));
        Require(Math.Abs(RouteMovement.AngleDifference(aircraft.BriteLabelBearing, 270)) < 1, "Dragging the T symbol did not rotate its label.");
        radar.Capture = false;
    }

    private static void CheckWindow(Airport airport, string runway)
    {
        var simulation = new Simulation(new() { Airport = airport, Options = new() { Runway = runway, AircraftPerMinute = 0 } });
        var arrival = simulation.AddAircraft(FlightDirection.Arrival, "ARRCHECK", "C182", useTrafficFlow: false);
        var departure = simulation.AddAircraft(FlightDirection.Departure, "DEPCHECK", "C182", useTrafficFlow: false);
        using var window = new Form_SimulationWindow(simulation, Path.Combine(Output, "Layouts", airport.Code), _ => { }) { Opacity = 0, ShowInTaskbar = false };
        var names = new[] { "Brite", "ASDE", "Commands", "ArrivalStrips", "DepartureStrips", "ButtonBar" };
        var children = names.Select(name => Field<Form>(window, name)).ToArray();
        foreach (var child in children)
        {
            child.Opacity = 0;
            child.ShowInTaskbar = false;
        }
        try
        {
            window.Show();
            Field<System.Windows.Forms.Timer>(window, "Timer").Stop();
            simulation.Paused = true;
            Application.DoEvents();
            Field<System.Windows.Forms.Timer>(window, "Timer").Stop();
            Call(window, "RefreshStrips");
            CheckViewModes(window, simulation, arrival, departure);
            var arrivals = Field<Form_FlightStrips>(window, "ArrivalStrips");
            var departures = Field<Form_FlightStrips>(window, "DepartureStrips");
            Require(arrivals.FlowLayoutPanel_StripList.Controls.OfType<FlightStripControl>().Single(strip => strip.Aircraft is not null).Aircraft == arrival, "Arrival strip is in the wrong window.");
            var strip = departures.FlowLayoutPanel_StripList.Controls.OfType<FlightStripControl>().Single(strip => strip.Aircraft is not null);
            Require(strip.Aircraft == departure, "Departure strip is in the wrong window.");
            typeof(Control).GetMethod("OnMouseDown", Private)!.Invoke(strip, [new MouseEventArgs(MouseButtons.Right, 1, 10, 10, 0)]);
            var input = Field<Control>(window, "CommandInput");
            Require(input.Text == "DEPCHECK/", "Right-clicking a strip did not address the command input.");
            var commands = Field<Form_Commands>(window, "Commands");
            commands.Width += 180;
            commands.PerformLayout();
            Require(input.Width == commands.Panel_CommandInput.ClientSize.Width, "Command input does not resize with its window.");
            foreach (var name in new[] { "AirRadar", "GroundRadar" })
            {
                var radar = Field<RadarControl>(window, name);
                Render(radar, airport.Code + "-" + name);
                var labels = Field<Dictionary<Aircraft, RectangleF>>(radar, "labels");
                var aircraft = name == "AirRadar" ? arrival : departure;
                Require(labels.TryGetValue(aircraft, out var label), name + " is missing its aircraft.");
                Call(radar, "Down", radar, new MouseEventArgs(MouseButtons.Right, 1, (int)label.X + 2, (int)label.Y + 2, 0));
                Require(input.Text == aircraft.CallSign + "/", name + " right-click did not address the aircraft.");
            }
            var view = Field<TowerView>(window, "View");
            var bearing = view.CameraFrame.Bearing;
            Field<ToolStripMenuItem>(window, "Menu_3DView_PanLeft").PerformClick();
            Require(Math.Abs(view.CameraFrame.Bearing - Coordinates.NormalizeHeading(bearing - 10)) < 0.000001, "Pan Left is not ten degrees.");
            Field<ToolStripMenuItem>(window, "Menu_3DView_PanRight").PerformClick();
            Require(Math.Abs(view.CameraFrame.Bearing - bearing) < 0.000001, "Pan Right is not ten degrees.");
            Field<ToolStripMenuItem>(window, "Menu_3DView_ZoomIn").PerformClick();
            Require(view.CameraFrame.FieldOfView == 72, "Zoom menu is not wired.");
            Field<ToolStripMenuItem>(window, "Menu_3DView_ZoomOut").PerformClick();
            view.Track(departure.CallSign);
            Render(view, airport.Code + "-TowerView");
            foreach (var child in children)
            {
                Render(child, airport.Code + "-" + child.Name);
            }
            // Complete a real arrival, then verify both strip removal and retained command log.
            simulation.State.Aircraft.Remove(departure);
            simulation.Paused = false;
            Require(simulation.Execute("ARRCHECK/CL").Success, "Landing clearance rejected.");
            AdvanceUntil(simulation, arrival, FlightPhase.ArrivalHold);
            Require(simulation.Execute("ARRCHECK/TP").Success, "Parking clearance rejected.");
            AdvanceUntil(simulation, arrival, FlightPhase.Completed);
            simulation.Paused = true;
            Call(window, "RefreshStrips");
            Require(!arrivals.FlowLayoutPanel_StripList.Controls.OfType<FlightStripControl>().Any(strip => strip.Aircraft is not null), "Completed arrival strip remains visible.");
            Require(commands.TextBox_Log.Text.Contains("parked at", StringComparison.Ordinal), "Parking log was lost.");
            CheckDeletion(window, simulation);
            simulation.Paused = false;
            AnswerClose(window, 7);
            Require(!window.IsDisposed && !simulation.Paused, "No must leave the simulation open and running.");
            AnswerClose(window, 6);
            Require(window.IsDisposed && simulation.Paused, "Yes must close the window and stop the simulation.");
            Require(children.All(child => child.IsDisposed), "Owned simulation windows were not disposed.");
            Report.Add($"PASS {airport.Code}/{runway}: radar and TowerView rendering, split strips, right-click commands, resize, pan/zoom, arrival completion/log and No/Yes shutdown.");
        }
        finally
        {
            if (!window.IsDisposed)
            {
                foreach (var child in children)
                {
                    child.Dispose();
                }
                Field<System.Windows.Forms.Timer>(window, "Timer").Dispose();
                Call(window, "Form_SimulationWindow_FormClosed", window, new FormClosedEventArgs(CloseReason.ApplicationExitCall));
            }
        }
    }

    private static void AdvanceUntil(Simulation simulation, Aircraft aircraft, FlightPhase phase)
    {
        for (var second = 0; second < 1800 && aircraft.Phase != phase; second++)
        {
            if (aircraft.Holding && aircraft.Phase is FlightPhase.TaxiIn or FlightPhase.TaxiOut)
            {
                Require(simulation.Execute(aircraft.CallSign + "/TU").Success, "Crossing clearance rejected.");
            }
            simulation.Advance(1);
        }
        Require(aircraft.Phase == phase, $"{aircraft.CallSign} did not reach {phase}: {aircraft.Phase}.");
    }

    private static void AnswerClose(Form window, int answer)
    {
        AnswerDialog(window.Close, answer);
    }

    private static void CheckViewModes(Form_SimulationWindow window, Simulation simulation, Aircraft arrival, Aircraft departure)
    {
        var view = Field<TowerView>(window, "View");
        var modes = new[] { TowerViewMode.Normal, TowerViewMode.Tracking, TowerViewMode.Binocular,
            TowerViewMode.SuperBinocular, TowerViewMode.SpotPlane, TowerViewMode.Pilot };
        ToolStripMenuItem Menu(TowerViewMode mode) => Field<ToolStripMenuItem>(window, "Menu_3DView_" + mode);
        void Check(TowerViewMode expected)
        {
            Require(view.ViewMode == expected && Menu(expected).Checked && modes.Count(mode => Menu(mode).Checked) == 1,
                "3D view checkmarks do not match the active camera: " + expected);
            Require(Field<ToolStripMenuItem>(window, "Menu_3DView_ZoomIn").Enabled == view.CanZoom,
                "Zoom availability does not match the view mode.");
        }
        Check(TowerViewMode.Normal);
        Console.WriteLine($"Checking {simulation.State.Airport.Code}: initial view selection.");
        AnswerDialog(Menu(TowerViewMode.Tracking).PerformClick, 1);
        Check(TowerViewMode.Normal);
        Menu(TowerViewMode.Binocular).PerformClick();
        Check(TowerViewMode.Binocular);
        Require(view.TrackingCallsign is null, "Fixed binoculars should not require an aircraft.");
        Call(window, "Select", departure);
        foreach (var mode in modes)
        {
            Console.WriteLine($"Checking {simulation.State.Airport.Code} view: {mode}.");
            Menu(mode).PerformClick();
            Check(mode);
            Render(view, simulation.State.Airport.Code + "-View-" + mode);
            if (mode is not (TowerViewMode.Normal or TowerViewMode.Binocular))
            {
                Require(view.TrackingCallsign == departure.CallSign, "Camera targets the wrong aircraft.");
                Call(window, "Select", arrival);
                Check(mode);
                Require(view.TrackingCallsign == arrival.CallSign, "Selecting another aircraft did not retarget the active camera.");
                Render(view, simulation.State.Airport.Code + "-ArrivalView-" + mode);
                Call(window, "Select", departure);
            }
        }
        view.Track(departure.CallSign);
        Check(TowerViewMode.Tracking);
        view.Pan(10);
        Check(TowerViewMode.Normal);
        view.SetViewMode(TowerViewMode.SuperBinocular, departure.CallSign);
        simulation.State.Aircraft.Remove(departure);
        Render(view, simulation.State.Airport.Code + "-MissingTarget");
        Check(TowerViewMode.Normal);
        simulation.State.Aircraft.Add(departure);
        Menu(TowerViewMode.Normal).PerformClick();
        Report.Add($"PASS {simulation.State.Airport.Code}: all six view modes, exclusive checkmarks, selection requirements, retargeting, double-click tracking path, manual pan and missing-target fallback.");
    }

    private static void CheckDeletion(Form_SimulationWindow window, Simulation simulation)
    {
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "DELETECHECK", "C182", useTrafficFlow: false);
        Call(window, "RefreshStrips");
        var menu = Field<ToolStripMenuItem>(window, "Menu_Simulation_DeleteAircraft");
        Console.WriteLine("Checking Delete Aircraft without selection: " + Field<string>(window, "Selected"));
        AnswerDialog(menu.PerformClick, 1);
        Require(simulation.State.Aircraft.Contains(aircraft), "Delete without selection removed an aircraft.");
        Call(window, "AddressAircraft", aircraft);
        Field<TowerView>(window, "View").Track(aircraft.CallSign);
        simulation.Paused = false;
        Console.WriteLine("Checking Delete Aircraft: No.");
        AnswerDialog(menu.PerformClick, 7);
        Require(simulation.State.Aircraft.Contains(aircraft) && !simulation.Paused, "No must preserve the aircraft and running state.");
        simulation.Paused = true;
        var score = simulation.State.Performance.Score;
        Console.WriteLine("Checking Delete Aircraft: Yes.");
        AnswerDialog(menu.PerformClick, 6);
        Require(!simulation.State.Aircraft.Contains(aircraft) && simulation.Paused, "Yes must delete the aircraft and preserve pause.");
        Require(simulation.State.Performance.Score == score, "Deleting aircraft changed the score.");
        Require(Field<Dictionary<string, FlightStripControl>>(window, "FlightStrips").Count == 0, "Deleted flightstrip remains.");
        Require(Field<string>(window, "Selected") == "" && Field<Control>(window, "CommandInput").Text == "", "Deleted aircraft remains selected or addressed.");
        Require(Field<TowerView>(window, "View").TrackingCallsign is null, "Deleted aircraft is still tracked.");
        Require(Field<ToolStripMenuItem>(window, "Menu_3DView_Normal").Checked, "Deleting the camera target did not restore the Normal menu check.");
        Require(Field<Form_Commands>(window, "Commands").TextBox_Log.Text.Contains("DELETECHECK: deleted from simulation.", StringComparison.Ordinal), "Deletion log is missing.");
        Report.Add($"PASS {simulation.State.Airport.Code}: Delete Aircraft menu, no selection, No/Yes, pause restoration, strip/input/tracking cleanup and unchanged score.");
    }

    private static void AnswerDialog(Action action, int answer)
    {
        var thread = GetCurrentThreadId();
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        using var timer = new System.Windows.Forms.Timer { Interval = 50 };
        timer.Tick += (_, _) =>
        {
            foreach (var review in Application.OpenForms.OfType<Form_Performance>().ToArray())
            {
                // Ending a scenario now opens its performance review before closing.
                review.Close();
            }
            if (elapsed.Elapsed > TimeSpan.FromSeconds(10))
            {
                timer.Stop();
                throw new TimeoutException("The expected Windows dialog did not accept its test response.");
            }
            EnumThreadWindows(thread, (handle, _) =>
            {
                var name = new System.Text.StringBuilder(64);
                GetClassName(handle, name, name.Capacity);
                if (name.ToString() == "#32770")
                {
                    // A dialog can be enumerated before its command handler is ready.
                    // Keep retrying until the modal action returns and disposes this timer.
                    var button = GetDlgItem(handle, answer);
                    if (button == 0 && answer == 1)
                    {
                        // Windows can assign IDCANCEL to the only OK button.
                        button = GetDlgItem(handle, 2);
                    }
                    if (button != 0)
                    {
                        PostMessage(button, 0xF5, 0, 0);
                    }
                }
                return true;
            }, 0);
        };
        timer.Start();
        action();
    }

    private static void CheckSpeech(string data, IReadOnlyList<string> airports)
    {
        using var synthesizer = new SpeechSynthesizer();
        var voice = synthesizer.GetInstalledVoices().FirstOrDefault(voice => voice.Enabled && voice.VoiceInfo.Culture.TwoLetterISOLanguageName == "en");
        if (voice is not null)
        {
            synthesizer.SelectVoice(voice.VoiceInfo.Name);
            using var wave = new MemoryStream();
            synthesizer.SetOutputToWaveStream(wave);
            synthesizer.Speak("Test one, roger taxi to runway one four.");
            Require(wave.Length > 44, "Windows speech did not produce audio.");
            File.WriteAllBytes(Path.Combine(Output, "windows-speech.wav"), wave.ToArray());
            Report.Add("PASS Windows English speech rendered to WAV; physical speaker playback was not checked.");
        }
        else
        {
            Report.Add("LIMITATION: no English Windows desktop voice installed.");
        }
        if (airports.Count == 0)
        {
            return;
        }
        var runtime = new WorkbenchSpeechRuntime(data);
        runtime.Preferences.Engine = SpeechEngineKind.BaoOriginal;
        var callsigns = runtime.GetTrafficCallsigns()!;
        Require(callsigns.Count > 0, "No approved BAO callsigns are available.");
        foreach (var code in airports)
        {
            var airport = OriginalData.LoadAirport(data, code);
            var runway = OriginalData.ReadDefaultRunways(Path.Combine(Path.GetDirectoryName(data)!, "settings.ini"), airport)[0];
            var simulation = new Simulation(new() { Airport = airport, Options = new() { Runway = runway, AircraftPerMinute = 0 } }, callsigns);
            var messages = new List<AircraftRadioMessage>();
            simulation.RadioMessage += messages.Add;
            var aircraft = simulation.AddAircraft(FlightDirection.Arrival, callsigns.First(), "C182", useTrafficFlow: false);
            simulation.Execute(aircraft.CallSign + "/CL");
            AdvanceUntil(simulation, aircraft, FlightPhase.ArrivalHold);
            simulation.Execute(aircraft.CallSign + "/TP");
            AdvanceUntil(simulation, aircraft, FlightPhase.Completed);
            aircraft = simulation.AddAircraft(FlightDirection.Departure, callsigns.First(), "C182", useTrafficFlow: false);
            if (aircraft.Pushback is not null)
            {
                Require(simulation.Execute(aircraft.CallSign + "/PB").Success, "Pushback rejected.");
                AdvanceUntil(simulation, aircraft, FlightPhase.ReadyForTaxi);
            }
            Require(simulation.Execute(aircraft.CallSign + "/T" + runway).Success, "Departure taxi rejected.");
            AdvanceUntil(simulation, aircraft, FlightPhase.HoldingShort);
            Require(simulation.Execute(aircraft.CallSign + "/TH").Success, "Lineup rejected.");
            AdvanceUntil(simulation, aircraft, FlightPhase.LinedUp);
            Require(simulation.Execute(aircraft.CallSign + "/CT").Success, "Takeoff rejected.");
            AdvanceUntil(simulation, aircraft, FlightPhase.Completed);
            var diagnostics = new List<string>();
            foreach (var message in messages)
            {
                runtime.Preferences.Engine = SpeechEngineKind.WindowsTts;
                var windows = runtime.Render(message);
                Require(!string.IsNullOrWhiteSpace(windows.Text) && windows.Diagnostic is null, "Windows radio text failed: " + windows.Diagnostic);
                runtime.Preferences.Engine = SpeechEngineKind.BaoOriginal;
                var original = runtime.Render(message);
                if (original.Diagnostic is not null)
                {
                    diagnostics.Add(message.IntentKey + ": " + original.Diagnostic);
                }
                else
                {
                    Require(original.Wave is { Length: > 44 }, "BAO message produced no audio or diagnostic.");
                }
            }
            File.WriteAllLines(Path.Combine(Output, code + "-bao-diagnostics.txt"), diagnostics.Distinct());
            Report.Add($"{code} speech: {messages.Count} real arrival/departure messages, Windows text complete, BAO {messages.Count - diagnostics.Count}/{messages.Count} rendered; see BAO diagnostics for missing reviews/fragments.");
        }
    }

    private delegate bool WindowCallback(nint window, nint parameter);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint threadId, WindowCallback callback, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, System.Text.StringBuilder name, int count);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern nint GetDlgItem(nint window, int identifier);
}
