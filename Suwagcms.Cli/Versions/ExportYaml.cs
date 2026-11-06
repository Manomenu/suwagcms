using System.Globalization;
using System.Text.Json;
using Suwagcms.Cli.Yaml;
using Suwagcms.Contracts.Versions;
using Suwagcms.Sdk.Format.References;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;

namespace Suwagcms.Cli.Versions;

// Why: the first line of every file says it is a read-only export, so nobody takes it for a way to change anything.
internal static class ExportYaml
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithQuotingNecessaryStrings()
        .DisableAliases()
        .WithEventEmitter(next => new LongTexts(next))
        .Build();

    public static string Text(VersionExport export, ExportedFile file)
    {
        ArgumentNullException.ThrowIfNull(export);
        ArgumentNullException.ThrowIfNull(file);
        var notice = string.Create(
            CultureInfo.InvariantCulture,
            $"# Read-only export of v{export.Number} of bot {export.Bot}. Changes here do nothing, and no command reads this file back.\n"
        );
        return Change(file) is { } change ? $"{notice}# To change this: {change}\n{Yaml(file.Data)}" : notice + Yaml(file.Data);
    }

    private static string? Change(ExportedFile file) =>
        file.Kind switch
        {
            null => null,
            ResourceKind.GlobalFact => $"suwagcms facts edit {file.Id}",
            ResourceKind.KnowledgeBase => $"suwagcms kb edit {file.Id}",
            ResourceKind.Article => $"suwagcms articles edit {file.Id}",
            ResourceKind.Procedure => $"suwagcms procedures edit {file.Id} --file <file>",
            ResourceKind.Action => $"suwagcms actions edit {file.Id} --file <file>",
            ResourceKind.Persona => "suwagcms persona edit --file <file>",
            ResourceKind.Definition => throw new ArgumentOutOfRangeException(nameof(file), "an export has no such file"),
            _ => throw new ArgumentOutOfRangeException(nameof(file), "unknown kind of resource"),
        };

    public static string Yaml(JsonElement value)
    {
        using var text = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };
        var settings = EmitterSettings.Default.WithBestWidth(int.MaxValue).WithNewLine("\n");
        Serializer.Serialize(new Emitter(text, settings), FormatYaml.Plain(value, keepNulls: true));
        return text.ToString();
    }

    // Why: a text of several lines reads and greps best line by line, and the emitter falls back to a quoted form for what a block cannot hold; only a quoted form keeps the other line breaks of YAML, which any other form turns into another character.
    private sealed class LongTexts(IEventEmitter next) : ChainedEventEmitter(next)
    {
        public override void Emit(ScalarEventInfo eventInfo, IEmitter emitter)
        {
            if (eventInfo.Source.Value is string text)
            {
                if (text.AsSpan().IndexOfAny('\r', '\u0085', '\u2028') >= 0 || text.Contains('\u2029', StringComparison.Ordinal))
                {
                    eventInfo.Style = ScalarStyle.DoubleQuoted;
                }
                else if (text.Contains('\n', StringComparison.Ordinal))
                {
                    eventInfo.Style = ScalarStyle.Literal;
                }
            }
            base.Emit(eventInfo, emitter);
        }
    }
}
