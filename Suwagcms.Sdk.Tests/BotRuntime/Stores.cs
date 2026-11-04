using Suwagcms.Sdk.BotRuntime;

namespace Suwagcms.Sdk.Tests.BotRuntime;

internal static class Stores
{
    public static TheoryData<string> Kinds => ["file", "memory"];

    public static IVersionStore Create(string kind, TempDirectory directory) =>
        kind == "file" ? new FileVersionStore(directory.Path) : new InMemoryVersionStore();
}
