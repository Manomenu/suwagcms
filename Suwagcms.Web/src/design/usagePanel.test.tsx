import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { Router } from "wouter";

import { UsagePanel, type UsageEntry } from "./usagePanel";

const usedBy: UsageEntry[] = [
    { kind: "article", id: "wycena", field: "content" },
    { kind: "article", id: "rabaty", field: "llmGuide" },
];
const uses: UsageEntry[] = [{ kind: "article", id: "warunki", field: "content" }];

function panel(entries: { usedBy: UsageEntry[]; uses: UsageEntry[] }, onDelete?: () => void): string {
    const props = onDelete === undefined ? {} : { onDelete };
    return renderToStaticMarkup(
        <Router ssrPath="/bots/rzeczoznawca/articles">
            <UsagePanel usage={entries} failure={null} hrefOf={(entry) => `/bots/rzeczoznawca/articles?open=${entry.id}`} {...props} />
        </Router>,
    );
}

describe("UsagePanel", () => {
    it("AC-delete-guard-7: every related resource is listed with its field as a link", () => {
        const html = panel({ usedBy, uses });

        expect(html).toContain('href="/bots/rzeczoznawca/articles?open=wycena"');
        expect(html).toContain('href="/bots/rzeczoznawca/articles?open=rabaty"');
        expect(html).toContain('href="/bots/rzeczoznawca/articles?open=warunki"');
        expect(html).toContain("article · content");
        expect(html).toContain("article · llmGuide");
    });

    it("AC-delete-guard-7: an empty list says so", () => {
        const html = panel({ usedBy: [], uses: [] });

        expect(html).toContain("Nothing uses this.");
        expect(html).toContain("This uses nothing.");
    });

    it("AC-delete-guard-9: Delete is disabled with the reason beside it while something uses the resource", () => {
        const html = panel({ usedBy, uses }, () => undefined);

        expect(html).toMatch(/<button[^>]*disabled=""[^>]*>Delete<\/button>/);
        expect(html).toContain("Used in 2 places. Remove those references first.");
    });

    it("AC-delete-guard-9: Delete works, with no reason shown, when nothing uses the resource", () => {
        const html = panel({ usedBy: [], uses }, () => undefined);

        expect(html).toContain(">Delete</button>");
        expect(html).not.toContain('disabled=""');
        expect(html).not.toContain("Remove those references first");
    });
});
