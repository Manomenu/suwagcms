namespace Suwagcms.Contracts.Changes;

public sealed record ChangesRefusal(string Title, int Status, string Detail, IReadOnlyList<ChangeMistake> Mistakes);
