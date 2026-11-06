import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { Router } from "wouter";

import { PageSizeContext } from "@/api/pagination";
import type { Mistake } from "@/api/client";

import type { BotStatus, PushPlan, PushResult, VersionSummary } from "./api";
import { VersionsView } from "./VersionsView";

const versions: VersionSummary[] = [
    {
        number: 14,
        createdAt: "2026-11-04T21:40:00+00:00",
        facts: 3,
        knowledgeBases: 1,
        articles: 12,
        procedures: 2,
        actions: 0,
        sha256: `3f9a${"0".repeat(56)}c21e`,
    },
    {
        number: 13,
        createdAt: "2026-11-02T09:12:00+00:00",
        facts: 3,
        knowledgeBases: 1,
        articles: 11,
        procedures: 2,
        actions: 0,
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
    search?: string;
    versions?: VersionSummary[];
    refused?: Mistake[] | null;
    status?: BotStatus | null;
    pushFailure?: PushResult | null;
    plan?: PushPlan | null;
}): string {
    return renderToStaticMarkup(
        <Router ssrPath={`/bots/rzeczoznawca/versions${shown.search ?? ""}`}>
            <PageSizeContext value={{ size: 20, failure: null }}>
                <VersionsView
                    bot="rzeczoznawca"
                    versions={shown.versions ?? versions}
                    status={shown.status ?? null}
                    pushFailure={shown.pushFailure ?? null}
                    pushing={false}
                    onPush={() => undefined}
                    refused={shown.refused ?? null}
                    plan={shown.plan ?? null}
                    onPlan={() => undefined}
                    onCancelPlan={() => undefined}
                    onCompare={() => undefined}
                    onCreate={() => undefined}
                    onOpen={() => undefined}
                />
            </PageSizeContext>
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

    it("AC-version-diff-10: Versions has no comparison on a row", () => {
        const html = view({});
        expect(html).not.toContain(">Changes<");
        expect(html).not.toContain("Nothing before it");
        const rows = html
            .split("<tr>")
            .filter((row) => row.includes(" · 2026"))
            .map((row) => row.split("</tr>")[0] ?? "");
        expect(rows).toHaveLength(2);
        for (const row of rows)
            expect(row.match(/<button[^>]*>[^<]*<\/button>/g)?.map((button) => button.replace(/<[^>]+>/g, ""))).toEqual(["Open"]);
    });

    it("AC-pagination-10: other version offers versions of every page", () => {
        const kept = Array.from({ length: 25 }, (_, index) => ({ ...versions[0], number: 25 - index }) as VersionSummary);

        const first = view({ versions: kept, status: { ...answering, has: 25, latest: 25, held: [] } });
        expect(first.split("<tr>").filter((row) => row.includes(" · 2026"))).toHaveLength(20);
        expect(first).toContain(">v1</option>");
        expect(first).toContain("Page 1 of 2");

        expect(view({ search: "?page=2", versions: kept })).toContain("Page 2 of 2");
    });

    it("AC-version-diff-13: the page offers a comparison of what the bot reports with the latest", () => {
        expect(view({ status: answering })).toContain("Compare v12 with v14");
        expect(view({ status: { ...answering, has: 14 } })).not.toContain("Compare v");
        expect(view({ status: { ...answering, has: null } })).not.toContain("Compare v");
        expect(view({ status: { ...answering, hasUnknownVersion: true } })).not.toContain("Compare v");
    });

    it("AC-version-rollback-13: offers the other kept versions in a select beside the main button", () => {
        const kept = [14, 13, 12, 11].map((number) => ({ ...versions[0], number }) as VersionSummary);

        const html = view({ versions: kept, status: answering });

        expect(html).toContain("other version:");
        expect(html.indexOf(">v13</option>")).toBeLessThan(html.indexOf(">v11</option>"));
        expect(html).not.toContain(">v14</option>");
        expect(html).not.toContain(">v12</option>");
        expect(html).toContain("Push v13");
    });

    it("AC-version-rollback-13: asks once, saying the bot's current and chosen versions, and AC-version-rollback-18: what goes back", () => {
        const html = view({
            status: answering,
            plan: {
                version: 11,
                botHas: 12,
                way: "MakeCurrent",
                problem: null,
                mistakes: [],
                changes: {
                    bot: "rzeczoznawca",
                    from: 12,
                    to: 11,
                    added: 0,
                    removed: 1,
                    changed: 0,
                    definition: [],
                    resources: [{ kind: "Article", id: "cennik", difference: "Removed", fields: [], parts: [] }],
                },
            },
        });

        expect(html).toContain("The bot goes from v12 to v11.");
        expect(html).toContain("The working state and the latest version stay as they are.");
        expect(html).toContain("What goes back: 0 added · 1 removed · 0 changed");
        expect(html).toContain("cennik");
        expect(html).toContain("Cancel");
    });
});
