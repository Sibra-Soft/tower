using Tower.Core;
using Tower.Infrastructure;
using TowerSpeech.Core;
using Xunit;

namespace Tower.Tests;

[Trait("Category", "Integration")]
public sealed class PushbackTests
{
    private static string Data => Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA")
        ?? throw new InvalidOperationException("Set TOWER_ORIGINAL_DATA.");

    private static Simulation Create(string code, string runway) => new(new Session
    {
        Airport = OriginalData.LoadAirport(Data, code),
        Options = new() { Runway = runway, AircraftPerMinute = 0 }
    });

    private static void Until(Simulation simulation, Func<bool> done, int seconds = 1200)
    {
        for (var i = 0; i < seconds && !done(); i++)
        {
            simulation.Advance(1);
        }
        Assert.True(done());
    }

    [Theory]
    [InlineData("CYN", "9", "G1", 9)]
    public void Original_airports_complete_request_pushback_and_taxi(string code, string runway, string gate, int definitions)
    {
        var simulation = Create(code, runway);
        Assert.Equal(definitions, simulation.State.Airport.PushbackGates.Count);
        var messages = new List<AircraftRadioMessage>();
        simulation.RadioMessage += messages.Add;
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "AAL1535", "C172", runway, gate);
        Assert.Equal("departure.pushback.request", Assert.Single(messages).IntentKey);
        var initialPosition = aircraft.Position;
        Assert.False(simulation.Execute($"AAL1535/T{runway}").Success);
        Assert.Equal(initialPosition, aircraft.Position);
        Assert.True(simulation.Execute("/PB", aircraft.CallSign).Success);
        Assert.False(simulation.Execute("AAL1535/PB").Success);
        Assert.StartsWith("Roger, pushback approved, face ", Assert.Single(messages, m => m.IntentKey == "command.PB.controller").Text);
        var previous = aircraft.Position;
        var heading = aircraft.Heading;
        for (var i = 0; i < 3000 && aircraft.Phase == FlightPhase.Pushback; i++)
        {
            simulation.Advance(0.1);
            Assert.InRange(previous.Distance(aircraft.Position), 0, 0.16);
            Assert.InRange(Math.Abs(RouteMovement.AngleDifference(heading, aircraft.Heading)), 0, 5);
            if (previous.Distance(aircraft.Position) > 0.0001)
            {
                Assert.InRange(Math.Abs(RouteMovement.AngleDifference(previous.Bearing(aircraft.Position), aircraft.Heading)), 150, 180.001);
            }
            previous = aircraft.Position;
            heading = aircraft.Heading;
        }
        Assert.Equal(FlightPhase.ReadyForTaxi, aircraft.Phase);
        Assert.True(aircraft.Pushback!.Completed);
        Assert.Equal(simulation.State.Airport.Point(aircraft.Pushback.ConnectionNode), aircraft.Position);
        Assert.Equal("departure.ready", messages.Last().IntentKey);
        Assert.True(simulation.Execute($"AAL1535/T{runway}").Success);
        Assert.Equal(heading, aircraft.Heading);
        Until(simulation, () => aircraft.Phase == FlightPhase.HoldingShort);
        Assert.Single(messages, m => m.IntentKey == "departure.takeoff.ready");
    }

    [Fact]
    public void Hold_save_resume_and_delete_obstruction_do_not_restart_pushback()
    {
        var simulation = Create("CYN", "9");
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "AAL1535", "C172", "9", "G1");
        Assert.True(simulation.Execute("AAL1535/pushback west").Success);
        simulation.Advance(3);
        Assert.True(simulation.Execute("AAL1535/HY").Success);
        var position = aircraft.Position;
        var heading = aircraft.Heading;
        simulation.Advance(10);
        Assert.Equal(position, aircraft.Position);
        var path = Path.Combine(Path.GetTempPath(), "Pushback-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            JsonStore.Write(path, simulation.State);
            var resumed = new Simulation(JsonStore.LoadSession(path));
            var restored = Assert.Single(resumed.State.Aircraft);
            Assert.True(restored.Holding);
            Assert.Equal(position, restored.Position);
            Assert.Equal(heading, restored.Heading);
            Assert.True(resumed.Execute("AAL1535/TU").Success);
            var index = restored.Pushback!.SampleIndex;
            var blocker = new Aircraft
            {
                CallSign = "BLOCK", Direction = FlightDirection.Departure, Phase = FlightPhase.AtGate,
                Position = restored.Position.Towards(restored.Pushback.Samples[Math.Min(128, index + 20)].Position, 3),
                Runway = "9", Specification = restored.Specification
            };
            resumed.State.Aircraft.Add(blocker);
            resumed.Advance(1);
            Assert.Equal(position, restored.Position);
            Assert.False(restored.TrafficObstructionReported);
            Assert.True(resumed.Execute("AAL1535/HY").Success);
            Assert.True(restored.TrafficObstructionReported);
            Assert.True(resumed.Execute("AAL1535/TU").Success);
            Assert.True(resumed.DeleteAircraft(blocker));
            Until(resumed, () => restored.Phase == FlightPhase.ReadyForTaxi, 300);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Canyon_alley_is_used_from_both_sides_of_the_gates_for_runway_nine()
    {
        foreach (var gate in Enumerable.Range(1, 9).Select(number => "G" + number))
        {
            var simulation = Create("CYN", "9");
            var airport = simulation.State.Airport;
            var definition = airport.PushbackGates[gate];
            Assert.Contains("ALLEY1", definition.Connections);
            Assert.NotEmpty(definition.AlleyConnectionNode);
            var aircraft = simulation.AddAircraft(FlightDirection.Departure, "CHECK", "C172", "9", gate);
            Assert.True(simulation.Execute("CHECK/PB").Success);
            Assert.Equal(270, aircraft.Pushback!.FinalHeading);
            Assert.Equal(definition.AlleyConnectionNode, aircraft.Pushback.ConnectionNode);
            Until(simulation, () => aircraft.Phase == FlightPhase.ReadyForTaxi, 400);
            Assert.True(simulation.Execute("CHECK/T9").Success);
            Assert.Contains(airport.Point("P174"), aircraft.Path);
            Assert.DoesNotContain(airport.Point("P29"), aircraft.Path);
            Assert.DoesNotContain(airport.Point("P12"), aircraft.Path);
            var first = aircraft.Path.First(point => point.Distance(aircraft.Position) > 0.01);
            Assert.InRange(Math.Abs(RouteMovement.AngleDifference(aircraft.Heading, aircraft.Position.Bearing(first))), 0, 1);
            Until(simulation, () => aircraft.Phase == FlightPhase.HoldingShort);
        }
    }

    [Fact]
    public void Different_runway_uses_the_shorter_eastern_connection_and_explicit_facing_is_preserved()
    {
        var simulation = Create("CYN", "27");
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "CHECK", "C172", "27", "G3");
        Assert.True(simulation.Execute("CHECK/PB").Success);
        Assert.Equal(simulation.State.Airport.PushbackGates["G3"].ConnectionNode, aircraft.Pushback!.ConnectionNode);
        Until(simulation, () => aircraft.Phase == FlightPhase.ReadyForTaxi, 400);
        Assert.True(simulation.Execute("CHECK/T27").Success);
        Assert.DoesNotContain(simulation.State.Airport.Point("P174"), aircraft.Path);
        Until(simulation, () => aircraft.Phase == FlightPhase.HoldingShort);

        simulation = Create("CYN", "9");
        aircraft = simulation.AddAircraft(FlightDirection.Departure, "EXPLICIT", "C172", "9", "G3");
        Assert.True(simulation.Execute("EXPLICIT/PB E").Success);
        Assert.Equal(90, aircraft.Pushback!.FinalHeading);
    }

    [Fact]
    public void Runway_change_after_pushback_routes_from_the_saved_actual_alley_position()
    {
        var simulation = Create("CYN", "9");
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "CHECK", "C172", "9", "G3");
        Assert.True(simulation.Execute("CHECK/PB").Success);
        Until(simulation, () => aircraft.Phase == FlightPhase.ReadyForTaxi, 400);
        var position = aircraft.Position;
        var heading = aircraft.Heading;
        var path = Path.Combine(Path.GetTempPath(), "Pushback-Alley-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            JsonStore.Write(path, simulation.State);
            simulation = new(JsonStore.LoadSession(path));
            aircraft = Assert.Single(simulation.State.Aircraft);
            Assert.Equal(position, aircraft.Position);
            Assert.Equal(heading, aircraft.Heading);
            Assert.True(simulation.Execute("CHECK/T27").Success);
            Assert.Equal(position, aircraft.Path[0]);
            Assert.Equal(position, aircraft.Position);
            Until(simulation, () => aircraft.Phase == FlightPhase.HoldingShort);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Stand_without_an_alley_keeps_its_existing_connected_pushback_exit()
    {
        var simulation = Create("CYN", "9");
        var airport = simulation.State.Airport;
        var definition = airport.PushbackGates["G3"] with { Connections = [], AlleyConnectionNode = "" };
        airport.PushbackGates["G3"] = definition;
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "CHECK", "C172", "9", "G3");
        Assert.True(simulation.Execute("CHECK/PB").Success);
        Assert.Equal(definition.ConnectionNode, aircraft.Pushback!.ConnectionNode);
        Until(simulation, () => aircraft.Phase == FlightPhase.ReadyForTaxi, 400);
        Assert.True(simulation.Execute("CHECK/T9").Success);
        Until(simulation, () => aircraft.Phase == FlightPhase.HoldingShort);
    }

    [Fact]
    public void Extra_stand_and_legacy_saved_departure_keep_direct_taxi()
    {
        var simulation = Create("CYN", "9");
        var extra = simulation.AddAircraft(FlightDirection.Departure, "EXTRA", "C172", "9", "E1");
        Assert.Null(extra.Pushback);
        Assert.False(simulation.Execute("EXTRA/PB").Success);
        Assert.True(simulation.Execute("EXTRA/T9").Success);
        simulation.State.Aircraft.Clear();
        var legacy = simulation.AddAircraft(FlightDirection.Departure, "OLD", "C172", "9", "G1");
        legacy.Pushback = null;
        Assert.True(simulation.Execute("OLD/T9").Success);
    }

    [Theory]
    [InlineData("north", 37)]
    [InlineData("east", 39)]
    [InlineData("south", 41)]
    [InlineData("west", 0)]
    public void Approval_has_pilot_roger_readback_and_complete_approved_audio(string direction, int compass)
    {
        var message = new AircraftRadioMessage("AAL1535", "CYN", $"Roger, pushback approved, face {direction}.", "9", "command.PB.controller", Detail: direction);
        var typed = WorkbenchRadioAdapter.Create(message);
        Assert.Equal(RadioSpeaker.Pilot, typed.Speaker);
        Assert.IsType<AircraftCallsign>(typed.Parts[0]);
        Assert.Equal(new FragmentId(2, 45), Assert.IsType<RecordedPhrase>(typed.Parts[1]).Preferred);
        if (compass > 0)
        {
            Assert.Contains(typed.Parts.OfType<RecordedPhrase>(), p => p.Preferred == new FragmentId(2, compass));
        }
        var runtime = new WorkbenchSpeechRuntime(Data);
        runtime.Preferences.Engine = SpeechEngineKind.BaoOriginal;
        var output = runtime.Render(message);
        Assert.Null(output.Diagnostic);
        Assert.True(output.Wave is { Length: > 44 });
        runtime.Preferences.Engine = SpeechEngineKind.WindowsTts;
        var windows = runtime.Render(message);
        Assert.Null(windows.Diagnostic);
        Assert.Contains("pushback approved face " + direction, windows.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("roger", windows.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("CYN", "9")]
    public void Every_defined_stand_accepts_its_default_geometry(string code, string runway)
    {
        var simulation = Create(code, runway);
        foreach (var gate in simulation.State.Airport.PushbackGates.Keys)
        {
            simulation.State.Aircraft.Clear();
            var aircraft = simulation.AddAircraft(FlightDirection.Departure, "CHECK", "C172", runway, gate);
            Assert.NotNull(aircraft.Pushback);
            var result = simulation.Execute("CHECK/PB");
            Assert.True(result.Success, gate + ": " + result.Message);
            Until(simulation, () => aircraft.Phase == FlightPhase.ReadyForTaxi, 600);
        }
    }

    [Fact]
    public void Occupied_stand_and_shared_connection_are_not_released_during_pushback()
    {
        var airport = Airport.Training();
        airport.Nodes.Add("PB1", new("PB1", "GATE", new(0, -100)));
        airport.Nodes.Add("PB2", new("PB2", "GATE", new(80, -100)));
        var join = airport.Nodes.Values.First(n => n.Kind == "TAXIWAY");
        foreach (var gate in new[] { "PB1", "PB2" })
        {
            airport.Segments.Add(new(gate + "-RAMP", "RAMP", join.Id, gate));
            airport.PushbackGates.Add(gate, new("Center", 100, 0, 20, gate + "-RAMP", join.Id, "test"));
        }
        var simulation = new Simulation(new Session { Airport = airport, Options = new() { AircraftPerMinute = 0 } });
        var first = simulation.AddAircraft(FlightDirection.Departure, "FIRST", "C172", assignedGate: "PB1");
        var second = simulation.AddAircraft(FlightDirection.Departure, "SECOND", "C172", assignedGate: "PB2");
        Assert.False(simulation.Execute("FIRST/PB invalid").Success);
        Assert.True(simulation.Execute("FIRST/PB").Success);
        Assert.False(simulation.Execute("SECOND/PB").Success);
        Assert.Equal(FlightPhase.AtGate, second.Phase);
        Assert.Throws<InvalidOperationException>(() => simulation.AddAircraft(FlightDirection.Departure, "THIRD", "C172", assignedGate: "PB1"));
        Assert.True(simulation.DeleteAircraft(first));
        Assert.True(simulation.Execute("SECOND/PB").Success);
    }
}
