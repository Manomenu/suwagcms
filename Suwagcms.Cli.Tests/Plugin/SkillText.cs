namespace Suwagcms.Cli.Tests.Plugin;

internal static class SkillText
{
    public static Dictionary<string, string> FrontMatter(string text)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        var matter = new Dictionary<string, string>();
        if (lines.Length == 0 || lines[0] != "---")
        {
            return matter;
        }
        foreach (var line in lines.Skip(1).TakeWhile(line => line != "---"))
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0)
            {
                matter[line[..colon].Trim()] = line[(colon + 1)..].Trim();
            }
        }
        return matter;
    }

    public static string Section(string text, string heading) =>
        string.Join(
            "\n",
            text.ReplaceLineEndings("\n")
                .Split('\n')
                .SkipWhile(line => line != $"## {heading}")
                .Skip(1)
                .TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal))
        );
}
