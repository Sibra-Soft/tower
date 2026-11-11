using System.Buffers.Binary;
using System.Text.Json;
using Tower.Core;
using Tower.Infrastructure;
using TowerSpeech.Core;
using Xunit;

namespace Tower.Tests;

public sealed class SpeechTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "TowerSpeechTests", Guid.NewGuid().ToString("N"), "Data");

    public SpeechTests()
    {
        Directory.CreateDirectory(root);


    }

    [Fact]
    public void Original_mode_rejects_partial_audio()
    {
        SaveCatalog(new TowerSpeech.Core.VoiceFragment
        {
            Bank = 2,
            Index = 1,
            Transcript = "roger",
            Status = "clear",
            Confidence = "high"
        });
        SaveVoc();
        var runtime = WorkbenchSpeechRuntime.ForAuthoring(root);
        var result = runtime.Render(new("AAL1535", "CYN", "roger", "27"));
        Assert.Null(result.Wave);
        Assert.NotNull(result.Diagnostic);
        Assert.Empty(result.Text);
    }

    [Fact]
    public void Original_mode_plays_complete_approved_template()
    {
        SaveCatalog(new TowerSpeech.Core.VoiceFragment
        {
            Bank = 2,
            Index = 1,
            Transcript = "roger",
            Status = "clear",
            Confidence = "high"
        });
        SaveVoc();
        SaveTemplate("custom", """[{"kind":"fragment","bank":2,"index":1}]""");
        var result = WorkbenchSpeechRuntime.ForAuthoring(root).Render(new("AAL1535", "CYN", "roger", "27", "custom"));
        Assert.NotNull(result.Wave);
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    public void Unapproved_candidates_are_never_used_as_original_audio()
    {
        SaveCatalog(new TowerSpeech.Core.VoiceFragment
        {
            Bank = 2,
            Index = 1,
            Transcript = "roger",
            Status = "uncertain",
            Confidence = "high"
        });
        SaveVoc();
        SaveTemplate("custom", """[{"kind":"fragment","bank":2,"index":1}]""");
        var result = WorkbenchSpeechRuntime.ForAuthoring(root).Render(new("AAL1535", "CYN", "roger", "27", "custom"));
        Assert.Null(result.Wave);
        Assert.NotNull(result.Diagnostic);
    }

    [Fact]
    public void Tts_uses_template_and_actual_arrival_distance_without_writing_workspace()
    {
        SaveCatalog();
        new SpeechPreferences { Engine = SpeechEngineKind.WindowsTts, ArrivalDistanceMiles = 12 }.Save(root);
        SaveTemplate("arrival.initial", """[{"kind":"text","text":"tower"},{"kind":"callsign"},{"kind":"text","text":"with you"},{"kind":"distance"},{"kind":"distanceunit"}]""");
        var before = Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllBytes);
        var result = WorkbenchSpeechRuntime.ForAuthoring(root).Render(new("AAL1535", "CYN", "inbound", "27", "arrival.initial", 7));
        Assert.Contains("eight miles", result.Text);
        Assert.DoesNotContain("twelve", result.Text);
        Assert.Contains("american fifteen thirty five", result.Text);
        Assert.Null(result.Diagnostic);
        Assert.Equal(before.Count, Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length);
        foreach (var file in before)
        {
            Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
        }
    }

    [Fact]
    public void Ambiguous_templates_block_transmission()
    {
        SaveCatalog();
        new SpeechPreferences { Engine = SpeechEngineKind.WindowsTts }.Save(root);
        SaveTemplate("custom", """[{"kind":"text","text":"roger"}]""");
        SpeechDataStore.Update(root, document => document.Sequences.Add(System.Text.Json.JsonSerializer.Deserialize<SpeechSequence>(System.Text.Json.JsonSerializer.Serialize(document.Sequences[0]))!));
        var result = WorkbenchSpeechRuntime.ForAuthoring(root).Render(new("AAL1535", "CYN", "roger", "27", "custom"));
        Assert.NotNull(result.Diagnostic);
        Assert.Empty(result.Text);
    }

    [Theory]
    [InlineData("27L", "runway two seven left")]
    [InlineData("09C", "runway zero niner center")]
    public void Runway_speech_preserves_digits_and_suffix(string runway, string expected)
    {
        Assert.Equal(expected, AviationSpeechFormatter.FormatRunway(runway));
    }

    [Fact]
    public void Generic_adapter_preserves_decimal_and_speed_values()
    {
        var message = WorkbenchRadioAdapter.Create(new("AAL1535", "CYN", "1.5 mile final, speed 80 knots", "27"));
        var text = new AviationSpeechFormatter().Format(message);
        Assert.Contains("1.5 mile", text);
        Assert.Contains("eight zero knots", text);
        Assert.DoesNotContain("zero eight zero", text);
    }

    [Fact]
    public void Bao_traffic_uses_only_full_approved_aliases_with_matching_audio()
    {
        SaveCatalog(
            new()
            {
                Bank = 1,
                Index = 1,
                Transcript = "american one",
                Status = "clear",
                Confidence = "high"
            },
            new()
            {
                Bank = 1,
                Index = 2,
                Transcript = "united two",
                Status = "uncertain",
                Confidence = "low"
            },
            new()
            {
                Bank = 1,
                Index = 3,
                Transcript = "delta three",
                Status = "clear",
                Confidence = "high"
            });
        SaveVoc();
        File.Copy(Path.Combine(root, "VOICE.VOC"), Path.Combine(root, "CALLSIGN.VOC"));
        var lexicon = new CallsignLexicon
        {
            FullCallsigns = new()
            {
                ["AAL1"] = new()
                {
                    Bank = 1,
                    Index = 1,
                    Text = "american one"
                },
                ["UAL2"] = new()
                {
                    Bank = 1,
                    Index = 2,
                    Text = "united two"
                },
                ["DAL3"] = new()
                {
                    Bank = 1,
                    Index = 3,
                    Text = "delta three"
                },
                ["WRONG1"] = new()
                {
                    Bank = 1,
                    Index = 1,
                    Text = "wrong transcript"
                },
                ["WRONGBANK1"] = new()
                {
                    Bank = 2,
                    Index = 1,
                    Text = "american one"
                }
            }
        };
        SpeechDataStore.Update(root, document => document.Callsigns = lexicon);
        Assert.Equal(new[] { "AAL1" }, WorkbenchSpeechRuntime.ForAuthoring(root).GetTrafficCallsigns());
        new SpeechPreferences { Engine = SpeechEngineKind.WindowsTts }.Save(root);
        Assert.Null(WorkbenchSpeechRuntime.ForAuthoring(root).GetTrafficCallsigns());
    }

    [Fact]
    public void Voc_reader_uses_game_root_and_each_airport_folder()
    {
        SaveVoc();
        var audio = new VocAudioRepository(root);
        Assert.Equal(Path.Combine(root, "VOICE.VOC"), audio.SourceFile(new(2, 1)));
        foreach (var airport in new[] { "CYN" })
        {
            Directory.CreateDirectory(Path.Combine(root, airport));
            foreach (var (bank, suffix) in new[] { (3, "F"), (4, "R"), (5, "T") })
            {
                var expected = Path.Combine(root, airport, airport + suffix + ".VOC");
                File.Copy(Path.Combine(root, "VOICE.VOC"), expected);
                var id = new FragmentId(bank, 1, airport);
                Assert.Equal(expected, audio.SourceFile(id));
                Assert.True(audio.HasFragment(id));
                Assert.True(audio.ReadWav(id).Length > 44);
            }
        }
    }

    [Fact]
    public void Changed_bank_blocks_playback_without_changing_approvals()
    {
        SaveCatalog(new TowerSpeech.Core.VoiceFragment()
        {
            Bank = 2,
            Index = 1,
            Transcript = "roger",
            Status = "clear",
            Confidence = "high"
        });
        SaveVoc();
        var file = Path.Combine(root, "VOICE.VOC");
        var bytes = File.ReadAllBytes(file);
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        SpeechDataStore.Update(root, document => document.AudioBankHashes["VOICE.VOC"] = hash);
        _ = WorkbenchSpeechRuntime.ForAuthoring(root);
        bytes[10] ^= 1;
        File.WriteAllBytes(file, bytes);
        Assert.Throws<InvalidDataException>(() => WorkbenchSpeechRuntime.ForAuthoring(root));
        Assert.Equal("clear", SpeechDataStore.Load(root).Fragments[0].Status);
    }

    private void SaveCatalog(params TowerSpeech.Core.VoiceFragment[] fragments)
    {
        SpeechDataStore.Create(root, new SpeechDocument { Fragments = [.. fragments] });
    }

    private void SaveTemplate(string intent, string steps)
    {
        var sequence = JsonSerializer.Deserialize<SpeechSequence>(
            "{\"schemaVersion\":1,\"speaker\":\"Pilot\",\"airportCode\":\"*\",\"intentKey\":\"" + intent + "\",\"steps\":" + steps + "}",
            SpeechDataStore.Options)!;
        SpeechDataStore.Update(root, document => document.Sequences.Add(sequence));
    }

    private void SaveVoc()
    {
        byte[] bytes = new byte[118];
        bytes[0] = 1;
        bytes[1] = 102;
        bytes[4] = 0x91;
        Array.Fill(bytes, (byte)128, 6, 100);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(110), 106);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(114), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(116), 1);
        File.WriteAllBytes(Path.Combine(root, "VOICE.VOC"), bytes);
    }

    public void Dispose()
    {
        Directory.Delete(Path.GetDirectoryName(root)!, recursive: true);
    }
}
