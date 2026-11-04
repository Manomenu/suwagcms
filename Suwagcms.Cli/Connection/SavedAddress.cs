namespace Suwagcms.Cli.Connection;

internal static class SavedAddress
{
    private const string FileName = "server";

    public static string? Read(string folder)
    {
        if (!Path.IsPathRooted(folder))
        {
            return null;
        }
        var path = Path.Combine(folder, FileName);
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    public static void Write(string folder, string address)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, FileName), address + "\n");
    }
}
