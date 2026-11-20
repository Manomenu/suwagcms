using System.Text.Json;

namespace Suwagcms.Contracts.Changes;

public sealed record ApplyChanges(string? Bot, IReadOnlyList<JsonElement>? Changes);
