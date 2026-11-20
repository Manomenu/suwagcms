using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text;

namespace Suwagcms.Cli.Articles;

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

    private static string Decode(byte[] bytes) => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(bytes);
}
