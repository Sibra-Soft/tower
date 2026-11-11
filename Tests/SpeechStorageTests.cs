using System.Text.Json;
using TowerSpeech.Core;
using Xunit;

namespace Tower.Tests;

public sealed class SpeechStorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "TowerSpeechStorage", Guid.NewGuid().ToString("N"), "Data");
    public SpeechStorageTests() => Directory.CreateDirectory(root);

    [Fact]
    public void Development_uses_repository_Data_even_when_old_build_data_exists()
    {
        File.WriteAllText(Path.Combine(root, "Tower.slnx"), "<Solution />");
        Directory.CreateDirectory(Path.Combine(root, "Data"));
        var application = Path.Combine(root, "Bin", "Tools", "Debug", "net10.0-windows");
        Directory.CreateDirectory(Path.Combine(application, "Data"));
        Directory.CreateDirectory(Path.Combine(application, "Speech", "Data"));
        Assert.Equal(Path.Combine(root, "Data"), WorkspacePaths.Resolve(application));
        Assert.Equal(Path.Combine(root, "Data"), WorkspacePaths.Resolve(Path.Combine(root, "Bin", "Release")));
    }

    [Fact]
    public void Standalone_uses_Data_beside_executable_without_searching_other_workspaces()
    {
        Assert.Equal(Path.Combine(root, "Data"), WorkspacePaths.Resolve(root));
    }

    [Fact]
    public void Every_save_retains_exact_previous_file_and_preserves_unrelated_sections()
    {
        var document = new SpeechDocument
        {
            RecoveredReviewHistory = "original review history",
            Sequences = [new() { Name = "Reviewed sequence" }],
            Fragments = [new() { Bank = 1, Index = 1, Transcript = "american one", Status = "clear", Confidence = "high" }],
            AdditionalData = new() { ["preservedField"] = JsonSerializer.SerializeToElement("unknown metadata") }
        };
        SpeechDataStore.Create(root, document);
        var previous = File.ReadAllBytes(SpeechDataStore.FilePath(root));
        new SpeechPreferences { RadioEffect = true }.Save(root);
        var backups = Path.Combine(Path.GetDirectoryName(root)!, "Backups", "Speech");
        var backup = Assert.Single(Directory.GetFiles(backups, "*.bak"));
        Assert.Equal(previous, File.ReadAllBytes(backup));
        Assert.False(Directory.Exists(Path.Combine(root, "Backups")));
        var loaded = SpeechDataStore.Load(root);
        Assert.Equal("clear", Assert.Single(loaded.Fragments).Status);
        Assert.Equal("Reviewed sequence", Assert.Single(loaded.Sequences).Name);
        Assert.Equal("original review history", loaded.RecoveredReviewHistory);
        Assert.Equal("unknown metadata", loaded.AdditionalData!["preservedField"].GetString());
        Assert.True(loaded.Preferences.RadioEffect);
        Assert.Single(Directory.GetFiles(root, "*.json", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Concurrent_edits_preserve_all_changes()
    {
        SpeechDataStore.Create(root, new());
        await Task.WhenAll(Enumerable.Range(0, 12).Select(index => Task.Run(() =>
            SpeechDataStore.Update(root, document => document.ReviewHistory.Add(JsonSerializer.SerializeToElement(index))))));
        var result = SpeechDataStore.Load(root);
        Assert.Equal(12, result.ReviewHistory.Count);
        Assert.Equal(12, result.Revision);
        Assert.Equal(12, result.ReviewHistory.Select(item => item.GetInt32()).Distinct().Count());
        Assert.Equal(12, Directory.GetFiles(SpeechDataStore.BackupDirectory(root), "*.bak").Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Backup_path_is_beside_Data_with_or_without_a_trailing_separator(bool trailingSeparator)
    {
        var data = trailingSeparator ? root + Path.DirectorySeparatorChar : root;
        Assert.Equal(Path.Combine(Path.GetDirectoryName(root)!, "Backups", "Speech"), SpeechDataStore.BackupDirectory(data));
    }

    [Fact]
    public void Failed_edits_and_imports_do_not_replace_existing_data()
    {
        SpeechDataStore.Create(root, new());
        var previous = File.ReadAllBytes(SpeechDataStore.FilePath(root));
        Assert.Throws<IOException>(() => SpeechDataStore.Create(root, new()));
        Assert.Throws<InvalidDataException>(() => SpeechDataStore.Update(root, document => document.Preferences.Rate = 100));
        Assert.Throws<InvalidOperationException>(() => SpeechDataStore.Update(root, _ => throw new InvalidOperationException("save interrupted")));
        Assert.Equal(previous, File.ReadAllBytes(SpeechDataStore.FilePath(root)));
        Assert.Empty(Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Readers_keep_complete_snapshots_while_saves_replace_the_file()
    {
        SpeechDataStore.Create(root, new());
        var reader = Task.Run(() =>
        {
            for (var index = 0; index < 100; index++)
            {
                var snapshot = SpeechDataStore.Load(root);
                Assert.Equal(snapshot.Revision, snapshot.ReviewHistory.Count);
            }
        }, TestContext.Current.CancellationToken);
        var writer = Task.Run(() =>
        {
            for (var index = 0; index < 12; index++)
            {
                SpeechDataStore.Update(root, current => current.ReviewHistory.Add(JsonSerializer.SerializeToElement(index)));
            }
        }, TestContext.Current.CancellationToken);
        await Task.WhenAll(reader, writer);
        Assert.Equal(12, SpeechDataStore.Load(root).Revision);
    }

    [Fact]
    public void Corrupt_data_is_reported_and_never_replaced_with_empty_defaults()
    {
        var path = SpeechDataStore.FilePath(root);
        File.WriteAllText(path, "{broken");
        Assert.Throws<JsonException>(() => SpeechDataStore.Update(root, _ => { }));
        Assert.Equal("{broken", File.ReadAllText(path));
        File.WriteAllText(path, "{}");
        Assert.Throws<JsonException>(() => SpeechDataStore.Load(root));
        Assert.Equal("{}", File.ReadAllText(path));
    }

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(root)!, true);
}
