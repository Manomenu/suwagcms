using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Sdk.BotRuntime;

public interface IBotDefinitions
{
    int? CurrentNumber { get; }

    Task<BotDefinition> GetAsync(int number, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeedArticle>> GetArticlesAsync(int number, CancellationToken cancellationToken = default);

    void OnCurrentChanged(Func<int, CancellationToken, Task> listener);

    void OnAvailabilityChanged(Func<int, CancellationToken, Task> listener);

    bool IsOff(int version, ResourceKind kind, string id);

    Task ReportInUseAsync(IReadOnlyCollection<int> numbers, CancellationToken cancellationToken = default);
}
