import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { Router } from "wouter";

import type { Mistake } from "@/api/client";

import type { VersionDetail, VersionSummary } from "./api";
import { VersionsView } from "./VersionsView";

const versions: VersionSummary[] = [
    {
        number: 14,
        createdAt: "2026-11-04T21:40:00+00:00",
        facts: 3,
        knowledgeBases: 1,
        articles: 12,
        procedures: 2,
        sha256: `3f9a${"0".repeat(56)}c21e`,
    },
    {
        number: 13,
        createdAt: "2026-11-02T09:12:00+00:00",
        facts: 3,
        knowledgeBases: 1,
        articles: 11,
        procedures: 2,
        sha256: `b07d${"0".repeat(56)}1a44`,
    },
];

const mistakes: Mistake[] = [
    {
        code: "bot.conversation.missing",
        stage: "Version",
        resource: { kind: "Definition", id: "" },
        path: "procedures",
        message: "This bot has no 'conversation' procedure.",
        fix: "Add it: every conversation starts there.",
        related: [],
    },
    {
        code: "step.unreachable",
        stage: "Version",
        resource: { kind: "Procedure", id: "orderValuation" },
        path: "steps.goodbye",
        message: "No path reaches step 'goodbye'.",
        fix: "Connect it or delete it.",
        related: [],
    },
];

function view(shown: { versions?: VersionSummary[]; refused?: Mistake[] | null; shown?: VersionDetail | null }): string {
    return renderToStaticMarkup(
        <Router ssrPath="/bots/rzeczoznawca/versions">
            <VersionsView
                bot="rzeczoznawca"
                versions={shown.versions ?? versions}
                refused={shown.refused ?? null}
                shown={shown.shown ?? null}
                onCreate={() => undefined}
                onOpen={() => undefined}
                onClose={() => undefined}
            />
        </Router>,
    );
}

describe("VersionsView", () => {
    it("AC-version-create-13: lists each version with its number, date, counts of resources and digest, with a button to open it", () => {
        const html = view({});

        expect(html).toContain("Versions of rzeczoznawca");
        expect(html).toContain("v14 · 2026-11-04 21:40");
        expect(html).toContain("3 facts, 1 knowledge bases, 12 articles");
        expect(html).toContain("3f9a…c21e");
        expect(html).toContain("v13 · 2026-11-02 09:12");
        expect(html).toContain("Open");
        expect(html).toContain("Create version");
    });

    it("AC-version-create-13: says what to do when there are no versions", () => {
        expect(view({ versions: [] })).toContain("No versions yet. Create one");
    });

    it("AC-version-create-13: shows an opened version with its digest and its document as stored", () => {
        const opened: VersionSummary = {
            number: 14,
            createdAt: "2026-11-04T21:40:00+00:00",
            facts: 3,
            knowledgeBases: 1,
            articles: 12,
            procedures: 2,
            sha256: `3f9a${"0".repeat(56)}c21e`,
        };
        const html = view({ shown: { version: opened, document: '{"formatVersion":1}' } });

        expect(html).toContain("Version 14");
        expect(html).toContain("sha256 3f9a");
        expect(html).toContain("formatVersion");
    });

    it("AC-version-create-2: lists every mistake of a refused creation with a link to the resource's screen", () => {
        const html = view({ refused: mistakes });

        expect(html).toContain("Not created, 2 mistakes");
        expect(html).toContain("This bot has no &#x27;conversation&#x27; procedure.");
        expect(html).toContain("Connect it or delete it.");
        expect(html).toContain('href="/bots/rzeczoznawca/procedures"');
        expect(html).toContain("Open orderValuation");
    });
});
