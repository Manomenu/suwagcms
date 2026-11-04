namespace Suwagcms.Sdk.Tests.Repository;

internal static class RepositoryFiles
{
    public static string Root { get; } = FindRoot();

    public static string Path(string relative) => System.IO.Path.Combine(Root, relative);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "Suwagcms.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Suwagcms.slnx not found above the test binaries.");
    }
}
