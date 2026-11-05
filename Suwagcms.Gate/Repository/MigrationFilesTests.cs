using System.Text.RegularExpressions;

namespace Suwagcms.Gate.Repository;

public sealed partial class MigrationFilesTests
{
    private const string Up = "Suwagcms.Server/Database/Migrations/up";

    [GeneratedRegex(@"^\d{4}_[a-z0-9]+(_[a-z0-9]+)*\.sql$")]
    private static partial Regex Name();

    private static List<string> Scripts() =>
        [
            .. Directory
                .EnumerateFiles(Path.Combine(RepositoryFiles.Root, Up))
                .Select(path => Path.GetFileName(path))
                .Where(name => name != ".gitkeep"),
        ];

    [Fact]
    public void Every_script_is_named_NNNN_snake_case_sql()
    {
        var wrong = Scripts().Where(name => !Name().IsMatch(name)).ToList();

        Assert.True(wrong.Count == 0, $"rename to NNNN_what_it_does.sql in {Up}: {string.Join(", ", wrong)}");
    }

    [Fact]
    public void Script_numbers_are_unique()
    {
        var duplicated = Scripts()
            .Select(name => name[..4])
            .GroupBy(number => number)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.True(duplicated.Count == 0, $"give the newer script the next free number in {Up}: {string.Join(", ", duplicated)}");
    }
}
