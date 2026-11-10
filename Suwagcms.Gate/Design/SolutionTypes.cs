using System.Reflection;
using System.Runtime.CompilerServices;
using Suwagcms.Server.Api;

namespace Suwagcms.Gate.Design;

internal static class SolutionTypes
{
    public static IReadOnlyList<Assembly> Assemblies { get; } =
    [
        typeof(ServerApp).Assembly,
        typeof(Suwagcms.Sdk.BotRuntime.Actions.ActionOutcome).Assembly,
        typeof(Suwagcms.Contracts.Actions.ActionView).Assembly,
        // Why: the CLI's types are all internal; its assembly is named after the command
        Assembly.Load("suwagcms"),
        typeof(SolutionTypes).Assembly,
    ];

    public static IReadOnlyList<Type> All { get; } =
    [
        .. Assemblies
            .SelectMany(assembly => assembly.DefinedTypes)
            .Select(info => info.AsType())
            .Where(type => type.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .Where(type => !(type.FullName ?? "").Contains('<', StringComparison.Ordinal)),
    ];

    public static string SimpleName(Type type) => type.Name.Split('`')[0];
}
