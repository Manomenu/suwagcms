import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { Router } from "wouter";

import { UsagePanel, type UsageEntry } from "./usagePanel";

const usedBy: UsageEntry[] = [
    { kind: "article", id: "wycena", field: "content" },
    { kind: "article", id: "rabaty", field: "llmGuide" },
];
const uses: UsageEntry[] = [{ kind: "article", id: "warunki", field: "content" }];

function panel(entries: { usedBy: UsageEntry[]; uses: UsageEntry[] }): string {
    return renderToStaticMarkup(
        <Router ssrPath="/bots/rzeczoznawca/articles">
            <UsagePanel usage={entries} failure={null} hrefOf={(entry) => `/bots/rzeczoznawca/articles/${entry.id}/edit`} />
        </Router>,
    );
}

describe("UsagePanel", () => {
    it("AC-delete-guard-7: every related resource is listed with its field as a link", () => {
        const html = panel({ usedBy, uses });

        expect(html).toContain('href="/bots/rzeczoznawca/articles/wycena/edit"');
        expect(html).toContain('href="/bots/rzeczoznawca/articles/rabaty/edit"');
        expect(html).toContain('href="/bots/rzeczoznawca/articles/warunki/edit"');
        expect(html).toContain("article · content");
        expect(html).toContain("article · llmGuide");
    });

    it("AC-delete-guard-7: an empty list says so", () => {
        const html = panel({ usedBy: [], uses: [] });

        expect(html).toContain("Nothing uses this.");
        expect(html).toContain("This uses nothing.");
    });
});
