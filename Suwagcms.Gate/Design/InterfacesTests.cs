namespace Suwagcms.Gate.Design;

public sealed class InterfacesTests
{
    [Fact]
    public void Every_interface_has_at_least_two_implementations()
    {
        var lonely = SolutionTypes
            .All.Where(type => type.IsInterface)
            // Why: a public interface of the SDK is a seam for the bots that use it, and their stand-ins are its other implementations
            .Where(type => !(type.IsPublic && type.Assembly == typeof(Suwagcms.Sdk.BotRuntime.IBotDefinitions).Assembly))
            .Select(contract =>
                (contract, implementations: SolutionTypes.All.Count(type => !type.IsInterface && contract.IsAssignableFrom(type)))
            )
            .Where(item => item.implementations < 2)
            .Select(item => $"{item.contract.FullName} ({item.implementations})")
            .Order()
            .ToList();

        Assert.True(
            lonely.Count == 0,
            $"delete the interface and use the class; extract one when the second implementation arrives: {string.Join(", ", lonely)}"
        );
    }
}
