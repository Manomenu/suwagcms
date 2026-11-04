using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.BotRuntime;

public interface IBotDefinitions
{
    int? CurrentNumber { get; }

    Task<BotDefinition> GetAsync(int number, CancellationToken cancellationToken = default);

    Task ReportInUseAsync(IReadOnlyCollection<int> numbers, CancellationToken cancellationToken = default);
}
