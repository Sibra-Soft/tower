using Tower.Core;
using Tower.Infrastructure;
using TowerSpeech.Core;
using Xunit;

namespace Tower.Tests;

[Trait("Category", "Integration")]
public sealed class ParkingAndVoterTests
{
    private static string Data => Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA")
        ?? throw new InvalidOperationException("Set TOWER_ORIGINAL_DATA.");

    [Theory]
    [InlineData("CYN", "9", "E1,E2,N1,N2", 13)]
    public void Additional_stands_are_selected_for_both_directions(string code, string runway, string added, int count)
    {
        var airport = OriginalData.LoadAirport(Data, code);
        Assert.Equal(count, airport.Nodes.Values.Count(node => node.Kind == "GATE"));
        var imported = added.Split(',');
        foreach (var id in imported)
        {
            Assert.Equal("GATE", airport.Nodes[id].Kind);
            Assert.False(string.IsNullOrWhiteSpace(airport.ParkingRampCodes[id]));
        }
        var simulation = new Simulation(Simulation.CreateFreeSession(airport,
            new() { Runway = runway, ArrivalRunways = [runway] }, seed: 42));
        var expected = imported;
        var seen = new HashSet<(string, FlightDirection)>();
        // A small aircraft avoids inventing unconfirmed type restrictions for ramps.
        airport.TrafficPool = [new("CHECK1", "C172")];
        for (var index = 0; index < 4000 && seen.Count < expected.Length * 2; index++)
        {
            Assert.True(simulation.TryAddFreeTraffic());
            var aircraft = Assert.Single(simulation.State.Aircraft);
            if (expected.Contains(aircraft.Gate))
            {
                seen.Add((aircraft.Gate, aircraft.Direction));
            }
            simulation.State.Aircraft.Clear();
        }
        foreach (var id in expected)
        {
            Assert.Contains((id, FlightDirection.Arrival), seen);
            Assert.Contains((id, FlightDirection.Departure), seen);
        }
    }

    [Fact]
    public void Original_voter_bindings_use_existing_approved_audio_without_editing_metadata()
    {
        var path = CompiledSpeechData.FilePath(Data);
        var before = File.ReadAllBytes(path);
        var document = CompiledSpeechData.Load(Data);
        var catalog = VoiceCatalog.FromFragments(document.Fragments);
        var audio = new VocAudioRepository(Data);
        audio.ValidateSources(document, code => code == "CYN");
        var runtime = new WorkbenchSpeechRuntime(Data);
        runtime.Preferences.Engine = SpeechEngineKind.BaoOriginal;
        var aliases = OriginalCallsignBindings.Resolve(Data, document.Callsigns, catalog, audio.HasFragment);
        var composer = new SpeechComposer(catalog, audio, aliases);
        foreach (var (callsign, index) in new[] { ("VOTER51", 194), ("VOTER22", 195), ("VOTER23", 196), ("VOTER24", 197), ("VOTER71", 198) })
        {
            Assert.True(aliases.TryGetFull(callsign, out var alias));
            Assert.Equal(index, alias!.Index);
            Assert.True(audio.HasFragment(new(1, index)));
            Assert.Contains(callsign, runtime.GetTrafficCallsigns()!);
            var rendered = composer.Render(new(callsign, "CYN", RadioSpeaker.Pilot, callsign,
                [new AircraftCallsign(callsign)]), VoiceMode.Strict);
            Assert.Empty(rendered.Warnings);
            Assert.True(rendered.WaveBytes is { Length: > 44 });
            Assert.Equal(new FragmentId(1, index), Assert.Single(rendered.Pieces).Id);
        }
        var explicitAliases = new CallsignLexicon();
        explicitAliases.FullCallsigns["VOTER51"] = new() { Bank = 1, Index = 9999, Text = "user choice" };
        var kept = OriginalCallsignBindings.Resolve(Data, explicitAliases, catalog, _ => true);
        Assert.Equal(9999, kept.FullCallsigns["VOTER51"].Index);
        var unavailable = OriginalCallsignBindings.Resolve(Data, new(), catalog, _ => false);
        Assert.Empty(unavailable.FullCallsigns);
        var unapproved = VoiceCatalog.FromFragments([new() { Bank = 1, Index = 194, Transcript = "voter five one" }]);
        Assert.Empty(OriginalCallsignBindings.Resolve(Data, new(), unapproved, _ => true).FullCallsigns);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("CYN", "9")]
    public void Voter_uses_its_original_model_in_free_traffic(string code, string runway)
    {
        var airport = OriginalData.LoadAirport(Data, code);
        var simulation = new Simulation(Simulation.CreateFreeSession(airport,
            new() { Runway = runway, ArrivalRunways = [runway] }, seed: 42), ["VOTER51"]);
        // Some runway/direction combinations cannot fit the C130's PRF landing roll.
        var added = false;
        for (var index = 0; index < 20 && !added; index++)
        {
            added = simulation.TryAddFreeTraffic();
        }
        Assert.True(added);
        var aircraft = Assert.Single(simulation.State.Aircraft);
        Assert.Equal("VOTER51", aircraft.CallSign);
        Assert.Equal("C130", aircraft.Type);
        Assert.False(aircraft.FreeTraffic!.IsVfr);
    }
}
