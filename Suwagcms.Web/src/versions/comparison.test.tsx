import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";

import type { VersionComparison } from "./api";
import { comparisonLine, ComparisonRows } from "./comparison";

const base = { bot: "rzeczoznawca", from: 13, to: 14, added: 0, removed: 0, changed: 1, definition: [] };

const persona: VersionComparison = {
    ...base,
    from: 14,
    to: 15,
    resources: [
        {
            kind: "Persona",
            id: "persona",
            difference: "Changed",
            fields: [
                {
                    field: "scope.whenOutOfScope",
                    old: null,
                    new: null,
                    lines: [
                        { change: "Kept", text: "Decline" },
                        { change: "Removed", text: "Old way" },
                        { change: "Added", text: "New way" },
                    ],
                },
            ],
            parts: [],
        },
    ],
};

function rows(comparison: VersionComparison): string {
    return renderToStaticMarkup(<ComparisonRows comparison={comparison} />);
}

describe("ComparisonRows", () => {
    it("AC-bot-persona-9: a changed persona names the part of the scope and the lines added and removed", () => {
        const page = rows(persona);

        expect(page).toContain("scope.whenOutOfScope");
        expect(page).toMatch(/text-destructive[^>]*>− Old way/);
        expect(page).toMatch(/text-success[^>]*>\+ New way/);
        expect(page).toContain("persona");
    });

    it("AC-version-diff-1: lists the resources by change, and the field's old and new values", () => {
        const comparison: VersionComparison = {
            ...base,
            added: 1,
            removed: 1,
            changed: 2,
            resources: [
                {
                    kind: "GlobalFact",
                    id: "customer.city",
                    difference: "Changed",
                    fields: [{ field: "maxLength", old: "80", new: "120", lines: null }],
                    parts: [],
                },
                {
                    kind: "Article",
                    id: "cennik",
                    difference: "Changed",
                    fields: [
                        {
                            field: "content",
                            old: null,
                            new: null,
                            lines: [
                                { change: "Removed", text: "Wycena domu: 900 zł" },
                                { change: "Added", text: "Wycena domu: 1100 zł" },
                            ],
                        },
                    ],
                    parts: [],
                },
                { kind: "Article", id: "rabaty", difference: "Added", fields: [], parts: [] },
                { kind: "Article", id: "stary", difference: "Removed", fields: [], parts: [] },
            ],
        };

        const html = rows(comparison);

        expect(comparisonLine(comparison)).toBe("1 added · 1 removed · 2 changed");
        expect(html).toContain("customer.city");
        expect(html).toContain("maxLength");
        expect(html).toContain("− Wycena domu: 900 zł");
        expect(html).toContain("+ Wycena domu: 1100 zł");
        expect(html).toContain("rabaty");
        expect(html).toContain("stary");
    });

    it("AC-version-diff-1: a format version that differs is listed like a changed field", () => {
        const html = rows({ ...base, definition: [{ field: "formatVersion", old: "1", new: "2", lines: null }], resources: [] });

        expect(html).toContain("formatVersion");
        expect(html).toContain("changed");
    });

    it("AC-version-diff-3: on the web each part of a changed procedure is folded and opens to what changed in it", () => {
        const html = rows({
            ...base,
            resources: [
                {
                    kind: "Procedure",
                    id: "orderValuation",
                    difference: "Changed",
                    fields: [],
                    parts: [
                        {
                            part: "step",
                            name: "askCity",
                            difference: "Changed",
                            fields: [{ field: "say", old: "Skąd?", new: "Z jakiego miasta?", lines: null }],
                        },
                        { part: "step", name: "askStreet", difference: "Added", fields: [] },
                    ],
                },
            ],
        });

        expect(html).toMatch(/<details[^>]*><summary[^>]*>step &#x27;askCity&#x27; changed<\/summary>.*Z jakiego miasta\?/);
        expect(html).toContain("step &#x27;askStreet&#x27; added");
    });

    it("AC-version-diff-6: a comparison with no differences says so", () => {
        const none = { ...base, changed: 0, from: 14, resources: [] };

        expect(comparisonLine(none)).toBe("no differences");
        expect(rows(none)).toBe("");
    });
});
