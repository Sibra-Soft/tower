namespace Tower.Core;

public sealed class OriginalAirway
{
    public string SourceFlags { get; set; } = "";
    public List<string[]> Segments { get; set; } = [];

    public bool IsValid() => Segments is { Count: > 0 } && Segments.All(segment => segment is { Length: > 0 } &&
        segment.All(name => !string.IsNullOrWhiteSpace(name)));

    // Endpoints must identify one unambiguous contiguous segment. Direction flags
    // are retained as source text; this reconstruction permits either traversal.
    public string[]? Between(string entry, string exit)
    {
        string[]? result = null;
        foreach (var segment in Segments)
        {
            var starts = Enumerable.Range(0, segment.Length).Where(index => segment[index].Equals(entry, StringComparison.OrdinalIgnoreCase)).ToArray();
            var ends = Enumerable.Range(0, segment.Length).Where(index => segment[index].Equals(exit, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (starts.Length == 0 || ends.Length == 0)
            {
                continue;
            }
            if (result is not null || starts.Length != 1 || ends.Length != 1 || starts[0] == ends[0])
            {
                return null;
            }
            var step = starts[0] < ends[0] ? 1 : -1;
            var names = new List<string>();
            for (var index = starts[0]; ; index += step)
            {
                names.Add(segment[index]);
                if (index == ends[0])
                {
                    break;
                }
            }
            result = names.ToArray();
        }
        return result;
    }
}
