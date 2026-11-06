namespace Suwagcms.Sdk.BotRuntime.Actions;

// Why: a definition names a secret and whoever edits a definition chooses the name, so only variables the bot's owner gave this prefix can be reached, never DATABASE_URL or the like.
public static class ActionSecrets
{
    public const string Prefix = "SUWAGCMS_SECRET_";

    public static string? FromEnvironment(string name) => Environment.GetEnvironmentVariable(Prefix + name);
}
