import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { Router } from "wouter";

import type { Mistake } from "@/api/client";
import { PageSizeContext } from "@/api/pagination";

import type { KnowledgeBase } from "./api";
import { HINTS, emptyForm, fieldOf, mistakesFor, saveOf } from "./knowledgeBaseForm";
import { KnowledgeBaseForm } from "./KnowledgeBaseForm";
import { KnowledgeBasesView } from "./KnowledgeBasesView";

const bases: KnowledgeBase[] = [
    { id: "oferta", title: "Our offer", language: "pl", embedding: "default", results: 5, revision: 2 },
    { id: "dlaRzeczoznawcow", title: "For valuers", language: "en", embedding: "default", results: 5, revision: 1 },
];

function view(list: KnowledgeBase[]): string {
    return renderToStaticMarkup(
        <Router ssrPath="/bots/rzeczoznawca/knowledge-bases">
            <PageSizeContext value={{ size: 20, failure: null }}>
                <KnowledgeBasesView bot="rzeczoznawca" bases={list} onAdd={() => undefined} onEdit={() => undefined} />
            </PageSizeContext>
        </Router>,
    );
}

function mistake(code: string, path: string, resource: string): Mistake {
    return { code, path, message: "m", fix: "f", stage: "Save", resource: { kind: "KnowledgeBase", id: resource }, related: [] };
}

describe("KnowledgeBasesView", () => {
    it("AC-knowledge-bases-1: every base is listed with its title and id", () => {
        const html = view(bases);

        expect(html).toContain("Knowledge bases of rzeczoznawca");
        expect(html).toContain("Our offer");
        expect(html).toContain("oferta");
        expect(html).toContain("For valuers");
        expect(html).toContain("dlaRzeczoznawcow");
    });

    it("AC-knowledge-bases-7: a bot with no bases shows a screen offering to create the first one", () => {
        const html = view([]);

        expect(html).toContain("No knowledge bases yet.");
        expect(html).toContain("Create the first one");
        expect(html).toContain("Add a knowledge base");
        expect(html).not.toContain(">Edit<");
    });
});

describe("the language of a knowledge base", () => {
    function form(): string {
        return renderToStaticMarkup(
            <KnowledgeBaseForm
                bot="rzeczoznawca"
                initial={emptyForm()}
                editing={false}
                revision={0}
                onSaved={() => undefined}
                onReload={() => undefined}
                onCancel={() => undefined}
                onDirtyChange={() => undefined}
            />,
        );
    }

    it("AC-knowledge-base-language-2: the select of a new base offers pl and en and starts on en", () => {
        const html = form();
        const field = html.slice(html.indexOf('data-field="language"'));

        expect(field).toContain('<option value="en" selected="">en</option>');
        expect(field).toContain('<option value="pl">pl</option>');
        expect(field.match(/<option/g)).toHaveLength(2);
        expect(emptyForm().language).toBe("en");
    });

    it("AC-knowledge-base-language-2: the saved language of each base is listed", () => {
        const html = view(bases);

        expect(html).toContain("<td>pl</td>");
        expect(html).toContain("<td>en</td>");
    });

    it("AC-knowledge-base-language-11: the tooltip of the language select says what the field is for", () => {
        expect(form()).toContain(`>${HINTS.language}<`);
        expect(HINTS.language).toMatch(/language of every article in this base/);
        expect(HINTS.language).toMatch(/pick the right articles/);
    });

    it("AC-web-shell-12: each field of the form has its help", () => {
        const html = form();

        expect(html.match(/>About /g)?.length).toBe(html.match(/data-field=/g)?.length);
    });

    it("AC-knowledge-base-language-4: a refused language is shown beside the language select", () => {
        expect(fieldOf(mistake("knowledgeBase.language.unknown", "language", "oferta"))).toBe("language");
    });
});

describe("the embedding profile and results of a knowledge base", () => {
    function form(): string {
        return renderToStaticMarkup(
            <KnowledgeBaseForm
                bot="rzeczoznawca"
                initial={emptyForm()}
                editing={false}
                revision={0}
                onSaved={() => undefined}
                onReload={() => undefined}
                onCancel={() => undefined}
                onDirtyChange={() => undefined}
            />,
        );
    }

    it("AC-knowledge-bases-13: a new base starts on the profile default and 5 results, each field with its tooltip", () => {
        const html = form();
        const embedding = html.slice(html.indexOf('data-field="embedding"'), html.indexOf('data-field="results"'));
        const results = html.slice(html.indexOf('data-field="results"'));

        expect(embedding).toContain('value="default"');
        expect(embedding).toContain("Embedding profile");
        expect(embedding).toContain(`>${HINTS.embedding}<`);
        expect(results).toContain('value="5"');
        expect(results).toContain("Results per search (1–20)");
        expect(results).toContain(`>${HINTS.results}<`);
        expect(HINTS.embedding).toMatch(/set on the bot, never here/);
    });

    it("AC-knowledge-bases-15: a refused profile or results is shown beside its field, and results that are no number are sent as none", () => {
        expect(fieldOf(mistake("knowledgeBase.embedding.missing", "embedding", "a"))).toBe("embedding");
        expect(fieldOf(mistake("knowledgeBase.results.outOfRange", "results", "a"))).toBe("results");
        expect(saveOf({ ...emptyForm(), id: "a", title: "A", results: "x" }).results).toBeNull();
        expect(saveOf({ ...emptyForm(), id: "a", title: "A", embedding: "legalText", results: " 12 " })).toEqual({
            title: "A",
            language: "en",
            embedding: "legalText",
            results: 12,
        });
    });
});

describe("the knowledge base form's mistakes", () => {
    it("AC-knowledge-bases-4: a mistake goes beside the field its path names, and a taken id beside the id", () => {
        expect(fieldOf(mistake("knowledgeBase.title.missing", "title", "a"))).toBe("title");
        expect(fieldOf(mistake("knowledgeBase.id.invalid", "id", "a"))).toBe("id");
        expect(fieldOf(mistake("resource.exists", "revision", "a"))).toBe("id");
        expect(fieldOf(mistake("save.invalid", "$", "a"))).toBeNull();
    });

    it("AC-knowledge-bases-4: a repeated title reported for another base is not shown on this form", () => {
        const both = [
            mistake("knowledgeBase.title.repeated", "title", "oferta"),
            mistake("knowledgeBase.title.repeated", "title", "second"),
        ];

        expect(mistakesFor(both, "second")).toEqual([both[1]]);
    });
});

describe("a long list", () => {
    it("AC-pagination-9: page 2 of 45 at 20 a page shows 20 rows and the shared controls", () => {
        const many = Array.from({ length: 45 }, (_, index) => ({
            id: `base${String(index)}`,
            title: "A base",
            language: "pl",
            embedding: "default",
            results: 5,
            revision: 1,
        }));
        const html = renderToStaticMarkup(
            <Router ssrPath="/bots/b/knowledge-bases?page=2">
                <PageSizeContext value={{ size: 20, failure: null }}>
                    <KnowledgeBasesView bot="b" bases={many} onAdd={() => undefined} onEdit={() => undefined} />
                </PageSizeContext>
            </Router>,
        );

        expect(/<tbody>[\s\S]*<\/tbody>/.exec(html)?.[0].match(/<tr>/g)).toHaveLength(20);
        expect(html).toContain("Page 2 of 3");
    });
});
