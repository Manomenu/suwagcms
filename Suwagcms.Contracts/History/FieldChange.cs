using System.Text.Json;

namespace Suwagcms.Contracts.History;

public sealed record FieldChange(string Field, JsonElement? Old, JsonElement? New);
