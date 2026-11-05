using System.Text.Json;

namespace Suwagcms.Contracts;

/// <summary>One changed field of a history entry. Old is empty on an add, New is empty on a delete; each is the field's value as the API shows it.</summary>
public sealed record FieldChange(string Field, JsonElement? Old, JsonElement? New);
