using System.Reflection;
using System.Runtime.CompilerServices;
using Suwagcms.Server.Api;

namespace Suwagcms.Gate.Design;

internal static class SolutionTypes
{
    public static IReadOnlyList<Type> All { get; } =
    [
        .. new[] { typeof(ServerApp).Assembly, typeof(SolutionTypes).Assembly }
            .SelectMany(assembly => assembly.DefinedTypes)
            .Select(info => info.AsType())
            .Where(type => type.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .Where(type => !(type.FullName ?? "").Contains('<', StringComparison.Ordinal)),
    ];

    public static string SimpleName(Type type) => type.Name.Split('`')[0];
}
