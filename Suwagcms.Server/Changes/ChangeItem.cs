using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Changes;

internal sealed record ChangeItem(int Position, string Action, string Kind, Change Change);
