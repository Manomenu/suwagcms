using Suwagcms.Sdk.BotRuntime.Availability;
using Suwagcms.Sdk.BotRuntime.Versions;

namespace Suwagcms.Sdk.Tests.BotRuntime.Versions;

internal sealed class FlakyStore(IVersionStore inner) : IVersionStore
{
    public bool FailReads { get; set; }

    public bool FailWrites { get; set; }

    public Task<VersionStoreState> ReadStateAsync(CancellationToken cancellationToken) =>
        FailReads ? throw new IOException("the store is down (read)") : inner.ReadStateAsync(cancellationToken);

    public Task<string?> ReadVersionAsync(int number, CancellationToken cancellationToken) =>
        FailReads ? throw new IOException("the store is down (read)") : inner.ReadVersionAsync(number, cancellationToken);

    public Task WriteVersionAsync(int number, string document, CancellationToken cancellationToken) =>
        FailWrites ? throw new IOException("the store is down (write)") : inner.WriteVersionAsync(number, document, cancellationToken);

    public Task WriteCurrentAsync(int number, CancellationToken cancellationToken) =>
        FailWrites ? throw new IOException("the store is down (write)") : inner.WriteCurrentAsync(number, cancellationToken);

    public Task WriteInUseAsync(IReadOnlyList<int> numbers, CancellationToken cancellationToken) =>
        FailWrites ? throw new IOException("the store is down (write)") : inner.WriteInUseAsync(numbers, cancellationToken);

    public Task WriteAvailabilityAsync(BotAvailability availability, CancellationToken cancellationToken) =>
        FailWrites ? throw new IOException("the store is down (write)") : inner.WriteAvailabilityAsync(availability, cancellationToken);

    public Task RemoveVersionAsync(int number, CancellationToken cancellationToken) =>
        FailWrites ? throw new IOException("the store is down (write)") : inner.RemoveVersionAsync(number, cancellationToken);
}
