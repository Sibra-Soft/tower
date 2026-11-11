namespace Tower.Core;

public enum TrafficRateField
{
    Aircraft, Minutes
}

public readonly record struct TrafficRate(int Aircraft, int Minutes)
{
    public double PerMinute => (double)Aircraft / Minutes;

    // Reconstructed from TOWER.EXE 0x402C4E. The caption uses the capped value
    // before correcting a field, including the original temporary mismatch.
    public (TrafficRate Rate, int Display) Adjust(TrafficRateField changed)
    {
        var aircraft = Math.Clamp(Aircraft, 1, 99);
        var minutes = Math.Clamp(Minutes, 1, 99);
        var value = 100.0 * aircraft / minutes;

        if (value > 150)
        {
            value = 150;

            if (changed == TrafficRateField.Aircraft)
            {
                aircraft = (int)(1.5 * minutes);
            }
            else
            {
                minutes = (int)(aircraft / 1.5);
            }
        }

        return (new(aircraft, minutes), (int)(value + 0.5));
    }

    public static TrafficRate FromPerMinute(double rate)
    {
        if (!double.IsFinite(rate) || rate <= 0 || rate > 99)
        {
            return new(1, 2);
        }

        var best = new TrafficRate(1, 2);
        var error = double.MaxValue;

        for (var minutes = 1; minutes <= 99; minutes++)
        {
            var aircraft = (int)Math.Clamp(Math.Round(rate * minutes), 1, 99);
            var candidate = new TrafficRate(aircraft, minutes);
            var difference = Math.Abs(candidate.PerMinute - rate);

            if (difference < error)
            {
                best = candidate;
                error = difference;
            }
            if (error < 1e-10)
            {
                break;
            }
        }

        return best;
    }
}
