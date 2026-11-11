using System.Text;
using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class AircraftModelTests
{
    private static byte[] ModelData(int faceIndex = 2)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Encoding.ASCII.GetBytes("TW001000"));
        writer.Write(0);
        var name = Encoding.ASCII.GetBytes("Synthetic\0");
        writer.Write(name.Length);
        writer.Write(name);
        var offset = (int)stream.Position;
        writer.Write(10);
        writer.Write(20);
        writer.Write(30);
        writer.Write(1001);
        writer.Write(3);
        foreach (var point in new[] { (0, 0, -10), (-5, 0, 10), (5, 0, 10) })
        {
            writer.Write(point.Item1);
            writer.Write(point.Item2);
            writer.Write(point.Item3);
        }
        writer.Write(1003);
        writer.Write(1);
        writer.Write(0);
        writer.Write(2147483644);
        writer.Write(0);
        writer.Write(0);
        writer.Write(2);
        writer.Write(1015);
        writer.Write(12);
        writer.Write(0);
        writer.Write(1);
        writer.Write(faceIndex);
        writer.Write(1014);
        writer.Write(5);
        writer.Write(Encoding.ASCII.GetBytes("BODY\0"));
        writer.Write(1);
        writer.Write(1011);
        writer.Write(1);
        writer.Write(3);
        writer.Write(0);
        writer.Write(7);
        for (var index = 0; index < 7; index++)
        {
            writer.Write(0);
        }
        writer.Write(1016);
        stream.Position = 8;
        writer.Write(offset);
        return stream.ToArray();
    }

    [Fact]
    public void Loader_resolves_compiled_offset_derived_vertices_and_indexed_polygons()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".DBT");
        try
        {
            File.WriteAllBytes(path, ModelData());
            var mesh = OriginalAircraftModels.Read(path);
            Assert.Equal("Synthetic", mesh.Name);
            Assert.Equal(4, mesh.Vertices.Length);
            Assert.Equal(2, mesh.Vertices[3].Y, 6);
            var face = Assert.Single(mesh.Faces);
            Assert.Equal(new[] { 0, 1, 2 }, face.Vertices);
            Assert.Equal("BODY", face.Part);
            Assert.Equal(7, face.ColorIndex);
            Assert.Equal(1, face.Flags);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Invalid_mesh_references_are_rejected_instead_of_rendering_arbitrary_memory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".DBT");
        try
        {
            File.WriteAllBytes(path, ModelData(500));
            Assert.Throws<InvalidDataException>(() => OriginalAircraftModels.Read(path));
            File.WriteAllBytes(path, ModelData()[..^8]);
            Assert.Throws<InvalidDataException>(() => OriginalAircraftModels.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(0, 100, 210)]
    [InlineData(90, 110, 200)]
    [InlineData(180, 100, 190)]
    [InlineData(270, 90, 200)]
    public void Heading_rotates_the_source_nose_into_world_coordinates(double heading, double east, double north)
    {
        var mesh = new AircraftMesh("Test", [new(-5, 0, 0), new(5, 0, 0)], []);
        var point = mesh.WorldPoint(new(0, 0, -10), new(100, 200), 1000, heading, 10);
        Assert.Equal(east, point.Position.X, 6);
        Assert.Equal(north, point.Position.Y, 6);
        Assert.Equal(1000, point.Altitude);
        var scaled = mesh.WorldPoint(new(0, 0, -10), new(100, 200), 0, heading, 20);
        Assert.Equal(20, scaled.Position.Distance(new(100, 200)), 6);
    }

    [Fact]
    public void Model_polygons_are_clipped_at_the_camera_and_screen_edges()
    {
        var projection = new TowerProjection(new(0, 0), 0, 90, 800, 600);
        var visible = projection.Polygon([(new(-20, -5), 0), (new(200, 500), 50), (new(-200, 500), 50)]);
        Assert.True(visible.Length >= 3);
        Assert.All(visible, point =>
        {
            Assert.InRange(point.X, 0, 800);
            Assert.InRange(point.Y, 0, 600);
        });
        Assert.Empty(projection.Polygon([(new(-10, -100), 0), (new(10, -100), 0), (new(0, -200), 0)]));
    }

    [Fact]
    public void Missing_catalogue_or_unknown_type_returns_the_explicit_fallback()
    {
        var catalogue = new AircraftModelCatalog(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        Assert.Empty(catalogue.Types);
        Assert.Null(catalogue.Get("UNKNOWN"));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void All_44_original_aircraft_decode_and_are_cached_without_changing_source_files()
    {
        var data = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA");
        if (string.IsNullOrWhiteSpace(data))
        {
            Assert.Skip("Set TOWER_ORIGINAL_DATA to verify original aircraft meshes.");
            return;
        }
        var catalogue = new AircraftModelCatalog(data);
        Assert.Equal(44, catalogue.Types.Count);
        foreach (var type in catalogue.Types)
        {
            var path = Path.Combine(data, "GRAPHICS", "MODELS", type + ".DBT");
            var source = File.ReadAllBytes(path);
            var mesh = catalogue.Get(type);
            Assert.True(mesh is not null, type + ": " + catalogue.Errors.GetValueOrDefault(type));
            Assert.True(mesh.Width > 0);
            Assert.Contains(mesh.Faces, face => face.Vertices.Length >= 3);
            Assert.Same(mesh, catalogue.Get(type));
            Assert.Equal(source, File.ReadAllBytes(path));
        }
    }
}
