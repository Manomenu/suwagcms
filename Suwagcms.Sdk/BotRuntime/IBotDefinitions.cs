using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.BotRuntime;

public interface IBotDefinitions
{
    int? CurrentNumber { get; }

    Task<BotDefinition> GetAsync(int number, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeedArticle>> GetArticlesAsync(int number, CancellationToken cancellationToken = default);

    void OnCurrentChanged(Func<int, CancellationToken, Task> listener);

    void OnAvailabilityChanged(Func<int, CancellationToken, Task> listener);

    // Why: the version is asked for because an older version may hold a resource under its former id; the setting itself is one and the same on every version.
    bool IsOff(int version, ResourceKind kind, string id);

    Task ReportInUseAsync(IReadOnlyCollection<int> numbers, CancellationToken cancellationToken = default);
}
