import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { Router } from "wouter";

import { HistoryList, HistoryPanel, type HistoryLine } from "./historyPanel";

const edited: HistoryLine = {
    id: 9,
    at: "2026-11-04T21:40:00+00:00",
    kind: "article",
    resourceId: "cennik",
    action: "edited",
    revision: 8,
    changes: [{ field: "content", old: "Wycena domu: 900 zł", new: "Wycena domu: 1100 zł" }],
    batch: null,
};
const added: HistoryLine = {
    ...edited,
    id: 3,
    at: "2026-11-02T09:12:00+00:00",
    action: "added",
    revision: 1,
    changes: [{ field: "title", old: null, new: "Cennik" }],
};
const deleted: HistoryLine = {
    ...edited,
    id: 12,
    action: "deleted",
    revision: null,
    changes: [{ field: "content", old: "Ostatni tekst", new: null }],
};

const nothing = () => undefined;

function panel(entries: HistoryLine[] | null, hasOlder = false): string {
    return renderToStaticMarkup(
        <HistoryPanel entries={entries} since="2026-11-01T08:00:00+00:00" hasOlder={hasOlder} failure={null} onOlder={nothing} />,
    );
}

describe("HistoryPanel", () => {
    it("AC-change-history-1: an entry shows its action, time, revision and the changed field's old and new values", () => {
        const html = panel([edited, added]);

        expect(html).toContain('aria-label="History"');
        expect(html).toContain("edited");
        expect(html).toContain("2026-11-04 21:40");
        expect(html).toContain("revision 8");
        expect(html).toContain("content");
        expect(html).toContain("Wycena domu: 900 zł");
        expect(html).toContain("Wycena domu: 1100 zł");
        expect(html).toContain("revision 1");
    });

    it("AC-change-history-4: a delete shows what the resource held last", () => {
        const html = panel([deleted]);

        expect(html).toContain("deleted");
        expect(html).toContain("Ostatni tekst");
        expect(html).not.toContain("revision");
    });

    it("AC-change-history-8: no entries say since when, not a blank", () => {
        expect(panel([])).toContain("No change has been recorded since 2026-11-01, when history started.");
    });

    it("AC-change-history-6: another page is offered only when there is one", () => {
        expect(panel([edited], true)).toContain("Older changes");
        expect(panel([edited], false)).not.toContain("Older changes");
    });

    it("AC-change-history-6: the bot's list names each resource with a link to its own history", () => {
        const html = renderToStaticMarkup(
            <Router ssrPath="/bots/sklep/history">
                <HistoryList
                    entries={[edited]}
                    since="2026-11-01T08:00:00+00:00"
                    hasOlder={false}
                    failure={null}
                    onOlder={nothing}
                    hrefOf={(line) => `/bots/sklep/history?kind=articles&id=${line.resourceId}`}
                />
            </Router>,
        );

        expect(html).toContain('href="/bots/sklep/history?kind=articles&amp;id=cennik"');
    });
});
