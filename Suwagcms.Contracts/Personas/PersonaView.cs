using Suwagcms.Sdk.Format.Personas;

namespace Suwagcms.Contracts.Personas;

public sealed record PersonaView(string? Personality, PersonaScope? Scope, int Revision);
