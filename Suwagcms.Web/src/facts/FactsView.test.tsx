import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { Router } from "wouter";

import type { Mistake } from "@/api/client";

import type { Fact } from "./api";
import { emptyForm } from "./factForm";
import { FactFields } from "./FactFields";
import { FactsView } from "./FactsView";

const facts: Fact[] = [
    {
        name: "customer.city",
        type: "text",
        description: "The city the customer lives in",
        writableBy: "set",
        values: null,
        min: null,
        max: null,
        maxLength: 80,
        revision: 1,
        usedBy: [],
    },
    {
        name: "order.kind",
        type: "enum",
        description: "What is valued",
        writableBy: "correct",
        values: ["house", "flat"],
        min: null,
        max: null,
        maxLength: null,
        revision: 1,
        usedBy: ["conversation", "orderValuation"],
    },
    {
        name: "order.floors",
        type: "int",
        description: "How many floors",
        writableBy: "assign",
        values: null,
        min: 1,
        max: 40,
        maxLength: null,
        revision: 1,
        usedBy: [],
    },
];

function view(list: Fact[]): string {
    return renderToStaticMarkup(
        <Router ssrPath="/bots/rzeczoznawca/facts">
            <FactsView bot="rzeczoznawca" facts={list} form={null} onAdd={() => undefined} onEdit={() => undefined} />
        </Router>,
    );
}

function mistake(code: string, path: string, message: string, fix: string): Mistake {
    return { code, path, message, fix, stage: "Save", resource: { kind: "GlobalFact", id: "order.kind" }, related: [] };
}

describe("FactsView", () => {
    it("AC-global-facts-1: every fact is listed with its type, limit, description and writableBy", () => {
        const html = view(facts);

        expect(html).toContain("Facts of rzeczoznawca");
        expect(html).toContain("customer.city");
        expect(html).toContain("text, up to 80");
        expect(html).toContain("The city the customer lives in");
        expect(html).toContain("enum: house, flat");
        expect(html).toContain("int, 1 to 40");
        expect(html).toMatch(/text, up to 80 · set/);
        expect(html).toMatch(/enum: house, flat · correct/);
        expect(html).toMatch(/int, 1 to 40 · assign/);
    });

    it("AC-procedure-model-16: a fact names the procedures that use it, and one no procedure uses says nothing", () => {
        const html = view(facts);

        expect(html).toContain("Used by: conversation, orderValuation");
        expect(html.match(/Used by:/g)).toHaveLength(1);
    });

    it("AC-global-facts-8: each fact has an edit button, and Delete is on the opened fact beside what uses it", () => {
        const html = view(facts);

        expect(html).not.toContain(">Delete<");
        expect(html.match(/>Edit</g)).toHaveLength(3);
    });

    it("says there are no facts and how to add one", () => {
        const html = view([]);

        expect(html).toContain("No facts yet.");
        expect(html).toContain("Add a fact");
    });
});

describe("FactFields", () => {
    function fields(mistakes: Mistake[], type = "enum"): string {
        return renderToStaticMarkup(
            <FactFields form={{ ...emptyForm(), type }} mistakes={mistakes} nameLocked={false} onChange={() => undefined} />,
        );
    }

    function fieldHtml(html: string, field: string): string {
        const start = html.indexOf(`data-field="${field}"`);
        const next = html.indexOf("data-field=", start + 1);
        return html.slice(start, next === -1 ? undefined : next);
    }

    it("AC-global-facts-6: each mistake is shown, with its fix, beside the field its path names and no other", () => {
        const html = fields([
            mistake("fact.description.missing", "description", "'order.kind' has no description.", "Add one: the model reads it."),
            mistake("fact.enum.tooFewValues", "values", "'order.kind' has 1 value.", "An enum needs at least two values: add another."),
        ]);

        expect(fieldHtml(html, "description")).toContain("has no description.");
        expect(fieldHtml(html, "description")).toContain("Add one: the model reads it.");
        expect(fieldHtml(html, "values")).toContain("An enum needs at least two values: add another.");
        expect(fieldHtml(html, "values")).toContain('aria-invalid="true"');
        expect(fieldHtml(html, "name")).not.toContain("no description");
        expect(fieldHtml(html, "type")).not.toContain("add another");
    });

    it("AC-global-facts-6: a field with no mistake is not marked invalid", () => {
        const html = fields([]);

        expect(html).not.toContain('aria-invalid="');
    });

    it("AC-global-facts-4: a taken name is shown beside the name field", () => {
        const html = fields([mistake("resource.exists", "revision", "The fact 'a' already exists.", "Choose another name.")]);

        expect(fieldHtml(html, "name")).toContain("Choose another name.");
    });

    it("shows the constraint fields of the chosen type only", () => {
        expect(fields([], "enum")).toContain('data-field="values"');
        expect(fields([], "enum")).not.toContain('data-field="maxLength"');
        expect(fields([], "int")).toContain('data-field="min"');
        expect(fields([], "int")).toContain('data-field="max"');
        expect(fields([], "text")).toContain('data-field="maxLength"');
        expect(fields([], "text")).not.toContain('data-field="values"');
    });
});
