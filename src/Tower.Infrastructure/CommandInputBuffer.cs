using System.Text;
using System.Text.RegularExpressions;

namespace Tower.Infrastructure;

/// <summary>Keeps executable input separate from its expanded presentation.</summary>
public sealed class CommandInputBuffer
{
    private static readonly Dictionary<string, string> Meanings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C"] = "cleared", ["CL"] = "cleared to land", ["CG"] = "cleared for touch and go",
        ["CA"] = "cleared for low approach", ["CO"] = "cleared for the option", ["CT"] = "cleared for takeoff",
        ["CC"] = "cancel takeoff clearance", ["T"] = "taxi to", ["TP"] = "taxi to parking",
        ["TH"] = "taxi into position and hold", ["TU"] = "continue taxi", ["H"] = "hold", ["HY"] = "hold position",
        ["HS"] = "hold short", ["F"] = "contact", ["FD"] = "contact departure",
        ["E"] = "enter", ["EL"] = "enter left", ["ER"] = "enter right",
        ["ELD"] = "enter left downwind", ["ERD"] = "enter right downwind",
        ["ELB"] = "enter left base", ["ERB"] = "enter right base", ["EF"] = "enter final",
        ["P"] = "report", ["PL"] = "report left", ["PR"] = "report right",
        ["PLD"] = "report left downwind", ["PRD"] = "report right downwind",
        ["PLB"] = "report left base", ["PRB"] = "report right base", ["PF"] = "report final",
        ["M"] = "make", ["ML"] = "make left", ["MR"] = "make right",
        ["ML1"] = "make left 180", ["ML3"] = "make left 360", ["MR1"] = "make right 180", ["MR3"] = "make right 360",
        ["MS"] = "make short approach", ["U"] = "turn", ["UL"] = "turn left", ["UR"] = "turn right",
        ["ULH"] = "turn left heading", ["URH"] = "turn right heading",
        ["A"] = "approved", ["AT"] = "approved turn out", ["AL"] = "approved left", ["AR"] = "approved right",
        ["ALT"] = "approved left turn out", ["ART"] = "approved right turn out",
        ["S"] = "say", ["SA"] = "say altitude", ["SH"] = "say heading", ["SR"] = "say rate of climb",
        ["SP"] = "say position", ["SS"] = "say speed", ["SG"] = "say again",
        ["B"] = "break out of traffic", ["G"] = "go around", ["X"] = "extend downwind", ["PB"] = "pushback"
    };
    private int[] boundaries = [0];
    public string RawText { get; private set; } = "";
    public string DisplayText { get; private set; } = "";
    public bool Expanded { get; private set; }

    public void SetText(string text)
    {
        RawText = text.Replace("\r", "").Replace("\n", " ");
        Rebuild();
    }

    public void SetExpanded(bool expanded)
    {
        Expanded = expanded;
        Rebuild();
    }

    public int ReplaceSelection(int start, int length, string insertion)
    {
        var first = RawIndex(start, false);
        var last = length == 0 ? first : RawIndex(start + length, true);
        insertion = insertion.Replace("\r", "").Replace("\n", " ");
        SetText(RawText[..first] + insertion + RawText[last..]);
        return boundaries[first + insertion.Length];
    }

    public int Erase(int start, int length, bool backwards)
    {
        if (length > 0)
        {
            return ReplaceSelection(start, length, "");
        }
        var index = RawIndex(start, false);
        var first = backwards ? Math.Max(0, index - 1) : index;
        var last = backwards ? index : Math.Min(RawText.Length, index + 1);
        if (backwards)
        {
            while (first > 0 && boundaries[first - 1] == boundaries[first])
            {
                first--;
            }
        }
        else
        {
            while (last < RawText.Length && boundaries[last] == boundaries[first])
            {
                last++;
            }
        }
        SetText(RawText[..first] + RawText[last..]);
        return boundaries[first];
    }

    private int RawIndex(int position, bool roundUp)
    {
        position = Math.Clamp(position, 0, DisplayText.Length);
        for (var index = 0; index < boundaries.Length; index++)
        {
            if (boundaries[index] >= position)
            {
                return roundUp || boundaries[index] == position ? index : Math.Max(0, index - 1);
            }
        }
        return RawText.Length;
    }

    private void Rebuild()
    {
        boundaries = new int[RawText.Length + 1];
        if (!Expanded)
        {
            DisplayText = RawText;
            for (var index = 0; index < boundaries.Length; index++)
            {
                boundaries[index] = index;
            }
            return;
        }
        var display = new StringBuilder();
        var separator = RawText.IndexOfAny(['/', ',', ' ']);
        if (separator < 0)
        {
            separator = RawText.Length;
        }
        for (var index = 0; index < separator; index++)
        {
            display.Append(RawText[index]);
            boundaries[index + 1] = display.Length;
        }
        var cursor = separator;
        if (cursor < RawText.Length)
        {
            display.Append(cursor == 0 ? "/" : ", ");
            boundaries[++cursor] = display.Length;
        }
        while (cursor < RawText.Length)
        {
            var end = RawText.IndexOf('/', cursor);
            if (end < 0)
            {
                end = RawText.Length;
            }
            var fragment = RawText[cursor..end];
            var expanded = Expand(fragment);
            var offset = display.Length;
            display.Append(expanded);
            for (var count = 1; count <= fragment.Length; count++)
            {
                var prefix = expanded == fragment ? fragment[..count] : Expand(fragment[..count]);
                var boundary = expanded.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? offset + prefix.Length : boundaries[cursor + count - 1];
                boundaries[cursor + count] = Math.Max(boundaries[cursor + count - 1], boundary);
            }
            boundaries[end] = display.Length;
            cursor = end;
            if (cursor < RawText.Length)
            {
                display.Append(" / ");
                boundaries[++cursor] = display.Length;
            }
        }
        DisplayText = display.ToString();
    }

    private static string Expand(string fragment)
    {
        if (Meanings.TryGetValue(fragment, out var meaning))
        {
            return meaning;
        }
        var match = Regex.Match(fragment, @"^(T|HS|EF|ELD|ERD|ELB|ERB|PLD|PRD|PLB|PRB|ULH|URH|PF)(\d.*)$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var code = match.Groups[1].Value.ToUpperInvariant();
            var argument = match.Groups[2].Value;
            if (code == "PF")
            {
                var parts = argument.Split(',', 2);
                return "report final at " + parts[0] + " miles" + (parts.Length == 2 ? " runway " + parts[1] : "");
            }
            return Meanings[code] + (code is "ULH" or "URH" ? " " : " runway ") + argument;
        }
        match = Regex.Match(fragment, @"^PB\s+([NESW]?)$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            if (match.Groups[1].Value.Length == 0)
            {
                return "pushback face ";
            }
            var direction = match.Groups[1].Value.ToUpperInvariant() switch
            {
                "N" => "north", "E" => "east", "S" => "south", _ => "west"
            };
            return "pushback face " + direction;
        }
        return fragment;
    }
}
