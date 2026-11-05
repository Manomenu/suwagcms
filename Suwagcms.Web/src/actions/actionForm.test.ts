import { describe, expect, it } from "vitest";

import { bodyOf, emptyForm, emptyRow, formOf, placeOf } from "./actionForm";
import type { ActionView } from "./api";

const action: ActionView = {
    id: "check",
    description: "Checks",
    parameters: { address: { type: "text", description: "Where", maxLength: 200, values: null, min: null, max: null } },
    results: { served: { type: "enum", description: "Served", values: ["yes", "no"], min: null, max: null, maxLength: null } },
    implementation: {
        http: { method: "POST", url: "https://x.internal", headers: { Authorization: "Bearer {secret:CRM_TOKEN}" }, timeoutSeconds: 20 },
    },
    revision: 2,
    usedBy: [],
};

describe("the action form", () => {
    it("AC-http-actions-1: a form read from an action sends the same action back", () => {
        expect(bodyOf(formOf(action))).toEqual({
            description: "Checks",
            parameters: action.parameters,
            results: action.results,
            implementation: action.implementation,
        });
    });

    it("AC-http-actions-1: only the constraint of a row's type is sent, and a form with nothing has no lists", () => {
        const form = {
            ...emptyForm(),
            parameters: [{ ...emptyRow(), name: "floors", type: "int", min: "1", max: "9", maxLength: "5", values: "a" }],
        };

        expect(bodyOf(form).parameters).toEqual({
            floors: { type: "int", description: "", values: null, min: 1, max: 9, maxLength: null },
        });
        expect(bodyOf(emptyForm()).results).toBeNull();
    });

    it("AC-http-actions-8: a mistake's path names the field it shows beside", () => {
        const mistake = (code: string, path: string) =>
            ({ code, path, stage: "Save", resource: { kind: "Action", id: "x" }, message: "", fix: "", related: [] }) as Parameters<
                typeof placeOf
            >[0];

        expect(placeOf(mistake("action.http.url", "implementation.http.url"))).toBe("implementation.http.url");
        expect(placeOf(mistake("fact.enum.valueBlank", "results.served.values[1]"))).toBe("results.served.values");
        expect(placeOf(mistake("resource.exists", "revision"))).toBe("id");
    });
});
