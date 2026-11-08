import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { Router } from "wouter";

import { ChangeList, type ChangeLine } from "./changeList";
import { Page } from "./page";
import { ResourceEdit } from "./resourceEdit";

const edited: ChangeLine = {
    id: 9,
    at: "2026-11-04T21:40:00+00:00",
    kind: "article",
    resourceId: "cennik",
    action: "edited",
    revision: 8,
    changes: [{ field: "content", old: "Wycena domu: 900 zł", new: "Wycena domu: 1100 zł" }],
    batch: null,
    written: {
        fields: [
            {
                field: "content",
                old: null,
                new: null,
                lines: [
                    { change: "Folded", text: "12 unchanged lines" },
                    { change: "Kept", text: "Cennik" },
                    { change: "Removed", text: "Wycena domu: 900 zł" },
                    { change: "Added", text: "Wycena domu: 1100 zł" },
                ],
            },
            { field: "maxLength", old: "80", new: "120", lines: null },
        ],
        parts: [],
    },
};
const added: ChangeLine = {
    ...edited,
    id: 3,
    at: "2026-11-02T09:12:00+00:00",
    action: "added",
    revision: 1,
    changes: [{ field: "title" }, { field: "content" }],
    written: { fields: [{ field: "title", old: null, new: "Cennik", lines: null }], parts: [] },
};
const deleted: ChangeLine = {
    ...edited,
    id: 12,
    action: "deleted",
    revision: null,
    written: { fields: [{ field: "content", old: null, new: null, lines: [{ change: "Removed", text: "Ostatni tekst" }] }], parts: [] },
};

function paging(pages: number) {
    return { page: 1, pages, asking: false, failure: null, onMove: () => undefined };
}

const SINCE = "2026-11-04T08:00:00+00:00";

function list(entries: ChangeLine[] | null, pages = 1): string {
    return renderToStaticMarkup(<ChangeList entries={entries} since={SINCE} failure={null} paging={paging(pages)} />);
}

function panel(entries: ChangeLine[] | null): string {
    return renderToStaticMarkup(<Page title="A page" history={{ entries, since: SINCE, failure: null, paging: paging(1) }} />);
}

function textOf(html: string): string {
    return html.replace(/<[^>]+>/g, " ");
}

describe("the shared list of changes", () => {
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
        expect(panel([])).toContain("No change has been recorded since 2026-11-04.");
    });

    it("AC-change-history-6: another page is offered only when there is one", () => {
        expect(list([edited], 2)).toContain("Page 1 of 2");
        expect(list([edited], 2)).not.toContain("invisible");
        expect(list([edited], 1)).toContain("invisible");
    });

    it("AC-change-history-6: the bot's list names each resource with a link to its own history", () => {
        const html = renderToStaticMarkup(
            <Router ssrPath="/bots/sklep/history">
                <ChangeList
                    entries={[edited]}
                    since={SINCE}
                    failure={null}
                    paging={paging(1)}
                    hrefOf={(line) => `/bots/sklep/history?kind=articles&id=${line.resourceId}`}
                />
            </Router>,
        );

        expect(html).toContain('href="/bots/sklep/history?kind=articles&amp;id=cennik"');
    });

    it("AC-change-history-19: loading, empty or with entries, the list draws no heading and no word for what it holds", () => {
        expect(list(null)).toContain("Loading…");
        expect(list([])).toContain("No change has been recorded since 2026-11-04.");
        for (const html of [list(null), list([]), list([edited, added, deleted], 2)]) {
            expect(html).not.toMatch(/<h[1-6]/);
            expect(textOf(html)).not.toMatch(/history/i);
        }
    });

    it("AC-change-history-20: the wrapper has exactly one History heading, also on a resource's edit view", () => {
        const edit = renderToStaticMarkup(
            <Router ssrPath="/bots/sklep/articles/cennik/edit">
                <ResourceEdit
                    noun="article"
                    plural="articles"
                    addTitle="Add an article"
                    onBack={() => undefined}
                    ready
                    missingId={null}
                    editedId="cennik"
                    deleteFailure={null}
                    dirty={false}
                    changingId={false}
                    onChangingId={() => undefined}
                    preview={() => Promise.reject(new Error("not asked"))}
                    apply={() => Promise.reject(new Error("not asked"))}
                    onChanged={() => undefined}
                    usage={null}
                    onDelete={() => undefined}
                    history={{ entries: [edited], since: SINCE, failure: null, paging: paging(1) }}
                >
                    <p>The form</p>
                </ResourceEdit>
            </Router>,
        );

        for (const html of [panel([edited]), panel([]), edit]) {
            expect(html.match(/<h\d[^>]*>History<\/h\d>/g)).toHaveLength(1);
        }
    });

    it("AC-change-history-34: the page frame draws the History panel last, under the screen's own content, and none without history", () => {
        const html = renderToStaticMarkup(
            <Page
                title="Procedures of sklep"
                actions={<button>Add a procedure</button>}
                history={{ entries: [edited], since: SINCE, failure: null, paging: paging(1) }}
            >
                <p>The list</p>
            </Page>,
        );

        expect(html.indexOf('aria-label="History"')).toBeGreaterThan(html.indexOf("The list"));
        expect(renderToStaticMarkup(<Page title="History of sklep" />)).not.toContain('aria-label="History"');
    });

    it("AC-change-history-21: an entry reads kept lines, folds and old → new as written, and draws none of it twice", () => {
        const html = list([edited]);

        expect(html).toContain("… 12 unchanged lines");
        expect(html).toContain("  Cennik");
        expect(html).toContain("− Wycena domu: 900 zł");
        expect(html).toContain("+ Wycena domu: 1100 zł");
        expect(textOf(html)).toMatch(/maxLength:\s+80\s+→\s+120/);
    });

    it("AC-change-history-22: an entry that changes a procedure's steps reads as parts, each opening to its fields, with no JSON", () => {
        const html = list([
            {
                ...edited,
                kind: "procedure",
                changes: [{ field: "steps", old: { askCity: { say: "A" } }, new: { askCity: { say: "B" } } }],
                written: {
                    fields: [],
                    parts: [
                        {
                            part: "step",
                            name: "askCity",
                            difference: "Changed",
                            fields: [{ field: "say", old: "Skąd jesteś?", new: "Z jakiego miasta?", lines: null }],
                        },
                        { part: "local fact", name: "city", difference: "Removed", fields: [] },
                    ],
                },
            },
        ]);

        expect(html).toMatch(/<details[^>]*><summary[^>]*>step &#x27;askCity&#x27; changed<\/summary>/);
        expect(textOf(html)).toContain("Z jakiego miasta?");
        expect(html).toContain("local fact &#x27;city&#x27; removed");
        expect(html).not.toContain("{");
    });

    it("AC-change-history-1: an add stays folded as how many fields it holds", () => {
        expect(list([added])).toMatch(/<details[^>]*><summary[^>]*>2 fields<\/summary>/);
    });
});
