using System.Text;

internal static class MotionCapture
{
    // A small, self-contained MJPEG AVI writer for review captures; no recording tools ship with Tower.
    public static void Write(string path, IReadOnlyList<byte[]> frames, int width, int height, int framesPerSecond)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII);
        void Tag(string value) => writer.Write(Encoding.ASCII.GetBytes(value));
        long Begin(string tag)
        {
            Tag(tag);
            var offset = stream.Position;
            writer.Write(0);
            return offset;
        }
        void End(long offset)
        {
            var position = stream.Position;
            stream.Position = offset;
            writer.Write(checked((int)(position - offset - 4)));
            stream.Position = position;
        }
        var riff = Begin("RIFF");
        Tag("AVI ");
        var header = Begin("LIST");
        Tag("hdrl");
        Tag("avih");
        writer.Write(56);
        writer.Write(1000000 / framesPerSecond);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0x10);
        writer.Write(frames.Count);
        writer.Write(0);
        writer.Write(1);
        writer.Write(frames.Max(frame => frame.Length));
        writer.Write(width);
        writer.Write(height);
        writer.Write(new byte[16]);
        var track = Begin("LIST");
        Tag("strl");
        Tag("strh");
        writer.Write(56);
        Tag("vids");
        Tag("MJPG");
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(1);
        writer.Write(framesPerSecond);
        writer.Write(0);
        writer.Write(frames.Count);
        writer.Write(0);
        writer.Write(-1);
        writer.Write(0);
        writer.Write((short)0);
        writer.Write((short)0);
        writer.Write((short)width);
        writer.Write((short)height);
        Tag("strf");
        writer.Write(40);
        writer.Write(40);
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1);
        writer.Write((short)24);
        Tag("MJPG");
        writer.Write(width * height * 3);
        writer.Write(new byte[16]);
        End(track);
        End(header);
        var movie = Begin("LIST");
        var movieStart = stream.Position;
        Tag("movi");
        var offsets = new List<int>();
        foreach (var frame in frames)
        {
            offsets.Add(checked((int)(stream.Position - movieStart)));
            Tag("00dc");
            writer.Write(frame.Length);
            writer.Write(frame);
            if (frame.Length % 2 != 0)
            {
                writer.Write((byte)0);
            }
        }
        End(movie);
        Tag("idx1");
        writer.Write(frames.Count * 16);
        for (var index = 0; index < frames.Count; index++)
        {
            Tag("00dc");
            writer.Write(0x10);
            writer.Write(offsets[index]);
            writer.Write(frames[index].Length);
        }
        End(riff);
    }
}
