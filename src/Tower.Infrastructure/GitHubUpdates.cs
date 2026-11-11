using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tower.Infrastructure;

public sealed record TowerUpdate(Version Version, string Tag, string Notes, Uri Page, string InstallerName,
    Uri Installer, long InstallerBytes, Uri Checksums)
{
    public bool IsNewerThan(string installed)
    {
        var withoutBuild = installed.Split('+')[0];
        if (!System.Version.TryParse(withoutBuild.Split('-')[0], out var current))
        {
            throw new InvalidDataException("The installed Tower version could not be determined.");
        }
        current = new(current.Major, current.Minor, Math.Max(0, current.Build));
        return Version > current || Version == current && withoutBuild.Contains('-');
    }
}

public sealed class GitHubUpdates : IDisposable
{
    public const string Repository = "Sibra-Soft/tower-development";
    public const long MaximumInstallerBytes = 512L * 1024 * 1024;
    private readonly HttpClient client;
    private readonly bool ownsClient;

    public GitHubUpdates(HttpClient? client = null)
    {
        ownsClient = client is null;
        this.client = client ?? new HttpClient { Timeout = TimeSpan.FromMinutes(5), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
    }

    public async Task<TowerUpdate?> LatestAsync(CancellationToken cancellation = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var request = Request(new("https://api.github.com/repos/" + Repository + "/releases/latest"));
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await client.SendAsync(request, timeout.Token);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
        {
            throw new HttpRequestException("GitHub is temporarily limiting update checks. Please try again later.");
        }
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        var root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean())
        {
            return null;
        }
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        var match = Regex.Match(tag, @"^v?((0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*))$", RegexOptions.CultureInvariant);
        if (!match.Success || !Version.TryParse(match.Groups[1].Value, out var version))
        {
            throw new InvalidDataException("The latest release has an unsupported version tag.");
        }
        var assets = root.GetProperty("assets").EnumerateArray().ToArray();
        JsonElement Asset(string name)
        {
            var found = assets.Where(asset => asset.GetProperty("name").GetString() == name &&
                asset.GetProperty("state").GetString() == "uploaded").ToArray();
            return found.Length == 1 ? found[0] : throw new InvalidDataException("The latest release is not ready for installation. Please try again later.");
        }
        var installerName = assets.Any(asset => asset.GetProperty("name").GetString() == "Tower-Windows-x64-Setup.exe")
            ? "Tower-Windows-x64-Setup.exe" : $"Tower-{version}-Windows-x64-Setup.exe";
        var installer = Asset(installerName);
        var checksumsName = $"Tower-{version}-SHA256SUMS.txt";
        var checksums = Asset(checksumsName);
        Uri Download(JsonElement asset, string name)
        {
            var expected = new Uri($"https://github.com/{Repository}/releases/download/{Uri.EscapeDataString(tag)}/{Uri.EscapeDataString(name)}");
            if (!Uri.TryCreate(asset.GetProperty("browser_download_url").GetString(), UriKind.Absolute, out var url) || url != expected)
            {
                throw new InvalidDataException("The release contains an unexpected download address.");
            }
            return url;
        }
        var size = installer.GetProperty("size").GetInt64();
        if (size is <= 0 or > MaximumInstallerBytes)
        {
            throw new InvalidDataException("The release installer has an invalid size.");
        }
        return new(version, tag, root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "",
            new($"https://github.com/{Repository}/releases/tag/{Uri.EscapeDataString(tag)}"), installerName,
            Download(installer, installerName), size, Download(checksums, checksumsName));
    }

    public async Task<string> DownloadAsync(TowerUpdate update, string directory, IProgress<int>? progress = null, CancellationToken cancellation = default)
    {
        using var checksumRequest = Request(update.Checksums);
        using var checksumResponse = await client.SendAsync(checksumRequest, cancellation);
        checksumResponse.EnsureSuccessStatusCode();
        var sums = await checksumResponse.Content.ReadAsStringAsync(cancellation);
        var hashes = sums.Split('\n').Select(line => Regex.Match(line.Trim(), @"^([a-fA-F0-9]{64})\s+\*?(.+)$"))
            .Where(match => match.Success && match.Groups[2].Value == update.InstallerName).ToArray();
        if (hashes.Length != 1)
        {
            throw new InvalidDataException("The release does not contain an installer checksum.");
        }
        directory = Path.Combine(directory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, update.InstallerName);
        var partial = target + ".part";
        try
        {
            using var request = Request(update.Installer);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(cancellation);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long total = 0;
            await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920];
                int count;
                while ((count = await input.ReadAsync(buffer, cancellation)) > 0)
                {
                    total += count;
                    if (total > update.InstallerBytes || total > MaximumInstallerBytes)
                    {
                        throw new InvalidDataException("The installer size does not match the release.");
                    }
                    hash.AppendData(buffer, 0, count);
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellation);
                    progress?.Report((int)(total * 100 / update.InstallerBytes));
                }
            }
            if (total != update.InstallerBytes || !Convert.ToHexString(hash.GetHashAndReset()).Equals(hashes[0].Groups[1].Value, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Installer verification failed. Nothing has been installed; please try downloading again.");
            }
            cancellation.ThrowIfCancellationRequested();
            File.Move(partial, target);
            return target;
        }
        finally
        {
            if (File.Exists(partial))
            {
                File.Delete(partial);
            }
        }
    }

    private static HttpRequestMessage Request(Uri url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("Tower-Updater/1.0");
        return request;
    }

    public void Dispose()
    {
        if (ownsClient)
        {
            client.Dispose();
        }
    }
}
