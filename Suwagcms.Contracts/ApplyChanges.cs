using System.Text.Json;

namespace Suwagcms.Contracts;

/// <summary>A file of changes as a request. Each change says what it does (add, edit or delete, with the kind as the value: fact, knowledgeBase, article, procedure, action), the resource's id (name for a fact), and for an add or edit the fields of that resource's own save; an edit or delete may give ifRevision. Bot, when given, must be the bot of the address.</summary>
public sealed record ApplyChanges(string? Bot, IReadOnlyList<JsonElement>? Changes);
