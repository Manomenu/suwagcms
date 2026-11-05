using Suwagcms.Cli.Connection;

namespace Suwagcms.Cli.Output;

internal static class ServerFailure
{
    public static Task<int> ReportAsync(this Report report, ServerAnswer answer, Uri address) =>
        report.FailedAsync(
            answer.ExitCode,
            new FailureDocument(answer.Message, ServerAddress.Display(address), answer.Mistakes.Count == 0 ? null : answer.Mistakes),
            answer.Message
        );
}
