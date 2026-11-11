namespace Tower.Core;

public static class OriginalRadioDirection
{
    private static readonly string[] Names =
        ["north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest"];

    public static string FromBearing(double bearing)
    {
        if (!double.IsFinite(bearing))
        {
            throw new ArgumentOutOfRangeException(nameof(bearing));
        }
        // TOWER.EXE 0x442E7E-0x442EA8: truncate(bearing / 45 + 0.5), wrap 8 to 0.
        var sector = (int)Math.Floor(Coordinates.NormalizeHeading(bearing) / 45 + 0.5) % 8;
        return Names[sector];
    }

    public static int Sector(string direction) => Array.FindIndex(Names,
        name => name.Equals(direction, StringComparison.OrdinalIgnoreCase));
}
