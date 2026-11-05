import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { Router } from "wouter";

import type { Mistake } from "@/api/client";

import type { Article, KnowledgeBase } from "./api";
import { ArticleFields } from "./ArticleFields";
import { emptyForm, fieldOf, HINTS, isDirty, bodyOf } from "./articleForm";
import { ArticlesView } from "./ArticlesView";

const bases: KnowledgeBase[] = [
    { id: "offer", title: "Offer", revision: 1 },
    { id: "forValuers", title: "For valuers", revision: 1 },
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
            <ArticlesView
                bot="rzeczoznawca"
                articles={list}
                bases={knowledgeBases}
                filters={filters}
                form={null}
                onFilters={() => undefined}
                onAdd={() => undefined}
                onEdit={() => undefined}
                onDelete={() => undefined}
                onLeave={() => undefined}
            />
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

function fields(mistakes: Mistake[] = []): string {
    return renderToStaticMarkup(
        <ArticleFields
            form={emptyForm("offer")}
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

describe("ArticlesView", () => {
    it("AC-articles-1: an article is listed with its title, id, base and description", () => {
        const html = view(articles);

        expect(html).toContain("Articles of rzeczoznawca");
        expect(html).toContain("Price list");
        expect(html).toContain("priceList · offer · pl");
        expect(html).toContain("What a valuation costs");
    });

    it("AC-articles-8: each article has a delete button and an edit button", () => {
        const html = view(articles);

        expect(html.match(/>Delete</g)).toHaveLength(1);
        expect(html.match(/>Edit</g)).toHaveLength(1);
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

    it("a bot with no knowledge base is sent to create one before an article", () => {
        const html = view([], []);

        expect(html).toContain("Create the first knowledge base");
        expect(html).toContain('href="/bots/rzeczoznawca/knowledge-bases"');
    });
});

describe("ArticleFields", () => {
    it("AC-articles-9: the tooltip of content, llmGuide and description says who reads the field and what it is for", () => {
        const html = fields();

        for (const field of ["content", "llmGuide", "description"] as const) {
            expect(fieldHtml(html, field)).toContain(`title="${HINTS[field].replaceAll("'", "&#x27;")}"`);
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
        expect(fieldOf(mistake("resource.exists", "revision"))).toBe("id");
        expect(fieldOf(mistake("article.knowledgeBase.unknown", "knowledgeBase"))).toBe("knowledgeBase");
        expect(fieldOf(mistake("save.invalid", "$"))).toBeNull();
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
    it("AC-article-language-2: the select of a new article offers pl and en and starts on en", () => {
        const html = fieldHtml(fields(), "language");

        expect(html).toContain('<option value="en" selected="">en</option>');
        expect(html).toContain('<option value="pl">pl</option>');
        expect(html.match(/<option/g)).toHaveLength(2);
        expect(emptyForm("offer").language).toBe("en");
    });

    it("AC-article-language-2: an article's own language is the one selected and the one listed", () => {
        const html = renderToStaticMarkup(
            <ArticleFields
                form={{ ...emptyForm("offer"), language: "pl" }}
                bases={bases}
                mistakes={[]}
                idLocked
                targets={[]}
                onOpenArticle={() => undefined}
                onChange={() => undefined}
            />,
        );

        expect(fieldHtml(html, "language")).toContain('<option value="pl" selected="">pl</option>');
        expect(view(articles)).toContain("· pl");
        expect(bodyOf({ ...emptyForm("offer"), language: "pl" }, emptyForm("offer")).language).toBe("pl");
    });

    it("AC-article-language-8: the tooltip of the language select says what the field is for", () => {
        const html = fieldHtml(fields(), "language");

        expect(html).toContain(`title="${HINTS.language}"`);
        expect(HINTS.language).toMatch(/language the article is written in/);
        expect(HINTS.language).toMatch(/pick the right articles/);
    });

    it("AC-article-language-4: a refused language is shown beside the language select", () => {
        const html = fields([mistake("article.language.unknown", "language")]);

        expect(fieldOf(mistake("article.language.unknown", "language"))).toBe("language");
        expect(fieldHtml(html, "language")).toContain("the message the fix");
    });

    it("AC-article-language-6: the list offers a language filter with pl and en", () => {
        const html = view(articles);

        expect(html).toContain("All languages");
        expect(html).toContain('aria-label="Language"');
    });

    it("AC-article-language-6: an empty list under a language filter says that no article matches", () => {
        expect(view([], bases, { knowledgeBase: "", language: "en", text: "" })).toContain("No article matches the filters.");
    });
});
