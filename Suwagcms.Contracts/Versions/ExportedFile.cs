using System.Text.Json;
using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Contracts.Versions;

public sealed record ExportedFile(ResourceKind? Kind, string Id, string Path, JsonElement Data);
