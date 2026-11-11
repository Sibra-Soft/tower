using TowerSpeech.Core;
using Xunit;

namespace Tower.Tests;

public sealed class GeneratedSpeechTests
{
    [Theory]
    [InlineData("CYN")]
    public void Generated_phrases_are_global_and_still_require_review(string airport)
    {
        var fragment = new VoiceFragment
        {
            Bank = 6,
            Index = 1,
            Transcript = "request pushback",
            Status = "uncertain",
            Confidence = "low"
        };
        var catalog = VoiceCatalog.FromFragments([fragment]);
        Assert.False(catalog.TryResolve("request pushback", airport, VoiceMode.Strict, new(6, 1), out _, out _));
        fragment.Status = "clear";
        fragment.Confidence = "high";
        Assert.True(catalog.TryResolve("request pushback", airport, VoiceMode.Strict, new(6, 1), out var piece, out _));
        Assert.Equal(new FragmentId(6, 1), piece!.Id);
        Assert.True(catalog.TryResolve("request pushback", airport, VoiceMode.Strict, null, out _, out _));
    }

    [Fact]
    public void Generated_bank_cannot_be_redirected_to_an_airport()
    {
        var audio = new VocAudioRepository(Path.Combine(Path.GetTempPath(), "GeneratedSpeech", "Data"));
        Assert.Equal("GENERATED.VOC", Path.GetFileName(audio.SourceFile(new(6, 1))));
        Assert.Throws<InvalidDataException>(() => audio.SourceFile(new(6, 1, "CYN")));
    }
}
