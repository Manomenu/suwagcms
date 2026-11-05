import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { Router } from "wouter";

import type { Mistake } from "@/api/client";

import type { BotStatus, PushResult, VersionDetail, VersionSummary } from "./api";
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

const answering: BotStatus = {
    bot: "rzeczoznawca",
    address: "http://rzeczoznawca.internal:8080",
    reach: "Answered",
    has: 12,
    held: [11, 12],
    inUse: [11],
    lastKnown: 12,
    lastKnownAt: "2026-11-04T21:40:00+00:00",
    reason: null,
    otherBot: null,
    latest: 14,
    hasUnknownVersion: false,
    attempts: [
        {
            id: "1",
            version: 14,
            outcome: "Failed",
            error: "cannot reach http://rzeczoznawca.internal:8080.",
            at: "2026-11-04T21:42:00+00:00",
        },
        { id: "0", version: 13, outcome: "Succeeded", error: null, at: "2026-11-02T09:20:00+00:00" },
    ],
};

function view(shown: {
    versions?: VersionSummary[];
    refused?: Mistake[] | null;
    shown?: VersionDetail | null;
    status?: BotStatus | null;
    pushFailure?: PushResult | null;
}): string {
    return renderToStaticMarkup(
        <Router ssrPath="/bots/rzeczoznawca/versions">
            <VersionsView
                bot="rzeczoznawca"
                versions={shown.versions ?? versions}
                status={shown.status ?? null}
                pushFailure={shown.pushFailure ?? null}
                pushing={false}
                onPush={() => undefined}
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

    it("AC-version-push-3: shows what the bot has beside the latest version, with the main button to push the latest", () => {
        const html = view({ status: answering });

        expect(html).toContain("Bot has v12 · Latest v14");
        expect(html).toContain("Push v14");
    });

    it("AC-version-push-3: shows no push button when the bot is up to date", () => {
        const html = view({ status: { ...answering, has: 14 } });

        expect(html).toContain("Bot has v14 · up to date");
        expect(html).not.toContain("Push v14");
    });

    it("AC-version-push-13: lists the versions still in use by conversations", () => {
        expect(view({ status: answering })).toContain("Still in use by conversations: v11");
    });

    it("AC-version-push-12: lists the recent pushes with their time, version, result and reason", () => {
        const html = view({ status: answering });

        expect(html).toContain("2026-11-04 21:42 UTC · v14 · failed: cannot reach http://rzeczoznawca.internal:8080.");
        expect(html).toContain("2026-11-02 09:20 UTC · v13 · pushed");
    });

    it("AC-version-push-6: shows a refused push as failed with the bot's mistakes", () => {
        const html = view({
            status: answering,
            pushFailure: { outcome: "Failed", version: 14, botHas: null, reason: "the bot refused v14: 1 mistake(s)", mistakes },
        });

        expect(html).toContain("failed");
        expect(html).toContain("the bot refused v14");
        expect(html).toContain("No path reaches step");
        expect(html).toContain("Bot has v12 · Latest v14");
    });
});
