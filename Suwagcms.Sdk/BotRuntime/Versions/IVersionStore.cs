using Suwagcms.Sdk.BotRuntime.Availability;

namespace Suwagcms.Sdk.BotRuntime.Versions;

public interface IVersionStore
{
    Task<VersionStoreState> ReadStateAsync(CancellationToken cancellationToken);

    Task<string?> ReadVersionAsync(int number, CancellationToken cancellationToken);

    Task WriteVersionAsync(int number, string document, CancellationToken cancellationToken);

    Task WriteCurrentAsync(int number, CancellationToken cancellationToken);

    Task WriteInUseAsync(IReadOnlyList<int> numbers, CancellationToken cancellationToken);

    Task RemoveVersionAsync(int number, CancellationToken cancellationToken);

    // Why: the store decides, in one step, that a lower number changes nothing, since instances sharing it have no lock in common; ReadStateAsync gives back what is kept.
    Task WriteAvailabilityAsync(BotAvailability availability, CancellationToken cancellationToken);
}
