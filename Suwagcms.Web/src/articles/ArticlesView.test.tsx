import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { Router } from "wouter";

import { PageSizeContext } from "@/api/pagination";
import type { Mistake } from "@/api/client";

import type { Article, KnowledgeBase } from "./api";
import { ArticleFields } from "./ArticleFields";
import { emptyForm, fieldOf, HINTS, isDirty, bodyOf } from "./articleForm";
import { ArticlesView } from "./ArticlesView";

const bases: KnowledgeBase[] = [
    { id: "offer", title: "Offer", language: "pl", embedding: "default", results: 5, revision: 1 },
    { id: "forValuers", title: "For valuers", language: "en", embedding: "default", results: 5, revision: 1 },
];

const articles: Article[] = [
    {
        id: "priceList",
        knowledgeBase: "offer",
        language: "pl",
        title: "Price list",
        content: "What it costs",
        llmGuide: "Ask first",
        description: "What a valuation costs",
        revision: 1,
    },
];

function view(list: Article[], knowledgeBases = bases, filters = { knowledgeBase: "", language: "", text: "" }): string {
    return renderToStaticMarkup(
        <Router ssrPath="/bots/rzeczoznawca/articles">
            <PageSizeContext value={{ size: 20, failure: null }}>
                <ArticlesView
                    bot="rzeczoznawca"
                    articles={list}
                    bases={knowledgeBases}
                    filters={filters}
                    onFilters={() => undefined}
                    onAdd={() => undefined}
                    onEdit={() => undefined}
                    onLeave={() => undefined}
                />
            </PageSizeContext>
        </Router>,
    );
}

function mistake(code: string, path: string): Mistake {
    return {
        code,
        path,
        message: "the message",
        fix: "the fix",
        stage: "Save",
        resource: { kind: "Article", id: "priceList" },
        related: [],
    };
}

function fields(mistakes: Mistake[] = [], id = ""): string {
    return renderToStaticMarkup(
        <ArticleFields
            form={{ ...emptyForm("offer"), id }}
            bases={bases}
            mistakes={mistakes}
            idLocked={false}
            targets={[]}
            onOpenArticle={() => undefined}
            onChange={() => undefined}
        />,
    );
}

function fieldHtml(html: string, field: string): string {
    const start = html.indexOf(`data-field="${field}"`);
    const next = html.indexOf("data-field=", start + 1);
    return html.slice(start, next === -1 ? undefined : next);
}

describe("the list of articles and availability", () => {
    it("AC-availability-1: says which article is off, and one never touched is on", () => {
        const shown = (isOff: (id: string) => boolean) =>
            renderToStaticMarkup(
                <Router ssrPath="/bots/rzeczoznawca/articles">
                    <PageSizeContext value={{ size: 20, failure: null }}>
                        <ArticlesView
                            bot="rzeczoznawca"
                            articles={articles}
                            bases={bases}
                            isOff={isOff}
                            filters={{ knowledgeBase: "", language: "", text: "" }}
                            onFilters={() => undefined}
                            onAdd={() => undefined}
                            onEdit={() => undefined}
                            onLeave={() => undefined}
                        />
                    </PageSizeContext>
                </Router>,
            );

        expect(shown(() => true)).toContain(">off<");
        expect(shown(() => false)).toContain(">on<");
    });
});

describe("ArticlesView", () => {
    it("AC-articles-1: an article is listed with its title, id, base and description", () => {
        const html = view(articles);

        expect(html).toContain("Articles of rzeczoznawca");
        expect(html).toContain("Price list");
        expect(html).toContain("<td>offer</td><td>pl</td>");
        expect(html).toContain("What a valuation costs");
    });

    it("AC-articles-11: the list offers a filter by knowledge base and one by text", () => {
        const html = view(articles);

        expect(html).toContain("All knowledge bases");
        expect(html).toContain(">For valuers<");
        expect(html).toContain('aria-label="Search"');
    });

    it("AC-articles-11: an empty list says whether the filters or the bot have no article", () => {
        expect(view([])).toContain("No articles yet.");
        expect(view([], bases, { knowledgeBase: "offer", language: "", text: "" })).toContain("No article matches the filters.");
    });

    it("AC-articles-11: a bot with no knowledge base is sent to create one before an article", () => {
        const html = view([], []);

        expect(html).toContain("Create the first knowledge base");
        expect(html).toContain('href="/bots/rzeczoznawca/knowledge-bases"');
    });
});

describe("ArticleFields", () => {
    it("AC-articles-9: the tooltip of content, llmGuide and description says who reads the field and what it is for", () => {
        const html = fields();

        for (const field of ["content", "llmGuide", "description"] as const) {
            expect(fieldHtml(html, field)).toContain(`>${HINTS[field].replaceAll("'", "&#x27;")}<`);
        }
        expect(HINTS.content).toMatch(/customer reads it/);
        expect(HINTS.llmGuide).toMatch(/Only the model reads it/);
        expect(HINTS.description).toMatch(/search reads this/);
    });

    it("AC-articles-10: content, llmGuide and description are shown at the same time, in one row", () => {
        const html = fields();

        expect(html).toContain('data-field="content"');
        expect(html).toContain('data-field="llmGuide"');
        expect(html).toContain('data-field="description"');
        expect(html).toContain("md:grid-cols-3");
        expect(html.match(/<textarea/g)).toHaveLength(1);
        expect(html.match(/data-markdown-editor=/g)).toHaveLength(2);
    });

    it("AC-articles-2: each mistake is shown, with its fix, beside the field its path names and no other", () => {
        const html = fields([mistake("article.llmGuide.missing", "llmGuide"), mistake("article.title.missing", "title")]);

        expect(fieldHtml(html, "llmGuide")).toContain("the message the fix");
        // Why: the editor itself exists only in a browser, so the page marks its wrapper and the browser test checks the textbox.
        expect(fieldHtml(html, "llmGuide")).toContain('data-invalid="true"');
        expect(fieldHtml(html, "title")).toContain("the message the fix");
        expect(fieldHtml(html, "content")).not.toContain("the message");
        expect(fields()).not.toContain('aria-invalid="');
    });

    it("AC-articles-3: a taken id is shown beside the id and an unknown base beside the base", () => {
        expect(fieldOf(mistake("resource.exists", "revision"), "priceList")).toBe("id");
        expect(fieldOf(mistake("article.knowledgeBase.unknown", "knowledgeBase"), "priceList")).toBe("knowledgeBase");
        expect(fieldOf(mistake("save.invalid", "$"), "priceList")).toBeNull();
    });
});

describe("a refused loop", () => {
    const loop: Mistake = {
        ...mistake("reference.loop", "content"),
        message: "These links close a loop: discounts → pricing → discounts.",
        resource: { kind: "Article", id: "discounts" },
        related: [{ resource: { kind: "Article", id: "pricing" }, location: "llmGuide" }],
    };

    it("AC-loop-guard-1: the path is shown beside the field of the link of the article being edited, whichever article the mistake is on", () => {
        const onPricing = fields([loop], "pricing");
        const onDiscounts = fields([loop], "discounts");

        expect(fieldHtml(onPricing, "llmGuide")).toContain("discounts → pricing → discounts");
        expect(fieldHtml(onPricing, "content")).not.toContain("→");
        expect(fieldHtml(onDiscounts, "content")).toContain("discounts → pricing → discounts");
        expect(fieldHtml(onDiscounts, "llmGuide")).not.toContain("→");
    });

    it("AC-loop-guard-3: a loop of one article is shown beside the field that holds the link", () => {
        const self: Mistake = {
            ...mistake("reference.loop", "llmGuide"),
            message: "pricing → pricing",
            resource: { kind: "Article", id: "pricing" },
        };

        expect(fieldHtml(fields([self], "pricing"), "llmGuide")).toContain("pricing → pricing");
    });
});

describe("the article form", () => {
    it("AC-articles-4: the texts are sent exactly as typed, not trimmed or changed", () => {
        const form = { ...emptyForm("offer"), content: "  # Cennik\r\n\r\n\n", llmGuide: "\tzażółć\n\n", description: " d " };

        expect(bodyOf(form, emptyForm("offer"))).toMatchObject({
            content: "  # Cennik\r\n\r\n\n",
            llmGuide: "\tzażółć\n\n",
            description: " d ",
        });
    });

    it("AC-articles-4: a text the person did not change keeps its stored line breaks, which a textarea reports as \\n", () => {
        const initial = { ...emptyForm("offer"), content: "# Cennik\r\n\r\nText\r\n", llmGuide: "One\rTwo\n" };
        const untouched = { ...initial, content: "# Cennik\n\nText\n", llmGuide: "One\nTwo\n", title: "New title" };
        const edited = { ...untouched, content: "# Cennik\n\nChanged\n" };

        expect(bodyOf(untouched, initial)).toMatchObject({
            content: "# Cennik\r\n\r\nText\r\n",
            llmGuide: "One\rTwo\n",
            title: "New title",
        });
        expect(bodyOf(edited, initial).content).toBe("# Cennik\n\nChanged\n");
    });

    it("AC-articles-13: a form is dirty from the first change and clean again when it matches what it started with", () => {
        const initial = emptyForm("offer");

        expect(isDirty(initial, initial)).toBe(false);
        expect(isDirty({ ...initial, title: "x" }, initial)).toBe(true);
        expect(isDirty({ ...initial, content: "x" }, initial)).toBe(true);
        expect(isDirty({ ...initial, title: "x" }, { ...initial, title: "x" })).toBe(false);
    });
});

describe("the language of an article", () => {
    it("AC-knowledge-base-language-12: the article form shows the language of the chosen base as read-only text, not as a control", () => {
        const html = fieldHtml(fields(), "language");

        expect(html).toMatch(/<output[^>]*>pl<\/output>/);
        expect(html).not.toContain("<select");
        expect(html).not.toContain("<input");
    });

    it("AC-knowledge-base-language-12: the text follows the base selector", () => {
        const html = renderToStaticMarkup(
            <ArticleFields
                form={{ ...emptyForm("forValuers"), id: "guide" }}
                bases={bases}
                mistakes={[]}
                idLocked
                targets={[]}
                onOpenArticle={() => undefined}
                onChange={() => undefined}
            />,
        );

        expect(fieldHtml(html, "language")).toMatch(/<output[^>]*>en<\/output>/);
    });

    it("AC-knowledge-base-language-12: the tooltip says the language comes from the knowledge base", () => {
        const html = fieldHtml(fields(), "language");

        expect(html).toContain(`>${HINTS.language}<`);
        expect(HINTS.language).toMatch(/language of the knowledge base the article is in/);
        expect(HINTS.language).toMatch(/pick the right articles/);
    });

    it("AC-knowledge-base-language-7: the body of a save carries no language", () => {
        expect(bodyOf(emptyForm("offer"), emptyForm("offer"))).not.toHaveProperty("language");
        expect(emptyForm("offer")).not.toHaveProperty("language");
    });

    it("AC-knowledge-base-language-9: the list shows the language of each article and offers a language filter with pl and en", () => {
        const html = view(articles);

        expect(html).toContain("<td>pl</td>");
        expect(html).toContain("All languages");
        expect(html).toContain('aria-label="Language"');
        expect(html.match(/<option value="(pl|en)">/g)).toEqual(['<option value="pl">', '<option value="en">']);
    });

    it("AC-knowledge-base-language-9: an empty list under a language filter says that no article matches", () => {
        expect(view([], bases, { knowledgeBase: "", language: "en", text: "" })).toContain("No article matches the filters.");
    });
});

describe("a long list", () => {
    it("AC-pagination-9: page 2 of 45 at 20 a page shows 20 rows and the shared controls", () => {
        const many = Array.from({ length: 45 }, (_, index) => ({ ...articles[0], id: `article${String(index)}` }) as Article);
        const html = renderToStaticMarkup(
            <Router ssrPath="/bots/b/articles?page=2">
                <PageSizeContext value={{ size: 20, failure: null }}>
                    <ArticlesView
                        bot="b"
                        articles={many}
                        bases={bases}
                        filters={{ knowledgeBase: "", language: "", text: "" }}
                        onFilters={() => undefined}
                        onAdd={() => undefined}
                        onEdit={() => undefined}
                        onLeave={() => undefined}
                    />
                </PageSizeContext>
            </Router>,
        );

        expect(/<tbody>[\s\S]*<\/tbody>/.exec(html)?.[0].match(/<tr>/g)).toHaveLength(20);
        expect(html).toContain("Page 2 of 3");
    });
});
