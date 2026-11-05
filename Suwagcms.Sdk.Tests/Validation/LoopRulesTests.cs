using System.Diagnostics;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation;

public sealed class LoopRulesTests
{
    private static string Link(string id) => $"[{id}](cms://article/{id})";

    private static Article Linking(string id, string content = "Text.", string llmGuide = "Ask first.") =>
        Samples.Pricing with
        {
            Id = id,
            Content = content,
            LlmGuide = llmGuide,
        };

    private static BotDefinition Bot(params Article[] articles) =>
        new()
        {
            FormatVersion = 1,
            KnowledgeBases = [Samples.Faq],
            Articles = articles,
        };

    private static List<Mistake> Loops(BotDefinition definition) =>
        [.. DefinitionValidator.Validate(definition, ValidationStage.Save).Where(mistake => mistake.Code == MistakeCodes.ReferenceLoop)];

    private static List<Mistake> Loops(params Article[] articles) => Loops(Bot(articles));

    [Fact]
    [Trait("criterion", "AC-loop-guard-2")]
    public void A_loop_of_two_is_refused_with_the_whole_path_starting_and_ending_on_the_same_article()
    {
        var mistake = Assert.Single(Loops(Linking("pricing", Link("discounts")), Linking("discounts", "Text.", Link("pricing"))));

        Assert.Equal(MistakeCodes.ReferenceLoop, mistake.Code);
        Assert.Equal(ValidationStage.Save, mistake.Stage);
        Assert.Contains("discounts → pricing → discounts", mistake.Message, StringComparison.Ordinal);
        Assert.Equal(new ResourceRef(ResourceKind.Article, "discounts"), mistake.Resource);
        Assert.Equal("llmGuide", mistake.Path);
        Assert.Equal([new RelatedResource(new ResourceRef(ResourceKind.Article, "pricing"), "content")], mistake.Related);
        Assert.Contains("Remove the link from 'discounts' to 'pricing'", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-loop-guard-2")]
    public void A_longer_loop_is_shown_in_link_order_and_the_related_list_gives_each_other_link_with_its_field()
    {
        var mistake = Assert.Single(
            Loops(Linking("c", Link("a")), Linking("a", Link("b")), Linking("b", "Text.", Link("c")), Linking("outside", Link("a")))
        );

        Assert.Contains("a → b → c → a", mistake.Message, StringComparison.Ordinal);
        Assert.Equal([("b", "llmGuide"), ("c", "content")], mistake.Related.Select(related => (related.Resource.Id, related.Location)));
    }

    [Theory]
    [Trait("criterion", "AC-loop-guard-3")]
    [InlineData("content")]
    [InlineData("llmGuide")]
    public void An_article_linking_to_itself_is_refused_in_either_field(string field)
    {
        var self = field == "content" ? Linking("pricing", Link("pricing")) : Linking("pricing", "Text.", Link("pricing"));

        var mistake = Assert.Single(Loops(self));

        Assert.Contains("pricing → pricing", mistake.Message, StringComparison.Ordinal);
        Assert.Equal(field, mistake.Path);
        Assert.Empty(mistake.Related);
    }

    [Fact]
    [Trait("criterion", "AC-loop-guard-4")]
    public void A_loop_running_through_a_content_and_then_an_llm_guide_is_refused()
    {
        var mistakes = Loops(Linking("a", Link("b")), Linking("b", "Text.", Link("a")));

        Assert.Equal("content", Assert.Single(mistakes).Path);
        Assert.Equal("llmGuide", Assert.Single(mistakes[0].Related).Location);
    }

    [Fact]
    [Trait("criterion", "AC-loop-guard-5")]
    public void Several_loops_in_one_definition_are_each_reported_once_with_their_own_path()
    {
        var mistakes = Loops(
            Linking("a", Link("b") + Link("c")),
            Linking("b", Link("a")),
            Linking("c", Link("a")),
            Linking("x", Link("y")),
            Linking("y", Link("z")),
            Linking("z", Link("x")),
            Linking("solo", Link("solo"))
        );

        Assert.Equal(
            ["a → b → a", "a → c → a", "solo → solo", "x → y → z → x"],
            mistakes.Select(mistake => mistake.Message["These links close a loop: ".Length..].TrimEnd('.')).Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    [Trait("criterion", "AC-loop-guard-7")]
    public void A_shared_target_a_diamond_and_a_long_chain_are_not_loops()
    {
        var chain = Enumerable.Range(0, 200).Select(number => Linking($"n{number}", Link($"n{number + 1}"))).Append(Linking("n200"));

        Assert.Empty(Loops(Linking("a", Link("c")), Linking("b", Link("c")), Linking("c")));
        Assert.Empty(
            Loops(Linking("top", Link("left") + Link("right")), Linking("left", Link("end")), Linking("right", Link("end")), Linking("end"))
        );
        Assert.Empty(Loops([.. chain]));
    }

    [Fact]
    [Trait("criterion", "AC-loop-guard-10")]
    public void The_offline_validator_reports_a_loop_in_a_json_definition_with_the_same_code_and_path()
    {
        const string json = """
            {"formatVersion":1,"knowledgeBases":[{"id":"faq","title":"Questions"}],"articles":[
            {"id":"pricing","knowledgeBase":"faq","title":"T","content":"[d](cms://article/discounts)","llmGuide":"G","description":"D"},
            {"id":"discounts","knowledgeBase":"faq","title":"T","content":"C","llmGuide":"[p](cms://article/pricing)","description":"D"}]}
            """;

        var mistake = Assert.Single(DefinitionValidator.ValidateJson(json, ValidationStage.Save));

        Assert.Equal(MistakeCodes.ReferenceLoop, mistake.Code);
        Assert.Contains("discounts → pricing → discounts", mistake.Message, StringComparison.Ordinal);
        Assert.Equal(
            Assert.Single(Loops(Linking("pricing", Link("discounts")), Linking("discounts", "Text.", Link("pricing")))).Message,
            mistake.Message
        );
    }

    [Fact]
    public void The_same_definition_always_gives_the_same_path_whatever_the_order_of_its_articles()
    {
        var articles = new[] { Linking("c", Link("a")), Linking("a", Link("b")), Linking("b", Link("c")) };

        var messages = new[] { articles, [.. articles.Reverse()], [articles[1], articles[2], articles[0]] }
            .Select(order => Assert.Single(Loops(order)).Message)
            .Distinct()
            .ToList();

        Assert.Equal(["These links close a loop: a → b → c → a."], messages);
    }

    [Fact]
    public void Loops_between_the_steps_of_one_procedure_and_facts_are_not_loops_between_resources()
    {
        var procedure = Samples.Procedure("conversation") with
        {
            FirstStep = "listen",
            Steps = new Dictionary<string, ProcedureStep>
            {
                ["listen"] = new()
                {
                    Say = "Hello",
                    Next = new NextSteps { Step = "answer" },
                },
                ["answer"] = new()
                {
                    Say = "Answer",
                    Next = new NextSteps { Step = "listen" },
                },
            },
        };

        Assert.Empty(Loops(Bot(Linking("a", Link("b")), Linking("b")) with { Procedures = [procedure] }));
    }

    [Fact]
    public void A_missing_target_is_not_a_loop() => Assert.Empty(Loops(Linking("a", Link("ghost"))));

    [Fact]
    public void The_graph_function_finds_loops_in_any_graph_so_later_kinds_of_edges_need_no_change_to_the_rule()
    {
        var edges = new Dictionary<string, List<string>>
        {
            ["article:guide"] = ["procedure:evacuation"],
            ["procedure:evacuation"] = ["article:guide", "procedure:evacuation"],
            ["article:other"] = ["article:guide"],
        };

        var cycles = LoopRules.Cycles(edges, StringComparer.Ordinal);

        Assert.Equal(
            ["article:guide > procedure:evacuation", "procedure:evacuation"],
            cycles.Select(cycle => string.Join(" > ", cycle)).Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public void A_chain_of_5000_articles_closed_into_a_ring_is_found_in_one_pass_within_the_limit()
    {
        const int count = 5000;
        var ring = Enumerable.Range(0, count).Select(number => Linking($"a{number:D5}", Link($"a{(number + 1) % count:D5}"))).ToArray();
        var open = ring.Take(count - 1).Append(Linking($"a{count - 1:D5}")).ToArray();

        var started = Stopwatch.GetTimestamp();
        var closed = Loops(ring);
        var none = Loops(open);
        var elapsed = Stopwatch.GetElapsedTime(started);

        Assert.Equal(count, Assert.Single(closed).Related.Count + 1);
        Assert.Empty(none);
        Assert.True(
            elapsed < TimeSpan.FromSeconds(5),
            $"The loop rule took {elapsed.TotalMilliseconds:F0} ms for {count} articles; the limit is 5000 ms."
        );
    }

    [Fact]
    [Trait("criterion", "AC-loop-guard-9")]
    public void Whatever_sequence_of_link_edits_is_tried_the_accepted_articles_never_loop_and_can_all_be_deleted_in_some_order()
    {
        const int articles = 8;
        for (var seed = 1; seed <= 25; seed++)
        {
            // Why: a fixed generator, so a failing seed is the same on every machine.
            var state = (uint)seed;
            int Draw()
            {
                state = unchecked((state * 1664525) + 1013904223);
                return (int)((state >> 16) % articles);
            }
            var content = Enumerable.Range(0, articles).ToDictionary(number => number, _ => new HashSet<int>());
            for (var step = 0; step < 300; step++)
            {
                var from = Draw();
                var to = Draw();
                var attempt = content.ToDictionary(entry => entry.Key, entry => new HashSet<int>(entry.Value));
                if (!attempt[from].Remove(to))
                {
                    attempt[from].Add(to);
                }

                if (Loops(Written(attempt)).Count == 0)
                {
                    content = attempt;
                }
            }

            Assert.True(CanAllBeDeleted(content), $"Seed {seed}: the accepted articles cannot all be deleted in some order.");
        }

        static Article[] Written(Dictionary<int, HashSet<int>> links)
        {
            return
            [
                .. links.Select(entry => Linking($"a{entry.Key}", string.Concat(entry.Value.Order().Select(target => Link($"a{target}"))))),
            ];
        }

        // Why: independent of the rule under test: an article nobody refers to may go, then the next one, until none is left.
        static bool CanAllBeDeleted(Dictionary<int, HashSet<int>> links)
        {
            var left = links.Keys.ToHashSet();
            while (left.Count > 0)
            {
                var free = left.Where(candidate => !left.Any(other => links[other].Contains(candidate))).ToList();
                if (free.Count == 0)
                {
                    return false;
                }
                left.Remove(free[0]);
            }
            return true;
        }
    }
}
