using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class SceneryTests
{
    [Fact]
    public void Bitmap_header_palette_and_pixels_are_decoded_without_a_native_driver()
    {
        var bytes = new byte[1052];
        BitConverter.GetBytes(1048).CopyTo(bytes, 0);
        BitConverter.GetBytes((ushort)2).CopyTo(bytes, 4);
        BitConverter.GetBytes((ushort)2).CopyTo(bytes, 6);
        BitConverter.GetBytes(0x54575457u).CopyTo(bytes, 12);
        BitConverter.GetBytes(256).CopyTo(bytes, 16);
        BitConverter.GetBytes((ushort)238).CopyTo(bytes, 1044);
        bytes[24] = 10;
        bytes[25] = 20;
        bytes[26] = 30;
        bytes[1048] = 1;
        bytes[1051] = 1;
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".BIT");
        try
        {
            File.WriteAllBytes(path, bytes);
            var panorama = OriginalPanorama.Read(path);
            Assert.Equal(2, panorama.Width);
            Assert.Equal(2, panorama.Height);
            Assert.Equal(0, panorama.HorizonRow);
            Assert.Equal(unchecked((int)0xFF0A141E), panorama.Palette[1]);
            Assert.Equal(0, panorama.Palette[0]);
            Assert.Equal(new byte[] { 1, 0, 0, 1 }, panorama.Pixels);
            Assert.Equal(panorama.Column(0), panorama.Column(360));
            Assert.Equal(panorama.Column(270), panorama.Column(-90));
            BitConverter.GetBytes(int.MaxValue).CopyTo(bytes, 0);
            File.WriteAllBytes(path, bytes);
            Assert.Throws<InvalidDataException>(() => OriginalPanorama.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Scenery_colatitude_and_longitude_use_the_cfg_origin_not_real_airport_coordinates()
    {
        var origin = new Position(100, 200);
        var colatitude = Math.PI / 2;
        Assert.Equal(origin, AirportScenery.Placement(0, 0, colatitude, 0, origin));
        var offset = AirportScenery.Placement(0, 0, colatitude - 0.001, 0.002, origin);
        Assert.Equal(12800, offset.X, 5);
        Assert.Equal(6550, offset.Y, 5);
        var mesh = new AircraftMesh("Building", [new(-5000, 0, -1000), new(5000, 9000, 1000)], []);
        var item = new SceneryObject("Building", "OBJ", mesh, origin, 2);
        Assert.Equal(new Position(95, 201), item.WorldPoints()[0].Position);
        Assert.Equal(11, item.WorldPoints()[1].Altitude);
    }

    [Fact]
    public void Missing_airport_graphics_keep_the_existing_fallback()
    {
        var scene = AirportScenery.Read(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), Airport.Training());
        Assert.Empty(scene.Objects);
        Assert.Empty(scene.Errors);
        Assert.Null(scene.Panorama);
        Assert.Equal(40, scene.EyeHeightMetres);
    }

    [Theory]
    [InlineData("CYN", 10, 0, 0, 0)]
    [Trait("Category", "Integration")]
    public void Original_scenery_and_panoramas_load_from_the_single_data_directory(string code, int objects, int width, int height, int horizon)
    {
        var data = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA");
        if (string.IsNullOrWhiteSpace(data))
        {
            Assert.Skip("Set TOWER_ORIGINAL_DATA to verify original scenery and panoramas.");
            return;
        }
        var airport = OriginalData.LoadAirport(data, code);
        var scene = AirportScenery.Read(data, airport);
        Assert.Empty(scene.Errors);
        Assert.Equal(objects, scene.Objects.Count);
        if (code == "CYN")
        {
            Assert.True(scene.HasGround);
            Assert.Null(scene.Panorama);
            Assert.Contains(scene.Objects, item => item.Name.Equals("TERMINAL.dbt", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(60074.953184 / 1000, scene.EyeHeightMetres);
            var terminal = Assert.Single(scene.Objects, item => item.Name.Equals("TERMINAL.dbt", StringComparison.OrdinalIgnoreCase));
            Assert.InRange(terminal.Position.X, -179, -176);
            Assert.InRange(terminal.Position.Y, 150, 154);
        }
        else
        {
            Assert.NotNull(scene.Panorama);
            Assert.Equal(width, scene.Panorama.Width);
            Assert.Equal(height, scene.Panorama.Height);
            Assert.Equal(horizon, scene.Panorama.HorizonRow);
            Assert.Equal(width * height, scene.Panorama.Pixels.Length);
        }
    }

    [Fact]
    public void Projection_uses_the_scenery_camera_height_consistently()
    {
        var low = new TowerProjection(new(0, 0), 0, 90, 800, 600, 33);
        var high = low with { EyeHeight = 60 };
        Assert.True(high.Point(new(0, 500))!.Value.Y > low.Point(new(0, 500))!.Value.Y);
        var polygon = high.Polygon([(new(-10, 500), 0), (new(10, 500), 0), (new(0, 600), 0)]);
        Assert.True(polygon.Length >= 3);
        Assert.Equal(high.Point(new(-10, 500))!.Value.Y, polygon[0].Y, 6);
    }
}
