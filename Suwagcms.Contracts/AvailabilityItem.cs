namespace Suwagcms.Contracts;

/// <summary>One resource that has availability: its kind (article or procedure), its id, and whether it is off; a resource never touched is on.</summary>
public sealed record AvailabilityItem(string Kind, string Id, bool Off);
