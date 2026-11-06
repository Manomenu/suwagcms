using Suwagcms.Contracts.Personas;

namespace Suwagcms.Cli.Personas;

internal sealed record PersonaSavedDocument(string Bot, PersonaView Persona, bool DryRun);
