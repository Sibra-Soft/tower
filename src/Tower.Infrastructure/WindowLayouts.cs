using System.Globalization;

namespace Tower.Infrastructure;

public sealed record LayoutPlacement(int X, int Y, int? Width, int? Height, bool? Visible = null, string State = "Normal");

public sealed class WindowLayout
{
    public bool ScreenCoordinates { get; set; }
    public Dictionary<string, string> Windows { get; } = new(StringComparer.OrdinalIgnoreCase);

    public LayoutPlacement? Placement(string key)
    {
        if (!Windows.TryGetValue(key, out var value))
        {
            return null;
        }
        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        bool? visible = null;
        var state = "Normal";
        var offset = 0;
        if (parts[0].Equals("On", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("Off", StringComparison.OrdinalIgnoreCase))
        {
            visible = parts[0].Equals("On", StringComparison.OrdinalIgnoreCase);
            offset = 1;
        }
        else if (key.Equals("Operational", StringComparison.OrdinalIgnoreCase))
        {
            state = parts[0];
            if (state is not ("Normal" or "Maximized" or "Minimized"))
            {
                throw new InvalidDataException("Invalid window state.");
            }
            offset = 1;
        }
        var numbers = parts.Skip(offset).Select(part => int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            && number is >= -100000 and <= 100000 ? number : throw new InvalidDataException("Invalid window coordinates.")).ToArray();
        if (numbers.Length is not (2 or 4) || numbers.Length == 4 && (numbers[2] <= 0 || numbers[3] <= 0))
        {
            throw new InvalidDataException("Invalid window dimensions.");
        }
        return new(numbers[0], numbers[1], numbers.Length == 4 ? numbers[2] : null, numbers.Length == 4 ? numbers[3] : null, visible, state);
    }

    public bool? Visibility(string key) => Windows.TryGetValue(key, out var value)
        ? value.Split(',')[0].Trim().ToUpperInvariant() switch { "ON" => true, "OFF" => false, _ => null } : null;

    public void Set(string key, LayoutPlacement placement)
    {
        var values = new List<string>();
        if (key == "Operational")
        {
            values.Add(placement.State);
        }
        else if (placement.Visible is { } visible)
        {
            values.Add(visible ? "On" : "Off");
        }
        values.Add(placement.X.ToString(CultureInfo.InvariantCulture));
        values.Add(placement.Y.ToString(CultureInfo.InvariantCulture));
        if (placement.Width is { } width && placement.Height is { } height)
        {
            values.Add(width.ToString(CultureInfo.InvariantCulture));
            values.Add(height.ToString(CultureInfo.InvariantCulture));
        }
        Windows[key] = string.Join(',', values);
        _ = Placement(key);
    }
}

public sealed class WindowLayoutStore(string directory)
{
    public const string DefaultFileName = "default.set";
    public string DirectoryPath { get; } = Path.GetFullPath(directory);

    public WindowLayout ReadOrCreateDefault(WindowLayout initial)
    {
        if (!File.Exists(FilePath(DefaultFileName)))
        {
            Write(DefaultFileName, initial);
        }
        return Read(DefaultFileName);
    }

    public string FilePath(string name)
    {
        name = name.Trim();
        if (name.Length == 0 || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name.Contains('/') || name.Contains('\\') || name.EndsWith('.') || name.EndsWith(' '))
        {
            throw new InvalidDataException("Enter a file name without a directory path.");
        }
        if (!name.EndsWith(".set", StringComparison.OrdinalIgnoreCase))
        {
            name += ".set";
        }
        return Path.Combine(DirectoryPath, name);
    }

    public string[] List() => Directory.Exists(DirectoryPath)
        ? Directory.EnumerateFiles(DirectoryPath).Where(path => Path.GetExtension(path).Equals(".set", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetFileName(path)!).Order(StringComparer.OrdinalIgnoreCase).ToArray() : [];

    public WindowLayout Read(string name)
    {
        var values = LegacyAirportReader.ReadIni(FilePath(name));
        var layout = new WindowLayout { ScreenCoordinates = values.GetValueOrDefault("TowerLayout.Coordinates") == "Screen" };
        foreach (var (key, value) in values.Where(pair => pair.Key.StartsWith("Windows.", StringComparison.OrdinalIgnoreCase)))
        {
            layout.Windows[key[8..]] = value;
        }
        if (layout.Windows.Count == 0)
        {
            throw new InvalidDataException("The file does not contain a Windows layout.");
        }
        foreach (var key in new[] { "Operational", "3D", "ASDE", "BRITE", "ArrivalStrips", "DepartureStrips", "Buttons", "Comm" })
        {
            _ = layout.Placement(key);
        }
        return layout;
    }

    public void Write(string name, WindowLayout layout)
    {
        var path = FilePath(name);
        foreach (var key in new[] { "Operational", "3D", "ASDE", "BRITE", "ArrivalStrips", "DepartureStrips", "Buttons", "Comm" })
        {
            _ = layout.Placement(key);
        }
        Directory.CreateDirectory(DirectoryPath);
        var windows = File.Exists(path) ? Read(name).Windows : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in layout.Windows)
        {
            windows[key] = value;
        }
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var lines = new List<string> { "[TowerLayout]", "Version=1", "Coordinates=" + (layout.ScreenCoordinates ? "Screen" : "Legacy"), "", "[Windows]" };
            lines.AddRange(windows.Select(pair => pair.Key + "=" + pair.Value));
            File.WriteAllLines(temporary, lines);
            if (File.Exists(path))
            {
                File.Replace(temporary, path, path + ".bak");
            }
            else
            {
                File.Move(temporary, path);
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
