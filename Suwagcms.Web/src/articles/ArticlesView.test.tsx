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
        title: "Price list",
        content: "What it costs",
        llmGuide: "Ask first",
        description: "What a valuation costs",
        revision: 1,
    },
];

function view(list: Article[], knowledgeBases = bases, filters = { knowledgeBase: "", text: "" }): string {
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
        <ArticleFields form={emptyForm("offer")} bases={bases} mistakes={mistakes} idLocked={false} onChange={() => undefined} />,
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
        expect(html).toContain("priceList · offer");
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
        expect(view([], bases, { knowledgeBase: "offer", text: "" })).toContain("No article matches the filters.");
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
        expect(html.match(/<textarea/g)).toHaveLength(3);
    });

    it("AC-articles-2: each mistake is shown, with its fix, beside the field its path names and no other", () => {
        const html = fields([mistake("article.llmGuide.missing", "llmGuide"), mistake("article.title.missing", "title")]);

        expect(fieldHtml(html, "llmGuide")).toContain("the message the fix");
        expect(fieldHtml(html, "llmGuide")).toContain('aria-invalid="true"');
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

        expect(bodyOf(form)).toMatchObject({ content: "  # Cennik\r\n\r\n\n", llmGuide: "\tzażółć\n\n", description: " d " });
    });

    it("AC-articles-13: a form is dirty from the first change and clean again when it matches what it started with", () => {
        const initial = emptyForm("offer");

        expect(isDirty(initial, initial)).toBe(false);
        expect(isDirty({ ...initial, title: "x" }, initial)).toBe(true);
        expect(isDirty({ ...initial, content: "x" }, initial)).toBe(true);
        expect(isDirty({ ...initial, title: "x" }, { ...initial, title: "x" })).toBe(false);
    });
});
