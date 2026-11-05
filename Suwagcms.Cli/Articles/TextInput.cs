using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text;

namespace Suwagcms.Cli.Articles;

// Why: a long Markdown text comes from a file or from stdin, and is read as bytes so that the text the CMS stores is the text the author wrote.
internal sealed record TextInput(Option<string?> Text, Option<string?> File, string Field)
{
    public const string FromStandardInput = "-";

    public static TextInput Create(string field, string textOption, string fileOption, string what) =>
        new(
            new Option<string?>(textOption) { Description = $"The {what}, as text. Or use {fileOption}." },
            new Option<string?>(fileOption) { Description = $"A file with the {what}; - reads it from stdin. Or use {textOption}." },
            field
        );

    public void AddTo(Command command)
    {
        command.Options.Add(Text);
        command.Options.Add(File);
    }

    public bool Given(SymbolResult parsed) => parsed.GetValue(Text) is not null || parsed.GetValue(File) is not null;

    public string? Problem(SymbolResult parsed) =>
        parsed.GetValue(Text) is not null && parsed.GetValue(File) is not null
            ? $"Give the {Field} with {Text.Name} or with {File.Name}, not both."
            : null;

    public static string? StdinProblem(SymbolResult parsed, params TextInput[] inputs) =>
        inputs.Count(input => parsed.GetValue(input.File) == FromStandardInput) > 1
            ? $"Only one text can be read from stdin: give {string.Join(" or ", inputs.Select(input => input.File.Name))} as a file for the other."
            : null;

    // Why: the value is null when no text was given, and a file that cannot be read comes back as a problem instead of throwing.
    public async Task<(string? Value, string? Problem)> ReadAsync(
        ParseResult parsed,
        CliEnvironment environment,
        CancellationToken cancellationToken
    )
    {
        if (parsed.GetValue(Text) is { } text)
        {
            return (text, null);
        }
        if (parsed.GetValue(File) is not { } path)
        {
            return (null, null);
        }
        try
        {
            if (path == FromStandardInput)
            {
                if (environment.Stdin is not { } open)
                {
                    return (null, $"{File.Name} - cannot read stdin here: give a file instead.");
                }
                await using var stdin = open();
                using var copy = new MemoryStream();
                await stdin.CopyToAsync(copy, cancellationToken);
                return (Decode(copy.ToArray()), null);
            }
            return (Decode(await System.IO.File.ReadAllBytesAsync(path, cancellationToken)), null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (null, $"Cannot read {File.Name} '{path}': {error.Message}");
        }
    }

    // Why: GetString keeps a byte order mark and File.ReadAllText would drop it; the text is stored exactly as the file holds it.
    private static string Decode(byte[] bytes) => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(bytes);
}
