using System.Text.Json;
using Suwagcms.Sdk.Format;

namespace Suwagcms.Contracts;

/// <summary>One file of an exported version: the resource it holds (no kind for <c>version.yaml</c>), where it goes inside the folder, and what the version holds for it.</summary>
public sealed record ExportedFile(ResourceKind? Kind, string Id, string Path, JsonElement Data);
