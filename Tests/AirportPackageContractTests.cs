using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class AirportPackageContractTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("../escape.VOC")]
    [InlineData("VOICE.VOC")]
    [InlineData("CYN/CYN.TWR")]
    [InlineData("TST/plugin.dll")]
    [InlineData("TST/CON.VOC")]
    [InlineData("OTHER/SCENERY/GROUND.DBT")]
    [InlineData("GRAPHICS/MODELS/EIN.DBT")]
    public void Package_manifest_enforces_airport_ownership_before_touching_data(string? invalidPath)
    {
        var root = Path.Combine(Path.GetTempPath(), "Tower-PackageContract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var data = Path.Combine(root, "Data");
            Directory.CreateDirectory(data);
            File.WriteAllText(Path.Combine(data, "VOICE.VOC"), "shared bank must survive");
            var paths = new[] { "TWR", "PAT", "PRF", "SEC", "SPF", "TYP" }.Select(ext => "TST/TST." + ext)
                .Concat(new[] { "TST/TSTF.VOC", "TST/TSTR.VOC", "TST/TSTT.VOC", "AIRPORTS/TST.CFG" }).ToList();
            if (invalidPath is not null)
            {
                paths.Add(invalidPath);
            }
            var bytes = new byte[] { 1, 2, 3 };
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            var manifest = new AirportPackageManifest(1, "TST", "Contract airport", "1.0.0",
                paths.Select(path => new AirportPackageFile(path, bytes.Length, hash)).ToList());
            var package = Path.Combine(root, "test.towerairport");
            using (var archive = ZipFile.Open(package, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(archive.CreateEntry("manifest.json").Open()))
                {
                    writer.Write(JsonSerializer.Serialize(manifest));
                }
                foreach (var path in paths)
                {
                    using var stream = archive.CreateEntry("Data/" + path).Open();
                    stream.Write(bytes);
                }
            }
            if (invalidPath is null)
            {
                Assert.Equal("TST", AirportPackages.Inspect(package).AirportCode);
            }
            else
            {
                Assert.Throws<InvalidDataException>(() => AirportPackages.Install(package, data, cancellationToken: TestContext.Current.CancellationToken));
            }
            Assert.Equal("shared bank must survive", File.ReadAllText(Path.Combine(data, "VOICE.VOC")));
            Assert.Single(Directory.GetFiles(data));
            Assert.Empty(Directory.GetDirectories(data));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
