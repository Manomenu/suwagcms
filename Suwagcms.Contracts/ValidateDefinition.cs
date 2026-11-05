using System.Text.Json;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Contracts;

/// <summary>What the validate endpoint takes: a definition, the stage to check it at (Version when left out) and optionally what a bot declares. The two documents stay raw so that their mistakes are the validator's own.</summary>
public sealed record ValidateDefinition(JsonElement Definition, ValidationStage? Stage = null, JsonElement? Declaration = null);
