namespace Suwagcms.Sdk.BotRuntime.Actions;

public static class ActionSecrets
{
    public const string Prefix = "SUWAGCMS_SECRET_";

    public static string? FromEnvironment(string name) => Environment.GetEnvironmentVariable(Prefix + name);
}
