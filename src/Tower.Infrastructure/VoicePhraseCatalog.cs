using System.Text.RegularExpressions;
namespace Tower.Infrastructure;

public sealed record VoicePhrase(string Text,string Expression,string AirportCode="");

/// <summary>Human-verified text to VOC references. Site references never leak into another airport.</summary>
public sealed class VoicePhraseCatalog
{
    private readonly List<VoicePhrase> entries=[];
    public IReadOnlyList<VoicePhrase> Entries=>entries.AsReadOnly();
    public VoicePhraseCatalog(bool includeDefaults=true)
    {
        if(!includeDefaults) return;
        Set("Tower",Speak.Tower);
        Set("With you",Speak.WithYou);
    }
    public void Set(string text,string expression,string airportCode="")
    {
        if(text is null || expression is null || airportCode is null) throw new InvalidDataException("Voice mapping fields cannot be null.");
        var key=Normalize(text);
        if(key.Length==0 || text.Length>2048) throw new InvalidDataException("Enter a phrase of at most 2048 characters.");
        var references=VoiceBank.ParseExpression(expression);
        airportCode=airportCode.Trim().ToUpperInvariant();
        if(references.Any(r=>r.Bank>=3) && airportCode.Length==0)
            throw new InvalidDataException("Airport banks 3/4/5 require an airport scope.");
        if(references.Any(r=>r.Bank>5)) throw new InvalidDataException("Only banks 1 through 5 are supported.");
        entries.RemoveAll(e=>Normalize(e.Text)==key && e.AirportCode==airportCode);
        entries.Add(new(text.Trim(),expression.Trim(),airportCode));
    }
    public void Remove(VoicePhrase entry)=>entries.Remove(entry);
    public bool TryCompose(string text,string airportCode,out string expression)
    {
        expression="";
        if(string.IsNullOrWhiteSpace(text) || text.Length>2048) return false;
        var words=Normalize(text).Split(' ',StringSplitOptions.RemoveEmptyEntries);
        if(words.Length==0 || words.Length>256) return false;
        var candidates=entries.Where(e=>e.AirportCode.Length==0 || e.AirportCode.Equals(airportCode,StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e=>e.AirportCode.Length>0)
            .GroupBy(e=>Normalize(e.Text)).Select(g=>g.First())
            .Select(e=>(Words:Normalize(e.Text).Split(' '),e.Expression))
            .OrderByDescending(e=>e.Words.Length).ToArray();
        // Work backwards: a longer match must not hide a shorter, complete composition.
        var suffixes=new string?[words.Length+1]; suffixes[^1]="";
        for(var i=words.Length-1;i>=0;i--)
            foreach(var candidate in candidates)
            {
                var end=i+candidate.Words.Length;
                if(end>words.Length || suffixes[end] is null || !words.AsSpan(i,candidate.Words.Length).SequenceEqual(candidate.Words)) continue;
                var combined=candidate.Expression+suffixes[end];
                if(combined.Length>8192) continue;
                suffixes[i]=combined; break;
            }
        if(suffixes[0] is not {} complete) return false;
        expression=complete; return true;
    }
    private static string Normalize(string text)=>string.Join(' ',Regex.Matches(text.ToLowerInvariant(),@"[\p{L}\p{N}]+(?:\.[0-9]+)?").Select(m=>m.Value));
}

public sealed class VoiceSettings
{
    public bool ReadbacksEnabled { get; set; }=true;
    public bool WindowsFallback { get; set; }=true;
    public List<VoicePhrase> Phrases { get; set; }=[new("Tower",Speak.Tower),new("With you",Speak.WithYou)];
}
