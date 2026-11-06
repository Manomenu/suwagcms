import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { Router } from "wouter";

import { PageSizeContext } from "@/api/pagination";

import type { Fact } from "./api";
import { FactsView } from "./FactsView";

const facts: Fact[] = Array.from({ length: 45 }, (_, index) => ({
    name: `customer.field${String(index + 1)}`,
    type: "text",
    description: "A field",
    writableBy: "set",
    values: null,
    min: null,
    max: null,
    maxLength: 80,
    pattern: null,
    revision: 1,
    usedBy: [],
}));

function view(search: string, size: number | null = 20, failure: string | null = null, rows = facts): string {
    return renderToStaticMarkup(
        <Router ssrPath={`/bots/b/facts${search}`}>
            <PageSizeContext value={{ size, failure }}>
                <FactsView bot="b" facts={rows} onAdd={() => undefined} onEdit={() => undefined} />
            </PageSizeContext>
        </Router>,
    );
}

function disabled(html: string, label: string): boolean {
    return new RegExp(`<button[^>]*\\bdisabled=""[^>]*>${label}<`).test(html);
}

describe("a list divided into pages", () => {
    it("AC-pagination-4: page 2 shows rows 21 to 40 and reads Page 2 of 3, with both ways open", () => {
        const html = view("?page=2");

        expect(html).toContain("Page 2 of 3");
        expect(html.match(/>Edit</g)).toHaveLength(20);
        expect(html).toContain(">customer.field21<");
        expect(html).toContain(">customer.field40<");
        expect(html).not.toContain(">customer.field20<");
        expect(html).not.toContain(">customer.field41<");
        expect(disabled(html, "Previous")).toBe(false);
        expect(disabled(html, "Next")).toBe(false);
    });

    it("AC-pagination-4: Previous is disabled on the first page and Next on the last; past the last page is the last", () => {
        expect(disabled(view(""), "Previous")).toBe(true);
        expect(disabled(view(""), "Next")).toBe(false);
        expect(view("?page=abc")).toContain("Page 1 of 3");
        expect(view("?page=99")).toContain("Page 3 of 3");
        expect(disabled(view("?page=99"), "Next")).toBe(true);
        expect(disabled(view("?page=99"), "Previous")).toBe(false);
    });

    it("AC-pagination-1: until the server answers the list says it is loading, and a failed question shows the failure", () => {
        expect(view("", null)).toContain("Loading…");
        expect(view("", null)).not.toContain("customer.field1<");
        expect(view("", null, "The server did not answer.")).toContain("The server did not answer.");
    });

    it("AC-pagination-8: an empty list says so with no controls, also before the size is known", () => {
        for (const size of [20, null]) {
            const html = view("", size, null, []);
            expect(html).toContain("No facts yet");
            expect(html).not.toContain("Page ");
        }
    });
});
