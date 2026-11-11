using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class CommandInputTests
{
    private static CommandInputBuffer Expanded(string raw)
    {
        var buffer = new CommandInputBuffer();
        buffer.SetExpanded(true);
        buffer.SetText(raw);
        return buffer;
    }

    [Theory]
    [InlineData("ABC/TP", "ABC, taxi to parking")]
    [InlineData("ABC/T14", "ABC, taxi to runway 14")]
    [InlineData("ABC/CL", "ABC, cleared to land")]
    [InlineData("ABC/ELD14/CL", "ABC, enter left downwind runway 14 / cleared to land")]
    [InlineData("ABC/URH090", "ABC, turn right heading 090")]
    [InlineData("ABC/PF5,14", "ABC, report final at 5 miles runway 14")]
    [InlineData("ABC/PB N", "ABC, pushback face north")]
    [InlineData("ABC/ML3", "ABC, make left 360")]
    [InlineData("ABC/unknown", "ABC, unknown")]
    [InlineData("ABC/taxi to parking", "ABC, taxi to parking")]
    public void Expanded_display_preserves_the_executable_command(string raw, string display)
    {
        var buffer = Expanded(raw);
        Assert.Equal(raw, buffer.RawText);
        Assert.Equal(display, buffer.DisplayText);
        buffer.SetExpanded(false);
        Assert.Equal(raw, buffer.DisplayText);
        buffer.SetExpanded(true);
        Assert.Equal(display, buffer.DisplayText);
    }

    [Fact]
    public void Backspace_removes_parking_then_taxi_to_then_the_separator()
    {
        var buffer = Expanded("ABC/TP");
        buffer.Erase(buffer.DisplayText.Length, 0, true);
        Assert.Equal("ABC, taxi to", buffer.DisplayText);
        Assert.Equal("ABC/T", buffer.RawText);
        buffer.Erase(buffer.DisplayText.Length, 0, true);
        Assert.Equal("ABC, ", buffer.DisplayText);
        Assert.Equal("ABC/", buffer.RawText);
        buffer.Erase(buffer.DisplayText.Length, 0, true);
        Assert.Equal("ABC", buffer.DisplayText);
    }

    [Fact]
    public void Typing_inserting_selection_and_deleting_preserve_raw_boundaries()
    {
        var buffer = Expanded("ABC/");
        var caret = buffer.ReplaceSelection(buffer.DisplayText.Length, 0, "t");
        caret = buffer.ReplaceSelection(caret, 0, "p");
        Assert.Equal("ABC/tp", buffer.RawText);
        Assert.Equal("ABC, taxi to parking", buffer.DisplayText);
        buffer.ReplaceSelection(buffer.DisplayText.IndexOf("parking", StringComparison.Ordinal), "parking".Length, "14");
        Assert.Equal("ABC/t14", buffer.RawText);
        Assert.Equal("ABC, taxi to runway 14", buffer.DisplayText);
        buffer.Erase(buffer.DisplayText.Length, 0, true);
        Assert.Equal("ABC/t1", buffer.RawText);
        buffer.ReplaceSelection(0, buffer.DisplayText.Length, "NEW/CL/FD");
        Assert.Equal("NEW, cleared to land / contact departure", buffer.DisplayText);
        buffer.Erase(buffer.DisplayText.Length, 0, true);
        Assert.Equal("NEW, cleared to land / contact", buffer.DisplayText);
    }

    [Fact]
    public void Delete_of_an_expanded_word_removes_its_whole_code_and_unknown_text_stays_editable()
    {
        var buffer = Expanded("ABC/TP");
        buffer.Erase("ABC, taxi to".Length, 0, false);
        Assert.Equal("ABC/T", buffer.RawText);
        buffer.SetText("ABC/taxi to parking");
        buffer.Erase(buffer.DisplayText.Length, 0, true);
        Assert.Equal("ABC/taxi to parkin", buffer.RawText);
    }
}
