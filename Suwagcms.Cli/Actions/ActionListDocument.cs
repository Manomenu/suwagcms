using Suwagcms.Contracts.Actions;

namespace Suwagcms.Cli.Actions;

internal sealed record ActionListDocument(string Bot, IReadOnlyList<ActionView> Actions);
