using Suwagcms.Contracts.Actions;

namespace Suwagcms.Cli.Actions;

internal sealed record ActionSavedDocument(string Bot, ActionView Action, bool DryRun);
