namespace Suwagcms.Contracts;

/// <summary>One reference: the resource on the other end (<c>Kind</c> is fact, article or procedure) and the field of the referrer that holds it.</summary>
public sealed record UsageLink(string Kind, string Id, string Field);
