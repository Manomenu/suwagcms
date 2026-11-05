namespace Suwagcms.Cli.Tests.Plugin;

public sealed class MainSkillContentTests
{
    // Why: what the skill must say, as the section that says it and the words that section must hold.
    private static readonly (string Topic, string Section, string[] Words)[] Required =
    [
        ("how to install the CLI", "Install", ["dotnet tool install -g Suwagcms.Cli"]),
        ("how to pick the server", "Server and bot", ["--server", "SUWAGCMS_URL", "suwagcms server <url>"]),
        ("how to pick the bot", "Server and bot", ["--bot", "SUWAGCMS_BOT", "suwagcms use <id>"]),
        ("to use --json", "Output", ["--json", "one JSON document"]),
        ("how to read a refusal", "Refusals", ["Exit code `1`", "mistakes", "fix"]),
        (
            "how to link an article and find its target",
            "Link to an article",
            ["cms://article/<id>", "articles list --text", "reference.missing", "reference.malformed", "reference.loop"]
        ),
        (
            "the form of a link to a procedure, that it belongs only in llmGuide and how to find a target",
            "Link to a procedure",
            ["cms://procedure/<id>", "and only there", "suwagcms procedures list", "link.procedure.notInGuide"]
        ),
        ("the form of the start block", "Procedure file", ["start: <procedure id>", "start.target.conversation"]),
        (
            "the form of the inform block and how it differs from say",
            "Procedure file",
            ["inform: { instruction: text", "Where\n  `say` sends a fixed text"]
        ),
        (
            "the form of context and when to name an article or write a query",
            "Procedure file",
            ["context: { articles: [id, ...], searchQueries: [text, ...] }", "Name an article when", "Write a query when"]
        ),
        ("the form of localFacts", "Procedure file", ["localFacts:", "one camelCase word", "fact.local.nameClash"]),
        (
            "the form of a pattern on a text fact and how to take it off",
            "Text patterns",
            ["pattern: { regex:", "means:", "tellCustomer:", "--no-pattern", "pattern: null", "fact.pattern.inUse", "other"]
        ),
        (
            "what --dry-run on an edit answers and who sees a type change",
            "What would this change break?",
            ["--dry-run", "what would this change break?", "Only the server sees a type change"]
        ),
        ("that resources are never edited as local files", "Never edit local files", ["never edited as a local file"]),
        (
            "how to show and edit the persona, the form of its file and that it reaches the bot only with a version",
            "Persona",
            ["suwagcms persona show", "suwagcms persona edit --file <file>", "personality: |", "whenOutOfScope:", "only with a version"]
        ),
        ("to compare before pushing", "Before a push", ["suwagcms versions status", "suwagcms diff <has> <latest>"]),
    ];

    private static string MainSkill() =>
        File.ReadAllText(Path.Combine(RepositoryRoot.Path, "claude-plugin", "skills", "suwagcms", "SKILL.md"));

    private static List<string> Missing(string skillText) =>
        [
            .. Required
                .Where(item => item.Words.Any(word => !SkillText.Section(skillText, item.Section).Contains(word, StringComparison.Ordinal)))
                .Select(item => $"{item.Topic} (a section \"## {item.Section}\" with: {string.Join(", ", item.Words)})"),
        ];

    [Fact]
    [Trait("criterion", "AC-claude-plugin-5")]
    public void The_main_skill_says_how_to_install_pick_the_server_and_the_bot_use_json_read_a_refusal_and_never_edit_local_files()
    {
        var missing = Missing(MainSkill());

        Assert.True(
            missing.Count == 0,
            $"The main skill, claude-plugin/skills/suwagcms/SKILL.md, does not say: {string.Join("; ", missing)}."
        );
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-12")]
    public void The_main_skill_says_how_to_show_and_edit_the_persona_the_form_of_its_file_and_that_it_reaches_the_bot_only_with_a_version() =>
        Assert.DoesNotContain(Missing(MainSkill()), item => item.StartsWith("how to show and edit the persona", StringComparison.Ordinal));

    [Fact]
    [Trait("criterion", "AC-article-links-12")]
    public void The_main_skill_gives_the_link_form_and_how_to_find_a_target() =>
        Assert.DoesNotContain(Missing(MainSkill()), item => item.StartsWith("how to link an article", StringComparison.Ordinal));

    [Fact]
    [Trait("criterion", "AC-article-procedure-links-11")]
    public void The_main_skill_gives_the_form_of_a_link_to_a_procedure_where_it_belongs_and_how_to_find_a_target() =>
        Assert.DoesNotContain(Missing(MainSkill()), item => item.StartsWith("the form of a link to a procedure", StringComparison.Ordinal));

    [Fact]
    [Trait("criterion", "AC-start-block-9")]
    public void The_main_skill_gives_the_form_of_the_start_block() =>
        Assert.DoesNotContain(Missing(MainSkill()), item => item.StartsWith("the form of the start block", StringComparison.Ordinal));

    [Fact]
    [Trait("criterion", "AC-inform-block-9")]
    public void The_main_skill_gives_the_form_of_the_inform_block_and_how_it_differs_from_say() =>
        Assert.DoesNotContain(Missing(MainSkill()), item => item.StartsWith("the form of the inform block", StringComparison.Ordinal));

    [Fact]
    [Trait("criterion", "AC-instruction-context-9")]
    public void The_main_skill_gives_the_form_of_context_and_when_to_name_an_article_or_write_a_query() =>
        Assert.DoesNotContain(Missing(MainSkill()), item => item.StartsWith("the form of context", StringComparison.Ordinal));

    [Fact]
    [Trait("criterion", "AC-edit-guard-16")]
    public void The_main_skill_says_what_a_dry_run_on_an_edit_answers_and_that_only_the_server_sees_a_type_change() =>
        Assert.DoesNotContain(Missing(MainSkill()), item => item.StartsWith("what --dry-run on an edit", StringComparison.Ordinal));

    [Fact]
    [Trait("criterion", "AC-fact-pattern-14")]
    public void The_main_skill_gives_the_form_of_a_pattern_and_how_to_take_it_off() =>
        Assert.DoesNotContain(Missing(MainSkill()), item => item.StartsWith("the form of a pattern", StringComparison.Ordinal));

    [Fact]
    [Trait("criterion", "AC-local-facts-13")]
    public void The_main_skill_gives_the_form_of_local_facts() =>
        Assert.DoesNotContain(Missing(MainSkill()), item => item.StartsWith("the form of localFacts", StringComparison.Ordinal));

    [Fact]
    [Trait("criterion", "AC-claude-plugin-7")]
    public void The_main_skill_shows_the_comparison_of_what_the_bot_has_with_the_latest_before_a_push() =>
        Assert.DoesNotContain(Missing(MainSkill()), item => item.StartsWith("to compare before pushing", StringComparison.Ordinal));

    [Fact]
    [Trait("criterion", "AC-claude-plugin-5")]
    public void A_skill_that_leaves_something_out_is_reported_with_what_it_leaves_out()
    {
        var missing = Missing("## Install\n\nRun `dotnet tool install -g Suwagcms.Cli`.\n\n## Output\n\nUse `--json`.\n");

        Assert.Equal(Required.Length - 1, missing.Count);
        Assert.DoesNotContain(missing, item => item.StartsWith("how to install", StringComparison.Ordinal));
        Assert.Contains(missing, item => item.StartsWith("to use --json", StringComparison.Ordinal));
        Assert.Contains(missing, item => item.StartsWith("how to read a refusal", StringComparison.Ordinal));
    }
}
