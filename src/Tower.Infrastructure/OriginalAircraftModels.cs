using System.Text;
using Tower.Core;
using System.Collections.Concurrent;

namespace Tower.Infrastructure;

public static class OriginalAircraftModels
{
    public static AircraftMesh Read(string path)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length is < 20 or > 32000000 || Encoding.ASCII.GetString(data, 0, 8) != "TW001000")
        {
            throw new InvalidDataException("Unrecognized DBT aircraft model.");
        }
        using var reader = new BinaryReader(new MemoryStream(data));
        int Count(int maximum)
        {
            var count = reader.ReadInt32();
            return count >= 0 && count <= maximum ? count : throw new InvalidDataException("Invalid DBT record length.");
        }
        void Skip(int count)
        {
            if (count < 0 || count > reader.BaseStream.Length - reader.BaseStream.Position)
            {
                throw new InvalidDataException("Truncated DBT record.");
            }
            reader.BaseStream.Position += count;
        }
        string Text()
        {
            var count = Count(256);
            var bytes = reader.ReadBytes(count);
            return bytes.Length == count ? Encoding.ASCII.GetString(bytes).TrimEnd('\0') : throw new EndOfStreamException();
        }
        reader.BaseStream.Position = 12;
        var name = Text();
        var compiledOffset = BitConverter.ToInt32(data, 8);
        if (compiledOffset < reader.BaseStream.Position || compiledOffset > data.Length - 20)
        {
            throw new InvalidDataException("Invalid DBT compiled-data offset.");
        }
        reader.BaseStream.Position = compiledOffset;
        Skip(12);
        var vertices = new List<ModelPoint>();
        var faces = new List<ModelFace>();
        byte[] blob = [];
        var part = "";
        var flags = 0;
        var ended = false;
        while (reader.BaseStream.Position < data.Length && !ended)
        {
            var tag = reader.ReadInt32();
            switch (tag)
            {
                case 1001:
                    var initialCount = Count(20000);
                    if (vertices.Count != 0)
                    {
                        throw new InvalidDataException("Repeated DBT vertex table.");
                    }
                    for (var index = 0; index < initialCount; index++)
                    {
                        vertices.Add(new(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()));
                    }
                    break;
                case 1002:
                    Skip(vertices.Count * 4);
                    break;
                case >= 1003 and <= 1007:
                    var count = Count(20000 - vertices.Count);
                    var vector = new ModelPoint(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                    for (var index = 0; index < count; index++)
                    {
                        var source = reader.ReadInt32();
                        var distance = tag == 1007 ? 1 : reader.ReadInt32() / 2147483648d;
                        if (source < 0 || source >= vertices.Count)
                        {
                            throw new InvalidDataException("Invalid DBT derived-vertex reference.");
                        }
                        var origin = vertices[source];
                        vertices.Add(new(origin.X + vector.X * distance, origin.Y + vector.Y * distance, origin.Z + vector.Z * distance));
                    }
                    break;
                case 1008:
                    Skip(Count(20000) * 16);
                    break;
                case 1009:
                    Skip(Count(20000) * 12);
                    break;
                case 1010:
                    _ = Text();
                    Skip(16);
                    for (var index = 0; index < 6; index++)
                    {
                        _ = Text();
                    }
                    break;
                case 1014:
                    part = Text();
                    flags = reader.ReadInt32();
                    break;
                case 1015:
                    var length = Count(data.Length);
                    blob = reader.ReadBytes(length);
                    if (blob.Length != length)
                    {
                        throw new EndOfStreamException();
                    }
                    break;
                case 1011:
                    var faceCount = Count(50000 - faces.Count);
                    for (var index = 0; index < faceCount; index++)
                    {
                        var vertexCount = Count(4096);
                        var offset = reader.ReadInt32();
                        var color = reader.ReadInt32();
                        Skip(28);
                        if (vertexCount == 0 || offset < 0 || offset > blob.Length - vertexCount * 4)
                        {
                            throw new InvalidDataException("Invalid DBT polygon indices.");
                        }
                        var indices = new int[vertexCount];
                        for (var vertex = 0; vertex < vertexCount; vertex++)
                        {
                            indices[vertex] = BitConverter.ToInt32(blob, offset + vertex * 4);
                            if (indices[vertex] < 0 || indices[vertex] >= vertices.Count)
                            {
                                throw new InvalidDataException("Invalid DBT polygon vertex.");
                            }
                        }
                        faces.Add(new(indices, color, part, flags));
                    }
                    break;
                case 1012:
                case 1013:
                    break;
                case 1016:
                    ended = true;
                    break;
                default:
                    throw new InvalidDataException($"Unsupported DBT record: {tag}.");
            }
        }
        var mesh = new AircraftMesh(name, vertices.ToArray(), faces.ToArray());
        if (!ended || vertices.Count == 0 || faces.Count == 0 || !double.IsFinite(mesh.Width) || mesh.Width <= 0)
        {
            throw new InvalidDataException("Incomplete DBT aircraft mesh.");
        }
        return mesh;
    }
}

public sealed class AircraftModelCatalog
{
    private static readonly ConcurrentDictionary<(string Path, long Length, long Modified), AircraftFootprint> GroundBounds = new();

    public AircraftFootprint? GetGroundFootprint(string type, double wingSpanMetres)
    {
        if (!files.TryGetValue(type, out var path))
        {
            return null;
        }
        try
        {
            var file = new FileInfo(path);
            var bounds = GroundBounds.GetOrAdd((path, file.Length, file.LastWriteTimeUtc.Ticks),
                key => OriginalAircraftModels.Read(key.Path).GroundFootprint(1));
            if (!bounds.IsValid())
            {
                return null;
            }
            return new(bounds.MinForward * wingSpanMetres, bounds.MaxForward * wingSpanMetres,
                bounds.MinRight * wingSpanMetres, bounds.MaxRight * wingSpanMetres);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Errors[type] = exception.Message;
            return null;
        }
    }
    private readonly Dictionary<string, string> files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AircraftMesh?> cache = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Errors { get; } = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<string> Types => files.Keys;

    public AircraftModelCatalog()
    {
    }

    public AircraftModelCatalog(string dataDirectory)
    {
        var directory = Path.Combine(dataDirectory, "GRAPHICS", "MODELS");
        var path = Path.Combine(directory, "MODELS.INI");
        if (!File.Exists(path))
        {
            return;
        }
        var values = LegacyAirportReader.ReadIni(path);
        if (!int.TryParse(values.GetValueOrDefault("ACTypes.Count"), out var count) || count is < 0 or > 256)
        {
            throw new InvalidDataException("Invalid aircraft model catalogue.");
        }
        for (var index = 1; index <= count; index++)
        {
            var file = values.GetValueOrDefault($"ACTypes.ACType{index:00}");
            if (string.IsNullOrWhiteSpace(file) || file != Path.GetFileName(file) || file.Contains('\\') || file.Contains('/') ||
                !Path.GetExtension(file).Equals(".DBT", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Invalid aircraft model path.");
            }
            files[Path.GetFileNameWithoutExtension(file)] = Path.Combine(directory, file);
        }
    }

    public AircraftMesh? Get(string type)
    {
        if (cache.TryGetValue(type, out var cached))
        {
            return cached;
        }
        AircraftMesh? mesh = null;
        if (files.TryGetValue(type, out var path))
        {
            try
            {
                mesh = OriginalAircraftModels.Read(path);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                Errors[type] = exception.Message;
            }
        }
        cache[type] = mesh;
        return mesh;
    }
}
