namespace Suwagcms.Server.Tests;

// Why: starting the server costs about a second, so a test class starts it once; each test still works on a bot of its own
public sealed class SharedApp : IDisposable
{
    internal TestApp App { get; } = new();

    public void Dispose() => App.Dispose();
}
