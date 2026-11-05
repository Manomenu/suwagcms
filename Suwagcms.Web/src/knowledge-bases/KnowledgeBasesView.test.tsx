import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { Router } from "wouter";

import type { Mistake } from "@/api/client";

import type { KnowledgeBase } from "./api";
import { fieldOf, mistakesFor } from "./knowledgeBaseForm";
import { KnowledgeBasesView } from "./KnowledgeBasesView";

const bases: KnowledgeBase[] = [
    { id: "oferta", title: "Our offer", revision: 2 },
    { id: "dlaRzeczoznawcow", title: "For valuers", revision: 1 },
];

function view(list: KnowledgeBase[]): string {
    return renderToStaticMarkup(
        <Router ssrPath="/bots/rzeczoznawca/knowledge-bases">
            <KnowledgeBasesView
                bot="rzeczoznawca"
                bases={list}
                form={null}
                onAdd={() => undefined}
                onRename={() => undefined}
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

    it("AC-knowledge-bases-2: each base has a rename button, and AC-knowledge-bases-3: a delete button", () => {
        const html = view(bases);

        expect(html.match(/>Rename</g)).toHaveLength(2);
        expect(html.match(/>Delete</g)).toHaveLength(2);
    });

    it("AC-knowledge-bases-7: a bot with no bases shows a screen offering to create the first one", () => {
        const html = view([]);

        expect(html).toContain("No knowledge bases yet.");
        expect(html).toContain("Create the first one");
        expect(html).toContain("New knowledge base");
        expect(html).not.toContain(">Rename<");
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
