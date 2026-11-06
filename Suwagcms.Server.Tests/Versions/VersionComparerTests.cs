using Suwagcms.Contracts.Facts;
using Suwagcms.Contracts.Versions;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Server.Versions;

namespace Suwagcms.Server.Tests.Versions;

public sealed class VersionComparerTests
{
    private static string Document(
        string facts = "",
        string knowledgeBases = "",
        string articles = "",
        string procedures = "",
        int number = 1,
        int format = 1,
        string digest = "aaa"
    ) =>
        $$"""
            {"formatVersion":{{format}},"bot":"rzeczoznawca","number":{{number}},"digest":"{{digest}}","facts":[{{facts}}],"knowledgeBases":[{{knowledgeBases}}],"articles":[{{articles}}],"actions":[],"procedures":[{{procedures}}]}
            """;

    private static VersionComparison Compare(string before, string after) => VersionComparer.Compare("rzeczoznawca", 12, 14, before, after);

    private static string Article(string id, string content = "Text", string guide = "Guide", string title = "T") =>
        $$"""{"id":"{{id}}","knowledgeBase":"offer","title":"{{title}}","content":"{{content}}","llmGuide":"{{guide}}","description":"D"}""";

    private const string Procedure = """
        {"id":"orderValuation","description":"Old","firstStep":"ask",
         "localFacts":[{"name":"visitDate","type":"text","description":"When","maxLength":10},{"name":"gone","type":"text","description":"G","maxLength":1}],
         "steps":{"ask":{"say":"Hello","next":"gone"},"gone":{"say":"Bye"}},"whenStalled":{"instruction":"Old way"}}
        """;

    private const string ProcedureChanged = """
        {"id":"orderValuation","description":"New","firstStep":"ask",
         "localFacts":[{"name":"visitDate","type":"text","description":"When","maxLength":20},{"name":"born","type":"text","description":"B","maxLength":1}],
         "steps":{"ask":{"say":"Hi","next":"fresh"},"fresh":{"say":"Hey"}},"whenStalled":{"instruction":"New way"}}
        """;

    [Fact]
    [Trait("criterion", "AC-version-diff-1")]
    public void It_lists_added_removed_and_changed_resources_by_kind_and_no_unchanged_one()
    {
        var comparison = Compare(
            Document(
                facts: """{"name":"same","type":"text","description":"d","maxLength":1}""",
                articles: Article("cennik", "Old") + "," + Article("stary") + "," + Article("zzz")
            ),
            Document(
                facts: """{"name":"same","type":"text","description":"d","maxLength":1}""",
                knowledgeBases: """{"id":"offer","title":"Offer","language":"en"}""",
                articles: Article("cennik", "New") + "," + Article("rabaty") + "," + Article("zzz")
            )
        );

        Assert.Equal((1 + 1, 1, 1), (comparison.Added, comparison.Removed, comparison.Changed));
        Assert.Equal(
            [
                (ResourceKind.KnowledgeBase, "offer", Difference.Added),
                (ResourceKind.Article, "cennik", Difference.Changed),
                (ResourceKind.Article, "rabaty", Difference.Added),
                (ResourceKind.Article, "stary", Difference.Removed),
            ],
            comparison.Resources.Select(resource => (resource.Kind, resource.Id, resource.Difference))
        );
    }

    [Fact]
    [Trait("criterion", "AC-version-diff-2")]
    public void A_changed_fact_lists_each_changed_field_with_its_old_and_new_values()
    {
        var comparison = Compare(
            Document(facts: """{"name":"customer.city","type":"text","description":"Old","maxLength":80}"""),
            Document(facts: """{"name":"customer.city","type":"text","description":"Old","maxLength":120,"writableBy":"correct"}""")
        );

        var fact = Assert.Single(comparison.Resources);
        Assert.Equal(new ResourceRef(ResourceKind.GlobalFact, "customer.city"), new ResourceRef(fact.Kind, fact.Id));
        Assert.Equal(
            [("maxLength", "80", "120"), ("writableBy", null, "correct")],
            fact.Fields.Select(field => (field.Field, field.Old, field.New))
        );
    }

    [Fact]
    [Trait("criterion", "AC-version-diff-2")]
    public void An_article_lists_content_and_guide_as_lines_removed_and_added_and_other_fields_with_their_values()
    {
        var comparison = Compare(
            Document(articles: Article("cennik", "Wycena domu: 900 zł\\nRabat", "Be brief", "Old title")),
            Document(articles: Article("cennik", "Wycena domu: 1100 zł\\nRabat", "Be kind", "New title"))
        );

        var article = Assert.Single(comparison.Resources);
        Assert.Equal(["content", "llmGuide", "title"], article.Fields.Select(field => field.Field));
        var content = article.Fields[0];
        Assert.Null(content.Old);
        Assert.Equal(
            [
                new TextLine(LineChange.Removed, "Wycena domu: 900 zł"),
                new TextLine(LineChange.Added, "Wycena domu: 1100 zł"),
                new TextLine(LineChange.Kept, "Rabat"),
            ],
            content.Lines
        );
        Assert.Equal([new TextLine(LineChange.Removed, "Be brief"), new TextLine(LineChange.Added, "Be kind")], article.Fields[1].Lines);
        Assert.Equal(("Old title", "New title", null), (article.Fields[2].Old, article.Fields[2].New, article.Fields[2].Lines));
    }

    [Fact]
    [Trait("criterion", "AC-version-diff-2")]
    public void A_long_text_shows_the_changed_lines_with_a_few_around_them_and_folds_the_rest()
    {
        var lines = string.Join("\\n", Enumerable.Range(1, 20).Select(number => $"line {number}"));

        var comparison = Compare(
            Document(articles: Article("cennik", lines)),
            Document(articles: Article("cennik", lines.Replace("line 10\\n", "line ten\\n", StringComparison.Ordinal)))
        );

        var shown = Assert.Single(comparison.Resources).Fields[0].Lines ?? [];
        Assert.Equal(
            [
                new TextLine(LineChange.Folded, "7 unchanged lines"),
                new TextLine(LineChange.Kept, "line 8"),
                new TextLine(LineChange.Kept, "line 9"),
                new TextLine(LineChange.Removed, "line 10"),
                new TextLine(LineChange.Added, "line ten"),
                new TextLine(LineChange.Kept, "line 11"),
                new TextLine(LineChange.Kept, "line 12"),
                new TextLine(LineChange.Folded, "8 unchanged lines"),
            ],
            shown
        );
    }

    [Fact]
    [Trait("criterion", "AC-version-diff-2")]
    public void Two_whole_texts_too_long_to_align_still_read_as_lines_removed_and_added()
    {
        var old = string.Join("\n", Enumerable.Range(1, 3000).Select(number => $"old {number}"));
        var fresh = string.Join("\n", Enumerable.Range(1, 3000).Select(number => $"new {number}"));

        var lines = LineComparer.Compare(old, fresh);

        Assert.Equal(3000, lines.Count(line => line.Change == LineChange.Removed));
        Assert.Equal(3000, lines.Count(line => line.Change == LineChange.Added));
    }

    [Fact]
    [Trait("criterion", "AC-version-diff-3")]
    public void A_changed_procedure_lists_its_fields_its_local_facts_and_its_steps_with_what_changed_in_each()
    {
        var procedure = Assert.Single(Compare(Document(procedures: Procedure), Document(procedures: ProcedureChanged)).Resources);

        Assert.Equal(Difference.Changed, procedure.Difference);
        Assert.Equal(
            [("description", "Old", "New"), ("whenStalled.instruction", "Old way", "New way")],
            procedure.Fields.Select(field => (field.Field, field.Old, field.New))
        );
        Assert.Equal(
            [
                ("step", "ask", Difference.Changed),
                ("step", "fresh", Difference.Added),
                ("step", "gone", Difference.Removed),
                ("local fact", "born", Difference.Added),
                ("local fact", "gone", Difference.Removed),
                ("local fact", "visitDate", Difference.Changed),
            ],
            procedure.Parts.Select(part => (part.Part, part.Name, part.Difference))
        );
        Assert.Equal(
            [("next", "gone", "fresh"), ("say", "Hello", "Hi")],
            procedure.Parts[0].Fields.Select(field => (field.Field, field.Old, field.New))
        );
        Assert.Equal(
            ("maxLength", "10", "20"),
            (procedure.Parts[5].Fields[0].Field, procedure.Parts[5].Fields[0].Old, procedure.Parts[5].Fields[0].New)
        );
    }

    [Fact]
    [Trait("criterion", "AC-version-diff-6")]
    public void The_same_definition_under_other_numbers_and_digests_has_no_differences()
    {
        var comparison = Compare(
            Document(articles: Article("cennik"), number: 12, digest: "aaa"),
            Document(articles: Article("cennik"), number: 14, digest: "bbb")
        );

        Assert.Empty(comparison.Resources);
        Assert.Empty(comparison.Definition);
        Assert.Equal((0, 0, 0), (comparison.Added, comparison.Removed, comparison.Changed));
    }

    [Fact]
    [Trait("criterion", "AC-version-diff-7")]
    public void Swapping_the_two_documents_swaps_added_with_removed_and_old_values_with_new_ones()
    {
        var before = Document(
            facts: """{"name":"city","type":"text","description":"d","maxLength":80}""",
            articles: Article("cennik", "Old") + "," + Article("stary"),
            procedures: Procedure
        );
        var after = Document(
            facts: """{"name":"city","type":"text","description":"d","maxLength":120}""",
            articles: Article("cennik", "New") + "," + Article("rabaty"),
            procedures: ProcedureChanged
        );

        var forward = Compare(before, after);
        var back = Compare(after, before);

        Assert.Equal((forward.Added, forward.Removed), (back.Removed, back.Added));
        Assert.Equal(Difference.Added, forward.Resources.Single(resource => resource.Id == "rabaty").Difference);
        Assert.Equal(Difference.Removed, back.Resources.Single(resource => resource.Id == "rabaty").Difference);
        var there = forward.Resources.Single(resource => resource.Id == "city").Fields[0];
        var return_ = back.Resources.Single(resource => resource.Id == "city").Fields[0];
        Assert.Equal((there.Old, there.New), (return_.New, return_.Old));
        Assert.Equal(
            [new TextLine(LineChange.Removed, "New"), new TextLine(LineChange.Added, "Old")],
            back.Resources.Single(resource => resource.Id == "cennik").Fields[0].Lines
        );
        Assert.Equal(
            Difference.Added,
            back.Resources.Single(resource => resource.Id == "orderValuation")
                .Parts.Single(part => part.Name == "gone" && part.Part == "step")
                .Difference
        );
    }

    [Fact]
    [Trait("criterion", "AC-version-diff-1")]
    public void A_format_version_that_differs_is_listed_like_a_changed_field_and_the_rest_is_still_compared()
    {
        var comparison = Compare(Document(articles: Article("a", "Old"), format: 1), Document(articles: Article("a", "New"), format: 2));

        Assert.Equal(
            ("formatVersion", "1", "2"),
            (comparison.Definition[0].Field, comparison.Definition[0].Old, comparison.Definition[0].New)
        );
        Assert.Equal(1, comparison.Changed);
    }
}
