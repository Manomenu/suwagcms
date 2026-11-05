import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";

import type { VersionComparison } from "./api";
import { ComparisonDetails } from "./ComparisonPanel";

const comparison: VersionComparison = {
    bot: "rzeczoznawca",
    from: 14,
    to: 15,
    added: 0,
    removed: 0,
    changed: 1,
    definition: [],
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

describe("ComparisonDetails", () => {
    it("AC-bot-persona-9: a changed persona names the part of the scope and the lines added and removed", () => {
        const page = renderToStaticMarkup(<ComparisonDetails comparison={comparison} />);

        expect(page).toContain("scope.whenOutOfScope");
        expect(page).toMatch(/text-destructive[^>]*>− Old way/);
        expect(page).toMatch(/text-success[^>]*>\+ New way/);
        expect(page).toContain("persona");
    });
});
