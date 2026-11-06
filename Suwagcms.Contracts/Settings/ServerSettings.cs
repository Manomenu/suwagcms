namespace Suwagcms.Contracts.Settings;

/// <summary>What the server was started with and the web app must follow. PageSize is how many rows a page of a list holds.</summary>
public sealed record ServerSettings(int PageSize);
