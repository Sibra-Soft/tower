using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class GitHubUpdatesTests
{
    private const string Installer = "Tower-Windows-x64-Setup.exe";
    private static readonly byte[] Payload = Encoding.ASCII.GetBytes("MZ offline installer fixture");
    private static string Release(string tag = "v1.0.1", bool draft = false, bool prerelease = false, string? installerUrl = null, bool includeChecksum = true) =>
        JsonSerializer.Serialize(new
        {
            tag_name = tag, draft, prerelease, body = "New Tower release.",
            assets = new[]
            {
                new { name = Installer, state = "uploaded", size = Payload.Length,
                    browser_download_url = installerUrl ?? $"https://github.com/{GitHubUpdates.Repository}/releases/download/{tag}/{Installer}" },
                new { name = includeChecksum ? $"Tower-{tag.TrimStart('v')}-SHA256SUMS.txt" : "other.txt", state = "uploaded", size = 100,
                    browser_download_url = $"https://github.com/{GitHubUpdates.Repository}/releases/download/{tag}/Tower-{tag.TrimStart('v')}-SHA256SUMS.txt" }
            }
        });

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.NotEmpty(request.Headers.UserAgent);
            Assert.Null(request.Headers.Authorization);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpClient Client(string? release = null, bool badHash = false, bool truncated = false) => new(new Handler(request =>
    {
        if (request.RequestUri!.AbsolutePath.EndsWith("/latest", StringComparison.Ordinal))
        {
            return new(HttpStatusCode.OK) { Content = new StringContent(release ?? Release()) };
        }
        if (request.RequestUri.AbsolutePath.EndsWith("SHA256SUMS.txt", StringComparison.Ordinal))
        {
            var hash = badHash ? new string('0', 64) : Convert.ToHexString(SHA256.HashData(Payload));
            return new(HttpStatusCode.OK) { Content = new StringContent(hash + "  " + Installer + "\n") };
        }
        return new(HttpStatusCode.OK) { Content = new ByteArrayContent(truncated ? Payload[..^1] : Payload) };
    }));

    [Theory]
    [InlineData("1.0.0", true)]
    [InlineData("1.0.1", false)]
    [InlineData("1.0.2", false)]
    [InlineData("1.0.1-rc.1", true)]
    [InlineData("1.0.1+hash", false)]
    public async Task Stable_versions_do_not_downgrade_and_can_replace_a_matching_prerelease(string installed, bool newer)
    {
        using var client = Client();
        using var updates = new GitHubUpdates(client);
        var release = Assert.IsType<TowerUpdate>(await updates.LatestAsync(TestContext.Current.CancellationToken));
        Assert.Equal(new Version(1, 0, 1), release.Version);
        Assert.Equal(newer, release.IsNewerThan(installed));
    }

    [Theory]
    [InlineData("1.0.10")]
    [InlineData("v1.0.10")]
    public async Task Numeric_version_comparison_accepts_both_release_tag_styles(string tag)
    {
        using var client = Client(Release(tag));
        using var updates = new GitHubUpdates(client);
        Assert.True((await updates.LatestAsync(TestContext.Current.CancellationToken))!.IsNewerThan("1.0.2"));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Drafts_and_prereleases_are_not_offered(bool draft, bool prerelease)
    {
        using var client = Client(Release(draft: draft, prerelease: prerelease));
        using var updates = new GitHubUpdates(client);
        Assert.Null(await updates.LatestAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Private_repository_or_no_release_is_a_normal_unavailable_result()
    {
        using var client = new HttpClient(new Handler(_ => new(HttpStatusCode.NotFound)));
        using var updates = new GitHubUpdates(client);
        Assert.Null(await updates.LatestAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("https://example.com/installer.exe")]
    [InlineData("http://github.com/Sibra-Soft/tower-development/releases/download/v1.0.1/Tower-Windows-x64-Setup.exe")]
    [InlineData("https://github.com/other/project/releases/download/v1.0.1/Tower-Windows-x64-Setup.exe")]
    public async Task Unexpected_download_hosts_or_repositories_are_rejected(string url)
    {
        using var client = Client(Release(installerUrl: url));
        using var updates = new GitHubUpdates(client);
        await Assert.ThrowsAsync<InvalidDataException>(() => updates.LatestAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Incomplete_release_is_not_installable()
    {
        using var client = Client(Release(includeChecksum: false));
        using var updates = new GitHubUpdates(client);
        await Assert.ThrowsAsync<InvalidDataException>(() => updates.LatestAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Only_a_complete_verified_installer_becomes_executable(bool badHash, bool truncated)
    {
        using var client = Client(badHash: badHash, truncated: truncated);
        using var updates = new GitHubUpdates(client);
        var release = (await updates.LatestAsync(TestContext.Current.CancellationToken))!;
        var directory = Path.Combine(Path.GetTempPath(), "Tower-Update-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var unrelated = Path.Combine(directory, "keep.txt");
        File.WriteAllText(unrelated, "preserve");
        try
        {
            if (badHash || truncated)
            {
                await Assert.ThrowsAsync<InvalidDataException>(() => updates.DownloadAsync(release, directory, cancellation: TestContext.Current.CancellationToken));
                Assert.Empty(Directory.GetFiles(directory, "*.exe", SearchOption.AllDirectories));
            }
            else
            {
                var path = await updates.DownloadAsync(release, directory, cancellation: TestContext.Current.CancellationToken);
                Assert.Equal(Payload, File.ReadAllBytes(path));
            }
            Assert.Empty(Directory.GetFiles(directory, "*.part", SearchOption.AllDirectories));
            Assert.Equal("preserve", File.ReadAllText(unrelated));
        }
        finally
        {
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(directory));
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Cancellation_and_rate_limits_do_not_turn_into_an_available_update()
    {
        using var client = new HttpClient(new Handler(_ => new(HttpStatusCode.TooManyRequests)));
        using var updates = new GitHubUpdates(client);
        await Assert.ThrowsAsync<HttpRequestException>(() => updates.LatestAsync(TestContext.Current.CancellationToken));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => updates.LatestAsync(cancelled.Token));
    }

    private sealed class CancelProgress(CancellationTokenSource source) : IProgress<int>
    {
        public void Report(int value) => source.Cancel();
    }

    [Fact]
    public async Task Cancelling_an_in_progress_download_removes_its_partial_installer()
    {
        using var client = Client();
        using var updates = new GitHubUpdates(client);
        var release = (await updates.LatestAsync(TestContext.Current.CancellationToken))!;
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var directory = Path.Combine(Path.GetTempPath(), "Tower-Update-Test-" + Guid.NewGuid().ToString("N"));
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => updates.DownloadAsync(release, directory, new CancelProgress(cancel), cancel.Token));
            Assert.Empty(Directory.GetFiles(directory, "*.part", SearchOption.AllDirectories));
            Assert.Empty(Directory.GetFiles(directory, "*.exe", SearchOption.AllDirectories));
        }
        finally
        {
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(directory));
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
