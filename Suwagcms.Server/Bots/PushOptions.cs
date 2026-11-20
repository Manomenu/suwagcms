namespace Suwagcms.Server.Bots;

internal sealed class PushOptions
{
    public int PushTimeoutSeconds { get; set; } = 30;

    public int StateTimeoutSeconds { get; set; } = 5;

    public int ActionTestMarginSeconds { get; set; } = 5;
}
