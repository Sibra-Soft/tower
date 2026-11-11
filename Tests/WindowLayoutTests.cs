using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class WindowLayoutTests
{
    [Theory]
    [InlineData("../outside")]
    [InlineData("folder/layout")]
    [InlineData("folder\\layout")]
    [InlineData("")]
    [InlineData("..")]
    public void File_names_cannot_escape_the_layout_directory(string name)
    {
        var store = new WindowLayoutStore(Path.GetTempPath());
        Assert.Throws<InvalidDataException>(() => store.FilePath(name));
    }

    [Theory]
    [InlineData("ASDE", "On,10,20,300,400", 10, 20, 300, 400)]
    [InlineData("Buttons", "Off,600,-16", 600, -16, null, null)]
    [InlineData("ArrivalStrips", "752,0,273,201", 752, 0, 273, 201)]
    [InlineData("Operational", "Normal,0,0,1024,61", 0, 0, 1024, 61)]
    public void Original_window_formats_include_visibility_position_only_and_bounds(string key, string value, int x, int y, int? width, int? height)
    {
        var layout = new WindowLayout();
        layout.Windows[key] = value;
        var placement = layout.Placement(key)!;
        Assert.Equal(x, placement.X);
        Assert.Equal(y, placement.Y);
        Assert.Equal(width, placement.Width);
        Assert.Equal(height, placement.Height);
    }

    [Theory]
    [InlineData("On,10,20,0,40")]
    [InlineData("On,10,20,-5,40")]
    [InlineData("On,NaN,20,30,40")]
    [InlineData("On,2147483648,20,30,40")]
    [InlineData("On,1,2,3")]
    public void Invalid_geometry_is_rejected_before_changing_any_windows(string value)
    {
        var layout = new WindowLayout();
        layout.Windows["ASDE"] = value;
        Assert.Throws<InvalidDataException>(() => layout.Placement("ASDE"));
    }

    [Fact]
    public void Atomic_save_roundtrips_bounds_and_preserves_unknown_windows_with_backup()
    {
        var root = Path.Combine(Path.GetTempPath(), "TowerLayouts-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new WindowLayoutStore(root);
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "layout.SET"), "[Windows]\nASDE=On,10,20,300,400\nTime=785,-60,112,38\n");
            var original = File.ReadAllText(Path.Combine(root, "layout.SET"));
            var layout = new WindowLayout { ScreenCoordinates = true };
            layout.Set("ASDE", new(-1200, 25, 350, 450, false));
            layout.Set("DepartureStrips", new(250, 20, 300, 200, true));
            store.Write("layout.SET", layout);
            var restored = store.Read("layout.SET");
            Assert.True(restored.ScreenCoordinates);
            Assert.Equal(new(-1200, 25, 350, 450, false), restored.Placement("ASDE"));
            Assert.Equal("785,-60,112,38", restored.Windows["Time"]);
            Assert.Equal(original, File.ReadAllText(Path.Combine(root, "layout.SET.bak")));
            Assert.Equal(new[] { "layout.SET" }, store.List());
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void Original_root_presets_are_read_without_being_modified()
    {
        var store = new WindowLayoutStore(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Layouts"));
        foreach (var name in new[] { "640X480.SET", "800X600.SET", "1024X768.SET", "12801024.SET" })
        {
            var before = File.ReadAllBytes(store.FilePath(name));
            var layout = store.Read(name);
            Assert.NotNull(layout.Placement("3D"));
            Assert.NotNull(layout.Placement("Comm"));
            Assert.False(layout.ScreenCoordinates);
            Assert.Equal(before, File.ReadAllBytes(store.FilePath(name)));
        }
    }

    [Fact]
    public void No_layout_files_and_missing_layout_are_handled_explicitly()
    {
        var store = new WindowLayoutStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        Assert.Empty(store.List());
        Assert.Throws<DirectoryNotFoundException>(() => store.Read("missing"));
    }
}
