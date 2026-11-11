using Tower.Core;
using Tower.Infrastructure;
using TowerSpeech.Core;
using Xunit;

namespace Tower.Tests;

public sealed class RadioEventTimingTests
{
    private static Simulation Create() => new(new Session { Options = new() { AircraftPerMinute = 0 } });

    private static void Until(Simulation simulation, Func<bool> complete, int seconds = 1500)
    {
        for (var index = 0; index < seconds && !complete(); index++)
        {
            simulation.Advance(1);
        }
        Assert.True(complete(), "The expected radio/flight transition was not reached.");
    }

    [Fact]
    public void Reaching_departure_hold_reports_ready_once_and_repeats_only_on_SG()
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "TEST");
        var messages = new List<AircraftRadioMessage>();
        simulation.RadioMessage += messages.Add;
        Assert.True(simulation.Execute("TEST/T18").Success);
        Until(simulation, () => aircraft.Phase == FlightPhase.HoldingShort);
        Assert.Single(messages, message => message.IntentKey == "departure.takeoff.ready");
        Assert.DoesNotContain(messages, message => message.IntentKey == "ground.holding.short");
        simulation.Advance(10);
        Assert.Single(messages, message => message.IntentKey == "departure.takeoff.ready");
        simulation.Advance(120);
        Assert.Single(messages, message => message.IntentKey == "departure.takeoff.ready");
        Assert.True(simulation.Execute("TEST/SG").Success);
        Assert.Equal(2, messages.Count(message => message.IntentKey == "departure.takeoff.ready"));
        simulation.Advance(121);
        Assert.Equal(2, messages.Count(message => message.IntentKey == "departure.takeoff.ready"));
    }

    [Fact]
    public void Missed_approach_without_clearance_is_distinct_from_an_instruction()
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "TEST");
        var messages = new List<AircraftRadioMessage>();
        simulation.RadioMessage += messages.Add;
        Assert.True(simulation.Execute("TEST/G").Success);
        Assert.Equal("command.G.pilot", Assert.Single(messages).IntentKey);
        Assert.Equal("Roger, go around.", aircraft.LastTransmission);
        aircraft.Phase = FlightPhase.Approach;
        aircraft.Position = simulation.State.Airport.Point("R18").Offset(180, 100);
        aircraft.AltitudeFeet = 10;
        aircraft.LandingCleared = false;
        simulation.Advance(0.05);
        Assert.Equal("flight.goaround", messages[^1].IntentKey);
        Assert.Equal("Executing missed approach.", messages[^1].Text);
        Assert.Equal(2, messages.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Go_around_requests_approach_and_requires_new_entry_even_after_save(bool automatic)
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "TEST");
        var messages = new List<AircraftRadioMessage>();
        simulation.RadioMessage += messages.Add;
        if (automatic)
        {
            aircraft.Position = simulation.State.Airport.Point("R18").Offset(180, 100);
            aircraft.AltitudeFeet = 10;
            aircraft.LandingCleared = false;
            simulation.Advance(0.05);
            Assert.Equal("flight.goaround", messages[^1].IntentKey);
        }
        else
        {
            Assert.True(simulation.Execute("TEST/G").Success);
            Assert.Equal("command.G.pilot", messages[^1].IntentKey);
        }
        Assert.DoesNotContain(messages, m => m.IntentKey == "flight.approach.request");
        Until(simulation, () => aircraft.GoAroundApproachRequests == 1);
        Assert.Equal("Request approach to runway 18.", messages[^1].Text);
        var file = Path.Combine(Path.GetTempPath(), "ApproachRequest-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            JsonStore.Write(file, simulation.State);
            simulation = new Simulation(JsonStore.LoadSession(file));
            aircraft = Assert.Single(simulation.State.Aircraft);
            messages.Clear();
            simulation.RadioMessage += messages.Add;
            simulation.Advance(0.1);
            Assert.DoesNotContain(messages, m => m.IntentKey == "flight.approach.request");
            Until(simulation, () => aircraft.GoAroundApproachRequests == 2);
            Assert.Single(messages, m => m.IntentKey == "flight.approach.request");
            var position = aircraft.Position;
            simulation.Advance(120);
            Assert.NotEqual(position, aircraft.Position);
            Assert.Equal(FlightPhase.GoAround, aircraft.Phase);
            Assert.Single(messages, m => m.IntentKey == "flight.approach.request");
            Assert.DoesNotContain(messages, m => m.IntentKey == "flight.established.final");
            Assert.False(simulation.Execute("TEST/CL").Success);
            Assert.True(simulation.Execute("TEST/EF18").Success);
            Assert.Equal(FlightPhase.Circuit, aircraft.Phase);
            Assert.False(aircraft.LandingCleared);
            Assert.True(simulation.Execute("TEST/CL").Success);
            Until(simulation, () => aircraft.Phase == FlightPhase.ArrivalHold);
            Assert.Single(messages, m => m.IntentKey == "flight.approach.request");
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Pattern_readbacks_and_actual_reports_are_separate_and_not_repeated_every_tick()
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "TEST");
        var messages = new List<AircraftRadioMessage>();
        simulation.RadioMessage += messages.Add;
        Assert.True(simulation.Execute("TEST/PRD18/CL").Success);
        Assert.Equal("command.pattern.report", messages[0].IntentKey);
        Assert.Equal("right downwind", messages[0].Detail);
        Until(simulation, () => aircraft.Phase == FlightPhase.ArrivalHold);
        Assert.Single(messages, message => message.IntentKey == "flight.downwind");
        Assert.Single(messages, message => message.IntentKey == "flight.base.fullstop");
        Assert.Single(messages, message => message.IntentKey == "flight.turning.final");
    }

    [Theory]
    [InlineData("@TG @LP", "Single", "flight.onthego.turnout")]
    [InlineData("@TG @FS", "Single", "flight.onthego")]
    [InlineData("@TG @LP", "Airline", "flight.onthego")]
    [InlineData("@LA @LP", "Single", "flight.onthego.turnout")]
    public void On_the_go_request_depends_on_completed_pass_and_next_action(string actions, string group, string expected)
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "TEST", originalPlan: new()
        {
            Actions = OriginalActionProgress.Parse(actions.Split(' ')), Squawk = "1200"
        });
        aircraft.Specification.OriginalTypeGroup = group;
        aircraft.Position = simulation.State.Airport.Point("R18");
        aircraft.AltitudeFeet = 0;
        // Start at touchdown so this fixture tests radio/action ordering.
        aircraft.TouchdownDistanceMetres = 0;
        var messages = new List<AircraftRadioMessage>();
        simulation.RadioMessage += messages.Add;
        Assert.True(simulation.Execute("TEST/CL").Success);
        simulation.Advance(0.05);
        Assert.DoesNotContain(messages, message => message.IntentKey.StartsWith("flight.onthego", StringComparison.Ordinal));
        aircraft.Path = [aircraft.Position];
        aircraft.PathIndex = 0;
        aircraft.Movement = null;
        simulation.Advance(0.05);
        Assert.Equal(expected, messages[^1].IntentKey);
        simulation.Advance(1);
        Assert.Single(messages, message => message.IntentKey.StartsWith("flight.onthego", StringComparison.Ordinal));
    }

    [Fact]
    public void Blockage_is_reported_only_in_response_to_a_command_while_stopped()
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "TEST", assignedGate: "G1");
        var obstruction = simulation.AddAircraft(FlightDirection.Departure, "BLOCK", assignedGate: "G2");
        Assert.True(simulation.Execute("TEST/T18").Success);
        var next = aircraft.Path[aircraft.PathIndex];
        obstruction.Position = aircraft.Position.Towards(next, 20);
        var messages = new List<AircraftRadioMessage>();
        simulation.RadioMessage += messages.Add;
        simulation.Advance(5);
        Assert.Equal(0, aircraft.SpeedKnots);
        Assert.DoesNotContain(messages, message => message.IntentKey == "ground.blocked");
        Assert.True(simulation.Execute("TEST/TU").Success);
        Assert.Single(messages, message => message.IntentKey == "ground.blocked");
        simulation.Advance(5);
        Assert.Single(messages, message => message.IntentKey == "ground.blocked");
        Assert.True(simulation.Execute("TEST/SS").Success);
        Assert.Equal(2, messages.Count(message => message.IntentKey == "ground.blocked"));
        obstruction.Position = new(-10000, -10000);
        simulation.Advance(1);
        Assert.False(aircraft.TrafficObstructionReported);
        Assert.True(simulation.Execute("TEST/SS").Success);
        Assert.NotEqual("ground.blocked", messages[^1].IntentKey);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Departure_queue_is_silent_but_opposing_departure_is_reported_on_command(bool opposing)
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "TEST", assignedGate: "G1");
        var other = simulation.AddAircraft(FlightDirection.Departure, "QUEUE", assignedGate: "G2");
        Assert.True(simulation.Execute("TEST/T18").Success);
        other.Position = aircraft.Position.Towards(aircraft.Path[aircraft.PathIndex], 20);
        other.Phase = FlightPhase.HoldingShort;
        other.Runway = aircraft.Runway;
        other.Heading = Coordinates.NormalizeHeading(aircraft.Heading + (opposing ? 180 : 0));
        var messages = new List<AircraftRadioMessage>();
        simulation.RadioMessage += messages.Add;
        simulation.Advance(2);
        Assert.Equal(0, aircraft.SpeedKnots);
        Assert.DoesNotContain(messages, message => message.IntentKey == "ground.blocked");
        Assert.True(simulation.Execute("TEST/HY").Success);
        Assert.Equal(opposing, messages[^1].IntentKey == "ground.blocked");
    }

    [Fact]
    public void Taxi_to_parking_stops_silently_until_addressed()
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "TEST", assignedGate: "G1");
        var other = simulation.AddAircraft(FlightDirection.Departure, "GATE", assignedGate: "G2");
        Assert.True(simulation.Execute("TEST/T18").Success);
        aircraft.Direction = FlightDirection.Arrival;
        aircraft.Phase = FlightPhase.TaxiIn;
        other.Position = aircraft.Position.Towards(aircraft.Path[aircraft.PathIndex], 20);
        var messages = new List<AircraftRadioMessage>();
        simulation.RadioMessage += messages.Add;
        simulation.Advance(2);
        Assert.Equal(0, aircraft.SpeedKnots);
        Assert.Empty(messages);
        Assert.True(simulation.Execute("TEST/SS").Success);
        Assert.Single(messages, message => message.IntentKey == "ground.blocked");
    }

    [Fact]
    public void Say_again_keeps_numeric_snapshot_after_saving_without_new_settings()
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "TEST");
        aircraft.AltitudeFeet = 1200;
        aircraft.VerticalSpeedFpm = 300;
        Assert.True(simulation.Execute("TEST/SA").Success);
        var original = aircraft.LastRadioMessage;
        var path = Path.Combine(Path.GetTempPath(), "Tower-Radio-" + Guid.NewGuid() + ".json");
        try
        {
            JsonStore.Write(path, simulation.State);
            var restored = new Simulation(JsonStore.LoadSession(path));
            restored.State.Aircraft[0].AltitudeFeet = 2300;
            Assert.True(restored.Execute("TEST/SG").Success);
            Assert.Equal(original, restored.State.Aircraft[0].LastRadioMessage);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Final_readback_only_adds_runway_when_requested_and_retains_context_for_templates()
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "TEST");
        Assert.True(simulation.Execute("TEST/EF").Success);
        var message = aircraft.LastRadioMessage!;
        Assert.Equal("18", message.Runway);
        Assert.False(message.IncludeRunway);
        Assert.Equal("Roger, entering final.", message.Text);
        Assert.Empty(WorkbenchRadioAdapter.Create(message).Parts.OfType<RunwayNumber>());
        Assert.True(simulation.Execute("TEST/EF18").Success);
        Assert.Single(WorkbenchRadioAdapter.Create(aircraft.LastRadioMessage!).Parts.OfType<RunwayNumber>());
    }

    [Theory]
    [InlineData("AT", "")]
    [InlineData("ALT", "left")]
    [InlineData("ART", "right")]
    public void Turnout_commands_have_distinct_readbacks_without_being_parsed_as_heading_turns(string command, string side)
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "TEST");
        Assert.True(simulation.Execute("TEST/" + command).Success);
        Assert.Equal("command.turnout", aircraft.LastRadioMessage!.IntentKey);
        Assert.Equal(side, aircraft.LastRadioMessage.Detail);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Original_airport_banks_render_the_changed_radio_families()
    {
        var data = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA")
            ?? throw new InvalidOperationException("Set TOWER_ORIGINAL_DATA.");
        var runtime = new WorkbenchSpeechRuntime(data);
        runtime.Preferences.Engine = SpeechEngineKind.BaoOriginal;
        foreach (var (airport, runway) in new[] { ("CYN", "14") })
        {
            foreach (var (intent, detail, number) in new (string, string, double)[]
            {
                ("departure.takeoff.ready", "", 0), ("command.pattern.enter", "final", 0),
                ("command.pattern.report", "left downwind", 0), ("command.G.pilot", "", 0),
                ("flight.goaround", "", 0), ("flight.approach.request", "", 0), ("flight.onthego.turnout", "", 0),
                ("flight.base.touchandgo", "", 0), ("flight.turning.final", "", 0),
                ("command.FD.pilot", "", 0), ("ground.blocked", "", 0),
                ("status.altitude", "climbing", 1200), ("status.speed", "", 180),
                ("status.position", "gate:G3", 0), ("status.position", "2", 12),
                ("command.heading", "right", 90), ("command.turnout", "left", 0)
            })
            {
                var rendered = runtime.Render(new("VOTER51", airport, "ignored", runway, intent, Value: number, Detail: detail));
                Assert.True(rendered.Diagnostic is null, airport + " / " + intent + ": " + rendered.Diagnostic);
                Assert.True(rendered.Wave is { Length: > 44 });
            }
        }
    }
}
