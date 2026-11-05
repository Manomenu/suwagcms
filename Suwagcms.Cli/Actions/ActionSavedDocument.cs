using Suwagcms.Contracts;

namespace Suwagcms.Cli.Actions;

internal sealed record ActionSavedDocument(string Bot, ActionView Action, bool DryRun);
