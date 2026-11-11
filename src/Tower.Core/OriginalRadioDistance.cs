namespace Tower.Core;

public static class OriginalRadioDistance
{
    // TOWER.EXE 0x442E19/0x442E25 divides fixed-point metres by 256 and 1609,
    // then truncates. Radio "miles" are statute miles, unlike radar range NM.
    public const double MetresPerMile = 1609;

    public static int SpokenMiles(double nauticalMiles)
    {
        if (!double.IsFinite(nauticalMiles) || nauticalMiles < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nauticalMiles));
        }
        return (int)Math.Clamp(Math.Floor(nauticalMiles * 1852 / MetresPerMile + 1e-9), 0, 99);
    }

    public static double NauticalMiles(double miles) => miles * MetresPerMile / 1852;
}
