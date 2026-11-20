namespace Suwagcms.Server.Tests;

internal static class LinkedWorktree
{
    internal static void RefuseMainCheckoutDatabase(string? fromEnvironment, string testDirectory)
    {
        if (fromEnvironment is not null || !Contains(testDirectory))
        {
            return;
        }
        throw new InvalidOperationException(
            "TEST_CONNECTION_STRING is not set, and this is a linked git worktree, so the tests would use the main checkout's "
                + "database suwagcms_test, which the fixture drops and recreates under the tests running there: run them after "
                + "eval \"$(Suwagcms.Web/node_modules/.bin/suwgate dev checkout-env)\", or run just check"
        );
    }

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
