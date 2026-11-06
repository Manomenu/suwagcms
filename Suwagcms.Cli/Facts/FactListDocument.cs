using Suwagcms.Contracts.Facts;

namespace Suwagcms.Cli.Facts;

internal sealed record FactListDocument(string Bot, IReadOnlyList<Fact> Facts);
