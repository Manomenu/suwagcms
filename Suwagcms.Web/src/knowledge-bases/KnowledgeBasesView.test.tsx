import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { Router } from "wouter";

import type { Mistake } from "@/api/client";

import type { KnowledgeBase } from "./api";
import { HINTS, emptyForm, fieldOf, mistakesFor } from "./knowledgeBaseForm";
import { KnowledgeBaseForm } from "./KnowledgeBaseForm";
import { KnowledgeBasesView } from "./KnowledgeBasesView";

const bases: KnowledgeBase[] = [
    { id: "oferta", title: "Our offer", language: "pl", revision: 2 },
    { id: "dlaRzeczoznawcow", title: "For valuers", language: "en", revision: 1 },
];

function view(list: KnowledgeBase[]): string {
    return renderToStaticMarkup(
        <Router ssrPath="/bots/rzeczoznawca/knowledge-bases">
            <KnowledgeBasesView
                bot="rzeczoznawca"
                bases={list}
                form={null}
                onAdd={() => undefined}
                onEdit={() => undefined}
                onDelete={() => undefined}
            />
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
        expect(form()).toContain(`title="${HINTS.language}"`);
        expect(HINTS.language).toMatch(/language of every article in this base/);
        expect(HINTS.language).toMatch(/pick the right articles/);
    });

    it("AC-knowledge-base-language-4: a refused language is shown beside the language select", () => {
        expect(fieldOf(mistake("knowledgeBase.language.unknown", "language", "oferta"))).toBe("language");
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
