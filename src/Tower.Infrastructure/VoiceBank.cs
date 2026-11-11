using System.Text.RegularExpressions;
namespace Tower.Infrastructure;

public sealed record VoiceFragment(int Bank,int Index,int Offset,int ChunkSize);
public static class Speak
{
    public const string Tower = "(2:98)";
    public const string WithYou = "(2:53) + (2:151) + (2:56) + (2:44)";
}
public sealed class VoiceBank
{
    public const int SampleRate=8900;
    private readonly Dictionary<int,byte[]> banks=[];
    private readonly Dictionary<(int,int),VoiceFragment> fragments=[];
    public IReadOnlyCollection<VoiceFragment> Fragments => fragments.Values;
    public void Clear() { banks.Clear(); fragments.Clear(); }
    public void Load(int bank,byte[] data)
    {
        if(bank<1 || data.Length==0) throw new InvalidDataException("Voice bank must have an ID and data.");
        banks[bank]=data.ToArray();
        foreach(var key in fragments.Keys.Where(k=>k.Item1==bank).ToList()) fragments.Remove(key);
        // Faithful port of modVoice.LoadVoiceFile: LF=1, marker 145 -> 0,
        // skip first marker; retain original bank/fragment numbering and offsets.
        var last=1; var count=0; var previous=(int)data[0];
        for(var i=1;i<data.Length;i++)
        {
            var current=(int)data[i]; var delta=Math.Abs(current-previous);
            var hit=delta>=80 || (previous>=120 && current<=20) || Math.Abs(current-128)>=110;
            if(hit && current==0 && delta==145)
            {
                count++;
                if(count>1) { fragments.Add((bank,count),new(bank,count,last,i-last)); last=i; }
            }
            previous=current;
        }
    }
    // Airport VOCs are sequences of type-1 PCM records followed by an offset table.
    // Site-specific SPF numbers are 1-based; the old base-bank Load method is unchanged.
    public void LoadRecords(int bank,byte[] data)
    {
        var parsed=new List<VoiceFragment>(); var offset=0; var starts=new List<int>();
        while(offset+6<=data.Length && data[offset]==1)
        {
            var size=data[offset+1]|data[offset+2]<<8|data[offset+3]<<16;
            if(size<2 || offset+4L+size>data.Length || data[offset+4]!=145 || data[offset+5]!=0) throw new InvalidDataException("Unsupported or truncated PCM record.");
            starts.Add(offset);
            // BuildWave subtracts five for the compatibility fragment representation.
            parsed.Add(new(bank,parsed.Count+1,offset+6,size-2+5)); offset+=size+4;
        }
        if(bank<1 || parsed.Count==0 || data.Length-offset!=(parsed.Count+2)*4) throw new InvalidDataException("Invalid VOC offset table.");
        starts.Add(offset);
        for(var i=0;i<starts.Count;i++) if(BitConverter.ToInt32(data,offset+i*4)!=starts[i]) throw new InvalidDataException("VOC offset table does not match records.");
        if(BitConverter.ToInt32(data,data.Length-4)!=65536+starts.Count) throw new InvalidDataException("Invalid VOC table footer.");
        banks[bank]=data.ToArray(); foreach(var key in fragments.Keys.Where(k=>k.Item1==bank).ToList()) fragments.Remove(key);
        foreach(var f in parsed) fragments.Add((bank,f.Index),f);
    }
    // Accept both VB6-style separators and direct C# constant concatenation.
    // Consume the entire input; a malformed suffix must never play a partial command.
    public static IReadOnlyList<(int Bank,int Index)> ParseExpression(string phrase)
    {
        if(string.IsNullOrWhiteSpace(phrase) || phrase.Length>8192) throw new InvalidDataException("Empty or oversized voice phrase.");
        var result=new List<(int,int)>(); var position=0;
        while(position<phrase.Length)
        {
            var match=Regex.Match(phrase[position..],@"^\s*\(([0-9]+):([0-9]+)\)\s*");
            if(!match.Success || !int.TryParse(match.Groups[1].Value,out var bank) || bank<1 ||
                !int.TryParse(match.Groups[2].Value,out var index) || index<1)
                throw new InvalidDataException($"Invalid voice expression near position {position}.");
            result.Add((bank,index)); position+=match.Length;
            if(position<phrase.Length && phrase[position]=='+')
            {
                position++;
                if(string.IsNullOrWhiteSpace(phrase[position..])) throw new InvalidDataException("Voice expression ends in '+'.");
            }
        }
        return result;
    }
    public byte[] BuildWave(string phrase)
    {
        using var pcm=new MemoryStream();
        foreach(var (bank,index) in ParseExpression(phrase))
        {
            if(!fragments.TryGetValue((bank,index),out var fragment)) throw new InvalidDataException($"Voice fragment ({bank}:{index}) is unavailable.");
            var bytes=banks[bank]; var size=Math.Min(fragment.ChunkSize-5,bytes.Length-fragment.Offset);
            if(size>0) pcm.Write(bytes,fragment.Offset,size);
        }
        // Preserve unsigned 8-bit mono PCM at 8900 Hz; just add a WAV container.
        using var wave=new MemoryStream(); using var writer=new BinaryWriter(wave);
        var length=checked((int)pcm.Length); var padding=length%2;
        writer.Write("RIFF"u8); writer.Write(36+length+padding); writer.Write("WAVEfmt "u8);
        writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(SampleRate); writer.Write(SampleRate);
        writer.Write((short)1); writer.Write((short)8); writer.Write("data"u8); writer.Write(length); writer.Write(pcm.ToArray());
        if(padding!=0) writer.Write((byte)0); writer.Flush(); return wave.ToArray();
    }
}
