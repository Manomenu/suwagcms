using Suwagcms.Sdk.Tests.Repository;

namespace Suwagcms.Sdk.Tests.Validation;

public sealed class PurityTests
{
    private static readonly string[] Forbidden = ["Npgsql", "Dapper", "System.Net", "System.Data", "HttpClient"];

    private static readonly string[] Folders = ["Suwagcms.Sdk/Validation", "Suwagcms.Sdk/Format"];

    [Fact]
    [Trait("criterion", "AC-sdk-validator-6")]
    public void The_format_and_validation_code_uses_no_database_or_network()
    {
        var files = Folders
            .SelectMany(folder => Directory.EnumerateFiles(RepositoryFiles.Path(folder), "*.cs", SearchOption.AllDirectories))
            .ToList();

        Assert.NotEmpty(files);
        var offences = files
            .SelectMany(file => File.ReadAllLines(file).Select((line, index) => (file, line, number: index + 1)))
            .Where(entry => Forbidden.Any(name => entry.line.Contains(name, StringComparison.Ordinal)))
            .Select(entry => $"{Path.GetFileName(entry.file)}:{entry.number}: {entry.line.Trim()}");
        Assert.True(
            !offences.Any(),
            $"Validation and Format must stay offline (no {string.Join(", ", Forbidden)}): move the database or network code out. Found: {string.Join("; ", offences)}"
        );
    }
}
