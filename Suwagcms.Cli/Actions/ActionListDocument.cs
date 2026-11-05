using Suwagcms.Contracts;

namespace Suwagcms.Cli.Actions;

internal sealed record ActionListDocument(string Bot, IReadOnlyList<ActionView> Actions);
