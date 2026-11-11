using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tower.Infrastructure;

namespace Tower.Tests;

// Synthetic add-on derived solely from the shipped Canyon assets, never an installed airport.
internal static class AirportPackageFixture
{
    public static void Build(string source, string output, string code, bool mixedCaseFile = false)
    {
        var payload = new Dictionary<string, byte[]>();
        foreach (var extension in new[] { "TWR", "PAT", "PRF", "SEC", "SPF", "TYP", "INF" })
        {
            var text = File.ReadAllText(Path.Combine(source, "CYN", "CYN." + extension));
            var name = mixedCaseFile && extension == "INF" ? $"{code}.inf" : $"{code}.{extension}";
            payload[$"{code}/{name}"] = Encoding.UTF8.GetBytes(text.Replace("CYN", code, StringComparison.Ordinal));
        }
        foreach (var bank in new[] { "F", "R", "T" })
        {
            payload[$"{code}/{code}{bank}.VOC"] = File.ReadAllBytes(Path.Combine(source, "CYN", "CYN" + bank + ".VOC"));
        }
        payload[$"AIRPORTS/{code}.CFG"] = Encoding.UTF8.GetBytes($"[Config0]\nIMAGE={code}640.BIT\n");
        var panorama = new byte[1052];
        BitConverter.GetBytes(1048).CopyTo(panorama, 0);
        BitConverter.GetBytes((ushort)2).CopyTo(panorama, 4);
        BitConverter.GetBytes((ushort)2).CopyTo(panorama, 6);
        BitConverter.GetBytes(0x54575457u).CopyTo(panorama, 12);
        BitConverter.GetBytes(256).CopyTo(panorama, 16);
        BitConverter.GetBytes((ushort)238).CopyTo(panorama, 1044);
        payload[$"GRAPHICS/IMAGES/{code}640.BIT"] = panorama;
        var files = payload.Select(item => new AirportPackageFile(item.Key, item.Value.Length,
            Convert.ToHexString(SHA256.HashData(item.Value)))).ToList();
        var manifest = new AirportPackageManifest(1, code, "Synthetic Canyon-derived test airport", "1.0.0", files);
        using var archive = ZipFile.Open(output, ZipArchiveMode.Create);
        using (var writer = new StreamWriter(archive.CreateEntry("manifest.json").Open()))
        {
            writer.Write(JsonSerializer.Serialize(manifest));
        }
        foreach (var item in payload)
        {
            using var stream = archive.CreateEntry("Data/" + item.Key).Open();
            stream.Write(item.Value);
        }
    }
}
