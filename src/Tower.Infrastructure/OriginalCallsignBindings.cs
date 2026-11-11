using System.Globalization;
using TowerSpeech.Core;

namespace Tower.Infrastructure;

public static class OriginalCallsignBindings
{
    // CPL indices reference the shared CALLSIGN bank. Resolve only approved, present
    // recordings in memory; never modify reviews or overwrite explicit user aliases.
    public static CallsignLexicon Resolve(string data, CallsignLexicon explicitAliases,
        VoiceCatalog catalog, Func<FragmentId, bool> hasAudio)
    {
        var result = new CallsignLexicon
        {
            Prefixes = new(explicitAliases.Prefixes, StringComparer.OrdinalIgnoreCase),
            FullCallsigns = new(explicitAliases.FullCallsigns, StringComparer.OrdinalIgnoreCase)
        };
        var bindings = OriginalData.InstalledAirports(data)
            .Select(code => OriginalData.Find(data, code + ".CPL"))
            .Where(path => path is not null)
            .SelectMany(path => OriginalData.Sections(path!).Values.SelectMany(rows => rows))
            .Where(row => row.Length >= 3 && int.TryParse(row[2], NumberStyles.None, CultureInfo.InvariantCulture, out _))
            .GroupBy(row => row[0], StringComparer.OrdinalIgnoreCase);
        foreach (var binding in bindings)
        {
            var indices = binding.Select(row => int.Parse(row[2], CultureInfo.InvariantCulture)).Distinct().ToArray();
            if (result.FullCallsigns.ContainsKey(binding.Key) || indices.Length != 1)
            {
                continue;
            }
            var id = new FragmentId(1, indices[0], "");
            var fragment = catalog.Get(id);
            if (fragment is null || !catalog.TryResolve(fragment.Transcript ?? "", "", VoiceMode.Strict, id, out _, out _) || !hasAudio(id))
            {
                continue;
            }
            result.FullCallsigns.Add(binding.Key, new() { Bank = 1, Index = id.Index, Text = fragment.Transcript! });
        }
        return result;
    }
}
