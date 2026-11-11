using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tower.Core;
using Tower.Infrastructure;
using TowerSpeech.Core;
using Xunit;

namespace Tower.Tests;

[Trait("Category", "Integration")]
public sealed class AirportPackageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Tower-AirportPackages-" + Guid.NewGuid().ToString("N"));
    private readonly string source = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA") ?? WorkspacePaths.DefaultRoot;
    private string Data => Path.Combine(root, "Data");
    private string Package(string code) => Path.Combine(root, code + "-1.0.0.towerairport");

    public AirportPackageTests()
    {
        Directory.CreateDirectory(Data);
        AirportPackageFixture.Build(source, Package("TST"), "TST");
        foreach (var file in Directory.GetFiles(Path.Combine(source, "CYN"), "*", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(source, "*.VOC")).Append(CompiledSpeechData.FilePath(source)))
        {
            var target = Path.Combine(Data, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    [Fact]
    public void Mixed_case_package_filenames_are_installed_in_uppercase()
    {
        AirportPackageFixture.Build(source, Package("MIX"), "MIX", mixedCaseFile: true);
        AirportPackages.Install(Package("MIX"), Data, cancellationToken: TestContext.Current.CancellationToken);
        Assert.All(Directory.GetFiles(Path.Combine(Data, "MIX"), "*", SearchOption.AllDirectories), path =>
            Assert.Equal(Path.GetFileName(path).ToUpperInvariant(), Path.GetFileName(path)));
        Assert.Equal("MIX", OriginalData.LoadAirport(Data, "MIX").Code);
        AirportPackages.Remove(Data, "MIX", cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(Directory.Exists(Path.Combine(Data, "MIX")));
    }

    [Fact]
    public void Canyon_only_starts_and_a_synthetic_package_adds_an_airport_without_changing_shared_data()
    {
        Assert.Equal(new[] { "CYN" }, OriginalData.InstalledAirports(Data));
        var speech = File.ReadAllBytes(CompiledSpeechData.FilePath(Data));
        var voice = File.ReadAllBytes(Path.Combine(Data, "VOICE.VOC"));
        var manifest = AirportPackages.Inspect(Package("TST"));
        Assert.False(AirportPackages.NeedsReplacement(Data, manifest));
        AirportPackages.Install(Package("TST"), Data, cancellationToken: TestContext.Current.CancellationToken);
        Assert.All(Directory.GetFiles(Path.Combine(Data, "PACKAGES")), path =>
            Assert.Equal(Path.GetFileName(path).ToUpperInvariant(), Path.GetFileName(path)));
        Assert.Equal(new[] { "CYN", "TST" }, OriginalData.InstalledAirports(Data));
        foreach (var file in manifest.Files)
        {
            Assert.Equal(file.Sha256, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Data, file.Path)))), ignoreCase: true);
        }
        var airport = OriginalData.LoadAirport(Data, "TST");
        _ = new Simulation(Simulation.CreateFreeSession(airport, new() { Runway = "9", AircraftPerMinute = 0 }));
        Assert.Equal(speech, File.ReadAllBytes(CompiledSpeechData.FilePath(Data)));
        Assert.Equal(voice, File.ReadAllBytes(Path.Combine(Data, "VOICE.VOC")));
        Assert.False(File.Exists(Path.Combine(Data, "Speech.json")));
    }

    [Fact]
    public void Replacing_an_airport_requires_confirmation_and_retains_exact_backups()
    {
        AirportPackages.Install(Package("TST"), Data, cancellationToken: TestContext.Current.CancellationToken);
        var path = Path.Combine(Data, "TST", "TST.INF");
        var previous = Encoding.UTF8.GetBytes("a user modification that must be backed up");
        File.WriteAllBytes(path, previous);
        Assert.Throws<IOException>(() => AirportPackages.Install(Package("TST"), Data, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(previous, File.ReadAllBytes(path));
        var result = AirportPackages.Install(Package("TST"), Data, replace: true, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(previous, File.ReadAllBytes(Path.Combine(result.BackupDirectory, "Previous", "TST", "TST.INF")));
        Assert.Equal(AirportPackages.Inspect(Package("TST")).Files.Single(f => f.Path == "TST/TST.INF").Sha256,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), ignoreCase: true);
    }

    [Fact]
    public void Cancelling_during_commit_rolls_back_without_installing_a_partial_airport()
    {
        using var cancellation = new CancellationTokenSource();
        var progress = new InlineProgress(message =>
        {
            if (message.StartsWith("Installed ", StringComparison.Ordinal))
            {
                cancellation.Cancel();
            }
        });
        Assert.Throws<OperationCanceledException>(() => AirportPackages.Install(Package("TST"), Data,
            cancellationToken: cancellation.Token, progress: progress));
        AirportPackages.RecoverInterrupted(Data);
        Assert.Equal(new[] { "CYN" }, OriginalData.InstalledAirports(Data));
        Assert.False(File.Exists(Path.Combine(Data, "PACKAGES", "TST.json")));
        Assert.Empty(Directory.GetFiles(Data, "TST*", SearchOption.AllDirectories));
        Assert.NotEmpty(new WorkbenchSpeechRuntime(Data).GetTrafficCallsigns()!);
    }

    [Fact]
    public void Startup_recovers_an_interrupted_replacement_from_its_journal()
    {
        AirportPackages.Install(Package("TST"), Data, cancellationToken: TestContext.Current.CancellationToken);
        var target = Path.Combine(Data, "TST", "TST.INF");
        var previous = File.ReadAllBytes(target);
        var folder = Path.Combine(root, "Backups", "Airports", "Transactions", "interrupted");
        Directory.CreateDirectory(Path.Combine(folder, "Previous", "TST"));
        File.WriteAllBytes(Path.Combine(folder, "Previous", "TST", "TST.INF"), previous);
        File.WriteAllText(target, "incomplete replacement");
        File.WriteAllText(Path.Combine(folder, "transaction.json"), JsonSerializer.Serialize(new
        {
            DataRoot = Data, AirportCode = "TST", Status = "Applying", Files = new[] { new { Path = "TST/TST.INF", Existed = true } }
        }));
        AirportPackages.RecoverInterrupted(Data);
        Assert.Equal(previous, File.ReadAllBytes(target));
        Assert.Contains("RolledBack", File.ReadAllText(Path.Combine(folder, "transaction.json")));
        AirportPackages.RecoverInterrupted(Data);
    }

    [Theory]
    [InlineData("../escape.VOC")]
    [InlineData("VOICE.VOC")]
    [InlineData("CYN/CYN.TWR")]
    [InlineData("TST/plugin.dll")]
    [InlineData("TST/CON.VOC")]
    public void Unsafe_or_shared_targets_are_rejected_before_changes(string invalidPath)
    {
        var original = AirportPackages.Inspect(Package("TST"));
        var modified = original with { Files = [.. original.Files, new(invalidPath, 0, new string('0', 64))] };
        var package = Path.Combine(root, "invalid.towerairport");
        using (var archive = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(archive.CreateEntry("manifest.json").Open()))
            {
                writer.Write(JsonSerializer.Serialize(modified));
            }
            archive.CreateEntry("unused");
        }
        Assert.Throws<InvalidDataException>(() => AirportPackages.Install(package, Data, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(new[] { "CYN" }, OriginalData.InstalledAirports(Data));
    }

    [Fact]
    public void Manager_lists_version_and_installation_date_and_removal_preserves_shared_data()
    {
        var before = DateTimeOffset.UtcNow;
        AirportPackages.Install(Package("TST"), Data, cancellationToken: TestContext.Current.CancellationToken);
        var installed = AirportPackages.Installed(Data);
        var canyon = installed.Single(a => a.Code == "CYN");
        Assert.False(canyon.CanRemove);
        Assert.Equal("Included with Tower", canyon.Status);
        var airport = installed.Single(a => a.Code == "TST");
        Assert.Equal("1.0.0", airport.Version);
        Assert.True(airport.CanRemove);
        Assert.InRange(airport.InstalledAtUtc!.Value, before, DateTimeOffset.UtcNow);
        var metadata = File.ReadAllBytes(CompiledSpeechData.FilePath(Data));
        File.WriteAllText(Path.Combine(Data, "TST", "keep.txt"), "unrelated edit");
        var original = File.ReadAllBytes(Path.Combine(Data, "TST", "TST.INF"));
        var backup = AirportPackages.Remove(Data, "TST", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(AirportPackages.Installed(Data));
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(backup, "Previous", "TST", "TST.INF")));
        Assert.Equal(metadata, File.ReadAllBytes(CompiledSpeechData.FilePath(Data)));
        Assert.True(File.Exists(Path.Combine(Data, "TST", "keep.txt")));
        Assert.False(File.Exists(Path.Combine(Data, "AIRPORTS", "TST.CFG")));
        Assert.False(File.Exists(Path.Combine(Data, "GRAPHICS", "IMAGES", "TST640.BIT")));
        Assert.False(File.Exists(Path.Combine(Data, "PACKAGES", "TST.json")));
        Assert.Throws<InvalidDataException>(() => AirportPackages.Remove(Data, "CYN", cancellationToken: TestContext.Current.CancellationToken));
        Assert.NotEmpty(new WorkbenchSpeechRuntime(Data).GetTrafficCallsigns()!);
        AirportPackages.Install(Package("TST"), Data, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(AirportPackages.Installed(Data).Single(a => a.Code == "TST").InstalledAtUtc >= airport.InstalledAtUtc);
    }

    [Fact]
    public void Legacy_receipts_keep_known_version_without_inventing_a_date()
    {
        AirportPackages.Install(Package("TST"), Data, cancellationToken: TestContext.Current.CancellationToken);
        var receipt = Path.Combine(Data, "PACKAGES", "TST.json");
        File.WriteAllText(receipt, JsonSerializer.Serialize(AirportPackages.Inspect(Package("TST"))));
        Directory.Move(Path.Combine(Data, "PACKAGES"), Path.Combine(Data, "AirportPackages"));
        var old = AirportPackages.Installed(Data).Single(a => a.Code == "TST");
        Assert.True(File.Exists(receipt));
        Assert.Equal("1.0.0", old.Version);
        Assert.Null(old.InstalledAtUtc);
        File.Delete(receipt);
        var unregistered = AirportPackages.Installed(Data).Single(a => a.Code == "TST");
        Assert.Null(unregistered.Version);
        Assert.Null(unregistered.InstalledAtUtc);
        Assert.Equal("Unregistered", unregistered.Status);
        AirportPackages.Remove(Data, "TST", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "CYN" }, OriginalData.InstalledAirports(Data));
        Assert.False(File.Exists(Path.Combine(Data, "GRAPHICS", "IMAGES", "TST640.BIT")));
    }

    [Fact]
    public void Interrupted_removal_restores_payload_and_original_installation_registration()
    {
        AirportPackages.Install(Package("TST"), Data, cancellationToken: TestContext.Current.CancellationToken);
        var receipt = Path.Combine(Data, "PACKAGES", "TST.json");
        var original = File.ReadAllBytes(receipt);
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => AirportPackages.Remove(Data, "TST", cancellation.Token,
            new InlineProgress(_ => cancellation.Cancel())));
        Assert.Equal(original, File.ReadAllBytes(receipt));
        foreach (var file in AirportPackages.Inspect(Package("TST")).Files)
        {
            Assert.Equal(file.Sha256, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Data, file.Path)))), ignoreCase: true);
        }
        Assert.True(AirportPackages.Installed(Data).Single(a => a.Code == "TST").CanRemove);
    }

    [Fact]
    public void Corrupted_payload_is_rejected_without_touching_existing_airports()
    {
        var package = Path.Combine(root, "damaged.towerairport");
        File.Copy(Package("TST"), package);
        using (var archive = ZipFile.Open(package, ZipArchiveMode.Update))
        {
            var entry = archive.GetEntry("Data/TST/TST.INF")!;
            var length = entry.Length;
            entry.Delete();
            using var output = archive.CreateEntry("Data/TST/TST.INF").Open();
            output.Write(new byte[length]);
        }
        Assert.Throws<InvalidDataException>(() => AirportPackages.Install(package, Data, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(new[] { "CYN" }, OriginalData.InstalledAirports(Data));
    }

    [Fact]
    public void Discovery_is_not_limited_to_the_three_original_codes_or_variant_TWR_files()
    {
        var folder = Path.Combine(Data, "NEW");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "NEW.TWR"), "discovery only");
        File.WriteAllText(Path.Combine(folder, "NEW1.TWR"), "variant");
        Assert.Equal(new[] { "CYN", "NEW" }, OriginalData.InstalledAirports(Data));
    }

    private sealed class InlineProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }

    public void Dispose() => Directory.Delete(root, true);
}
