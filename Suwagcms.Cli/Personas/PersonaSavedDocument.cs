using Suwagcms.Contracts;

namespace Suwagcms.Cli.Personas;

internal sealed record PersonaSavedDocument(string Bot, PersonaView Persona, bool DryRun);
