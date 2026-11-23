using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Suwagcms.Sdk.Tests.Repository;

namespace Suwagcms.Sdk.Tests.Package;

public sealed partial class SdkReleaseTests
{
    private const string WorkflowPath = ".github/workflows/sdk.yml";
    private const string NothingStarted = "Nothing was started";

    private const string ThrowawayCheckout = """
        set -euo pipefail
        unset GIT_DIR GIT_WORK_TREE GIT_INDEX_FILE
        export GIT_AUTHOR_NAME=test GIT_AUTHOR_EMAIL=test@example.com GIT_COMMITTER_NAME=test GIT_COMMITTER_EMAIL=test@example.com
        cd "$1"
        mkdir bin
        printf '#!/usr/bin/env bash\necho "$*" >> "%s/gh.log"\n' "$1" > bin/gh
        chmod +x bin/gh
        export PATH="$1/bin:$PATH"
        git init -q --bare -b master origin.git
        git clone -q origin.git checkout 2>/dev/null
        cd checkout
        git switch -q -c master 2>/dev/null || true
        mkdir -p scripts/.internal
        cp "$2/sdk-publish.sh" "$2/sdk-version.sh" scripts/.internal/
        chmod +x scripts/.internal/*.sh
        git add .
        git commit -q -m first
        git push -q origin master
        eval "$3"
        exec scripts/.internal/sdk-publish.sh "$4"
        """;

    private static string Workflow()
    {
        var path = RepositoryFiles.Path(WorkflowPath);
        Assert.True(File.Exists(path), $"{WorkflowPath} does not exist: add it, docs/release.md says what a release does");
        return File.ReadAllText(path);
    }

    private static (string Pack, string Publish) Jobs()
    {
        var workflow = Workflow();
        var publish = workflow.IndexOf("\n  publish:\n", StringComparison.Ordinal);
        var pack = workflow.IndexOf("\n  pack:\n", StringComparison.Ordinal);
        Assert.True(pack >= 0 && publish > pack, $"{WorkflowPath} must hold a job pack and after it a job publish");
        return (workflow[pack..publish], workflow[publish..]);
    }

    [Fact]
    [Trait("criterion", "AC-sdk-validator-15")]
    public void The_workflow_starts_by_hand_refuses_a_commit_ci_has_not_passed_and_packs_and_pushes_that_version()
    {
        var workflow = Workflow();

        Assert.Contains("workflow_dispatch:", workflow, StringComparison.Ordinal);
        Assert.DoesNotMatch(AutomaticTrigger(), workflow);
        var (pack, publish) = Jobs();
        Assert.Contains("sdk-version.sh \"$VERSION\"", pack, StringComparison.Ordinal);
        Assert.Contains("statuses: read", pack, StringComparison.Ordinal);
        Assert.Contains("select(.context == \"ci/tree\" and .state == \"success\")", pack, StringComparison.Ordinal);
        Assert.Contains("grep -qxF \"$tree\"", pack, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet test", pack, StringComparison.Ordinal);
        Assert.Contains("dotnet pack Suwagcms.Sdk ", pack, StringComparison.Ordinal);
        Assert.Contains("dotnet pack Suwagcms.Cli ", pack, StringComparison.Ordinal);
        Assert.Contains("-p:Version=\"$VERSION\"", pack, StringComparison.Ordinal);
        Assert.Contains("-p:ContinuousIntegrationBuild=true", pack, StringComparison.Ordinal);
        Assert.Contains("dotnet nuget push \"nupkg/Suwagcms.Sdk.$VERSION.nupkg\"", publish, StringComparison.Ordinal);
        Assert.Contains("dotnet nuget push \"nupkg/Suwagcms.Cli.$VERSION.nupkg\"", publish, StringComparison.Ordinal);
        Assert.Contains("https://api.nuget.org/v3/index.json", publish, StringComparison.Ordinal);
        Assert.DoesNotContain("--no-symbols", publish, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-sdk-validator-16")]
    public void Only_the_job_that_pushes_can_get_a_key_and_no_key_is_stored()
    {
        var (pack, publish) = Jobs();

        Assert.DoesNotContain("id-token", pack, StringComparison.Ordinal);
        Assert.Contains("id-token: write", publish, StringComparison.Ordinal);
        Assert.DoesNotContain("actions/checkout", publish, StringComparison.Ordinal);
        Assert.Contains("NuGet/login@", publish, StringComparison.Ordinal);

        var workflow = Workflow();
        Assert.All(Secret().Matches(workflow), secret => Assert.Equal("NUGET_USER", secret.Groups[1].Value));
        var actions = Action().Matches(workflow);
        Assert.NotEmpty(actions);
        Assert.All(actions, action => Assert.Matches(PinnedByHash(), action.Groups[1].Value));
    }

    [Fact]
    [Trait("criterion", "AC-sdk-validator-17")]
    public async Task The_recipe_starts_the_workflow_on_master_and_says_how_to_follow_it()
    {
        Assert.Matches(Recipe(), await File.ReadAllTextAsync(RepositoryFiles.Path("justfile"), TestContext.Current.CancellationToken));

        var run = await Publish("0.1.0", setup: ":");

        Assert.True(run.ExitCode == 0, run.Errors);
        Assert.Equal("workflow run sdk.yml --ref master -f version=0.1.0\n", run.GhCalls);
        Assert.Contains("gh run watch", run.Output, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("criterion", "AC-sdk-validator-17")]
    [InlineData("v0.1.0")]
    [InlineData("0.1")]
    [InlineData("01.0.0")]
    public async Task A_version_of_the_wrong_form_starts_nothing(string version)
    {
        var run = await Publish(version, setup: ":");

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains("is not <major>.<minor>.<patch>", run.Errors, StringComparison.Ordinal);
        Assert.Empty(run.GhCalls);
    }

    [Theory]
    [Trait("criterion", "AC-sdk-validator-18")]
    [InlineData("git switch -q -c other")]
    [InlineData("echo uncommitted > stray.txt")]
    [InlineData("git commit -q --allow-empty -m unpushed")]
    [InlineData(
        "git clone -q ../origin.git ../second && git -C ../second commit -q --allow-empty -m newer && git -C ../second push -q origin master"
    )]
    public async Task A_release_from_anything_but_a_clean_up_to_date_master_starts_nothing(string setup)
    {
        var run = await Publish("0.1.0", setup);

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains(NothingStarted, run.Errors, StringComparison.Ordinal);
        Assert.Empty(run.GhCalls);
    }

    [Fact]
    [Trait("criterion", "AC-sdk-validator-18")]
    public void The_workflow_refuses_any_ref_but_master_before_it_checks_out_code()
    {
        var (pack, _) = Jobs();

        var refusal = pack.IndexOf("!= \"refs/heads/master\"", StringComparison.Ordinal);
        Assert.InRange(refusal, 0, pack.IndexOf("actions/checkout", StringComparison.Ordinal));
        Assert.Contains("exit 1", pack[refusal..], StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-sdk-validator-19")]
    public void The_package_is_ready_to_be_public()
    {
        var project = XDocument.Load(RepositoryFiles.Path("Suwagcms.Sdk/Suwagcms.Sdk.csproj"));
        string Property(string name)
        {
            return project.Descendants(name).Select(element => element.Value).SingleOrDefault() ?? "";
        }

        Assert.Equal("MIT", Property("PackageLicenseExpression"));
        var license = File.ReadAllText(RepositoryFiles.Path("LICENSE"));
        Assert.StartsWith("MIT License", license, StringComparison.Ordinal);
        Assert.NotEmpty(Property("Copyright"));
        Assert.Contains(Property("Copyright"), license, StringComparison.Ordinal);

        Assert.Equal("README.md", Property("PackageReadmeFile"));
        Assert.Contains(project.Descendants("None"), item =>
            item.Attribute("Include")?.Value == "README.md" && item.Attribute("Pack")?.Value == "true"
        );
        var readme = File.ReadAllText(RepositoryFiles.Path("Suwagcms.Sdk/README.md"));
        Assert.Contains("dotnet add package Suwagcms.Sdk", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("not published", readme, StringComparison.OrdinalIgnoreCase);

        Assert.NotEmpty(Property("Description"));
        Assert.NotEmpty(Property("PackageTags"));
        Assert.NotEmpty(Property("PackageProjectUrl"));
    }

    private static async Task<PublishRun> Publish(string version, string setup)
    {
        var work = Directory.CreateTempSubdirectory("suwagcms-release-");
        try
        {
            var scripts = RepositoryFiles.Path("scripts/.internal");
            var start = new ProcessStartInfo("bash") { RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "-c", ThrowawayCheckout, "bash", work.FullName, scripts, setup, version })
            {
                start.ArgumentList.Add(argument);
            }
            using var bash = Process.Start(start) ?? throw new InvalidOperationException("bash did not start");
            var cancellation = TestContext.Current.CancellationToken;
            var errors = bash.StandardError.ReadToEndAsync(cancellation);
            var output = await bash.StandardOutput.ReadToEndAsync(cancellation);
            await bash.WaitForExitAsync(cancellation);

            var log = Path.Combine(work.FullName, "gh.log");
            var ghCalls = File.Exists(log) ? await File.ReadAllTextAsync(log, cancellation) : "";
            return new PublishRun(bash.ExitCode, output, await errors, ghCalls);
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    private sealed record PublishRun(int ExitCode, string Output, string Errors, string GhCalls);

    [GeneratedRegex(@"^\s*(push|pull_request|pull_request_target|schedule|workflow_run|release):", RegexOptions.Multiline)]
    private static partial Regex AutomaticTrigger();

    [GeneratedRegex(@"secrets\.(\w+)")]
    private static partial Regex Secret();

    [GeneratedRegex(@"\buses:\s*(\S+)")]
    private static partial Regex Action();

    [GeneratedRegex("@[0-9a-f]{40}$")]
    private static partial Regex PinnedByHash();

    [GeneratedRegex(@"^sdk-publish version:\n\s+@\./scripts/\.internal/sdk-publish\.sh \{\{ version \}\}$", RegexOptions.Multiline)]
    private static partial Regex Recipe();
}
