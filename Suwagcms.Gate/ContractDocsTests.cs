using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Suwgate.Architecture.Sources;

namespace Suwagcms.Gate;

public sealed class ContractDocsTests
{
    private const string Contracts = "Suwagcms.Contracts/";

    [Fact]
    public void The_contracts_hold_no_comment_but_the_documentation_of_the_API()
    {
        var found = RepositoryFiles
            .Find()
            .CSharpSources(Contracts)
            .SelectMany(file =>
                file.Root.DescendantTrivia(descendIntoTrivia: true)
                    .Where(trivia => trivia.Kind() is SyntaxKind.SingleLineCommentTrivia or SyntaxKind.MultiLineCommentTrivia)
                    .Select(trivia => $"{file.Path}:{trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1} {trivia}")
            )
            .ToList();

        Assert.True(
            found.Count == 0,
            "suwgate.yml lets the contracts through comments-csharp only for their /// documentation, which the OpenAPI generator writes "
                + $"into contract/openapi.json; delete every other comment there: {string.Join(", ", found)}"
        );
    }
}
