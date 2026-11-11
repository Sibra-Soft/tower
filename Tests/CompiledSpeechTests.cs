using System.Buffers.Binary;
using System.Security.Cryptography;
using Tower.Core;
using Tower.Infrastructure;
using TowerSpeech.Core;
using Xunit;

namespace Tower.Tests;

public sealed class CompiledSpeechTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "CompiledSpeech-" + Guid.NewGuid().ToString("N"), "Data");

    public CompiledSpeechTests()
    {
        Directory.CreateDirectory(root);
        foreach (var file in new[] { "VOICE.VOC", "CALLSIGN.VOC" })
        {
            var bytes = new byte[118];
            bytes[0] = 1;
            bytes[1] = 102;
            bytes[4] = 0x91;
            Array.Fill(bytes, (byte)128, 6, 100);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(110), 106);
            bytes[114] = 2;
            bytes[116] = 1;
            File.WriteAllBytes(Path.Combine(root, file), bytes);
        }
        var document = new SpeechDocument
        {
            Fragments =
            [
                new() { Bank = 1, Index = 1, Transcript = "american one", Status = "clear", Confidence = "high" },
                new() { Bank = 2, Index = 1, Transcript = "roger", Status = "clear", Confidence = "high", Notes = "PRIVATE REVIEW NOTE" }
            ],
            RecoveredReviewHistory = "PRIVATE HISTORY"
        };
        document.Callsigns.FullCallsigns.Add("AAL1", new() { Bank = 1, Index = 1, Text = "american one" });
        document.Sequences.Add(new()
        {
            Name = "Readback", AirportCode = "*", IntentKey = "demo", Speaker = "Pilot",
            Steps = [new() { Kind = "callsign" }, SpeechSequenceStep.FromId(new(2, 1))]
        });
        foreach (var file in new[] { "VOICE.VOC", "CALLSIGN.VOC" })
        {
            document.AudioBankHashes.Add(file, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, file)))));
        }
        SpeechDataStore.Create(root, document);
    }

    [Fact]
    public void Separate_authoring_root_builds_without_creating_json_in_game_Data()
    {
        var tools = Path.Combine(Directory.GetParent(root)!.FullName, "Tower Tools");
        Directory.CreateDirectory(tools);
        File.Move(SpeechDataStore.FilePath(root), SpeechDataStore.FilePath(tools));
        var source = File.ReadAllBytes(SpeechDataStore.FilePath(tools));
        CompiledSpeechData.Build(root, sourceDirectory: tools);
        Assert.True(CompiledSpeechData.IsCurrent(root, tools));
        Assert.False(File.Exists(SpeechDataStore.FilePath(root)));
        Assert.Equal(source, File.ReadAllBytes(SpeechDataStore.FilePath(tools)));
        Assert.NotEmpty(new WorkbenchSpeechRuntime(root).GetTrafficCallsigns()!);
        SpeechDataStore.Update(tools, document => document.Fragments[0].Notes = "Updated in tools");
        Assert.False(CompiledSpeechData.IsCurrent(root, tools));
        Assert.Equal(Path.Combine(tools, "Backups", "Speech"), SpeechDataStore.BackupDirectory(tools));
        Assert.Equal(source, File.ReadAllBytes(Assert.Single(Directory.GetFiles(SpeechDataStore.BackupDirectory(tools)))));
    }

    [Fact]
    public void Standalone_runtime_uses_binary_without_json_or_private_reviews()
    {
        var source = File.ReadAllBytes(SpeechDataStore.FilePath(root));
        var result = CompiledSpeechData.Build(root);
        Assert.Equal(2, result.ApprovedFragments);
        Assert.Equal(source, File.ReadAllBytes(SpeechDataStore.FilePath(root)));
        var document = CompiledSpeechData.Load(root);
        Assert.All(document.Fragments, f => Assert.Empty(f.Notes));
        Assert.Empty(document.ReviewHistory);
        Assert.Empty(document.RecoveredReviewHistory);
        File.Delete(SpeechDataStore.FilePath(root));
        var runtime = new WorkbenchSpeechRuntime(root);
        var output = runtime.Render(new AircraftRadioMessage("AAL1", "CYN", "Roger.", "9", "demo"));
        Assert.Null(output.Diagnostic);
        Assert.True(output.Wave is { Length: > 44 });
        Assert.Equal(new[] { "AAL1" }, runtime.GetTrafficCallsigns());
    }

    [Fact]
    public void Preferences_are_atomic_preserved_on_build_and_do_not_edit_source()
    {
        CompiledSpeechData.Build(root);
        var original = File.ReadAllBytes(CompiledSpeechData.FilePath(root));
        var source = File.ReadAllBytes(SpeechDataStore.FilePath(root));
        var preferences = CompiledSpeechData.Load(root).Preferences;
        preferences.Engine = SpeechEngineKind.WindowsTts;
        preferences.Rate = 3;
        CompiledSpeechData.SavePreferences(root, preferences);
        var backupDirectory = Path.Combine(Directory.GetParent(root)!.FullName, "Backups", "SpeechBuild");
        Assert.Equal(original, File.ReadAllBytes(Assert.Single(Directory.GetFiles(backupDirectory))));
        Assert.Equal(source, File.ReadAllBytes(SpeechDataStore.FilePath(root)));
        Assert.True(CompiledSpeechData.IsCurrent(root));
        CompiledSpeechData.Build(root);
        Assert.Equal(3, CompiledSpeechData.Load(root).Preferences.Rate);
        CompiledSpeechData.Build(root, preserveSoundSettings: false);
        Assert.Equal(0, CompiledSpeechData.Load(root).Preferences.Rate);
    }

    [Fact]
    public void Pending_reviews_are_not_approved_by_compilation_and_build_is_deterministic()
    {
        SpeechDataStore.Update(root, document => document.Fragments[1].Status = "uncertain");
        CompiledSpeechData.Build(root, false);
        var bytes = File.ReadAllBytes(CompiledSpeechData.FilePath(root));
        CompiledSpeechData.Build(root, false);
        Assert.Equal(bytes, File.ReadAllBytes(CompiledSpeechData.FilePath(root)));
        var catalog = VoiceCatalog.FromFragments(CompiledSpeechData.Load(root).Fragments);
        Assert.False(catalog.TryResolve("roger", "CYN", VoiceMode.Strict, new(2, 1), out _, out _));
        Assert.Equal("uncertain", SpeechDataStore.Load(root).Fragments[1].Status);
        SpeechDataStore.Update(root, document => document.Fragments[1].Notes = "Another edit");
        Assert.False(CompiledSpeechData.IsCurrent(root));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(40)]
    [InlineData(72)]
    public void Corrupt_binary_is_rejected_without_json_fallback(int offset)
    {
        CompiledSpeechData.Build(root);
        var bytes = File.ReadAllBytes(CompiledSpeechData.FilePath(root));
        bytes[offset] ^= 0xFF;
        File.WriteAllBytes(CompiledSpeechData.FilePath(root), bytes);
        Assert.Throws<InvalidDataException>(() => new WorkbenchSpeechRuntime(root));
        Assert.Equal(2, SpeechDataStore.Load(root).Fragments.Count);
    }

    [Fact]
    public void Missing_build_requires_workbench_and_failed_build_preserves_previous_file()
    {
        Assert.Throws<FileNotFoundException>(() => new WorkbenchSpeechRuntime(root));
        CompiledSpeechData.Build(root);
        var bytes = File.ReadAllBytes(CompiledSpeechData.FilePath(root));
        File.Delete(Path.Combine(root, "VOICE.VOC"));
        Assert.ThrowsAny<IOException>(() => CompiledSpeechData.Build(root));
        Assert.Equal(bytes, File.ReadAllBytes(CompiledSpeechData.FilePath(root)));
    }

    [Fact]
    public void Airport_catalogue_is_scoped_and_does_not_change_shared_build_reviews_or_preferences()
    {
        CompiledSpeechData.Build(root);
        var shared = File.ReadAllBytes(CompiledSpeechData.FilePath(root));
        var airport = Path.Combine(root, "TST");
        Directory.CreateDirectory(airport);
        File.Copy(Path.Combine(root, "VOICE.VOC"), Path.Combine(airport, "TSTF.VOC"));
        SpeechDataStore.Update(root, document =>
        {
            document.Fragments.Add(new() { Bank = 3, Index = 1, AirportCode = "TST", Transcript = "test fix", Status = "uncertain", Confidence = "low" });
            document.AudioBankHashes.Add("TST/TSTF.VOC", Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(airport, "TSTF.VOC")))));
        });
        var reviews = File.ReadAllBytes(SpeechDataStore.FilePath(root));
        CompiledSpeechData.BuildAirport(root, "TST", root);
        var merged = CompiledSpeechData.LoadRuntime(root, ["TST"]);
        var fragment = Assert.Single(merged.Fragments, f => f.Bank == 3);
        Assert.Equal("uncertain", fragment.Status);
        Assert.Equal(shared, File.ReadAllBytes(CompiledSpeechData.FilePath(root)));
        Assert.Equal(reviews, File.ReadAllBytes(SpeechDataStore.FilePath(root)));
        Assert.Equal(2, CompiledSpeechData.LoadRuntime(root, []).Fragments.Count);
        CompiledSpeechData.BuildAirport(root, "TST", root);
        Assert.Single(Directory.GetFiles(Path.Combine(Directory.GetParent(root)!.FullName, "Backups", "SpeechBuild", "TST")));
        Assert.False(Directory.Exists(Path.Combine(root, "Backups")));
        File.Copy(CompiledSpeechData.FilePath(root), CompiledSpeechData.FilePath(airport), true);
        Assert.Throws<InvalidDataException>(() => CompiledSpeechData.LoadRuntime(root, ["TST"]));
        Assert.Equal(shared, File.ReadAllBytes(CompiledSpeechData.FilePath(root)));
    }

    public void Dispose() => Directory.Delete(Directory.GetParent(root)!.FullName, true);
}
