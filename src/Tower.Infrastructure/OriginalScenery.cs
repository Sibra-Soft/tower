using System.Globalization;
using Tower.Core;

namespace Tower.Infrastructure;

public sealed record OriginalPanorama(int Width, int Height, int HorizonRow, byte[] Pixels, int[] Palette)
{
    public static OriginalPanorama Read(string path)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length < 1048 || BitConverter.ToUInt32(data, 12) != 0x54575457 || BitConverter.ToInt32(data, 16) != 256)
        {
            throw new InvalidDataException("Unrecognized original BIT panorama.");
        }
        var offset = BitConverter.ToInt32(data, 0);
        var width = BitConverter.ToUInt16(data, 4);
        var height = BitConverter.ToUInt16(data, 6);
        var horizon = BitConverter.ToUInt16(data, 1044) + height - 240;
        if (width is < 1 or > 16384 || height is < 1 or > 4096 || offset < 1048 ||
            (long)offset + width * (long)height > data.Length || horizon < 0 || horizon >= height)
        {
            throw new InvalidDataException("Invalid original panorama dimensions or offsets.");
        }
        var palette = new int[256];
        for (var index = 1; index < palette.Length; index++)
        {
            var start = 20 + index * 4;
            palette[index] = unchecked((int)0xFF000000) | data[start] << 16 | data[start + 1] << 8 | data[start + 2];
        }
        // Index zero is the sky colour supplied dynamically by the original driver.
        return new(width, height, horizon, data.AsSpan(offset, width * height).ToArray(), palette);
    }

    public int Column(double heading) => Math.Min(Width - 1, (int)(Coordinates.NormalizeHeading(heading) / 360 * Width));
}

public sealed record SceneryObject(string Name, string Kind, AircraftMesh Mesh, Position Position, double AltitudeMetres)
{
    public (Position Position, double Altitude)[] WorldPoints() => Mesh.Vertices.Select(point =>
        (new Position(Position.X + point.X / 1000, Position.Y - point.Z / 1000), AltitudeMetres + point.Y / 1000)).ToArray();
}

public sealed class AirportScenery
{
    public List<SceneryObject> Objects { get; } = [];
    public List<string> Errors { get; } = [];
    public OriginalPanorama? Panorama { get; private set; }
    public double EyeHeightMetres { get; private set; } = 40;
    public bool HasGround => Objects.Any(item => item.Kind.Equals("GND", StringComparison.OrdinalIgnoreCase));

    public static Position Placement(double towerLatitudeDegrees, double towerLongitudeDegrees, double colatitude, double longitude, Position observer)
    {
        const double radiusMetres = 6350000;
        var towerColatitude = Math.PI / 2 - towerLatitudeDegrees * Math.PI / 180;
        var towerLongitude = towerLongitudeDegrees * Math.PI / 180;
        return new(observer.X + (longitude - towerLongitude) * radiusMetres * Math.Sin(towerColatitude),
            observer.Y + (towerColatitude - colatitude) * radiusMetres);
    }

    public static AirportScenery Read(string dataDirectory, Airport airport)
    {
        var result = new AirportScenery();
        var path = Path.Combine(dataDirectory, "AIRPORTS", airport.Code + ".CFG");
        if (!File.Exists(path))
        {
            return result;
        }
        try
        {
            var config = LegacyAirportReader.ReadIni(path);
            double Number(string value) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                && double.IsFinite(number) ? number : throw new InvalidDataException("Invalid scenery coordinate.");
            var altitude = Number(config.GetValueOrDefault("Tower.Altitude", "40000")) / 1000;
            if (altitude is <= 0 or > 1000)
            {
                throw new InvalidDataException("Invalid scenery camera height.");
            }
            result.EyeHeightMetres = altitude;
            if (config.TryGetValue("Config0.LocalSceneryCount", out var localCountText))
            {
                if (!int.TryParse(localCountText, out var localCount) || localCount is < 1 or > 256)
                {
                    throw new InvalidDataException("Invalid local scenery object count.");
                }
                for (var index = 0; index < localCount; index++)
                {
                    var words = config.GetValueOrDefault($"LocalScenery.ScenObj{index}", "")
                        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length != 2 || words[0] != Path.GetFileName(words[0]) ||
                        words[0].Contains('\\') || words[0].Contains('/') ||
                        !Path.GetExtension(words[0]).Equals(".DBT", StringComparison.OrdinalIgnoreCase) ||
                        words[1] is not ("GND" or "OBJ" or "TOWER" or "BEACON"))
                    {
                        throw new InvalidDataException("Invalid airport-local scenery definition.");
                    }
                    // Authored vertices use millimetres relative to the TWR origin; no global catalogue edits.
                    var mesh = OriginalAircraftModels.Read(Path.Combine(dataDirectory, airport.Code, "SCENERY", words[0]));
                    result.Objects.Add(new(words[0], words[1], mesh, airport.Observer, 0));
                }
            }
            string SafeFile(string name, string directory)
            {
                if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name) || name.Contains('\\') || name.Contains('/'))
                {
                    throw new InvalidDataException("Invalid scenery asset path.");
                }
                return Path.Combine(dataDirectory, "GRAPHICS", directory, name);
            }
            if (config.TryGetValue("Config0.IMAGE", out var image))
            {
                result.Panorama = OriginalPanorama.Read(SafeFile(image, "IMAGES"));
            }
            if (!config.TryGetValue("Config0.ScenObjCount", out var countText))
            {
                return result;
            }
            if (!int.TryParse(countText, out var count) || count is < 0 or > 256)
            {
                throw new InvalidDataException("Invalid scenery object count.");
            }
            var catalogue = LegacyAirportReader.ReadIni(Path.Combine(dataDirectory, "GRAPHICS", "MODELS", "MODELS.INI"));
            var allowed = catalogue.Where(pair => pair.Key.StartsWith("SceneryTypes.SceneryType", StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var meshes = new Dictionary<string, AircraftMesh>(StringComparer.OrdinalIgnoreCase);
            var latitude = Number(config.GetValueOrDefault("Tower.Latitude", "0"));
            var longitude = Number(config.GetValueOrDefault("Tower.Longitude", "0"));
            for (var index = 0; index < count; index++)
            {
                try
                {
                    var words = config.GetValueOrDefault($"Scenery.ScenObj{index}", "")
                        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length != 8 || !allowed.Contains(words[0]) || words[1].Equals("XYZ", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("Unsupported scenery object definition.");
                    }
                    var numbers = words.Skip(2).Select(Number).ToArray();
                    if (numbers.Skip(3).Any(value => value != 0))
                    {
                        throw new InvalidDataException("Rotated scenery placements are not supported yet.");
                    }
                    if (!meshes.TryGetValue(words[0], out var mesh))
                    {
                        mesh = OriginalAircraftModels.Read(SafeFile(words[0], "MODELS"));
                        meshes.Add(words[0], mesh);
                    }
                    result.Objects.Add(new(words[0], words[1], mesh,
                        Placement(latitude, longitude, numbers[1], numbers[2], airport.Observer), numbers[0] / 1000));
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
                {
                    result.Errors.Add($"Scenery object {index}: {exception.Message}");
                }
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            result.Errors.Add(exception.Message);
        }
        return result;
    }
}
