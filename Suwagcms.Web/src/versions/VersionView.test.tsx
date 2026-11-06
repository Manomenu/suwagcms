import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { Router } from "wouter";

import type { HistoryState } from "@/api/history";

import { versionNumberOf, type VersionDetail, type VersionSummary } from "./api";
import { NoSuchVersion, VersionView, type ComparisonState } from "./VersionView";

const SHA = `3f9a${"0".repeat(56)}c21e`;

const summary: VersionSummary = {
    number: 14,
    createdAt: "2026-11-04T21:40:00+00:00",
    facts: 3,
    knowledgeBases: 1,
    articles: 12,
    procedures: 2,
    actions: 0,
    sha256: SHA,
};

const detail: VersionDetail = { version: summary, document: '{"bot":"rzeczoznawca","number":14}' };

const entry = {
    id: 9,
    at: "2026-11-04T21:40:00+00:00",
    kind: "article",
    resourceId: "cennik",
    action: "edited",
    revision: 8,
    changes: [],
    batch: null,
    written: { fields: [{ field: "title", old: "Stary", new: "Nowy", lines: null }], parts: [] },
};

const history: HistoryState = {
    entries: [entry],
    since: "2026-11-04T08:00:00+00:00",
    failure: null,
    paging: { page: 1, pages: 1, asking: false, failure: null, onMove: () => undefined },
    incomplete: false,
};

function page(shown: { versions?: VersionSummary[]; comparison?: ComparisonState | null; history?: HistoryState } = {}): string {
    return renderToStaticMarkup(
        <Router ssrPath="/bots/rzeczoznawca/versions/14">
            <VersionView
                bot="rzeczoznawca"
                detail={detail}
                versions={shown.versions ?? [summary, { ...summary, number: 13 }, { ...summary, number: 12 }]}
                comparison={shown.comparison ?? null}
                history={shown.history ?? history}
                onCompare={() => undefined}
                onCloseComparison={() => undefined}
                onBack={() => undefined}
            />
        </Router>,
    );
}

describe("VersionView", () => {
    it("AC-version-create-13: shows the version with its digest and its document as stored", () => {
        const html = page();

        expect(html).toContain("Version 14");
        expect(html).toContain(`sha256 ${SHA}`);
        expect(html).toContain("&quot;bot&quot;:&quot;rzeczoznawca&quot;");
    });

    it("AC-change-history-24: shows its number, date, whole digest, counts and document, with a History panel beneath", () => {
        const html = page();

        expect(html.indexOf("Version 14")).toBeLessThan(html.indexOf("2026-11-04 21:40"));
        expect(html).toContain("3 facts, 1 knowledge bases, 12 articles, 2 procedures, 0 actions");
        expect(html.indexOf(SHA)).toBeLessThan(html.indexOf('aria-label="History"'));
    });

    it("AC-change-history-20: has exactly one History heading, from the wrapper", () => {
        expect(page().match(/<h\d[^>]*>History<\/h\d>/g)).toHaveLength(1);
    });

    it("AC-change-history-25: a version the bot does not have says so, with the way back to Versions", () => {
        const html = renderToStaticMarkup(<NoSuchVersion asked="99" onBack={() => undefined} />);

        expect(html).toContain("This bot has no version 99.");
        expect(html).toContain("Back to versions");
    });

    it("AC-change-history-25: an address that is not a number asks for no version", () => {
        expect(versionNumberOf("14")).toBe(14);
        for (const raw of ["abc", "0", "-1", "1.5", "99999999999", null]) expect(versionNumberOf(raw)).toBeNull();
    });

    it("AC-change-history-29: each row names its resource with a link to its History page", () => {
        expect(page()).toContain('href="/bots/rzeczoznawca/history?kind=articles&amp;id=cennik"');
    });

    it("AC-change-history-30: a window that opens before history says the list may be incomplete, and any other says no such thing", () => {
        const note = "Changes made before 2026-11-04 were not recorded, so this list may be incomplete.";
        const empty = page({ history: { ...history, entries: [], incomplete: true } });

        expect(empty).toContain(note);
        expect(empty).toContain("No change has been recorded for this version.");
        expect(empty).not.toContain("recorded since");
        expect(page({ history: { ...history, incomplete: true } })).toContain(note);
        expect(page()).not.toContain("may be incomplete");
    });

    it("AC-change-history-31: its entries move with the same controls as every list", () => {
        const html = page({ history: { ...history, paging: { ...history.paging, pages: 3 } } });

        expect(html).toContain("Page 1 of 3");
        expect(html).toContain(">Next<");
    });

    it("AC-version-diff-10: offers Compare with… and the bot's other versions, and shows the comparison", () => {
        const comparison: ComparisonState = {
            with: 13,
            answer: {
                bot: "rzeczoznawca",
                from: 13,
                to: 14,
                added: 0,
                removed: 0,
                changed: 1,
                definition: [],
                resources: [
                    {
                        kind: "GlobalFact",
                        id: "customer.city",
                        difference: "Changed",
                        fields: [{ field: "maxLength", old: "80", new: "120", lines: null }],
                        parts: [],
                    },
                ],
            },
            refusal: null,
        };

        expect(page()).toContain("Compare with…");
        expect(page()).toContain('<option value="13"');
        expect(page()).toContain('<option value="12"');
        expect(page()).not.toContain('<option value="14"');
        expect(page()).not.toContain(">Close<");

        const compared = page({ comparison });
        expect(compared).toContain("0 added · 0 removed · 1 changed");
        expect(compared).toContain("customer.city");
        expect(compared).toContain(">Close<");
    });

    it("AC-version-diff-10: a bot with one version does not offer it", () => {
        expect(page({ versions: [summary] })).not.toContain("Compare with…");
    });

    it("AC-version-diff-8: a number the bot does not have is the refusal in the comparison's place, with the page around it", () => {
        const html = page({
            comparison: { with: 99, answer: null, refusal: "Bot 'rzeczoznawca' has no version 99; its latest is 14." },
        });

        expect(html).toContain("has no version 99; its latest is 14.");
        expect(html).toContain("Version 14");
        expect(html).toContain('aria-label="History"');
    });
});
