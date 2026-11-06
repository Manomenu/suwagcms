using Suwagcms.Sdk.Format.Personas;

namespace Suwagcms.Contracts.Personas;

/// <summary>What saving a persona takes: the whole of it, each text exactly as written. A text left out, empty or only white space is no text.</summary>
public sealed record SavePersona(string? Personality, PersonaScope? Scope);
