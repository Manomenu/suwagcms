using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Suwgate.Architecture.Sources;

namespace Suwagcms.Gate;

public sealed partial class TestTimingTests
{
    private const string Fix =
        "a test never measures, waits on or limits real time: to prove a retry or a timeout, give the code a fake TimeProvider and move it; to see that something happened, await the event (a task, a signal, a later event that must come after it); a speed or a complexity promise does not belong on the gate at all, so drop it and its criterion (counting steps instead of milliseconds is a speed promise too)";

    private static readonly Dictionary<string, string> Spared = new(StringComparer.Ordinal)
    {
        ["Suwagcms.Web/e2e/playwright.config.ts"] = "the browser run's own ceilings stop a hang; no test asserts on them",
    };

    private static readonly string[] ElapsedNames = ["Stopwatch", "GetElapsedTime", "GetTimestamp", "TickCount", "TickCount64"];

    private static readonly string[] Clocks =
    [
        "DateTime.Now",
        "DateTime.UtcNow",
        "DateTime.Today",
        "DateTimeOffset.Now",
        "DateTimeOffset.UtcNow",
    ];

    private static bool IsTestCode(string path) =>
        path.Split('/')[0].EndsWith(".Tests", StringComparison.Ordinal)
        || path.Split('/')[0].EndsWith(".Testing", StringComparison.Ordinal);

    private static readonly RepositoryFiles Repository = RepositoryFiles.Find();

    private static IEnumerable<SourceFile> TestSources =>
        Repository.CSharpSources().Where(file => IsTestCode(file.Path) && !Spared.ContainsKey(file.Path));

    [Fact]
    public void No_test_measures_elapsed_time_or_reads_the_real_clock()
    {
        var found = TestSources
            .SelectMany(file =>
                file.Root.DescendantNodes()
                    .Where(node =>
                        (node is IdentifierNameSyntax name && ElapsedNames.Contains(name.Identifier.Text))
                        || (node is MemberAccessExpressionSyntax access && Clocks.Contains(access.ToString()))
                    )
                    .Select(node => $"{file.Path}:{file.LineOf(node)} {node}")
            )
            .ToList();

        Assert.True(found.Count == 0, $"{Fix}: {string.Join(", ", found)}");
    }

    [Fact]
    public void No_test_sleeps_or_sets_itself_a_time_limit()
    {
        var found = TestSources
            .SelectMany(file =>
                file.Root.DescendantNodes()
                    .Where(node =>
                        (node is InvocationExpressionSyntax call && (IsSleep(call) || IsDelayOutsideAPoll(call)))
                        || node is AttributeArgumentSyntax { NameEquals.Name.Identifier.Text: "Timeout" }
                    )
                    .Select(node => $"{file.Path}:{file.LineOf(node)} {node}")
            )
            .ToList();

        Assert.True(found.Count == 0, $"{Fix}: {string.Join(", ", found)}");
    }

    [Fact]
    public void No_web_test_measures_sleeps_or_limits_real_time()
    {
        var found = Repository
            .Paths.Where(path =>
                path.StartsWith("Suwagcms.Web/", StringComparison.Ordinal) && !path.Contains("/node_modules/", StringComparison.Ordinal)
            )
            .Where(path => !Spared.ContainsKey(path) && File.Exists(Path.Combine(Repository.Root, path)))
            .Where(path =>
                IsVitest(path)
                || path.StartsWith("Suwagcms.Web/e2e/", StringComparison.Ordinal)
                || path.EndsWith(".e2e.ts", StringComparison.Ordinal)
            )
            .SelectMany(path =>
                Repository
                    .Read(path)
                    .Split('\n')
                    .Select((line, index) => (line, number: index + 1))
                    .Where(item => WebTiming().IsMatch(item.line) || (IsVitest(path) && OwnLimit().IsMatch(item.line)))
                    .Select(item => $"{path}:{item.number} {item.line.Trim()}")
            )
            .ToList();

        Assert.True(found.Count == 0, $"{Fix}: {string.Join(", ", found)}");
    }

    private static bool IsVitest(string path) =>
        path.EndsWith(".test.ts", StringComparison.Ordinal) || path.EndsWith(".test.tsx", StringComparison.Ordinal);

    private static bool IsSleep(InvocationExpressionSyntax call) =>
        call.Expression.ToString() is "Thread.Sleep" or "System.Threading.Thread.Sleep";

    private static bool IsDelayOutsideAPoll(InvocationExpressionSyntax call) =>
        call.Expression.ToString() is "Task.Delay"
        && call.ArgumentList.Arguments.FirstOrDefault()?.Expression.ToString() is not ("Timeout.Infinite" or "Timeout.InfiniteTimeSpan")
        && !call.Ancestors().Any(node => node is WhileStatementSyntax or DoStatementSyntax or ForStatementSyntax or ForEachStatementSyntax);

    [GeneratedRegex(@"performance\.now|Date\.now\(\)\s*-|-\s*Date\.now\(\)|waitForTimeout|setTimeout\(|interruptAfterTimeLimit")]
    private static partial Regex WebTiming();

    [GeneratedRegex(@"\btimeout\s*:\s*[1-9]")]
    private static partial Regex OwnLimit();
}
