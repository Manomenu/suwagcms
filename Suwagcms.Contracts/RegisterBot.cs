namespace Suwagcms.Contracts;

/// <summary>What registering a bot takes. Registering never contacts the address.</summary>
public sealed record RegisterBot(string Id, string Name, string Address);
