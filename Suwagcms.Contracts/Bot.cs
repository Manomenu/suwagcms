namespace Suwagcms.Contracts;

/// <summary>A registered bot: the id that sits in addresses and commands, the name people see, and where the CMS reaches it.</summary>
public sealed record Bot(string Id, string Name, string Address);
