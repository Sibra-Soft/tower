using System.Buffers.Binary;
using System.Text;

namespace TowerSpeech.WindowsAudio;

/// <summary>Optional narrow-band radio filter and reproducible low-level noise. PCM RIFF 8/16-bit only.</summary>
public static class RadioWaveEffects
{
    public static byte[] Apply(byte[] wave, int noisePercent)
    {
        noisePercent = Math.Clamp(noisePercent, 0, 30);
        var bytes = (byte[])wave.Clone();
        var span = bytes.AsSpan();
        if (bytes.Length < 44 || Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF" ||
            Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE")
        {
            throw new InvalidDataException("Radio effects require a WAV file");
        }

        int rate = 0, channels = 0, bits = 0, dataStart = -1, dataLength = 0;
        for (int pos = 12; pos + 8 <= bytes.Length;)
        {
            int length = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(pos + 4, 4));
            if (length < 0 || length > bytes.Length - pos - 8)
            {
                throw new InvalidDataException("Invalid WAV chunk");
            }

            string type = Encoding.ASCII.GetString(bytes, pos, 4);
            if (type == "fmt ")
            {
                if (length < 16 || BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(pos + 8, 2)) != 1)
                {
                    throw new InvalidDataException("Only uncompressed PCM audio is supported");
                }

                channels = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(pos + 10, 2));
                rate = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(pos + 12, 4));
                bits = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(pos + 22, 2));
            }
            if (type == "data")
            {
                dataStart = pos + 8;
                dataLength = length;
            }
            pos += 8 + length + (length & 1);
        }
        if (dataStart < 0 || rate < 8000 || channels is < 1 or > 2 || bits is not (8 or 16) ||
            dataLength % (channels * (bits / 8)) != 0)
        {
            throw new InvalidDataException("WAV must be 8/16-bit PCM, mono or stereo");
        }
        // Two first-order filters approximate VHF/AM voice bandwidth.
        double hpDecay = Math.Exp(-2 * Math.PI * 300.0 / rate);
        double lpAlpha = 1 - Math.Exp(-2 * Math.PI * 2800.0 / rate);
        double[] low = new double[channels], smooth = new double[channels];
        var rng = new Random(1995);
        int sampleSize = bits / 8;
        int frames = dataLength / (channels * sampleSize);
        for (int frame = 0; frame < frames; frame++)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                int i = dataStart + (frame * channels + ch) * sampleSize;
                double value = bits == 8 ? (bytes[i] - 128) / 128.0 :
                    BinaryPrimitives.ReadInt16LittleEndian(span.Slice(i, 2)) / 32768.0;
                low[ch] = hpDecay * low[ch] + (1 - hpDecay) * value;
                smooth[ch] += lpAlpha * (value - low[ch] - smooth[ch]);
                double result = smooth[ch] * 1.75 + (rng.NextDouble() * 2 - 1) * noisePercent / 1000.0;
                result = Math.Clamp(result, -1, 1);
                if (bits == 8)
                {
                    bytes[i] = (byte)Math.Clamp((int)Math.Round(128 + result * 127), 0, 255);
                }
                else
                {
                    BinaryPrimitives.WriteInt16LittleEndian(span.Slice(i, 2), (short)Math.Clamp((int)Math.Round(result * 32767), -32768, 32767));
                }
            }
        }

        return bytes;
    }
}
