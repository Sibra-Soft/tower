namespace TowerSpeech.Core;

public static class WorkspacePaths
{
    public const string CatalogFile = "Speech.json";
    public static string DefaultRoot => Resolve(AppContext.BaseDirectory);
    public static string RuntimeRoot => DefaultRoot;
    public static string DataFolder(string root) => Path.GetFullPath(root);

    public static string Resolve(string applicationDirectory)
    {
        // Development uses repository Data, never an old copy under Bin or AppData.
        for (var directory = new DirectoryInfo(applicationDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Tower.slnx")))
            {
                return Path.Combine(directory.FullName, "Data");
            }
        }
        return Path.Combine(Path.GetFullPath(applicationDirectory), "Data");
    }
}
