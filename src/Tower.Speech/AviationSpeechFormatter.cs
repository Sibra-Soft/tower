using System.Text.RegularExpressions;

namespace TowerSpeech.Core;

/// <summary>Formats typed radio-message parts rather than reading command codes or raw IDs aloud.</summary>
public sealed class AviationSpeechFormatter(CallsignLexicon? lexicon = null)
{
    private static readonly string[] Digits =
        ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "niner"];
    private static readonly string[] Tens =
        ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];
    private static readonly string[] Teens =
        ["ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"];
    private static readonly Dictionary<char, string> Nato = new()
    {
        ['A'] = "alpha",
        ['B'] = "bravo",
        ['C'] = "charlie",
        ['D'] = "delta",
        ['E'] = "echo",
        ['F'] = "foxtrot",
        ['G'] = "golf",
        ['H'] = "hotel",
        ['I'] = "india",
        ['J'] = "juliet",
        ['K'] = "kilo",
        ['L'] = "lima",
        ['M'] = "mike",
        ['N'] = "november",
        ['O'] = "oscar",
        ['P'] = "papa",
        ['Q'] = "quebec",
        ['R'] = "romeo",
        ['S'] = "sierra",
        ['T'] = "tango",
        ['U'] = "uniform",
        ['V'] = "victor",
        ['W'] = "whiskey",
        ['X'] = "x-ray",
        ['Y'] = "yankee",
        ['Z'] = "zulu"
    };
    private static readonly Dictionary<string, string> AirlineNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AAL"] = "american",
        ["DAL"] = "delta",
        ["UAL"] = "united",
        ["NWA"] = "northwest",
        ["COA"] = "continental",
        ["FDX"] = "fedex",
        ["UPS"] = "u p s",
        ["USA"] = "u s air",
        ["TWA"] = "t w a",
        ["AWI"] = "air wisconsin"
    };

    public string Format(RadioMessage message) => FormatParts(message.Parts);
    public string FormatParts(IEnumerable<SpeechPart> parts)
    {
        var speechParts = parts.ToArray();
        List<string> words = [];
        foreach (var part in speechParts)
        {
            var value = part switch
            {
                RecordedPhrase p => p.Text.Trim(),
                AircraftCallsign c => FormatCallsign(c.Value),
                RunwayNumber r => FormatRunway(r.Value),
                SpokenDigits d => FormatNumber(d.Digits, d.Context),
                _ => ""
            };
            if (value.Length > 0)
            {
                words.Add(value);
            }
        }
        if (words.Count == 0)
        {
            return "";
        }
        // Controller addressee and the initial Ground/Tower call are separated by a brief pause.
        if (words[0].Equals("tower", StringComparison.OrdinalIgnoreCase) ||
            words[0].Equals("ground", StringComparison.OrdinalIgnoreCase))
        {
            words[0] += ",";
        }
        // Add a natural pause after the pilot's callsign in a Roger readback.
        if (speechParts.Length > 1 && speechParts[0] is AircraftCallsign &&
            speechParts[1] is RecordedPhrase { Text: "roger" })
        {
            words[0] += ",";
        }

        var text = Regex.Replace(string.Join(" ", words), @"\s+", " ").Trim();
        text = char.ToUpperInvariant(text[0]) + text[1..];
        return text.TrimEnd('.', '!', '?') + ".";
    }

    public string FormatCallsign(string id)
    {
        id = id.Trim().ToUpperInvariant();
        if (lexicon is not null && lexicon.TryGetFull(id, out var full) && full is not null)
        {
            return full.Text;
        }

        int letters = id.TakeWhile(char.IsAsciiLetter).Count();
        string prefix = id[..letters];
        string suffix = id[letters..];
        string head = "";
        if (lexicon is not null && lexicon.TryGet(prefix, out var alias) && alias is not null)
        {
            head = alias.Text;
        }
        else if (AirlineNames.TryGetValue(prefix, out var airline))
        {
            head = airline;
        }
        else
        {
            head = string.Join(" ", prefix.Where(c => Nato.ContainsKey(c)).Select(c => Nato[c]));
        }

        var tokens = Regex.Matches(suffix, "[0-9]+|[A-Z]+")
            .Select(m => m.Value.All(char.IsAsciiDigit) ? FlightNumber(m.Value) :
                string.Join(" ", m.Value.Select(c => Nato.GetValueOrDefault(c, c.ToString()))));
        return string.Join(" ", new[] { head }.Concat(tokens).Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    private static string FlightNumber(string digits)
    {
        if (digits.Length <= 2)
        {
            return digits.Length == 2 && digits[0] != '0'
            ? Cardinal(int.Parse(digits)) : SpeakDigits(digits);
        }

        if (digits.Length == 3 && digits[1] == '0')
        {
            return Digits[digits[0] - '0'] + " " + SpeakDigits(digits[1..]);
        }

        if (digits.Length == 4 && digits[0] != '0' && digits[2] != '0')
        {
            return Cardinal(int.Parse(digits[..2])) + " " + Cardinal(int.Parse(digits[2..]));
        }

        return SpeakDigits(digits);
    }
    public static string FormatRunway(string runway)
    {
        runway = runway.Trim().ToUpperInvariant().Replace("RWY", "").Trim();
        var match = Regex.Match(runway, "^([0-9]{1,2})([LRC]?)$");
        if (!match.Success)
        {
            throw new FormatException("Invalid runway: " + runway);
        }

        string direction = match.Groups[2].Value switch
        {
            "L" => " left",
            "R" => " right",
            "C" => " center",
            _ => ""
        };
        return "runway " + SpeakDigits(match.Groups[1].Value) + direction;
    }
    public static string FormatNumber(string value, NumberContext kind)
    {
        if (value.Length == 0 || !value.All(char.IsAsciiDigit))
        {
            throw new FormatException("Invalid number for " + kind + ": " + value);
        }

        return kind switch
        {
            NumberContext.Distance or NumberContext.Altitude or NumberContext.Cardinal => Cardinal(int.Parse(value)),
            NumberContext.Runway => SpeakDigits(value),
            NumberContext.Heading => SpeakDigits(value.PadLeft(3, '0')),
            NumberContext.FlightNumber => FlightNumber(value),
            _ => SpeakDigits(value)
        };
    }
    public static string SpeakDigits(string digits) =>
        string.Join(" ", digits.Select(c => Digits[c - '0']));
    public static string Cardinal(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        if (value < 10)
        {
            return Digits[value];
        }

        if (value < 20)
        {
            return Teens[value - 10];
        }

        if (value < 100)
        {
            return Tens[value / 10] + (value % 10 == 0 ? "" : " " + Cardinal(value % 10));
        }

        if (value < 1000)
        {
            return Cardinal(value / 100) + " hundred" + (value % 100 == 0 ? "" : " " + Cardinal(value % 100));
        }

        if (value < 1_000_000)
        {
            return Cardinal(value / 1000) + " thousand" + (value % 1000 == 0 ? "" : " " + Cardinal(value % 1000));
        }

        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
