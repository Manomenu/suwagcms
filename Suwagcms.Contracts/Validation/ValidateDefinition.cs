using System.Text.Json;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Contracts.Validation;

public sealed record ValidateDefinition(JsonElement Definition, ValidationStage? Stage = null, JsonElement? Declaration = null);
