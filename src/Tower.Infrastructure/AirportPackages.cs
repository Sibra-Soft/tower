using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using TowerSpeech.Core;

namespace Tower.Infrastructure;

public sealed record AirportPackageFile(string Path, long Bytes, string Sha256);
public sealed record AirportPackageManifest(int FormatVersion, string AirportCode, string Name, string Version, List<AirportPackageFile> Files)
{
    public DateTimeOffset? InstalledAtUtc { get; init; }
}
public sealed record AirportInstallResult(string AirportCode, string Name, string Version, string BackupDirectory);
public sealed record InstalledAirport(string Code, string Name, string? Version, DateTimeOffset? InstalledAtUtc, string Status, bool CanRemove);

/// <summary>Data-only airport packages. All validation precedes any change to active Data.</summary>
public static class AirportPackages
{
    private const long MaximumBytes = 512L * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private sealed record Replacement(string Path, bool Existed);
    private sealed record Transaction(string DataRoot, string AirportCode, string Status, List<Replacement> Files);

    public static AirportPackageManifest Inspect(string package)
    {
        using var archive = ZipFile.OpenRead(package);
        return Inspect(archive);
    }

    private static AirportPackageManifest Inspect(ZipArchive archive)
    {
        if (archive.Entries.Count is < 2 or > 4096 || archive.Entries.Any(e => e.Length > 128L * 1024 * 1024) ||
            archive.Entries.Sum(e => e.Length) > MaximumBytes ||
            archive.Entries.Select(e => e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != archive.Entries.Count)
        {
            throw new InvalidDataException("Invalid airport package size or duplicate entries.");
        }
        var entry = archive.GetEntry("manifest.json") ?? throw new InvalidDataException("The airport package has no manifest.");
        if (entry.Length > 1024 * 1024)
        {
            throw new InvalidDataException("Airport manifest is too large.");
        }
        using var stream = entry.Open();
        var manifestBytes = new byte[(int)entry.Length];
        stream.ReadExactly(manifestBytes);
        if (stream.ReadByte() != -1)
        {
            throw new InvalidDataException("Airport manifest exceeds its declared size.");
        }
        var manifest = JsonSerializer.Deserialize<AirportPackageManifest>(manifestBytes, Json) ?? throw new InvalidDataException("Empty airport manifest.");
        ValidateManifest(manifest);
        var names = manifest.Files.Select(f => "Data/" + f.Path).Append("manifest.json").ToHashSet(StringComparer.Ordinal);
        if (archive.Entries.Any(e => !names.Contains(e.FullName) || ((e.ExternalAttributes >> 16) & 0xF000) == 0xA000) ||
            names.Count != archive.Entries.Count)
        {
            throw new InvalidDataException("Airport package contains unlisted files, links or invalid paths.");
        }
        foreach (var file in manifest.Files)
        {
            if (archive.GetEntry("Data/" + file.Path)?.Length != file.Bytes)
            {
                throw new InvalidDataException("Airport package file size differs from its manifest: " + file.Path);
            }
        }
        return manifest;
    }

    private static void ValidateManifest(AirportPackageManifest manifest)
    {
        if (manifest.FormatVersion is not (1 or 2) || manifest.AirportCode is null || !Regex.IsMatch(manifest.AirportCode, @"^[A-Z0-9]{3,8}$") ||
            manifest.AirportCode is "CYN" or "AIRPORTS" or "GRAPHICS" or "BACKUPS" || string.IsNullOrWhiteSpace(manifest.Name) || manifest.Name.Length > 120 ||
            manifest.Name.Any(char.IsControl) || !System.Version.TryParse(manifest.Version, out _) ||
            manifest.Files is null || manifest.Files.Count is < 1 or > 4095 ||
            manifest.Files.Any(f => f is null || !OwnedPath(f.Path, manifest.AirportCode) || f.Bytes is < 0 or > 134217728 ||
                f.Sha256 is null || !Regex.IsMatch(f.Sha256, @"^[A-Fa-f0-9]{64}$")) ||
            manifest.Files.Select(f => f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.Files.Count)
        {
            throw new InvalidDataException("Invalid or unsupported airport package manifest.");
        }
        if (manifest.FormatVersion == 1 && manifest.Files.Any(file =>
            file.Path.StartsWith(manifest.AirportCode + "/SCENERY/", StringComparison.OrdinalIgnoreCase) ||
            file.Path.Equals(manifest.AirportCode + "/SPEECH.BIN", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("Airport-local scenery and speech require package format 2.");
        }
        foreach (var extension in new[] { "TWR", "PAT", "PRF", "SEC", "SPF", "TYP" })
        {
            Require(manifest, $"{manifest.AirportCode}/{manifest.AirportCode}.{extension}");
        }
        foreach (var bank in manifest.FormatVersion == 1 ? new[] { "F", "R", "T" } : Array.Empty<string>())
        {
            Require(manifest, $"{manifest.AirportCode}/{manifest.AirportCode}{bank}.VOC");
        }
        Require(manifest, $"AIRPORTS/{manifest.AirportCode}.CFG");
    }

    private static void Require(AirportPackageManifest manifest, string path)
    {
        if (!manifest.Files.Any(f => f.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("Required airport file is missing: " + path);
        }
    }

    private static bool SafeRelative(string? path) => !string.IsNullOrEmpty(path) && path.Length < 220 &&
        path.Split('/').All(part => Regex.IsMatch(part, @"^[A-Za-z0-9_-][A-Za-z0-9_. -]*$") &&
            part == part.TrimEnd(' ', '.') && !Regex.IsMatch(part, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)", RegexOptions.IgnoreCase));

    private static bool OwnedPath(string? path, string code)
    {
        if (!SafeRelative(path))
        {
            return false;
        }
        var parts = path!.Split('/');
        if (parts.Length == 3 && parts[0].Equals(code, StringComparison.OrdinalIgnoreCase) &&
            parts[1].Equals("SCENERY", StringComparison.OrdinalIgnoreCase) &&
            Path.GetExtension(parts[2]).Equals(".DBT", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (parts.Length == 2 && parts[0].Equals(code, StringComparison.OrdinalIgnoreCase))
        {
            if (parts[1].Equals("SPEECH.BIN", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return new[] { ".TWR", ".PAT", ".PRF", ".SEC", ".SPF", ".TYP", ".VOC", ".SIM", ".DAT", ".CON", ".CPL", ".INF", ".MAP" }
                .Contains(Path.GetExtension(parts[1]), StringComparer.OrdinalIgnoreCase);
        }
        return path.Equals($"AIRPORTS/{code}.CFG", StringComparison.OrdinalIgnoreCase) ||
            parts.Length == 3 && parts[0].Equals("GRAPHICS", StringComparison.OrdinalIgnoreCase) &&
            parts[1].Equals("IMAGES", StringComparison.OrdinalIgnoreCase) && parts[2].StartsWith(code, StringComparison.OrdinalIgnoreCase) &&
            Path.GetExtension(parts[2]).Equals(".BIT", StringComparison.OrdinalIgnoreCase);
    }

    private static string Target(string root, string relative)
    {
        if (!SafeRelative(relative))
        {
            throw new InvalidDataException("Invalid installation path.");
        }
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var path = Path.GetFullPath(Path.Combine(prefix, relative.ToUpperInvariant().Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(prefix + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Airport path escapes the Data folder.");
        }
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidDataException("Airport installation does not follow filesystem links: " + current);
            }
            if (current.Equals(prefix, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }
        return path;
    }

    public static bool NeedsReplacement(string dataRoot, AirportPackageManifest manifest)
        => manifest.Files.Any(f => File.Exists(Target(dataRoot, f.Path)));

    private static string Transactions(string dataRoot) => Path.Combine(Directory.GetParent(Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataRoot)))!.FullName, "Backups", "Airports", "Transactions");

    private static T Locked<T>(string dataRoot, Func<T> action)
    {
        var key = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataRoot)).ToUpperInvariant())));
        using var mutex = new Mutex(false, "TowerAirportInstall_" + key);
        var acquired = false;
        try
        {
            try
            {
                acquired = mutex.WaitOne(TimeSpan.FromSeconds(5));
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }
            if (!acquired)
            {
                throw new IOException("Another airport installation is in progress.");
            }
            return action();
        }
        finally
        {
            if (acquired)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    public static void RecoverInterrupted(string dataRoot) => Locked(dataRoot, () =>
    {
        var transactions = Transactions(dataRoot);
        if (Directory.Exists(transactions))
        {
            foreach (var path in Directory.EnumerateFiles(transactions, "transaction.json", SearchOption.AllDirectories))
            {
                var transaction = JsonSerializer.Deserialize<Transaction>(File.ReadAllText(path), Json)
                    ?? throw new InvalidDataException("Invalid airport recovery record.");
                if (transaction.Status == "Applying" && string.Equals(transaction.DataRoot, Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataRoot)), StringComparison.OrdinalIgnoreCase))
                {
                    Rollback(Path.GetDirectoryName(path)!, transaction);
                }
            }
        }
        var legacyFolder = Target(dataRoot, "AirportPackages");
        if (Directory.Exists(legacyFolder))
        {
            foreach (var legacyReceipt in Directory.GetFiles(legacyFolder, "*.json"))
            {
                var destination = Target(dataRoot, "PACKAGES/" + Path.GetFileName(legacyReceipt));
                if (!File.Exists(destination))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Move(legacyReceipt, destination);
                }
            }
            if (!Directory.EnumerateFileSystemEntries(legacyFolder).Any())
            {
                Directory.Delete(legacyFolder);
            }
        }
        return true;
    });

    public static AirportInstallResult Install(string package, string dataRoot, bool replace = false,
        CancellationToken cancellationToken = default, IProgress<string>? progress = null) => Locked(dataRoot, () =>
    {
        dataRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataRoot));
        RecoverInterrupted(dataRoot);
        cancellationToken.ThrowIfCancellationRequested();
        using var archive = ZipFile.OpenRead(package);
        var manifest = Inspect(archive);
        if (NeedsReplacement(dataRoot, manifest) && !replace)
        {
            throw new IOException("This airport already has installed files. Confirm replacement before installing.");
        }
        var folder = Path.Combine(Transactions(dataRoot), DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        var staging = Path.Combine(folder, "Staging");
        Directory.CreateDirectory(staging);
        foreach (var file in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report("Validating " + file.Path);
            var path = Target(staging, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var input = archive.GetEntry("Data/" + file.Path)!.Open();
            using var output = File.Create(path);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[65536];
            long total = 0;
            int read;
            while ((read = input.Read(buffer)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                total += read;
                if (total > file.Bytes)
                {
                    throw new InvalidDataException("Expanded airport file exceeds its declared size.");
                }
                hash.AppendData(buffer, 0, read);
                output.Write(buffer, 0, read);
            }
            if (total != file.Bytes || !Convert.ToHexString(hash.GetHashAndReset()).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Airport file checksum mismatch: " + file.Path);
            }
        }
        try
        {
            var airport = OriginalData.LoadAirport(staging, manifest.AirportCode);
            var scenery = AirportScenery.Read(staging, airport);
            if (scenery.Errors.Count > 0)
            {
                throw new InvalidDataException("Airport scenery is incomplete: " + string.Join("; ", scenery.Errors));
            }
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or KeyNotFoundException or IndexOutOfRangeException or OverflowException or InvalidOperationException)
        {
            throw new InvalidDataException("Airport data is invalid: " + exception.Message, exception);
        }
        foreach (var file in manifest.Files.Where(f => Path.GetExtension(f.Path).Equals(".VOC", StringComparison.OrdinalIgnoreCase)))
        {
            _ = new VocArchive(Target(staging, file.Path));
        }
        var speech = CompiledSpeechData.Load(dataRoot);
        speech.Fragments = speech.Fragments.Where(f => f.Bank is >= 3 and <= 5 && f.AirportCode.Equals(manifest.AirportCode, StringComparison.OrdinalIgnoreCase)).ToList();
        var localSpeech = Path.Combine(staging, manifest.AirportCode);
        if (File.Exists(CompiledSpeechData.FilePath(localSpeech)))
        {
            var local = CompiledSpeechData.LoadAirport(localSpeech, manifest.AirportCode);
            speech.Fragments = local.Fragments;
            foreach (var hash in local.AudioBankHashes)
            {
                speech.AudioBankHashes[hash.Key] = hash.Value;
            }
        }
        new VocAudioRepository(staging).ValidateSources(speech);
        var receipt = $"PACKAGES/{manifest.AirportCode}.json";
        AtomicJson(Target(staging, receipt), manifest with { InstalledAtUtc = DateTimeOffset.UtcNow });
        var changes = manifest.Files.Select(f => f.Path).Append(receipt).Select(path => new Replacement(path, File.Exists(Target(dataRoot, path)))).ToList();
        foreach (var change in changes.Where(c => c.Existed))
        {
            var backup = Target(Path.Combine(folder, "Previous"), change.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
            File.Copy(Target(dataRoot, change.Path), backup);
        }
        var transaction = new Transaction(dataRoot, manifest.AirportCode, "Applying", changes);
        AtomicJson(Path.Combine(folder, "transaction.json"), transaction);
        try
        {
            foreach (var change in changes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ReplaceFile(Target(staging, change.Path), Target(dataRoot, change.Path));
                progress?.Report("Installed " + change.Path);
            }
            cancellationToken.ThrowIfCancellationRequested();
            AtomicJson(Path.Combine(folder, "transaction.json"), transaction with { Status = "Complete" });
        }
        catch
        {
            Rollback(folder, transaction);
            throw;
        }
        return new AirportInstallResult(manifest.AirportCode, manifest.Name, manifest.Version, folder);
    });

    public static IReadOnlyList<InstalledAirport> Installed(string dataRoot) => Locked(dataRoot, () =>
    {
        RecoverInterrupted(dataRoot);
        return OriginalData.InstalledAirports(dataRoot).Select(code =>
        {
            var name = code switch { "CYN" => "Canyon Field", "DCA" => "Washington National", "ORD" => "Chicago O'Hare", _ => code };
            if (code == "CYN")
            {
                return new InstalledAirport(code, name, null, null, "Included with Tower", false);
            }
            var path = Target(dataRoot, $"PACKAGES/{code}.json");
            if (!File.Exists(path))
            {
                return new InstalledAirport(code, name, null, null, "Unregistered", true);
            }
            try
            {
                var manifest = ReadReceipt(path, code);
                return new InstalledAirport(code, manifest.Name, manifest.Version, manifest.InstalledAtUtc, "Installed", true);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
            {
                return new InstalledAirport(code, name, null, null, "Registration error", false);
            }
        }).ToArray();
    });

    private static AirportPackageManifest ReadReceipt(string path, string code)
    {
        if (new FileInfo(path).Length > 1024 * 1024)
        {
            throw new InvalidDataException("Airport registration is too large.");
        }
        var manifest = JsonSerializer.Deserialize<AirportPackageManifest>(File.ReadAllText(path), Json)
            ?? throw new InvalidDataException("Empty airport registration.");
        ValidateManifest(manifest);
        if (manifest.AirportCode != code)
        {
            throw new InvalidDataException("Airport registration does not match its folder.");
        }
        return manifest;
    }

    public static string Remove(string dataRoot, string code, CancellationToken cancellationToken = default,
        IProgress<string>? progress = null) => Locked(dataRoot, () =>
    {
        dataRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataRoot));
        code = code.ToUpperInvariant();
        if (!Regex.IsMatch(code, @"^[A-Z0-9]{3,8}$") || code is "CYN" or "AIRPORTS" or "GRAPHICS" or "BACKUPS")
        {
            throw new InvalidDataException("This airport cannot be removed.");
        }
        RecoverInterrupted(dataRoot);
        var receipt = $"PACKAGES/{code}.json";
        var receiptPath = Target(dataRoot, receipt);
        IEnumerable<string> paths;
        if (File.Exists(receiptPath))
        {
            paths = ReadReceipt(receiptPath, code).Files.Select(f => f.Path);
        }
        else
        {
            var folder = Target(dataRoot, code);
            paths = Directory.Exists(folder) ? Directory.GetFiles(folder).Select(f => code + "/" + Path.GetFileName(f)).Where(f => OwnedPath(f, code)).ToArray() : [];
            var localScenery = Target(dataRoot, code + "/SCENERY");
            if (Directory.Exists(localScenery))
            {
                paths = paths.Concat(Directory.GetFiles(localScenery).Select(f => code + "/SCENERY/" + Path.GetFileName(f))
                    .Where(f => OwnedPath(f, code)));
            }
            var config = $"AIRPORTS/{code}.CFG";
            var configPath = Target(dataRoot, config);
            if (File.Exists(configPath))
            {
                var image = LegacyAirportReader.ReadIni(configPath).GetValueOrDefault("Config0.IMAGE");
                var imagePath = "GRAPHICS/IMAGES/" + image;
                if (image is not null && OwnedPath(imagePath, code))
                {
                    paths = paths.Append(imagePath);
                }
                paths = paths.Append(config);
            }
        }
        var changes = paths.Append(receipt).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(p => File.Exists(Target(dataRoot, p))).Select(p => new Replacement(p, true)).ToList();
        if (changes.Count == 0)
        {
            throw new InvalidDataException("No installed files were found for this airport.");
        }
        var transactionFolder = Path.Combine(Transactions(dataRoot), "remove-" + Guid.NewGuid().ToString("N"));
        foreach (var change in changes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var backup = Target(Path.Combine(transactionFolder, "Previous"), change.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
            File.Copy(Target(dataRoot, change.Path), backup);
        }
        var transaction = new Transaction(dataRoot, code, "Applying", changes);
        AtomicJson(Path.Combine(transactionFolder, "transaction.json"), transaction);
        try
        {
            foreach (var change in changes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Delete(Target(dataRoot, change.Path));
                progress?.Report("Removed " + change.Path);
            }
            cancellationToken.ThrowIfCancellationRequested();
            AtomicJson(Path.Combine(transactionFolder, "transaction.json"), transaction with { Status = "Complete" });
        }
        catch
        {
            Rollback(transactionFolder, transaction);
            throw;
        }
        try
        {
            var airportFolder = Target(dataRoot, code);
            if (Directory.Exists(airportFolder) && !Directory.EnumerateFileSystemEntries(airportFolder).Any())
            {
                Directory.Delete(airportFolder);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            // Empty-folder cleanup must not report a committed removal as failed.
        }
        return transactionFolder;
    });

    private static void Rollback(string folder, Transaction transaction)
    {
        if (transaction.AirportCode is null || !Regex.IsMatch(transaction.AirportCode, @"^[A-Z0-9]{3,8}$") ||
            transaction.AirportCode == "CYN" || transaction.Files is null || transaction.Files.Count is < 1 or > 4096 ||
            transaction.Files.Any(f => f is null || !(OwnedPath(f.Path, transaction.AirportCode) ||
                string.Equals(f.Path, $"PACKAGES/{transaction.AirportCode}.json", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(f.Path, $"AirportPackages/{transaction.AirportCode}.json", StringComparison.OrdinalIgnoreCase))))
        {
            throw new InvalidDataException("Invalid airport recovery record.");
        }
        foreach (var change in transaction.Files.AsEnumerable().Reverse())
        {
            var target = Target(transaction.DataRoot, change.Path);
            if (change.Existed)
            {
                ReplaceFile(Target(Path.Combine(folder, "Previous"), change.Path), target);
            }
            else if (File.Exists(target))
            {
                File.Delete(target);
            }
        }
        AtomicJson(Path.Combine(folder, "transaction.json"), transaction with { Status = "RolledBack" });
    }

    private static void ReplaceFile(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.Copy(source, temporary);
            File.Move(temporary, destination, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static void AtomicJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            file.Write(bytes);
            file.Flush(true);
        }
        File.Move(temporary, path, true);
    }
}
