namespace Suwagcms.Server.Tests;

// Why: plain .NET only, so the gate compiles this same file and tests the decision without referencing a test project
internal static class LinkedWorktree
{
    // Why: the fixture drops and recreates its database, so a worktree on the main checkout's name wipes the tests running there
    internal static void RefuseMainCheckoutDatabase(string? fromEnvironment, string testDirectory)
    {
        if (fromEnvironment is not null || !Contains(testDirectory))
        {
            return;
        }
        throw new InvalidOperationException(
            "TEST_CONNECTION_STRING is not set, and this is a linked git worktree, so the tests would use the main checkout's "
                + "database suwagcms_test: run them after 'source scripts/.internal/checkout-env.sh' (bash or zsh), "
                + "or run Suwagcms.Gate/scripts/check.sh"
        );
    }

    // Why: a linked worktree has a .git file pointing into the main repository's .git/worktrees/, the main checkout a .git directory
    private static bool Contains(string directory)
    {
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            var git = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(git))
            {
                return false;
            }
            if (File.Exists(git))
            {
                return File.ReadAllText(git).Replace('\\', '/').Contains("/worktrees/", StringComparison.Ordinal);
            }
        }
        return false;
    }
}
