namespace Suwagcms.Cli.Bots;

internal static class SavedBot
{
    private const string FileName = "bot";

    public static string? Read(string folder)
    {
        var path = Path.Combine(folder, FileName);
        return Path.IsPathRooted(folder) && File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    public static void Write(string folder, string id)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, FileName), id + "\n");
    }
}
