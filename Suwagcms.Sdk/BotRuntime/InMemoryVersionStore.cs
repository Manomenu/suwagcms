namespace Suwagcms.Sdk.BotRuntime;

public sealed class InMemoryVersionStore : IVersionStore
{
    private readonly Lock gate = new();
    private readonly SortedDictionary<int, string> documents = [];
    private int? current;
    private int[]? inUse;

    public Task<VersionStoreState> ReadStateAsync(CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult(new VersionStoreState(current, [.. documents.Keys], inUse is null ? null : [.. inUse]));
        }
    }

    public Task<string?> ReadVersionAsync(int number, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult(documents.GetValueOrDefault(number));
        }
    }

    public Task WriteVersionAsync(int number, string document, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            documents[number] = document;
            current = number;
        }

        return Task.CompletedTask;
    }

    public Task WriteInUseAsync(IReadOnlyList<int> numbers, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            inUse = [.. numbers];
        }

        return Task.CompletedTask;
    }

    public Task RemoveVersionAsync(int number, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            documents.Remove(number);
        }

        return Task.CompletedTask;
    }
}
