namespace Suwagcms.Server.Bots;

internal sealed class PushOptions
{
    // Why: a bot validates a whole version before it answers, so a push waits long enough for that; a repeat is safe when a slow bot was still working.
    public int PushTimeoutSeconds { get; set; } = 30;

    // Why: a person waits on the page for the bot's state, which is read from a store and needs far less than a push.
    public int StateTimeoutSeconds { get; set; } = 5;
}
