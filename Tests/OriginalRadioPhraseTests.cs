using Tower.Core;
using Tower.Infrastructure;
using TowerSpeech.Core;
using Xunit;
using Fragment = TowerSpeech.Core.VoiceFragment;

namespace Tower.Tests;

public sealed class OriginalRadioPhraseTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "TowerOriginalRadio", Guid.NewGuid().ToString("N"), "Data");

    public OriginalRadioPhraseTests()
    {
        Directory.CreateDirectory(Path.Combine(root, "CYN"));
        File.WriteAllText(Path.Combine(root, "CYN", "CYN.SPF"), "[RUNWAYS]\nRUNWAY_9 1\nRUNWAY_09 1\nRUNWAY_27 2\nRUNWAY_14 3\nRUNWAY_32 4\n[End]\n");
        var fragments = new List<Fragment> { Approved(1, 1, "american one") };
        foreach (var direction in new[] { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" })
        {
            fragments.Add(Approved(2, 37 + OriginalRadioDirection.Sector(direction), direction));
        }
        foreach (var (index, text) in new (int, string)[]
        {
            (28, "one"), (45, "roger"), (52, "with you"), (54, "mile"), (55, "miles"), (57, "ready for taxi"),
            (69, "cleared"), (71, "four takeoff"), (72, "four touch and go"), (73, "four low approach"),
            (75, "cleared to land"), (76, "extending downwind"), (77, "making short approach"),
            (78, "breaking out of traffic"), (100, "cancel takeoff clearance"), (106, "proceeding"),
            (114, "position and hold"), (117, "taxi two"), (119, "holding short of"), (128, "executing missed approach"),
            (150, "twelve"), (158, "twenty"), (192, "up your frequency request taxi to parking"), (193, "taxiing and parking"),
            (58, "ready for takeoff"), (60, "request right turn out of traffic"), (63, "on the go"),
            (66, "downwind"), (67, "base"), (68, "final"), (70, "gear down"), (74, "four full stop"),
            (83, "around"), (84, "turn out approved"), (87, "mile final"), (92, "entering final"),
            (49, "good day"), (50, "right"), (51, "left"), (110, "switching"), (122, "unable"),
            (123, "request permission across"), (131, "established on final"), (132, "turning final"),
            (174, "climbing"), (175, "descending"), (176, "feet per minute"), (177, "feet"),
            (178, "heading"), (179, "indicating"), (180, "knots"), (181, "through"),
            (182, "thousand"), (183, "hunderd"), (184, "enter left downwind"), (185, "enter right downwind"),
            (186, "enter left base"), (187, "enter right base"), (188, "we'll report left downwind"),
            (189, "we'll report right downwind"), (190, "report left base"), (191, "report right base"),
            (194, "there's an aircraft in front of me"), (164, "eighty"), (166, "request approach two")
        })
        {
            fragments.Add(Approved(2, index, text));
        }
        fragments.Add(Approved(4, 1, "runway nine", "CYN"));
        fragments.Add(Approved(4, 2, "runway two seven", "CYN"));
        fragments.Add(Approved(4, 3, "runway one four", "CYN"));
        fragments.Add(Approved(4, 4, "runway three two", "CYN"));
        SpeechDataStore.Create(root, new SpeechDocument
        {
            Fragments = fragments,
            Callsigns = new()
            {
                FullCallsigns = new()
                {
                    ["AAL1"] = new()
                    {
                        Bank = 1,
                        Index = 1,
                        Text = "american one"
                    }
                }
            }
        });
        Bank(Path.Combine(root, "CALLSIGN.VOC"), 1);
        Bank(Path.Combine(root, "VOICE.VOC"), 196);
        Bank(Path.Combine(root, "CYN", "CYNR.VOC"), 4);
    }

    private static Fragment Approved(int bank, int index, string text, string airport = "") => new()
    {
        Bank = bank,
        Index = index,
        Transcript = text,
        AirportCode = airport,
        Status = "clear",
        Confidence = "high"
    };

    [Theory]
    [InlineData("18L", "one,eight,left")]
    [InlineData("18C", "one,eight,center")]
    [InlineData("18R", "one,eight,right")]
    [InlineData("06", "zero,six")]
    public void Authored_runways_compose_only_reviewed_digits_and_direction_suffixes(string runway, string expected)
    {
        File.AppendAllText(Path.Combine(root, "CYN", "CYN.SPF"), $"\n[RunwayDigits]\nPrefix runway\nRUNWAY_{runway} On\n");
        SpeechDataStore.Update(root, document => document.Fragments.AddRange([
            Approved(2, 30, "eight"), Approved(2, 31, "six"), Approved(2, 32, "zero"), Approved(2, 33, "center"), Approved(2, 34, "runway")]));
        var document = SpeechDataStore.Load(root);
        var composer = new SpeechComposer(VoiceCatalog.FromFragments(document.Fragments), new(root), document.Callsigns);
        var message = new RadioMessage("AAL1", "CYN", RadioSpeaker.Pilot, "", [new RunwayNumber(runway)]);
        var result = composer.Render(message, VoiceMode.Strict);
        Assert.Empty(result.Warnings);
        Assert.True(result.WaveBytes is { Length: > 44 });
        Assert.Equal(new[] { "runway" }.Concat(expected.Split(',')), result.Pieces.Select(piece => piece.Text));
        if (runway.EndsWith('C'))
        {
            document.Fragments.Single(fragment => fragment.Bank == 2 && fragment.Index == 33).Status = "uncertain";
            var unreviewed = new SpeechComposer(VoiceCatalog.FromFragments(document.Fragments), new(root), document.Callsigns).Render(message, VoiceMode.Strict);
            Assert.NotEmpty(unreviewed.Warnings);
        }
    }

    private static void Bank(string file, int count)
    {
        using var stream = File.Create(file);
        using var writer = new BinaryWriter(stream);
        for (int i = 0; i < count; i++)
        {
            writer.Write(new byte[] { 1, 4, 0, 0, 0x91, 0, 128, 128 });
        }
        for (int i = 0; i <= count; i++)
        {
            writer.Write(i * 8);
        }
        writer.Write((ushort)(count + 1));
        writer.Write((ushort)1);
    }

    [Theory]
    [InlineData("departure.takeoff.ready", "", 0, "2:58", "American one ready for takeoff.")]
    [InlineData("command.G.pilot", "", 0, "2:45,2:83", "American one, roger go around.")]
    [InlineData("flight.goaround", "", 0, "2:128", "American one executing missed approach.")]
    [InlineData("flight.approach.request", "", 0, "2:166,4:3", "American one request approach to runway one four.")]
    [InlineData("command.pattern.enter", "final", 0, "2:45,2:92,4:3", "American one, roger entering final runway one four.")]
    [InlineData("command.pattern.enter", "left base", 0, "2:186,4:3", "American one enter left base runway one four.")]
    [InlineData("command.pattern.enter", "right downwind", 0, "2:45,2:185,4:3", "American one, roger enter right downwind runway one four.")]
    [InlineData("command.pattern.report", "left downwind", 0, "2:45,2:188,4:3", "American one, roger will report left downwind runway one four.")]
    [InlineData("command.pattern.report", "right base", 0, "2:45,2:191,4:3", "American one, roger report right base runway one four.")]
    [InlineData("command.final.report", "", 12, "2:45,2:150,2:87,4:3", "American one, roger twelve mile final runway one four.")]
    [InlineData("flight.final.distance", "", 12, "2:150,2:87", "American one twelve mile final.")]
    [InlineData("flight.base.fullstop", "", 0, "2:67,2:74", "American one base for full stop.")]
    [InlineData("flight.base.touchandgo", "", 0, "2:67,2:70,2:72", "American one base gear down for touch and go.")]
    [InlineData("flight.base.lowapproach", "", 0, "2:67,2:73", "American one base for low approach.")]
    [InlineData("flight.turning.final", "", 0, "2:132", "American one turning final.")]
    [InlineData("flight.established.final", "", 0, "2:131", "American one established on final.")]
    [InlineData("flight.onthego", "", 0, "2:63", "American one on the go.")]
    [InlineData("flight.onthego.turnout", "", 0, "2:63,2:60", "American one on the go request right turn out of traffic.")]
    [InlineData("command.turnout", "right", 0, "2:50,2:84", "American one right turn out approved.")]
    [InlineData("command.turn", "left", 180, "2:45,2:51,2:28,2:164", "American one, roger left one eighty.")]
    [InlineData("command.FD.pilot", "", 0, "2:110,2:49", "American one switching good day.")]
    [InlineData("ground.blocked", "", 0, "2:194", "American one there's an aircraft in front of me.")]
    [InlineData("ground.crossing.request", "", 0, "2:123,4:3", "American one request permission to cross runway one four.")]
    [InlineData("status.altitude", "climbing", 1200, "2:174,2:181,2:28,2:182,2:29,2:183,2:177", "American one climbing through one thousand two hundred feet.")]
    public void Reconstructed_messages_preserve_original_fragment_order(string intent, string detail, double value, string fragments, string expectedText)
    {
        var document = SpeechDataStore.Load(root);
        // The numeric test needs the two recording alongside the pre-existing one.
        var catalog = VoiceCatalog.FromFragments(document.Fragments.Concat([Approved(2, 29, "two")]));
        var message = WorkbenchRadioAdapter.Create(new("AAL1", "CYN", "ignored", "14", intent, Value: value, Detail: detail));
        var rendered = new SpeechComposer(catalog, new(root), document.Callsigns).Render(message, VoiceMode.Strict);
        Assert.Empty(rendered.Warnings);
        Assert.True(rendered.WaveBytes is { Length: > 44 });
        Assert.Equal("1:1," + fragments, string.Join(',', rendered.Pieces.Select(piece => $"{piece.Id.Bank}:{piece.Id.Index}")));
        Assert.Equal(expectedText, new AviationSpeechFormatter(document.Callsigns).Format(message));
    }

    [Theory]
    [InlineData("north")]
    [InlineData("northeast")]
    [InlineData("east")]
    [InlineData("southeast")]
    [InlineData("south")]
    [InlineData("southwest")]
    [InlineData("west")]
    [InlineData("northwest")]
    public void Initial_contact_appends_the_same_direction_in_text_and_original_audio(string direction)
    {
        var source = new AircraftRadioMessage("AAL1", "CYN", "ignored", "14", "arrival.initial",
            OriginalRadioDistance.NauticalMiles(12), Detail: direction);
        var message = WorkbenchRadioAdapter.Create(source);
        var expected = "American one with you twelve miles " + direction + ".";
        Assert.Equal(expected, new AviationSpeechFormatter(SpeechDataStore.Load(root).Callsigns).Format(message));
        var rendered = WorkbenchSpeechRuntime.ForAuthoring(root).Render(source);
        Assert.Null(rendered.Diagnostic);
        Assert.True(rendered.Wave is { Length: > 44 });
        var last = Assert.IsType<RecordedPhrase>(message.Parts.Last());
        Assert.Equal(new FragmentId(2, 37 + OriginalRadioDirection.Sector(direction)), last.Preferred);
    }

    [Theory]
    [InlineData("arrival.initial", "American one with you twelve miles.")]
    [InlineData("ground.parking.request", "American one up your frequency request taxi to parking.")]
    [InlineData("command.T.pilot", "American one, roger taxi to runway one four.")]
    [InlineData("command.TP.pilot", "American one, roger taxiing to parking.")]
    [InlineData("command.CT.pilot", "American one cleared for takeoff.")]
    [InlineData("command.CG.pilot", "American one, roger cleared for touch and go.")]
    [InlineData("command.CA.pilot", "American one cleared for low approach.")]
    public void Windows_and_original_use_the_same_message_parts(string intent, string expected)
    {
        var message = new AircraftRadioMessage("AAL1", "CYN", "Operational status text must not be read aloud", "14", intent, 12 * 1609d / 1852);
        var document = SpeechDataStore.Load(root);
        var adapter = WorkbenchRadioAdapter.Create(message);
        Assert.Equal(expected, new AviationSpeechFormatter(document.Callsigns).Format(adapter));
        var original = WorkbenchSpeechRuntime.ForAuthoring(root).Render(message);
        Assert.Null(original.Diagnostic);
        Assert.NotNull(original.Wave);
        Assert.True(original.Wave.Length > 44);
    }

    [Theory]
    [InlineData("arrival.initial", "1:1,2:52,2:150,2:55")]
    [InlineData("ground.parking.request", "1:1,2:192")]
    [InlineData("command.T.pilot", "1:1,2:45,2:117,4:3")]
    [InlineData("command.CL.pilot", "1:1,2:45,2:75")]
    [InlineData("command.TH.pilot", "1:1,2:45,2:114")]
    public void Original_audio_uses_confirmed_whole_recordings(string intent, string expected)
    {
        var document = SpeechDataStore.Load(root);
        var composer = new SpeechComposer(VoiceCatalog.FromFragments(document.Fragments), new(root), document.Callsigns);
        var result = composer.Render(WorkbenchRadioAdapter.Create(new("AAL1", "CYN", "ignored", "14", intent, 12 * 1609d / 1852)), VoiceMode.Strict);
        Assert.Empty(result.Warnings);
        Assert.Equal(expected, string.Join(',', result.Pieces.Select(p => $"{p.Id.Bank}:{p.Id.Index}")));
    }

    [Theory]
    [InlineData(1, "American one with you one mile.", "1:1,2:52,2:28,2:54")]
    [InlineData(21, "American one with you twenty one miles.", "1:1,2:52,2:158,2:28,2:55")]
    public void Distance_uses_cardinal_fragments_and_correct_unit(int distance, string expected, string pieces)
    {
        var document = SpeechDataStore.Load(root);
        var message = WorkbenchRadioAdapter.Create(new("AAL1", "CYN", "ignored", "14", "arrival.initial", distance * 1609d / 1852));
        var result = new SpeechComposer(VoiceCatalog.FromFragments(document.Fragments), new(root), document.Callsigns).Render(message, VoiceMode.Strict);
        Assert.Empty(result.Warnings);
        Assert.Equal(pieces, string.Join(',', result.Pieces.Select(p => $"{p.Id.Bank}:{p.Id.Index}")));
        Assert.Equal(expected, new AviationSpeechFormatter(document.Callsigns).Format(message));
    }

    [Fact]
    public void Unreviewed_runway_is_reported_without_dropping_it_or_falling_back_to_digits()
    {
        SpeechDataStore.Update(root, d => d.Fragments.Single(f => f.Bank == 4 && f.Index == 3).Status = "uncertain");
        var result = WorkbenchSpeechRuntime.ForAuthoring(root).Render(new("AAL1", "CYN", "Taxi", "14", "command.T.pilot"));
        Assert.Null(result.Wave);
        Assert.Contains("(4:3)[CYN] not approved", result.Diagnostic);
    }

    [Fact]
    public void Spf_aliases_use_the_same_recording_and_never_another_airport()
    {
        var audio = new VocAudioRepository(root);
        Assert.Equal(new FragmentId(4, 1, "CYN"), audio.FindRunway("CYN", "09"));
        Assert.Equal(audio.FindRunway("CYN", "09"), audio.FindRunway("CYN", "9"));
        Assert.Null(audio.FindRunway("DCA", "14"));
        Assert.Null(audio.FindRunway("CYN", "99"));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(7, 8)]
    [InlineData(12, 13)]
    [InlineData(20, 23)]
    public void Radio_distance_uses_original_statute_miles_and_truncation(double nauticalMiles, int expectedMiles)
    {
        var message = WorkbenchRadioAdapter.Create(new("AAL1", "CYN", "ignored", "14", "arrival.initial", nauticalMiles));
        Assert.Equal(expectedMiles.ToString(), Assert.Single(message.Parts.OfType<SpokenDigits>()).Digits);
    }

    [Fact]
    public void Corrected_review_spelling_still_resolves_the_same_fragment()
    {
        SpeechDataStore.Update(root, d => d.Fragments.Single(f => f.Bank == 2 && f.Index == 117).Transcript = "taxi to");
        var result = WorkbenchSpeechRuntime.ForAuthoring(root).Render(new("AAL1", "CYN", "Taxi", "14", "command.T.pilot"));
        Assert.Null(result.Diagnostic);
        Assert.NotNull(result.Wave);
    }

    [Fact]
    public void Taxi_command_transmits_one_typed_readback_and_say_again_preserves_it()
    {
        var airport = Airport.Training();
        airport.Runways[0].Name = "14";
        var simulation = new Simulation(new Session { Airport = airport, Options = new() { Runway = "14", AircraftPerMinute = 0 } });
        simulation.AddAircraft(FlightDirection.Departure, "AAL1");
        var messages = new List<AircraftRadioMessage>();
        simulation.RadioMessage += messages.Add;
        Assert.True(simulation.Execute("AAL1/T14").Success);
        var message = Assert.Single(messages);
        Assert.Equal("command.T.pilot", message.IntentKey);
        Assert.Equal("14", message.Runway);
        simulation.Execute("AAL1/SG");
        Assert.Equal(message, messages[1]);
    }

    [Fact]
    public void Instructed_go_around_transmits_one_typed_readback()
    {
        var simulation = new Simulation(new Session { Options = new() { AircraftPerMinute = 0 } });
        simulation.AddAircraft(FlightDirection.Arrival, "AAL1");
        var messages = new List<AircraftRadioMessage>();
        simulation.RadioMessage += messages.Add;
        Assert.True(simulation.Execute("AAL1/G").Success);
        Assert.Equal("command.G.pilot", Assert.Single(messages).IntentKey);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Installed_original_banks_render_the_three_reported_messages_without_missing_fragments()
    {
        var data = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA");
        if (string.IsNullOrWhiteSpace(data))
        {
            Assert.Skip("Set TOWER_ORIGINAL_DATA to validate the original reviewed recordings.");
        }
        var document = CompiledSpeechData.Load(data);
        var catalog = VoiceCatalog.FromFragments(document.Fragments);
        var audio = new VocAudioRepository(data);
        audio.ValidateSources(document, code => code == "CYN");
        var callsign = document.Callsigns.FullCallsigns.First(kv => catalog.TryResolve(kv.Value.Text, "CYN", VoiceMode.Strict,
            new FragmentId(kv.Value.Bank, kv.Value.Index), out _, out _)).Key;
        var composer = new SpeechComposer(catalog, audio, document.Callsigns);
        foreach (var intent in new[] { "arrival.initial", "ground.parking.request", "command.T.pilot" })
        {
            var result = composer.Render(WorkbenchRadioAdapter.Create(new(callsign, "CYN", "ignored", "14", intent, 12)), VoiceMode.Strict);
            Assert.Empty(result.Warnings);
            Assert.NotNull(result.WaveBytes);
            Assert.True(result.WaveBytes.Length > 44);
            if (intent == "command.T.pilot")
            {
                Assert.Equal(new FragmentId(4, 3, "CYN"), result.Pieces[^1].Id);
            }
        }
        Assert.Equal(new FragmentId(4, 3, "CYN"), audio.FindRunway("CYN", "14"));
    }

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(root)!, recursive: true);
}
