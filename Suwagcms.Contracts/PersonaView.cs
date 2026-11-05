using Suwagcms.Sdk.Format;

namespace Suwagcms.Contracts;

/// <summary>A bot's persona as the API shows it: its texts, null when empty, and the revision to send back to guard an edit, 0 before the first save.</summary>
public sealed record PersonaView(string? Personality, PersonaScope? Scope, int Revision);
