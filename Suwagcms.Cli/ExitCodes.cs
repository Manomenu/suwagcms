namespace Suwagcms.Cli;

// Why: an agent fixes a refusal and retries an unreachable server, so each outcome keeps its own code on every command.
internal static class ExitCodes
{
    public const int Done = 0;
    public const int Refused = 1;
    public const int Misused = 2;
    public const int Unreachable = 3;
}
