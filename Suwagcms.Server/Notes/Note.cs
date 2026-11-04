namespace Suwagcms.Server.Notes;

internal sealed record Note(Guid Id, string Text, DateTimeOffset CreatedAt);
