using System.Text;

namespace TowerSpeech.Core;

public static class WaveComposer
{
    public const int SampleRate = 8900;
    private const int GapMilliseconds = 65;

    public static byte[] Compose(IReadOnlyList<byte[]> clips)
    {
        if (clips.Count == 0)
        {
            throw new ArgumentException("No audio fragments");
        }

        int gapSamples = SampleRate * GapMilliseconds / 1000;
        using var pcm = new MemoryStream();
        for (int i = 0; i < clips.Count; i++)
        {
            if (i > 0)
            {
                for (int j = 0; j < gapSamples; j++)
                {
                    pcm.WriteByte(128);
                }
            }

            pcm.Write(ReadUnsigned8MonoPcm(clips[i]));
        }
        return WrapPcm(pcm.ToArray());
    }

    public static byte[] WrapPcm(byte[] body)
    {
        using var output = new MemoryStream();
        using (var writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + body.Length);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(SampleRate);
            writer.Write(SampleRate);
            writer.Write((short)1);
            writer.Write((short)8);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(body.Length);
            writer.Write(body);
        }
        return output.ToArray();
    }

    public static byte[] ReadUnsigned8MonoPcm(byte[] wave)
    {
        using var ms = new MemoryStream(wave, writable: false);
        using var reader = new BinaryReader(ms);
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF")
        {
            throw new InvalidDataException("Not a RIFF wave");
        }

        reader.ReadUInt32();
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE")
        {
            throw new InvalidDataException("Not a WAVE file");
        }

        bool validFormat = false;
        byte[]? data = null;
        while (ms.Position + 8 <= ms.Length)
        {
            string tag = Encoding.ASCII.GetString(reader.ReadBytes(4));
            uint size = reader.ReadUInt32();
            long end = ms.Position + size;
            if (end > ms.Length)
            {
                throw new InvalidDataException("Invalid WAV chunk size");
            }

            if (tag == "fmt ")
            {
                if (size < 16)
                {
                    throw new InvalidDataException("Invalid WAV format");
                }

                var format = reader.ReadUInt16();
                var channels = reader.ReadUInt16();
                var rate = reader.ReadUInt32();
                reader.ReadUInt32();
                reader.ReadUInt16();
                var bits = reader.ReadUInt16();
                validFormat = format == 1 && channels == 1 && rate == SampleRate && bits == 8;
            }
            else if (tag == "data")
            {
                data = reader.ReadBytes(checked((int)size));
            }

            ms.Position = end + size % 2;
        }
        if (!validFormat || data is null)
        {
            throw new InvalidDataException($"Only unsigned 8-bit mono PCM at {SampleRate} Hz is supported");
        }

        return data;
    }
}
