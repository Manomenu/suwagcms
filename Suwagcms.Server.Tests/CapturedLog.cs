using Microsoft.Extensions.Logging;

namespace Suwagcms.Server.Tests;

internal sealed record CapturedLog(string Category, LogLevel Level, string Message, string? TraceId);
